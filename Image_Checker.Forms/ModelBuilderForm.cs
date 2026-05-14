// ══════════════════════════════════════════════════════════════════════════════
//  ModelBuilderForm.cs  –  Code-behind for the redesigned form.
// ══════════════════════════════════════════════════════════════════════════════

using Image_Checker.Services;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Image_Checker.Forms
{
    public partial class ModelBuilderForm : Form
    {
        // ── state ──────────────────────────────────────────────────────────
        private readonly MLContext _ml = new(seed: 42);
        private readonly DataPreprocessor _pp = new();
        private DataTable? _raw;
        private PreprocessingSummary? _lastPrep;
        private string? _savedModel;      // primary model path
        private string? _savedSsaModel;   // SSA model path (TS tasks)
        private CancellationTokenSource? _cts;

        // ── algorithm descriptions ─────────────────────────────────────────
        private static readonly Dictionary<string, (string Title, string Body)> _algo = new()
        {
            ["SDCA"] = ("SDCA – Stochastic Dual Coordinate Ascent",
                "A fast linear trainer using coordinate-wise gradient descent.\n\n" +
                "• Best for   : Large datasets; binary & multiclass tabular data.\n" +
                "• Speed      : ★★★★★  Very fast.\n" +
                "• Accuracy   : ★★★☆☆  Good for linearly separable problems.\n" +
                "• Explainable: Yes – linear feature weights.\n\n" +
                "⚠ Cannot capture non-linear patterns.\n" +
                "   Normalised features strongly recommended.\n\n" +
                "Metrics: Accuracy | Macro-Accuracy | R²"),

            ["LBFGS"] = ("LBFGS – Limited-memory BFGS",
                "Quasi-Newton optimiser that converges more accurately than SDCA.\n\n" +
                "• Best for   : Medium datasets where accuracy > speed.\n" +
                "• Speed      : ★★★☆☆  Moderate.\n" +
                "• Accuracy   : ★★★★☆  Generally better than SDCA.\n" +
                "• Explainable: Yes – linear coefficients.\n\n" +
                "⚠ Higher memory usage; struggles above ~500k rows.\n" +
                "   Normalised features strongly recommended.\n\n" +
                "Metrics: Accuracy | Macro-Accuracy | R²"),

            ["FastTree"] = ("FastTree – Gradient Boosted Decision Trees",
                "Ensemble of trees built sequentially; each corrects the previous.\n\n" +
                "• Best for   : Structured tabular data with complex patterns.\n" +
                "• Speed      : ★★★☆☆  Moderate (parallel per level).\n" +
                "• Accuracy   : ★★★★★  Excellent on mixed-type data.\n" +
                "• Explainable: Partial – feature importance available.\n\n" +
                "⚠ Can overfit small datasets (< 500 rows).\n" +
                "   Does NOT require feature normalisation.\n" +
                "   Auto-wrapped in One-vs-All for multiclass.\n\n" +
                "Metrics: Accuracy | Macro-Accuracy | R²"),

            ["FastForest"] = ("FastForest – Random Forest",
                "Many independent decision trees; predictions aggregated by vote.\n\n" +
                "• Best for   : Noisy / redundant features; robust baseline.\n" +
                "• Speed      : ★★★★☆  Fast (trees built in parallel).\n" +
                "• Accuracy   : ★★★★☆  Very robust; rarely overfits.\n" +
                "• Explainable: Partial – feature importance available.\n\n" +
                "⚠ Slower inference than FastTree for very large forests.\n" +
                "   Auto-wrapped in One-vs-All for multiclass.\n\n" +
                "Metrics: Accuracy | Macro-Accuracy | R²"),

            ["LightGBM"] = ("LightGBM – Light Gradient Boosting Machine",
                "Histogram-based boosting – faster and more memory-efficient.\n\n" +
                "• Best for   : Large datasets (100k+ rows); high-cardinality data.\n" +
                "• Speed      : ★★★★★  Very fast.\n" +
                "• Accuracy   : ★★★★★  State-of-the-art on tabular benchmarks.\n" +
                "• Explainable: Partial – feature importance available.\n\n" +
                "⚠ Benefits from tuning (num_leaves, learning rate).\n" +
                "   Supports multiclass natively – no OvA wrapper needed.\n\n" +
                "Metrics: Accuracy | Macro-Accuracy | R²"),

            ["Perceptron"] = ("Averaged Perceptron  (Binary Only)",
                "Online linear learner; processes samples one at a time.\n\n" +
                "• Best for   : Very large datasets; streaming; binary only.\n" +
                "• Speed      : ★★★★★  Extremely fast.\n" +
                "• Accuracy   : ★★☆☆☆  Lower than LBFGS/SDCA.\n" +
                "• Explainable: Yes – linear weights.\n\n" +
                "⚠ BINARY CLASSIFICATION ONLY.\n" +
                "   Auto-skipped for multiclass and regression tasks.\n\n" +
                "Metrics: Accuracy | AUC-ROC | F1"),

            ["LinearSGD"] = ("Linear SGD – Online Gradient Descent Regression",
                "Stochastic gradient descent for linear regression.\n\n" +
                "• Best for   : Quick regression baseline; very large datasets.\n" +
                "• Speed      : ★★★★★  Extremely fast.\n" +
                "• Accuracy   : ★★☆☆☆  Assumes linearity.\n" +
                "• Explainable: Yes – linear coefficients.\n\n" +
                "⚠ REGRESSION ONLY. Auto-skipped for classification.\n" +
                "   Normalised features strongly recommended.\n\n" +
                "Metrics: R² | MAE | RMSE"),
        };

        // ── constructor ────────────────────────────────────────────────────
        public ModelBuilderForm()
        {
            InitializeComponent();
            WireEvents();
            SetAlgoInfo("SDCA");
        }

        // ── wiring ─────────────────────────────────────────────────────────
        private void WireEvents()
        {
            btnBrowse.Click += OnBrowse;
            btnLoadFile.Click += OnLoadFile;
            btnCheckAll.Click += (_, _) => SetAll(clbFeatures, true);
            btnUncheckAll.Click += (_, _) => SetAll(clbFeatures, false);
            btnTrain.Click += OnTrain;
            btnCancelTrain.Click += (_, _) => { _cts?.Cancel(); SetStatus("Cancelling…", true); };
            btnSaveModel.Click += OnSave;
            btnExportReport.Click += OnExport;

            btnCleanApplyAll.Click += OnCleanApplyAll;
            btnTransformApplyAll.Click += OnTransformApplyAll;

            cmbTask.SelectedIndexChanged += (_, _) =>
                grpTS.Visible = cmbTask.SelectedIndex == 4;

            // When the label column changes: uncheck it in the feature list
            // and hide its row in all three preprocessing grids.
            cmbLabel.SelectedIndexChanged += (_, _) => OnLabelChanged();

            rbPCA.CheckedChanged += (_, _) => nudPCAComp.Enabled = rbPCA.Checked;

            Wire(chkSDCA, "SDCA");
            Wire(chkLBFGS, "LBFGS");
            Wire(chkFastTree, "FastTree");
            Wire(chkFastForest, "FastForest");
            Wire(chkLightGBM, "LightGBM");
            Wire(chkPerceptron, "Perceptron");
            Wire(chkLinearSGD, "LinearSGD");
        }

        private void Wire(CheckBox c, string k)
        {
            c.MouseEnter += (_, _) => SetAlgoInfo(k);
            c.CheckedChanged += (_, _) => { if (c.Checked) SetAlgoInfo(k); };
        }

        private void SetAlgoInfo(string k)
        {
            if (!_algo.TryGetValue(k, out var info)) return;
            if (InvokeRequired) { Invoke(() => SetAlgoInfo(k)); return; }
            lblAlgoTitle.Text = info.Title;
            rtbAlgoDesc.Clear();
            rtbAlgoDesc.Font = new Font("Segoe UI", 9.5f);
            rtbAlgoDesc.ForeColor = Color.FromArgb(35, 45, 85);
            rtbAlgoDesc.AppendText(info.Body);
        }

        // ─────────────────────────────────────────────────────────────────
        //  BROWSE & LOAD
        // ─────────────────────────────────────────────────────────────────
        private void OnBrowse(object? s, EventArgs e)
        {
            using var d = new OpenFileDialog
            { Filter = "Data Files (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls|All|*.*" };
            if (d.ShowDialog() == DialogResult.OK) txtFilePath.Text = d.FileName;
        }

        private async void OnLoadFile(object? s, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFilePath.Text))
            { Warn("Select a file first."); return; }

            SetBusy(true, "Loading…");
            try
            {
                await Task.Run(() =>
                {
                    var dt = _pp.LoadFile(txtFilePath.Text);
                    if (dt == null || dt.Columns.Count == 0)
                        throw new Exception("Could not load file or file has no columns.");
                    _raw = dt;
                });
                FillColumnsTab();
                FillGrids();
                FillPreview();
                lblFileStats.Text =
                    $"✅  {_raw!.Rows.Count:N0} rows × {_raw.Columns.Count} columns  │  {Path.GetFileName(txtFilePath.Text)}";
                lblPreviewHint.Text =
                    $"Showing first 200 of {_raw.Rows.Count:N0} rows × {_raw.Columns.Count} columns";
                SetStatus($"Loaded: {Path.GetFileName(txtFilePath.Text)}", false);
                tabMain.SelectedTab = tpColumns;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Load failed.", false);
            }
            finally { SetBusy(false, ""); }
        }

        // ─────────────────────────────────────────────────────────────────
        //  FILL COLUMNS TAB
        // ─────────────────────────────────────────────────────────────────
        private void FillColumnsTab()
        {
            if (_raw == null) return;
            var cols = _raw.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

            cmbLabel.Items.Clear();
            cmbLabel.Items.AddRange(cols.Cast<object>().ToArray());
            cmbLabel.SelectedIndex = cols.Count - 1;

            cmbTsDate.Items.Clear();
            cmbTsDate.Items.Add("(none)");
            cols.ForEach(c => cmbTsDate.Items.Add(c));
            cmbTsDate.SelectedIndex = 0;

            clbFeatures.Items.Clear(); clbIgnore.Items.Clear();
            cols.ForEach(c => { clbFeatures.Items.Add(c, true); clbIgnore.Items.Add(c, false); });

            // Uncheck the default label column straight away
            OnLabelChanged();
        }

        private void FillPreview()
        {
            if (_raw == null) return;
            var view = _raw.Rows.Count <= 200
                ? _raw : _raw.AsEnumerable().Take(200).CopyToDataTable();
            dgvPreview.DataSource = null;
            dgvPreview.DataSource = view;
        }

        // ─────────────────────────────────────────────────────────────────
        //  FILL PER-COLUMN GRIDS
        // ─────────────────────────────────────────────────────────────────
        private void FillGrids()
        {
            if (_raw == null) return;
            dgvCleaning.Rows.Clear();
            dgvTransform.Rows.Clear();
            dgvReduction.Rows.Clear();

            foreach (DataColumn col in _raw.Columns)
            {
                bool num = IsNumeric(col.ColumnName);
                string t = num ? "Numeric" : "Text";

                // Cleaning
                int ci = dgvCleaning.Rows.Add();
                var cr = dgvCleaning.Rows[ci];
                cr.Cells["ColName"].Value = col.ColumnName;
                cr.Cells["ColType"].Value = t;
                cr.Cells["Missing"].Value = num ? "Mean (average)" : "Mode (most frequent)";
                cr.Cells["OutlierMethod"].Value = "None";
                cr.Cells["OutlierAction"].Value = "Cap to boundary value";
                cr.Cells["IQRk"].Value = "1.5";
                cr.Cells["ZThresh"].Value = "3.0";
                if (!num)
                {
                    cr.DefaultCellStyle.ForeColor = Color.FromArgb(110, 110, 130);
                    cr.Cells["OutlierMethod"].ReadOnly = true;
                    cr.Cells["OutlierAction"].ReadOnly = true;
                    cr.Cells["IQRk"].ReadOnly = true;
                    cr.Cells["ZThresh"].ReadOnly = true;
                }

                // Transform
                int ti = dgvTransform.Rows.Add();
                var tr = dgvTransform.Rows[ti];
                tr.Cells["ColName"].Value = col.ColumnName;
                tr.Cells["ColType"].Value = t;
                tr.Cells["Norm"].Value = "None – raw values";
                tr.Cells["Enc"].Value = num ? "None – keep as text"
                                                : "One-Hot Encoding  (binary dummy columns)";
                tr.Cells["Bin"].Value = false;
                tr.Cells["BinCount"].Value = "10";
                if (!num) tr.Cells["Norm"].ReadOnly = true;
                else tr.Cells["Enc"].ReadOnly = true;

                // Reduction
                int ri = dgvReduction.Rows.Add();
                var rr = dgvReduction.Rows[ri];
                rr.Cells["ColName"].Value = col.ColumnName;
                rr.Cells["ColType"].Value = t;
                rr.Cells["Include"].Value = true;
                rr.Cells["Strategy"].Value = "Always include";
                rr.Cells["VarThresh"].Value = "0.01";
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  LABEL COLUMN CHANGED
        // ─────────────────────────────────────────────────────────────────
        private string _previousLabel = string.Empty;

        private void OnLabelChanged()
        {
            string newLabel = cmbLabel.SelectedItem?.ToString() ?? string.Empty;
            if (newLabel == string.Empty) return;

            // ── Feature checklist ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(_previousLabel))
            {
                int prevIdx = clbFeatures.Items.IndexOf(_previousLabel);
                if (prevIdx >= 0) clbFeatures.SetItemChecked(prevIdx, true);
            }
            int lblIdx = clbFeatures.Items.IndexOf(newLabel);
            if (lblIdx >= 0) clbFeatures.SetItemChecked(lblIdx, false);

            // ── Preprocessing grids ───────────────────────────────────────
            HideGridRow(dgvCleaning, newLabel, _previousLabel);
            HideGridRow(dgvTransform, newLabel, _previousLabel);
            HideGridRow(dgvReduction, newLabel, _previousLabel);

            _previousLabel = newLabel;
        }

        private static void HideGridRow(DataGridView grid,
                                         string labelName, string previousLabel)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                string colName = row.Cells["ColName"].Value?.ToString() ?? string.Empty;

                if (colName == labelName)
                {
                    row.Height = 0;
                    row.ReadOnly = true;
                    row.DefaultCellStyle.BackColor = Color.FromArgb(220, 228, 245);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(180, 185, 210);
                    row.Visible = false;
                }
                else if (colName == previousLabel && !string.IsNullOrEmpty(previousLabel))
                {
                    row.Visible = true;
                    row.ReadOnly = false;
                    row.Height = 30;
                    row.DefaultCellStyle.BackColor = Color.Empty;
                    row.DefaultCellStyle.ForeColor = Color.Empty;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  APPLY-ALL DIALOGS
        // ─────────────────────────────────────────────────────────────────
        private void OnCleanApplyAll(object? s, EventArgs e)
        {
            using var d = new ApplyCleaningDialog();
            if (d.ShowDialog() != DialogResult.OK) return;
            foreach (DataGridViewRow r in dgvCleaning.Rows)
            {
                if (d.SetMissing) r.Cells["Missing"].Value = d.Missing;
                if (d.SetOutlier) r.Cells["OutlierMethod"].Value = d.OutlierMethod;
                if (d.SetOutlier) r.Cells["OutlierAction"].Value = d.OutlierAction;
            }
        }

        private void OnTransformApplyAll(object? s, EventArgs e)
        {
            using var d = new ApplyTransformDialog();
            if (d.ShowDialog() != DialogResult.OK) return;
            foreach (DataGridViewRow r in dgvTransform.Rows)
            {
                string type = r.Cells["ColType"].Value?.ToString() ?? "";
                if (d.SetNorm && type == "Numeric") r.Cells["Norm"].Value = d.Norm;
                if (d.SetEnc && type == "Text") r.Cells["Enc"].Value = d.Enc;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  SNAPSHOT TRAINER CONFIG  (call only on UI thread)
        // ─────────────────────────────────────────────────────────────────
        private DataTrainerConfig SnapshotTrainerConfig()
        {
            bool ts = ParseTask() == TaskType.TimeSeries;
            return new DataTrainerConfig
            {
                DataFilePath = string.Empty,
                Separator = ',',
                HasHeader = true,
                LabelColumnName = cmbLabel.SelectedItem?.ToString() ?? "Label",
                FeatureColumns = new List<string>(),
                CategoricalColumns = new List<string>(),
                IgnoreColumns = new List<string>(),
                Task = ParseTask(),
                TestFraction = (double)nudTestPct.Value / 100.0,
                Seed = (int)nudSeed.Value,
                OutputDirectory = Path.GetDirectoryName(txtFilePath.Text) ?? "",
                TimeSeries = ts ? new TimeSeriesOptions
                {
                    DateColumn = cmbTsDate.SelectedIndex > 0
                                        ? cmbTsDate.SelectedItem?.ToString() : null,
                    HorizonSteps = (int)nudTsHorizon.Value > 0 ? (int)nudTsHorizon.Value : 12,
                    Granularity = cmbTsGran.SelectedItem?.ToString() ?? "Month",
                    WindowSize = (int)nudTsWindow.Value,
                    SeriesLength = (int)nudTsSeries.Value,
                    ConfidenceLevel = (float)nudTsConf.Value / 100f
                } : new TimeSeriesOptions(),
                Algorithms = new AlgorithmOptions
                {
                    UseSDCA = chkSDCA.Checked,
                    UseLBFGS = chkLBFGS.Checked,
                    UseFastTree = chkFastTree.Checked,
                    UseFastForest = chkFastForest.Checked,
                    UseLightGBM = chkLightGBM.Checked,
                    UseAveragedPerceptron = chkPerceptron.Checked,
                    UseSdcaRegression = chkSDCA.Checked,
                    UseFastTreeRegression = chkFastTree.Checked,
                    UseLightGbmRegression = chkLightGBM.Checked,
                    UseFastForestRegression = chkFastForest.Checked,
                    UseOlsRegression = chkLinearSGD.Checked
                }
            };
        }

        private FullPreprocessingConfig BuildPPConfig()
        {
            string DomVal(DataGridView g, string col) =>
                g.Rows.Cast<DataGridViewRow>()
                 .Select(r => r.Cells[col].Value?.ToString() ?? "")
                 .Where(v => v != "" && !v.StartsWith("None"))
                 .GroupBy(v => v).OrderByDescending(x => x.Count())
                 .FirstOrDefault()?.Key ?? "None";

            string domMissing = DomVal(dgvCleaning, "Missing");
            string domOutlier = DomVal(dgvCleaning, "OutlierMethod");
            string domNorm = DomVal(dgvTransform, "Norm");
            bool useOneHot = dgvTransform.Rows.Cast<DataGridViewRow>()
                                     .Any(r => r.Cells["Enc"].Value?.ToString()?
                                          .StartsWith("One-Hot") == true);
            bool useBin = dgvTransform.Rows.Cast<DataGridViewRow>()
                                     .Any(r => r.Cells["Bin"].Value is true);
            string domStrategy = DomVal(dgvReduction, "Strategy");

            double iqrK = 1.5, zT = 3.0, varT = 0.01;
            foreach (DataGridViewRow r in dgvCleaning.Rows)
                if (r.Cells["OutlierMethod"].Value?.ToString() != "None")
                {
                    double.TryParse(r.Cells["IQRk"].Value?.ToString(), out iqrK);
                    double.TryParse(r.Cells["ZThresh"].Value?.ToString(), out zT);
                    break;
                }
            foreach (DataGridViewRow r in dgvReduction.Rows)
                if (r.Cells["Strategy"].Value?.ToString()?.Contains("Variance") == true)
                { double.TryParse(r.Cells["VarThresh"].Value?.ToString(), out varT); break; }

            return new FullPreprocessingConfig
            {
                Cleaning = new DataCleaningConfig
                {
                    MissingValueStrategy = domMissing switch
                    {
                        string s when s.StartsWith("Median") => MissingValueStrategy.Median,
                        string s when s.StartsWith("Mode") => MissingValueStrategy.Mode,
                        string s when s.StartsWith("Delete") => MissingValueStrategy.DeleteRow,
                        _ => MissingValueStrategy.Mean
                    },
                    RemoveDuplicates = chkDuplicates.Checked,
                    OutlierMethod = domOutlier switch
                    {
                        string s when s.StartsWith("IQR") => OutlierDetectionMethod.IQR,
                        string s when s.StartsWith("Z-Score") => OutlierDetectionMethod.ZScore,
                        _ => OutlierDetectionMethod.None
                    },
                    OutlierAction = OutlierAction.Cap,
                    IQRMultiplier = iqrK,
                    ZScoreThreshold = zT
                },
                Transformation = new DataTransformationConfig
                {
                    Normalization = domNorm switch
                    {
                        string s when s.Contains("Min-Max") => NormalizationMethod.MinMax,
                        string s when s.Contains("Z-Score") => NormalizationMethod.ZScore,
                        string s when s.Contains("Decimal") => NormalizationMethod.DecimalScaling,
                        string s when s.Contains("Log") => NormalizationMethod.Log,
                        _ => NormalizationMethod.None
                    },
                    Encoding = useOneHot ? CategoricalEncoding.OneHot : CategoricalEncoding.LabelEncoding,
                    EnableBinning = useBin,
                    NumberOfBins = 10
                },
                Reduction = new DataReductionConfig
                {
                    DimReduction = rbPCA.Checked ? DimensionalityReductionMethod.PCA : DimensionalityReductionMethod.None,
                    PCAComponents = (int)nudPCAComp.Value,
                    FeatureSelection = domStrategy switch
                    {
                        string s when s.Contains("Variance") => FeatureSelectionMethod.VarianceFilter,
                        string s when s.Contains("Top-N") => FeatureSelectionMethod.TopNCorrelation,
                        _ => FeatureSelectionMethod.None
                    },
                    VarianceThreshold = varT,
                    TopNFeatures = 10,
                    Sampling = rbRandSample.Checked ? SamplingMethod.Random
                                      : rbStratSample.Checked ? SamplingMethod.Stratified
                                      : SamplingMethod.None,
                    SampleFraction = (double)nudSamplePct.Value / 100.0,
                    SamplingSeed = (int)nudSeed.Value
                }
            };
        }

        // ─────────────────────────────────────────────────────────────────
        //  TRAINING
        // ─────────────────────────────────────────────────────────────────
        private async void OnTrain(object? s, EventArgs e)
        {
            if (_raw == null) { Warn("Load a data file first."); return; }
            string lbl = cmbLabel.SelectedItem?.ToString() ?? "";
            if (lbl == "") { Warn("Select a label column."); return; }

            var feat = Checked(clbFeatures).Where(c => c != lbl).ToList();
            var ign = Checked(clbIgnore);
            bool ts = ParseTask() == TaskType.TimeSeries;

            if (!ts && feat.Count == 0) { Warn("Select at least one feature column."); return; }

            var catCols = dgvTransform.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells["Enc"].Value?.ToString()?.StartsWith("One-Hot") == true
                         || r.Cells["Enc"].Value?.ToString()?.StartsWith("Label") == true)
                .Select(r => r.Cells["ColName"].Value?.ToString() ?? "")
                .Where(c => c != "" && feat.Contains(c))
                .ToList();

            // ── Snapshot ALL UI values on the UI thread BEFORE Task.Run ────
            var filePath = txtFilePath.Text;
            var ppConfig = BuildPPConfig();
            var trainerBase = SnapshotTrainerConfig();

            var logW = new RtbWriter(rtbLog, this);
            var old = Console.Out;
            Console.SetOut(logW);

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            SetBusy(true, "Running…");
            tabMain.SelectedTab = tpTraining;
            rtbLog.Clear();
            btnTrain.Enabled = false;
            btnCancelTrain.Enabled = true;
            btnSaveModel.Enabled = btnExportReport.Enabled = false;
            _savedModel = null;
            _savedSsaModel = null;

            PreprocessingSummary? prep = null;
            string? mPath = null;
            Exception? err = null;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        prep = _pp.Run(filePath, lbl, feat, catCols, ign, ppConfig);
                        ct.ThrowIfCancellationRequested();

                        var cfg = new DataTrainerConfig
                        {
                            DataFilePath = trainerBase.Task == TaskType.TimeSeries
                                                    ? filePath
                                                    : prep.TempCsvPath,
                            Separator = ',',
                            HasHeader = true,
                            LabelColumnName = trainerBase.LabelColumnName,
                            FeatureColumns = prep.FinalFeatureCols,
                            CategoricalColumns = catCols.Intersect(prep.FinalFeatureCols).ToList(),
                            IgnoreColumns = new List<string>(),
                            Task = trainerBase.Task,
                            TestFraction = trainerBase.TestFraction,
                            Seed = trainerBase.Seed,
                            OutputDirectory = trainerBase.OutputDirectory,
                            TimeSeries = trainerBase.TimeSeries,
                            Algorithms = trainerBase.Algorithms
                        };

                        mPath = new DataModelTrainer(_ml, cfg).TrainAndEvaluate(ct);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) { err = ex; }
                }, ct);

                Console.SetOut(old);
                if (err != null) { LogColor($"\n❌ {err.GetBaseException().Message}", Color.OrangeRed); SetStatus("Failed.", false); return; }
                if (mPath == null) { LogColor("\n⚠️ Cancelled.", Color.Gold); SetStatus("Cancelled.", false); return; }

                _savedModel = mPath; _lastPrep = prep;

                if (ParseTask() == TaskType.TimeSeries && mPath != null)
                {
                    _savedSsaModel = mPath;
                    var dir = Path.GetDirectoryName(mPath) ?? "";
                    var regressionZip = Directory.GetFiles(dir, "bestModel-*_Regression-*.zip")
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .FirstOrDefault();
                    if (regressionZip != null) _savedModel = regressionZip;
                }
                else
                {
                    _savedSsaModel = null;
                }

                LogColor($"\n✅ Saved:\n   {mPath}", Color.LightGreen);
                FillResults(mPath);
                tabMain.SelectedTab = tpResults;
                SetStatus("Training complete.", false);
            }
            catch (OperationCanceledException)
            { Console.SetOut(old); LogColor("\n⚠️ Cancelled.", Color.Gold); SetStatus("Cancelled.", false); }
            catch (Exception ex)
            { Console.SetOut(old); LogColor($"\n❌ {ex.Message}", Color.OrangeRed); SetStatus("Error.", false); }
            finally
            {
                SetBusy(false, "");
                btnTrain.Enabled = true;
                btnCancelTrain.Enabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  RESULTS
        // ─────────────────────────────────────────────────────────────────
        private void FillResults(string path)
        {
            if (InvokeRequired) { Invoke(() => FillResults(path)); return; }

            bool isTs = ParseTask() == TaskType.TimeSeries;

            lblResultTitle.Text = isTs
                ? "📅  Time-Series training complete — SSA + Regression models saved"
                : $"🏆  Best model: {Path.GetFileNameWithoutExtension(path)}";

            var dt = new DataTable();
            dt.Columns.Add("Property"); dt.Columns.Add("Value");

            if (isTs && _savedSsaModel != null)
            {
                dt.Rows.Add("== SSA FORECAST MODEL ==", "(load this for forecasting)");
                dt.Rows.Add("SSA File", Path.GetFileName(_savedSsaModel));
                dt.Rows.Add("SSA Folder", Path.GetDirectoryName(_savedSsaModel));
                dt.Rows.Add("Use for", "Time-Series Forecast tab");
                dt.Rows.Add("", "");

                var dir = Path.GetDirectoryName(_savedSsaModel) ?? "";
                var regZip = Directory.GetFiles(dir, "bestModel-*_Regression-*.zip")
                    .OrderByDescending(f => File.GetLastWriteTime(f)).FirstOrDefault();
                if (regZip != null)
                {
                    dt.Rows.Add("== REGRESSION MODEL ==", "(load this for row prediction)");
                    dt.Rows.Add("Regression File", Path.GetFileName(regZip));
                    dt.Rows.Add("Regression Folder", Path.GetDirectoryName(regZip));
                    dt.Rows.Add("Use for", "Input Data tab — predict by customer/item");
                    dt.Rows.Add("", "");
                }
            }
            else
            {
                dt.Rows.Add("Model File", Path.GetFileName(path));
                dt.Rows.Add("Saved To", Path.GetDirectoryName(path));
            }

            dt.Rows.Add("Label", cmbLabel.SelectedItem?.ToString());
            dt.Rows.Add("Task", ParseTask().ToString());
            dt.Rows.Add("Split", $"{100 - (int)nudTestPct.Value}% train / {nudTestPct.Value}% test");
            if (_lastPrep != null)
            {
                dt.Rows.Add("Original Rows", _lastPrep.OriginalRows);
                dt.Rows.Add("Final Rows", _lastPrep.FinalRows);
                dt.Rows.Add("Features", _lastPrep.FinalFeatureCols.Count);
                dt.Rows.Add("Duplicates Removed", _lastPrep.DuplicatesRemoved);
                dt.Rows.Add("Missing Fixed", _lastPrep.MissingValuesFixed);
                dt.Rows.Add("Outliers Handled", _lastPrep.OutliersHandled);
            }

            dgvResults.DataSource = dt;
            rtbResultLog.Clear();

            if (isTs && _savedSsaModel != null)
            {
                void Ln(string t, Color c)
                {
                    rtbResultLog.SelectionColor = c;
                    rtbResultLog.AppendText(t + "\n");
                }
                Ln("TWO MODELS WERE SAVED", Color.Cyan);
                Ln("═══════════════════════════════════════════════════", Color.FromArgb(100, 145, 215));
                Ln("", Color.White);
                Ln("📅  MODEL 1 — SSA FORECAST  (for time trend forecasting)", Color.LightGreen);
                Ln($"   File : {Path.GetFileName(_savedSsaModel)}", Color.FromArgb(188, 210, 188));
                Ln("   How  : Open Prediction Form", Color.FromArgb(188, 210, 188));
                Ln("         → Browse & Load → select SSA file", Color.FromArgb(188, 210, 188));
                Ln("         → Time-Series Forecast tab", Color.FromArgb(188, 210, 188));
                Ln("         → Set horizon (e.g. 12) + Granularity = Month", Color.FromArgb(188, 210, 188));
                Ln("         → Run Forecast", Color.FromArgb(188, 210, 188));
                Ln("", Color.White);
                var dir2 = Path.GetDirectoryName(_savedSsaModel) ?? "";
                var reg2 = Directory.GetFiles(dir2, "bestModel-*_Regression-*.zip")
                    .OrderByDescending(f => File.GetLastWriteTime(f)).FirstOrDefault();
                if (reg2 != null)
                {
                    Ln("📊  MODEL 2 — REGRESSION  (predict qty for specific customer/item)", Color.LightSkyBlue);
                    Ln($"   File : {Path.GetFileName(reg2)}", Color.FromArgb(188, 210, 188));
                    Ln("   How  : Open Prediction Form", Color.FromArgb(188, 210, 188));
                    Ln("         → Browse & Load → select Regression file", Color.FromArgb(188, 210, 188));
                    Ln("         → Input Data tab", Color.FromArgb(188, 210, 188));
                    Ln("         → Add Row, enter PARTY_NAME / INVENTORY_ITEM_ID / TRX_DATE", Color.FromArgb(188, 210, 188));
                    Ln("         → Run Prediction", Color.FromArgb(188, 210, 188));
                }
            }
            else if (_lastPrep != null)
            {
                foreach (var l in _lastPrep.Log) rtbResultLog.AppendText(l + "\n");
            }

            btnSaveModel.Enabled = btnExportReport.Enabled = true;
        }

        // ─────────────────────────────────────────────────────────────────
        //  SAVE & EXPORT
        // ─────────────────────────────────────────────────────────────────
        private void OnSave(object? s, EventArgs e)
        {
            if (_savedModel == null || !File.Exists(_savedModel))
            { Warn("No model found."); return; }
            using var d = new SaveFileDialog
            { Filter = "ML.NET Model (*.zip)|*.zip", FileName = Path.GetFileName(_savedModel) };
            if (d.ShowDialog() != DialogResult.OK) return;
            File.Copy(_savedModel, d.FileName, true);
            var j = Path.ChangeExtension(_savedModel, ".json");
            if (File.Exists(j)) File.Copy(j, Path.ChangeExtension(d.FileName, ".json"), true);
            MessageBox.Show($"Saved to:\n{d.FileName}", "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void OnExport(object? s, EventArgs e)
        {
            if (_savedModel == null || _lastPrep == null) { Warn("Train a model first."); return; }
            using var d = new FolderBrowserDialog { Description = "Choose output folder" };
            if (d.ShowDialog() != DialogResult.OK) return;
            SetBusy(true, "Exporting…");

            var labelCol = cmbLabel.SelectedItem?.ToString() ?? "Label";
            var task = ParseTask();
            var savedModel = _savedModel!;
            var tempCsv = _lastPrep!.TempCsvPath;
            var outDir = d.SelectedPath;

            try
            {
                string csv = "", html = "";
                await Task.Run(() => (csv, html) = PredictionReportExporter.Export(
                    _ml, savedModel, tempCsv, labelCol, task, outDir));
                SetStatus("Exported.", false);
                if (MessageBox.Show(
                        $"CSV: {Path.GetFileName(csv)}\nHTML: {Path.GetFileName(html)}\n\nOpen HTML?",
                        "Done", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(html) { UseShellExecute = true });
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { SetBusy(false, ""); }
        }

        // ─────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────
        private TaskType ParseTask() => cmbTask.SelectedIndex switch
        {
            1 => TaskType.BinaryClassification,
            2 => TaskType.MulticlassClassification,
            3 => TaskType.Regression,
            4 => TaskType.TimeSeries,
            _ => TaskType.Auto
        };

        private bool IsNumeric(string col)
        {
            if (_raw == null) return false;
            int n = 0, t = 0;
            foreach (DataRow r in _raw.Rows)
            {
                var v = r[col]?.ToString();
                if (string.IsNullOrWhiteSpace(v)) continue;
                if (double.TryParse(v, out _)) n++;
                if (++t >= 30) break;
            }
            return t > 0 && (double)n / t > 0.75;
        }

        private static List<string> Checked(CheckedListBox c) =>
            c.CheckedItems.Cast<object>().Select(o => o.ToString()!).ToList();

        private static void SetAll(CheckedListBox c, bool v)
        { for (int i = 0; i < c.Items.Count; i++) c.SetItemChecked(i, v); }

        private void SetBusy(bool b, string msg)
        {
            if (InvokeRequired) { Invoke(() => SetBusy(b, msg)); return; }
            tsspb.Visible = b;
            if (msg != "") tssl.Text = msg;
        }

        private void SetStatus(string msg, bool b)
        {
            if (InvokeRequired) { Invoke(() => SetStatus(msg, b)); return; }
            tssl.Text = msg; tsspb.Visible = b;
        }

        private void LogColor(string t, Color c)
        {
            if (InvokeRequired) { Invoke(() => LogColor(t, c)); return; }
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = c;
            rtbLog.AppendText(t + "\n");
            rtbLog.ScrollToCaret();
        }

        private static void Warn(string msg) =>
            MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Console → RichTextBox writer
    // ════════════════════════════════════════════════════════════════════════
    internal class RtbWriter : System.IO.TextWriter
    {
        private readonly RichTextBox _r;
        private readonly Control _o;
        public override Encoding Encoding => Encoding.UTF8;
        public RtbWriter(RichTextBox r, Control o) { _r = r; _o = o; }
        public override void WriteLine(string? v) => Write(v + "\n");
        public override void Write(string? v)
        {
            if (string.IsNullOrEmpty(v)) return;
            var c = v.Contains("❌") || v.Contains("Error") ? Color.OrangeRed
                  : v.Contains("⚠") ? Color.Gold
                  : v.Contains("✅") || v.Contains("complete") || v.Contains("saved") ? Color.LightGreen
                  : v.Contains("🏆") || v.Contains("BEST") ? Color.Cyan
                  : v.StartsWith("═") || v.StartsWith("─") ? Color.FromArgb(100, 145, 215)
                  : Color.FromArgb(188, 210, 188);
            void Do()
            {
                _r.SelectionStart = _r.TextLength;
                _r.SelectionLength = 0;
                _r.SelectionColor = c;
                _r.AppendText(v);
                _r.ScrollToCaret();
            }
            if (_o.InvokeRequired) _o.BeginInvoke(Do); else Do();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Bulk-apply dialogs
    // ════════════════════════════════════════════════════════════════════════
    internal class ApplyCleaningDialog : Form
    {
        public bool SetMissing, SetOutlier;
        public string Missing = "Mean (average)", OutlierMethod = "None",
               OutlierAction = "Cap to boundary value";

        public ApplyCleaningDialog()
        {
            Text = "Bulk Apply – Cleaning";
            Size = new Size(480, 240);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;

            int y = 16;
            void Row(string lbl, CheckBox chk, ComboBox cmb, string[] items, int sel = 0)
            {
                chk.Location = new Point(14, y); chk.AutoSize = true; chk.Text = lbl;
                cmb.Location = new Point(240, y - 2); cmb.Width = 200;
                cmb.DropDownStyle = ComboBoxStyle.DropDownList;
                cmb.Items.AddRange(items.Cast<object>().ToArray());
                cmb.SelectedIndex = sel;
                Controls.Add(chk); Controls.Add(cmb); y += 34;
            }

            var chkM = new CheckBox(); var cmbM = new ComboBox();
            Row("Set Missing Strategy:", chkM, cmbM,
                new[] { "Mean (average)", "Median (middle)", "Mode (most frequent)",
                        "Delete Row", "None – leave as is" });

            var chkO = new CheckBox(); var cmbO = new ComboBox();
            Row("Set Outlier Method:", chkO, cmbO,
                new[] { "None", "IQR  (Q1 – k×IQR  to  Q3 + k×IQR)",
                        "Z-Score  ( |z| > threshold )" });

            var chkA = new CheckBox(); var cmbA = new ComboBox();
            Row("Set Outlier Action:", chkA, cmbA,
                new[] { "Cap to boundary value", "Remove the row" });

            y += 8;
            var ok = new Button
            {
                Text = "Apply",
                Location = new Point(270, y),
                Width = 90,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 98, 200),
                ForeColor = Color.White
            };
            ok.FlatAppearance.BorderSize = 0;
            var cn = new Button
            {
                Text = "Cancel",
                Location = new Point(368, y),
                Width = 80,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };

            ok.Click += (_, _) =>
            {
                SetMissing = chkM.Checked;
                Missing = cmbM.SelectedItem?.ToString() ?? "";
                SetOutlier = chkO.Checked || chkA.Checked;
                OutlierMethod = cmbO.SelectedItem?.ToString() ?? "";
                OutlierAction = cmbA.SelectedItem?.ToString() ?? "";
            };
            Controls.AddRange(new Control[] { ok, cn });
            AcceptButton = ok; CancelButton = cn;
            ClientSize = new Size(460, y + 44);
        }
    }

    internal class ApplyTransformDialog : Form
    {
        public bool SetNorm, SetEnc;
        public string Norm = "None – raw values",
               Enc = "One-Hot Encoding  (binary dummy columns)";

        public ApplyTransformDialog()
        {
            Text = "Bulk Apply – Transform";
            Size = new Size(480, 190);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;

            int y = 16;
            var chkN = new CheckBox { Text = "Set Normalization (numeric):", Location = new Point(14, y), AutoSize = true };
            var cmbN = new ComboBox { Location = new Point(240, y - 2), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbN.Items.AddRange(new object[] {
                "None – raw values", "Min-Max  →  [0, 1]",
                "Z-Score  →  (x − μ) / σ", "Decimal Scaling  →  ÷ 10ᵏ",
                "Log Transform  →  ln(x + 1)" });
            cmbN.SelectedIndex = 0; y += 34;

            var chkE = new CheckBox { Text = "Set Encoding (categorical):", Location = new Point(14, y), AutoSize = true };
            var cmbE = new ComboBox { Location = new Point(240, y - 2), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbE.Items.AddRange(new object[] {
                "One-Hot Encoding  (binary dummy columns)",
                "Label Encoding  (integer index)", "None – keep as text" });
            cmbE.SelectedIndex = 0; y += 44;

            var ok = new Button
            {
                Text = "Apply",
                Location = new Point(270, y),
                Width = 90,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 98, 200),
                ForeColor = Color.White
            };
            ok.FlatAppearance.BorderSize = 0;
            var cn = new Button
            {
                Text = "Cancel",
                Location = new Point(368, y),
                Width = 80,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };

            ok.Click += (_, _) =>
            {
                SetNorm = chkN.Checked; Norm = cmbN.SelectedItem?.ToString() ?? "";
                SetEnc = chkE.Checked; Enc = cmbE.SelectedItem?.ToString() ?? "";
            };
            Controls.AddRange(new Control[] { chkN, cmbN, chkE, cmbE, ok, cn });
            AcceptButton = ok; CancelButton = cn;
            ClientSize = new Size(460, y + 44);
        }
    }
}