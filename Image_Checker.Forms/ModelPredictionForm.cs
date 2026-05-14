// ══════════════════════════════════════════════════════════════════════════════
//  ModelPredictionForm.cs  –  Complete rewrite with working predictions
// ══════════════════════════════════════════════════════════════════════════════

using Image_Checker.Services;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Image_Checker.Forms
{
    public partial class ModelPredictionForm : Form
    {
        // ── ML state ───────────────────────────────────────────────────────
        private readonly MLContext _ml = new MLContext(seed: 42);
        private ITransformer? _model;
        private DataViewSchema? _schema;
        private ModelMetaConfig? _meta;
        private string? _modelPath;
        private DataTable? _lastOutput;
        private bool _clampNegative = true;

        // ── Constructor ────────────────────────────────────────────────────
        public ModelPredictionForm()
        {
            InitializeComponent();
            InitForm();
            WireEvents();
        }

        // ── Event wiring ───────────────────────────────────────────────────
        private void WireEvents()
        {
            btnLoadModel.Click += OnLoadModel;
            btnPredict.Click += OnPredict;
            btnForecast.Click += OnForecast;
            btnExportCsv.Click += (_, _) => ExportResults("csv");
            btnExportHtml.Click += (_, _) => ExportResults("html");
            btnAddRow.Click += OnAddRow;
            btnClearRows.Click += (_, _) => ClearInputGrid();
        }

        // ════════════════════════════════════════════════════════════════════
        //  LOAD MODEL
        // ════════════════════════════════════════════════════════════════════
        private void OnLoadModel(object? s, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            { Filter = "ML.NET Model (*.zip)|*.zip|All Files|*.*", Title = "Select Model" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                _modelPath = dlg.FileName;
                txtModelPath.Text = _modelPath;
                _model = _ml.Model.Load(_modelPath, out _schema);

                var jsonPath = Path.ChangeExtension(_modelPath, ".json");
                _meta = File.Exists(jsonPath)
                    ? JsonSerializer.Deserialize<ModelMetaConfig>(
                        File.ReadAllText(jsonPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    : null;

                bool isTs = _meta?.Task?.Equals("TimeSeries",
                    StringComparison.OrdinalIgnoreCase) ?? false;

                if (isTs)
                {
                    lblModelInfo.Text =
                        $"TimeSeries (SSA)  |  Label: {_meta?.LabelColumn ?? "?"}  |  " +
                        $"Horizon: {_meta?.HorizonSteps} {_meta?.Granularity ?? "steps"}  |  " +
                        $"Trained: {_meta?.TrainedAt?[..10] ?? "?"}";

                    SetupForecastTab();
                    tabMain.SelectedTab = tabForecast;
                    tabForecast.Enabled = true;
                    tabInput.Enabled = false;
                }
                else
                {
                    lblModelInfo.Text =
                        $"Task: {_meta?.Task ?? "Regression"}  |  " +
                        $"Label: {_meta?.LabelColumn ?? "?"}  |  " +
                        $"Best Model: {_meta?.BestModel ?? "?"}  |  " +
                        $"R²: {_meta?.PrimaryMetric:F4}  |  " +
                        $"Trained: {_meta?.TrainedAt?[..10] ?? "?"}";

                    BuildInputGrid();
                    tabMain.SelectedTab = tabInput;
                    tabInput.Enabled = true;
                    tabForecast.Enabled = false;
                }

                SetStatus($"Loaded: {Path.GetFileName(_modelPath)}", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load model:\n{ex.Message}",
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  INPUT GRID  –  dropdowns populated from JSON unique values
        // ════════════════════════════════════════════════════════════════════
        private void BuildInputGrid()
        {
            pnlInputGrid.Controls.Clear();
            if (_meta == null) return;

            var rawCols = (_meta.FeatureColumns ?? Array.Empty<string>())
                .Where(c => !string.Equals(c, _meta.LabelColumn, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (rawCols.Length == 0) return;

            var catSet = new HashSet<string>(
                _meta.CategoricalColumns ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Padding = new Padding(8, 8, 8, 8)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            tbl.RowCount = rawCols.Length + 1;

            void AddHeader(string t, int col) =>
                tbl.Controls.Add(new Label
                {
                    Text = t,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(41, 98, 200),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(6, 0, 0, 0),
                    Height = 28
                }, col, 0);

            AddHeader("Column", 0); AddHeader("Value", 1); AddHeader("Type", 2);

            _inputControls.Clear();
            for (int ri = 0; ri < rawCols.Length; ri++)
            {
                string col = rawCols[ri];
                bool isCat = catSet.Contains(col);
                Color bg = ri % 2 == 0 ? Color.FromArgb(245, 248, 255) : Color.White;

                var lbl = new Label
                {
                    Text = col,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 60, 120),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(6, 0, 0, 0),
                    BackColor = bg
                };

                Control input;
                if (_meta.UniqueValues != null
                    && _meta.UniqueValues.TryGetValue(col, out var vals)
                    && vals.Count > 0)
                {
                    var cmb = new ComboBox
                    {
                        Dock = DockStyle.Fill,
                        DropDownStyle = ComboBoxStyle.DropDown,
                        Font = new Font("Segoe UI", 9f),
                        BackColor = bg,
                        Tag = col
                    };
                    cmb.Items.Add("");
                    foreach (var v in vals) cmb.Items.Add(v);
                    cmb.SelectedIndex = 0;
                    cmb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    cmb.AutoCompleteSource = AutoCompleteSource.ListItems;
                    input = cmb;
                }
                else
                {
                    input = new TextBox
                    {
                        Dock = DockStyle.Fill,
                        Font = new Font("Segoe UI", 9f),
                        BackColor = bg,
                        Tag = col
                    };
                }

                var badge = new Label
                {
                    Text = isCat ? "text" : "number",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = Color.White,
                    BackColor = isCat ? Color.FromArgb(100, 130, 200) : Color.FromArgb(40, 160, 100)
                };

                tbl.Controls.Add(lbl, 0, ri + 1);
                tbl.Controls.Add(input, 1, ri + 1);
                tbl.Controls.Add(badge, 2, ri + 1);
                _inputControls[col] = input;
            }

            pnlInputGrid.Controls.Add(tbl);
            pnlInputGrid.AutoScroll = true;
            lblColumnHint.Text = $"Fill in values for {rawCols.Length} feature column(s), then click Predict.";
        }

        private readonly Dictionary<string, Control> _inputControls = new();

        private void ClearInputGrid()
        {
            foreach (var ctrl in _inputControls.Values)
            {
                if (ctrl is ComboBox cmb) cmb.SelectedIndex = 0;
                else if (ctrl is TextBox tb) tb.Text = "";
            }
        }

        private void OnAddRow(object? s, EventArgs e) => ClearInputGrid();

        // ════════════════════════════════════════════════════════════════════
        //  RUN TABULAR PREDICTION
        // ════════════════════════════════════════════════════════════════════
        private async void OnPredict(object? s, EventArgs e)
        {
            if (_model == null) { AlertNoModel(); return; }
            if (_inputControls.Count == 0)
            {
                MessageBox.Show("Load a model first.", "No Model", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var inputValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _inputControls)
            {
                inputValues[kv.Key] = kv.Value switch
                {
                    ComboBox cmb => cmb.Text?.Trim() ?? "",
                    TextBox tb => tb.Text?.Trim() ?? "",
                    _ => ""
                };
            }

            var empty = inputValues.Where(kv => kv.Value == "").Select(kv => kv.Key).ToList();
            if (empty.Count > 0)
            {
                var ans = MessageBox.Show(
                    $"The following columns are empty:\n{string.Join(", ", empty)}\n\n" +
                    "Proceed anyway? (empty values will be treated as 0 / unknown)",
                    "Missing Values", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ans == DialogResult.No) return;
            }

            SetBusy(true, "Running prediction...");
            DataTable? result = null;
            Exception? err = null;
            var metaSnapshot = _meta;

            await Task.Run(() =>
            {
                try { result = PredictTabular(inputValues, metaSnapshot); }
                catch (Exception ex) { err = ex; }
            });

            SetBusy(false, "");
            if (err != null)
            {
                MessageBox.Show($"Prediction failed:\n{err.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ShowOutput(result!, $"Predicted  |  Label: {_meta?.LabelColumn ?? "?"}");
            tabMain.SelectedTab = tabOutput;
        }

        private DataTable PredictTabular(
            Dictionary<string, string> inputValues,
            ModelMetaConfig? meta)
        {
            var catSet = new HashSet<string>(
                meta?.CategoricalColumns ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            var columns = inputValues.Keys.ToArray();
            var tmp = Path.Combine(Path.GetTempPath(), $"pred_{Guid.NewGuid():N}.csv");

            try
            {
                using (var sw = new StreamWriter(tmp, false, Encoding.UTF8))
                {
                    sw.WriteLine(string.Join(",", columns.Select(CsvEsc)));
                    sw.WriteLine(string.Join(",", columns.Select(c => CsvEsc(inputValues[c]))));
                }

                var loaderCols = columns.Select((c, i) =>
                    new TextLoader.Column(c, catSet.Contains(c) ? DataKind.String : DataKind.Single, i))
                    .ToArray();

                var loader = _ml.Data.CreateTextLoader(new TextLoader.Options
                {
                    HasHeader = true,
                    Separators = new[] { ',' },
                    Columns = loaderCols,
                    AllowQuoting = true,
                    TrimWhitespace = true
                });

                var data = loader.Load(tmp);
                var preds = _model!.Transform(data);

                float predicted = 0f;
                try { predicted = preds.GetColumn<float>("Score").First(); }
                catch
                {
                    try
                    {
                        var lbl = preds.GetColumn<string>("PredictedLabel").First();
                        float.TryParse(lbl, out predicted);
                    }
                    catch { }
                }

                if (_clampNegative) predicted = Math.Max(0f, predicted);

                var result = new DataTable();
                result.Columns.Add("Column", typeof(string));
                result.Columns.Add("Input Value", typeof(string));
                foreach (var kv in inputValues) result.Rows.Add(kv.Key, kv.Value);
                result.Rows.Add("", "");
                result.Rows.Add("► PREDICTED " + (meta?.LabelColumn ?? "QUANTITY").ToUpper(),
                    predicted.ToString("N2"));
                return result;
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  FORECAST TAB SETUP
        // ════════════════════════════════════════════════════════════════════
        private void SetupForecastTab()
        {
            if (_meta == null) return;
            if (_meta.HorizonSteps > 0)
                nudHorizonCount.Value = Math.Min(_meta.HorizonSteps, nudHorizonCount.Maximum);

            for (int i = 0; i < cmbGranularity.Items.Count; i++)
                if (string.Equals(cmbGranularity.Items[i]?.ToString(),
                    _meta.Granularity, StringComparison.OrdinalIgnoreCase))
                { cmbGranularity.SelectedIndex = i; break; }

            dtpStartDate.Value = DateTime.Today;
            BuildFilterPanel();
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILTER PANEL
        // ════════════════════════════════════════════════════════════════════
        private readonly Dictionary<string, ComboBox> _filterControls = new();

        private void BuildFilterPanel()
        {
            pnlFilterGrid.Controls.Clear();
            _filterControls.Clear();
            if (_meta?.FeatureColumns == null) return;

            var filterCols = _meta.FeatureColumns
                .Where(c => !string.Equals(c, _meta.DateColumn, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (filterCols.Length == 0) return;

            var tbl = new TableLayoutPanel
            { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(0, 4, 0, 4) };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));

            foreach (var (t, col) in new[] { ("Column", 0), ("Filter Value (blank = all)", 1), ("Type", 2) })
                tbl.Controls.Add(new Label
                {
                    Text = t,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(41, 98, 200),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0),
                    Height = 26
                }, col, 0);

            var catSet = new HashSet<string>(
                _meta.CategoricalColumns ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            for (int ri = 0; ri < filterCols.Length; ri++)
            {
                string col = filterCols[ri];
                bool isCat = catSet.Contains(col);
                Color bg = ri % 2 == 0 ? Color.FromArgb(245, 248, 255) : Color.White;

                tbl.Controls.Add(new Label
                {
                    Text = col,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 60, 120),
                    BackColor = bg,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0)
                }, 0, ri + 1);

                var cmb = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDown,
                    Font = new Font("Segoe UI", 8.5f),
                    BackColor = bg,
                    Tag = col
                };
                cmb.Items.Add("");
                if (_meta.UniqueValues != null && _meta.UniqueValues.TryGetValue(col, out var vals))
                    foreach (var v in vals) cmb.Items.Add(v);
                cmb.SelectedIndex = 0;
                cmb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                cmb.AutoCompleteSource = AutoCompleteSource.ListItems;

                tbl.Controls.Add(cmb, 1, ri + 1);
                tbl.Controls.Add(new Label
                {
                    Text = isCat ? "text" : "num",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = Color.White,
                    BackColor = isCat ? Color.FromArgb(100, 130, 200) : Color.FromArgb(40, 160, 100)
                }, 2, ri + 1);

                _filterControls[col] = cmb;
            }

            pnlFilterGrid.Controls.Add(tbl);
            pnlFilterGrid.AutoScroll = true;
        }

        private Dictionary<string, string> GetFilterValues()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _filterControls)
            {
                string val = kv.Value.Text?.Trim() ?? "";
                if (val != "") result[kv.Key] = val;
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  RUN FORECAST
        // ════════════════════════════════════════════════════════════════════
        private async void OnForecast(object? s, EventArgs e)
        {
            if (_model == null) { AlertNoModel(); return; }

            int horizon = (int)nudHorizonCount.Value;
            string gran = cmbGranularity.SelectedItem?.ToString() ?? "Month";
            DateTime startDate = dtpStartDate.Value.Date;
            var filterVals = GetFilterValues();
            var labelCol = _meta?.LabelColumn ?? "QUANTITY_INVOICED";

            string filterDesc = filterVals.Count > 0
                ? "  |  " + string.Join(", ", filterVals.Select(kv => $"{kv.Key}={kv.Value}"))
                : "  |  All data";

            SetBusy(true, $"Forecasting {horizon} {gran}(s)...");
            DataTable? result = null;
            Exception? err = null;

            await Task.Run(() =>
            {
                try { result = RunSSAForecast(horizon, gran, startDate, filterVals); }
                catch (Exception ex) { err = ex; }
            });

            SetBusy(false, "");
            if (err != null)
            {
                MessageBox.Show($"Forecast failed:\n{err.Message}",
                    "Forecast Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ShowOutput(result!, $"SSA Forecast: {horizon} {gran}(s)  |  {labelCol}{filterDesc}");
            tabMain.SelectedTab = tabOutput;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SSA FORECAST ENGINE
        // ════════════════════════════════════════════════════════════════════
        private DataTable RunSSAForecast(int horizon, string gran, DateTime startDate,
            Dictionary<string, string> filterValues)
        {
            bool hasFilters = filterValues.Count > 0;

            if (hasFilters && _meta?.DateColumn != null && _meta?.LabelColumn != null)
            {
                string? rowsJson = _modelPath != null
                    ? Path.ChangeExtension(_modelPath, ".rows.json") : null;
                string? rawCsv = _meta?.RawDataPath;

                if (rowsJson != null && File.Exists(rowsJson))
                    return RunFilteredForecast(horizon, gran, startDate, filterValues,
                        rowsJson, _meta!.DateColumn, _meta.LabelColumn, useJson: true);

                if (rawCsv != null && File.Exists(rawCsv))
                    return RunFilteredForecast(horizon, gran, startDate, filterValues,
                        rawCsv, _meta!.DateColumn, _meta!.LabelColumn, useJson: false);

                throw new InvalidOperationException(
                    "Filtered forecast needs the companion .rows.json file.\n" +
                    "Please retrain the model to generate it automatically.");
            }

            // Global forecast using the pre-trained SSA model
            var dummyRows = Enumerable.Repeat(new TsValueRow { Value = 0f }, horizon).ToList();
            var dummyView = _ml.Data.LoadFromEnumerable(dummyRows);
            var out_ = _model!.Transform(dummyView);

            float[] fc, lo, hi;
            try
            {
                fc = out_.GetColumn<float[]>("Forecast").FirstOrDefault() ?? Array.Empty<float>();
                lo = out_.GetColumn<float[]>("LowerBound").FirstOrDefault() ?? Array.Empty<float>();
                hi = out_.GetColumn<float[]>("UpperBound").FirstOrDefault() ?? Array.Empty<float>();
            }
            catch
            {
                fc = TryFloatCol(out_, "Forecast").ToArray();
                lo = TryFloatCol(out_, "LowerBound").ToArray();
                hi = TryFloatCol(out_, "UpperBound").ToArray();
            }

            if (fc.Length == 0)
                throw new InvalidOperationException(
                    "Model produced no forecast values. " +
                    "Ensure the SSA model file (bestModel-SSA-*.zip) is loaded.");

            return BuildForecastTable(fc, lo, hi, horizon, gran, startDate);
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILTERED FORECAST
        // ════════════════════════════════════════════════════════════════════
        private DataTable RunFilteredForecast(
            int horizon, string gran, DateTime startDate,
            Dictionary<string, string> filters,
            string dataPath, string dateCol, string labelCol, bool useJson)
        {
            List<Dictionary<string, string>> allRows;
            if (useJson)
            {
                var jsonText = File.ReadAllText(dataPath);
                allRows = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonText)
                    ?? new List<Dictionary<string, string>>();
            }
            else
            {
                allRows = ReadCsvAsRows(dataPath);
            }

            if (allRows.Count == 0)
                throw new InvalidOperationException("Data file is empty.");

            var periodSums = new SortedDictionary<DateTime, double>();
            int matchedRows = 0;

            foreach (var row in allRows)
            {
                if (!filters.All(kv =>
                {
                    string cell = row.TryGetValue(kv.Key, out var v) ? v?.Trim() ?? "" : "";
                    return string.Equals(cell, kv.Value, StringComparison.OrdinalIgnoreCase);
                })) continue;

                matchedRows++;

                string rawDate = row.TryGetValue(dateCol, out var dv) ? dv ?? "" : "";
                DateTime? pd = ParseDate(rawDate);
                if (!pd.HasValue) continue;

                string rawQty = row.TryGetValue(labelCol, out var qv) ? qv ?? "" : "";
                if (!double.TryParse(rawQty, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double qty)) continue;

                DateTime key = gran switch
                {
                    "Year" => new DateTime(pd.Value.Year, 1, 1),
                    "Day" => pd.Value.Date,
                    _ => new DateTime(pd.Value.Year, pd.Value.Month, 1)
                };

                if (periodSums.ContainsKey(key)) periodSums[key] += qty;
                else periodSums[key] = qty;
            }

            if (matchedRows == 0)
                throw new InvalidOperationException(
                    "No rows matched the filters:\n" +
                    string.Join("\n", filters.Select(kv => $"  {kv.Key} = {kv.Value}")) +
                    "\n\nValues are case-sensitive. Check the dropdown values.");

            if (periodSums.Count < 4)
                throw new InvalidOperationException(
                    $"Only {periodSums.Count} period(s) after filtering " +
                    $"(matched {matchedRows:N0} rows). SSA needs >= 4 periods. " +
                    "Try a coarser granularity (Month instead of Day).");

            var series = periodSums.Values.Select(v => (float)v).ToList();
            var seriesRows = series.Select(v => new TsValueRow { Value = v }).ToList();
            var seriesView = _ml.Data.LoadFromEnumerable(seriesRows);
            int n = series.Count;

            int windowSize = Math.Max(horizon + 1, n / 3);
            if (windowSize > n / 2) windowSize = Math.Max(2, n / 2);
            if (windowSize <= horizon) windowSize = horizon + 1;
            windowSize = Math.Min(windowSize, n - 1);
            windowSize = Math.Max(windowSize, 2);
            int seriesLen = n;
            if (seriesLen <= windowSize) seriesLen = Math.Min(windowSize + 1, n);

            var ssaPipeline = _ml.Forecasting.ForecastBySsa(
                outputColumnName: "Forecast", inputColumnName: "Value",
                windowSize: windowSize, seriesLength: seriesLen,
                trainSize: n, horizon: horizon,
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: "LowerBound",
                confidenceUpperBoundColumn: "UpperBound");

            var ssaModel = ssaPipeline.Fit(seriesView);
            var eng = ssaModel.CreateTimeSeriesEngine<TsValueRow, TsForecastRow>(_ml);
            var forecast = eng.Predict();

            return BuildForecastTable(
                forecast.Forecast, forecast.LowerBound, forecast.UpperBound,
                horizon, gran, startDate);
        }

        // ════════════════════════════════════════════════════════════════════
        //  BUILD FORECAST TABLE
        // ════════════════════════════════════════════════════════════════════
        private DataTable BuildForecastTable(
            float[] fc, float[] lo, float[] hi,
            int horizon, string gran, DateTime startDate)
        {
            var dt = new DataTable();
            dt.Columns.Add("Step", typeof(int));
            dt.Columns.Add("Period", typeof(string));
            dt.Columns.Add("Forecast", typeof(double));
            dt.Columns.Add("Lower (95%)", typeof(double));
            dt.Columns.Add("Upper (95%)", typeof(double));

            double Clamp(double v) => _clampNegative ? Math.Max(0.0, v) : v;

            int steps = Math.Min(fc.Length, horizon);
            for (int i = 0; i < steps; i++)
            {
                string period = gran switch
                {
                    "Year" => startDate.AddYears(i + 1).ToString("yyyy"),
                    "Day" => startDate.AddDays(i + 1).ToString("yyyy-MM-dd"),
                    _ => startDate.AddMonths(i + 1).ToString("yyyy-MM")
                };
                dt.Rows.Add(
                    i + 1, period,
                    Math.Round(Clamp(fc[i]), 2),
                    Math.Round(Clamp(lo.Length > i ? lo[i] : 0), 2),
                    Math.Round(hi.Length > i ? (double)hi[i] : 0, 2));
            }
            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════
        private static DateTime? ParseDate(string raw)
        {
            raw = raw?.Trim().Trim('"') ?? string.Empty;
            if (string.IsNullOrEmpty(raw)) return null;
            string[] fmts = {
                "dd-MM-yyyy HH:mm", "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy",
                "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                "MM/dd/yyyy", "yyyy/MM/dd", "dd-MMM-yyyy"
            };
            if (DateTime.TryParseExact(raw, fmts,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt)) return dt;
            if (DateTime.TryParse(raw,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt2)) return dt2;
            return null;
        }

        private static List<Dictionary<string, string>> ReadCsvAsRows(string path)
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length < 2) return new List<Dictionary<string, string>>();
            var headers = ParseCsvLine(lines[0]);
            var result = new List<Dictionary<string, string>>();
            for (int li = 1; li < lines.Length; li++)
            {
                if (string.IsNullOrWhiteSpace(lines[li])) continue;
                var cells = ParseCsvLine(lines[li]);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int ci = 0; ci < headers.Length && ci < cells.Length; ci++)
                    row[headers[ci]] = cells[ci];
                result.Add(row);
            }
            return result;
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQ = false;
            foreach (char ch in line)
            {
                if (ch == '"') inQ = !inQ;
                else if (ch == ',' && !inQ) { fields.Add(sb.ToString().Trim()); sb.Clear(); }
                else sb.Append(ch);
            }
            fields.Add(sb.ToString().Trim());
            return fields.Select(f => f.Trim('"')).ToArray();
        }

        private static List<float> TryFloatCol(IDataView view, string col)
        {
            try { return view.GetColumn<float>(col).ToList(); }
            catch { return new List<float>(); }
        }

        private static string CsvEsc(string s) =>
            s.Contains(',') || s.Contains('"') || s.Contains('\n')
                ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        // ════════════════════════════════════════════════════════════════════
        //  OUTPUT / EXPORT
        // ════════════════════════════════════════════════════════════════════
        private void ShowOutput(DataTable dt, string info)
        {
            _lastOutput = dt;
            dgvOutput.DataSource = null;
            dgvOutput.DataSource = dt;
            lblOutputInfo.Text = $"{info}  |  {dt.Rows.Count:N0} rows";
            btnExportCsv.Enabled = btnExportHtml.Enabled = true;
            ApplyOutputGridStyle();
        }

        private void ApplyOutputGridStyle()
        {
            if (dgvOutput.Columns.Count == 0) return;
            foreach (DataGridViewColumn col in dgvOutput.Columns)
            {
                col.DefaultCellStyle.Alignment =
                    col.ValueType == typeof(double) || col.ValueType == typeof(float)
                        ? DataGridViewContentAlignment.MiddleRight
                        : DataGridViewContentAlignment.MiddleLeft;
                if (col.ValueType == typeof(double))
                    col.DefaultCellStyle.Format = "N2";
            }
            foreach (DataGridViewRow row in dgvOutput.Rows)
            {
                var cell = row.Cells[0].Value?.ToString() ?? "";
                if (cell.StartsWith("►"))
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(200, 240, 210);
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(0, 100, 0);
                }
            }
        }

        private void ExportResults(string format)
        {
            if (_lastOutput == null) return;
            using var dlg = new SaveFileDialog
            {
                Filter = format == "csv" ? "CSV (*.csv)|*.csv" : "HTML (*.html)|*.html",
                FileName = $"prediction_{DateTime.Now:yyyyMMddHHmm}"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            if (format == "csv") WriteCsv(_lastOutput, dlg.FileName);
            else WriteHtml(_lastOutput, dlg.FileName);
            MessageBox.Show($"Exported:\n{dlg.FileName}", "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void WriteCsv(DataTable dt, string path)
        {
            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => CsvEsc(c.ColumnName))));
            foreach (DataRow r in dt.Rows)
                sw.WriteLine(string.Join(",", r.ItemArray.Select(v => CsvEsc(v?.ToString() ?? ""))));
        }

        private static void WriteHtml(DataTable dt, string path)
        {
            var sb = new StringBuilder();
            sb.Append(
                "<!DOCTYPE html><html><head><meta charset='UTF-8'>" +
                "<title>Prediction Results</title><style>" +
                "body{font-family:Segoe UI,sans-serif;margin:32px;color:#222;background:#f5f7fc}" +
                "h2{color:#2962c0;margin-bottom:4px}" +
                ".meta{color:#666;font-size:12px;margin-bottom:16px}" +
                "table{border-collapse:collapse;width:100%;font-size:13px;background:#fff;" +
                "box-shadow:0 2px 8px rgba(0,0,0,.1)}" +
                "th{background:#2962c0;color:#fff;padding:10px 14px;text-align:left}" +
                "tr:nth-child(even){background:#f0f5ff}" +
                "td{padding:8px 14px;border-bottom:1px solid #e0e8f0}" +
                ".highlight{background:#c8f0d0!important;font-weight:bold;color:#005000}" +
                "</style></head><body>");
            sb.Append(
                $"<h2>Prediction Results</h2>" +
                $"<div class='meta'>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} " +
                $"| Rows: {dt.Rows.Count:N0}</div>");
            sb.Append("<table><tr>");
            foreach (DataColumn c in dt.Columns)
                sb.Append($"<th>{HtmlEnc(c.ColumnName)}</th>");
            sb.Append("</tr>");
            foreach (DataRow r in dt.Rows)
            {
                bool highlight = r[0]?.ToString()?.StartsWith("►") == true;
                sb.Append($"<tr{(highlight ? " class='highlight'" : "")}>");
                foreach (var v in r.ItemArray)
                    sb.Append($"<td>{HtmlEnc(v?.ToString() ?? "")}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></body></html>");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static string HtmlEnc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // ════════════════════════════════════════════════════════════════════
        //  UI HELPERS
        // ════════════════════════════════════════════════════════════════════
        private void SetBusy(bool busy, string msg)
        {
            if (InvokeRequired) { Invoke(() => SetBusy(busy, msg)); return; }
            tsslProgress.Visible = busy;
            if (msg != "") tsslStatus.Text = msg;
        }

        private void SetStatus(string msg, bool busy = false)
        {
            if (InvokeRequired) { Invoke(() => SetStatus(msg, busy)); return; }
            tsslStatus.Text = msg;
            tsslProgress.Visible = busy;
        }

        private void AlertNoModel() =>
            MessageBox.Show("Load a model first using Browse & Load.",
                "No Model", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        internal Button Btn(string text, Point loc, int w, bool accent)
        {
            var b = new Button
            {
                Text = text,
                Location = loc,
                Width = w,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f)
            };
            if (accent)
            {
                b.BackColor = Color.FromArgb(41, 98, 200); b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                b.BackColor = Color.FromArgb(226, 233, 248); b.ForeColor = Color.FromArgb(40, 60, 120);
                b.FlatAppearance.BorderColor = Color.FromArgb(185, 205, 235);
            }
            return b;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SSA INNER TYPES
        // ════════════════════════════════════════════════════════════════════
        private class TsValueRow
        {
            [ColumnName("Value")]
            public float Value { get; set; }
        }

        private class TsForecastRow
        {
            public float[] Forecast { get; set; } = Array.Empty<float>();
            public float[] LowerBound { get; set; } = Array.Empty<float>();
            public float[] UpperBound { get; set; } = Array.Empty<float>();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  JSON CONFIG POCO
    // ════════════════════════════════════════════════════════════════════════
    internal class ModelMetaConfig
    {
        public string? Task { get; set; }
        public string? LabelColumn { get; set; }
        public string[]? FeatureColumns { get; set; }
        public string[]? CategoricalColumns { get; set; }
        public string[]? NumericColumns { get; set; }
        public Dictionary<string, List<string>>? UniqueValues { get; set; }
        public string? TrainedAt { get; set; }
        public string? BestModel { get; set; }
        public double PrimaryMetric { get; set; }
        public string? DateColumn { get; set; }
        public int HorizonSteps { get; set; }
        public string? Granularity { get; set; }
        public int WindowSize { get; set; }
        public double MAE { get; set; }
        public double RMSE { get; set; }
        public string? RawDataPath { get; set; }
    }
}