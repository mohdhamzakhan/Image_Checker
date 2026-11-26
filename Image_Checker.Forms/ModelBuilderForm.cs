// ══════════════════════════════════════════════════════════════════════════════
//  ModelBuilderForm.cs  –  Code-behind for the redesigned form.
//
//  Time-Series task now uses Image_Checker.Forecasting (ForecastEngine):
//    • one forecast per series key (item, item|customer, ...)
//    • zero-filled calendar, demand-pattern aware models, rolling backtest
//    • result saved as  bestModel-Forecast-<stamp>.forecast.json
//  All other tasks (classification / regression) are unchanged.
// ══════════════════════════════════════════════════════════════════════════════

using Image_Checker.Forecasting;
using Image_Checker.Services;
using Microsoft.ML;
using System.Data;
using System.Text;

namespace Image_Checker.Forms
{
    public partial class ModelBuilderForm : Form
    {
        // ── state ──────────────────────────────────────────────────────────
        private readonly MLContext _ml = new(seed: 42);
        private readonly DataPreprocessor _pp = new();
        private DataTable? _raw;
        private PreprocessingSummary? _lastPrep;
        private string? _savedModel;      // primary model / forecast file path
        private CancellationTokenSource? _cts;
        private Forecasting.ForecastReport? _lastForecast;

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

            // Force handle creation for all hidden tabs immediately.
            // This prevents layout exceptions when filling the DataGridViews in the background.
            foreach (TabPage tab in tabMain.TabPages) { var _ = tab.Handle; }
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

        /// <summary>
        /// Picks a sensible default label column instead of blindly taking
        /// the last column in the file.
        ///
        /// Preference order:
        ///   1. A numeric column whose name matches a common target pattern
        ///      (quantity, amount, sales, revenue, demand, value, price...).
        ///   2. The first numeric column, if no name match is found.
        ///   3. The last column, only as a last resort when NO column in the
        ///      file is numeric at all.
        /// </summary>
        private string GuessLabelColumn(List<string> cols)
        {
            if (_raw == null || cols.Count == 0) return cols.LastOrDefault() ?? "";

            bool IsNumericCol(string col)
            {
                var type = _raw.Columns[col]?.DataType;
                return type == typeof(int) || type == typeof(long) || type == typeof(short)
                    || type == typeof(float) || type == typeof(double) || type == typeof(decimal);
            }

            var numericCols = cols.Where(IsNumericCol).ToList();
            if (numericCols.Count == 0) return cols[^1]; // nothing numeric — old fallback

            string[] targetPatterns =
                { "QUANTITY", "QTY", "AMOUNT", "SALES", "REVENUE", "DEMAND", "VALUE", "PRICE" };

            foreach (var pattern in targetPatterns)
            {
                var match = numericCols.FirstOrDefault(c =>
                    c.Contains(pattern, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            return numericCols[0];
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
            cmbLabel.SelectedIndex = Math.Max(0, cols.IndexOf(GuessLabelColumn(cols)));

            // Time-series date column: pre-select a date-looking column
            var dateLike = cols.FirstOrDefault(c =>
                c.Contains("DATE", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("TRX", StringComparison.OrdinalIgnoreCase));

            cmbTsDate.Items.Clear();
            cmbTsDate.Items.Add("(none)");
            cols.ForEach(c => cmbTsDate.Items.Add(c));
            cmbTsDate.SelectedIndex = dateLike != null ? cols.IndexOf(dateLike) + 1 : 0;

            cmbSplitDateCol.Items.Clear();
            cmbSplitDateCol.Items.Add("(none)");
            cols.ForEach(c => cmbSplitDateCol.Items.Add(c));

            // For sales/forecasting data a time-based split is far more often
            // correct than a random split, so default to it when a date column exists.
            if (dateLike != null)
            {
                cmbSplitDateCol.SelectedItem = dateLike;
                chkTimeBasedSplit.Checked = true;
            }
            else
            {
                cmbSplitDateCol.SelectedIndex = 0;
                chkTimeBasedSplit.Checked = false;
            }

            clbFeatures.Items.Clear(); clbIgnore.Items.Clear();
            cols.ForEach(c => { clbFeatures.Items.Add(c, true); clbIgnore.Items.Add(c, false); });

            // Series keys for forecasting: one forecast per combination of the ticked columns.
            // Default = columns that look like an item / SKU / product id.
            // (Do NOT match "PART": PARTY_NAME would match it.)
            clbSeriesKeys.Items.Clear();
            foreach (var c in cols)
            {
                bool key = new[] { "ITEM", "SKU", "PRODUCT" }
                    .Any(p => c.Contains(p, StringComparison.OrdinalIgnoreCase));
                clbSeriesKeys.Items.Add(c, key);
            }

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

            // ── Series keys: the quantity column can never be a key ────────
            int keyIdx = clbSeriesKeys.Items.IndexOf(newLabel);
            if (keyIdx >= 0) clbSeriesKeys.SetItemChecked(keyIdx, false);

            // ── Preprocessing grids ───────────────────────────────────────
            HideGridRow(dgvCleaning, newLabel, _previousLabel);
            HideGridRow(dgvTransform, newLabel, _previousLabel);
            HideGridRow(dgvReduction, newLabel, _previousLabel);

            _previousLabel = newLabel;
        }

        private static void HideGridRow(DataGridView grid, string labelName, string previousLabel)
        {
            // Prevent Handle crashes by dropping active cell focus before hiding rows
            grid.CurrentCell = null;

            foreach (DataGridViewRow row in grid.Rows)
            {
                string colName = row.Cells["ColName"].Value?.ToString() ?? string.Empty;

                if (colName == labelName)
                {
                    row.Visible = false;
                    row.ReadOnly = true;
                }
                else if (colName == previousLabel && !string.IsNullOrEmpty(previousLabel))
                {
                    row.Visible = true;
                    row.ReadOnly = false;
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
                UseTimeBasedSplit = chkTimeBasedSplit.Checked,
                SplitDateColumn = cmbSplitDateCol.SelectedIndex > 0
                                    ? cmbSplitDateCol.SelectedItem?.ToString() : null,
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
            // ── 1. Validate EVERYTHING before touching the UI state ─────────
            if (_raw == null) { Warn("Load a data file first."); return; }
            string lbl = cmbLabel.SelectedItem?.ToString() ?? "";
            if (lbl == "") { Warn("Select a label column."); return; }

            bool ts = ParseTask() == TaskType.TimeSeries;

            // forecasting inputs
            var keyCols = Checked(clbSeriesKeys).Where(c => c != lbl).ToList();
            string dateCol = cmbTsDate.SelectedIndex > 0 ? cmbTsDate.SelectedItem?.ToString() ?? "" : "";

            if (ts)
            {
                if (dateCol == "")
                { Warn("Pick the date column in the Time-Series options."); return; }
                if (keyCols.Count == 0)
                { Warn("Tick at least one series key column (for example the item column)."); return; }
                if (keyCols.Contains(dateCol, StringComparer.OrdinalIgnoreCase))
                { Warn("The date column cannot also be a series key."); return; }
            }

            Granularity gran = Granularity.Month;
            if (Enum.TryParse(cmbTsGran.SelectedItem?.ToString(), true, out Granularity parsedGran))
                gran = parsedGran;      // "Year" (not supported by the engine) falls back to Month

            var fcOpt = new ForecastOptions
            {
                Granularity = gran,
                Horizon = (int)nudTsHorizon.Value > 0 ? (int)nudTsHorizon.Value : 6,
                IntervalLevel = (double)nudTsConf.Value / 100.0
            };

            var feat = Checked(clbFeatures).Where(c => c != lbl).ToList();
            var ign = Checked(clbIgnore);
            if (!ts && feat.Count == 0) { Warn("Select at least one feature column."); return; }

            var catCols = dgvTransform.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells["Enc"].Value?.ToString()?.StartsWith("One-Hot") == true
                         || r.Cells["Enc"].Value?.ToString()?.StartsWith("Label") == true)
                .Select(r => r.Cells["ColName"].Value?.ToString() ?? "")
                .Where(c => c != "" && feat.Contains(c))
                .ToList();

            // ── 2. Snapshot ALL UI values on the UI thread BEFORE Task.Run ──
            var filePath = txtFilePath.Text;
            var ppConfig = BuildPPConfig();
            var trainerBase = SnapshotTrainerConfig();
            var outDir = trainerBase.OutputDirectory;

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
            _lastForecast = null;
            _lastPrep = null;

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

                        // ── NEW forecasting path ──────────────────────────────
                        // Deliberately skips DataPreprocessor: removing "duplicate" rows or
                        // capping "outliers" on raw sales transactions destroys real demand.
                        if (ts)
                        {
                            var recs = ForecastDataLoader.FromDataTable(
                                _raw!, dateCol, lbl, keyCols, Console.WriteLine);
                            var rep = ForecastEngine.Run(recs, fcOpt, Console.WriteLine, ct);
                            _lastForecast = rep;
                            mPath = ForecastStore.Save(rep, keyCols, lbl, outDir);
                            return;
                        }

                        // ── Classification / regression path (unchanged) ──────
                        prep = _pp.Run(filePath, lbl, feat, catCols, ign, ppConfig);
                        ct.ThrowIfCancellationRequested();

                        var cfg = new DataTrainerConfig
                        {
                            DataFilePath = prep.TempCsvPath,
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
                if (err != null)
                { LogColor($"\n❌ {err.GetBaseException().Message}", Color.OrangeRed); SetStatus("Failed.", false); return; }
                if (mPath == null)
                { LogColor("\n⚠️ Cancelled.", Color.Gold); SetStatus("Cancelled.", false); return; }

                _savedModel = mPath;
                _lastPrep = prep;

                LogColor($"\n✅ Saved:\n   {mPath}", Color.LightGreen);
                FillResults(mPath);
                tabMain.SelectedTab = tpResults;
                SetStatus("Training complete.", false);
            }
            catch (OperationCanceledException)
            { LogColor("\n⚠️ Cancelled.", Color.Gold); SetStatus("Cancelled.", false); }
            catch (Exception ex)
            { LogColor($"\n❌ {ex.Message}", Color.OrangeRed); SetStatus("Error.", false); }
            finally
            {
                Console.SetOut(old);        // always restore the console, whatever happened
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

            var dt = new DataTable();
            dt.Columns.Add("Property"); dt.Columns.Add("Value");

            // ── Forecast result ───────────────────────────────────────────
            if (ParseTask() == TaskType.TimeSeries && _lastForecast != null)
            {
                var f = _lastForecast;
                lblResultTitle.Text = "📈  Forecast complete";

                dt.Rows.Add("Forecast file", Path.GetFileName(path));
                dt.Rows.Add("Saved to", Path.GetDirectoryName(path));
                dt.Rows.Add("Series forecast", f.Series.Count.ToString("N0"));
                dt.Rows.Add("Granularity", f.Options.Granularity.ToString());
                dt.Rows.Add("Horizon", f.Options.Horizon);
                dt.Rows.Add("Backtest WAPE (chosen models)",
                    double.IsNaN(f.OverallWape) ? "n/a" : f.OverallWape.ToString("P1"));
                dt.Rows.Add("Backtest WAPE (baseline)",
                    double.IsNaN(f.BaselineWape) ? "n/a" : f.BaselineWape.ToString("P1"));
                dt.Rows.Add("Series beating baseline",
                    double.IsNaN(f.ShareBeatingBaseline) ? "n/a" : f.ShareBeatingBaseline.ToString("P0"));
                dt.Rows.Add("", "");
                dt.Rows.Add("Open in", "Prediction form → Browse & Load → this .forecast.json");

                dgvResults.DataSource = dt;
                rtbResultLog.Clear();
                rtbResultLog.AppendText(f.SummaryText());
                btnSaveModel.Enabled = btnExportReport.Enabled = true;
                return;
            }

            // ── Classification / regression result ─────────────────────────
            lblResultTitle.Text = $"🏆  Best model: {Path.GetFileNameWithoutExtension(path)}";

            dt.Rows.Add("Model File", Path.GetFileName(path));
            dt.Rows.Add("Saved To", Path.GetDirectoryName(path));
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
            if (_lastPrep != null)
                foreach (var l in _lastPrep.Log) rtbResultLog.AppendText(l + "\n");

            btnSaveModel.Enabled = btnExportReport.Enabled = true;
        }

        // ─────────────────────────────────────────────────────────────────
        //  SAVE & EXPORT
        // ─────────────────────────────────────────────────────────────────
        private void OnSave(object? s, EventArgs e)
        {
            if (_savedModel == null || !File.Exists(_savedModel))
            { Warn("No model found."); return; }

            // Forecast file: a single self-contained .forecast.json
            if (ForecastStore.IsForecastFile(_savedModel))
            {
                using var fd = new SaveFileDialog
                {
                    Filter = "Forecast (*.forecast.json)|*.forecast.json",
                    FileName = Path.GetFileName(_savedModel)
                };
                if (fd.ShowDialog() != DialogResult.OK) return;
                File.Copy(_savedModel, fd.FileName, true);
                MessageBox.Show($"Saved to:\n{fd.FileName}", "Done",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

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
            // Forecast: export the two CSVs (forecast + accuracy)
            if (_lastForecast != null && ParseTask() == TaskType.TimeSeries)
            {
                using var fb = new FolderBrowserDialog { Description = "Choose output folder" };
                if (fb.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var fcPath = Path.Combine(fb.SelectedPath, "forecast.csv");
                    var acPath = Path.Combine(fb.SelectedPath, "accuracy.csv");
                    _lastForecast.ExportForecastCsv(fcPath);
                    _lastForecast.ExportAccuracyCsv(acPath);
                    MessageBox.Show($"Exported:\n{fcPath}\n{acPath}", "Done",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                { MessageBox.Show(ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                return;
            }

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