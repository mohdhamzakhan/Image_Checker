// ══════════════════════════════════════════════════════════════════════════════
//  ModelPredictionForm.cs
//  Load a saved .zip model and run predictions.
//  Tabular  → load file or type rows → predict
//  TS       → set horizon + granularity → SSA forecast
// ══════════════════════════════════════════════════════════════════════════════

using Image_Checker.Services;
using Microsoft.ML;
using Microsoft.ML.Data;
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

        // ════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        //  Order matters: InitializeComponent first (WinForms requirement),
        //  then InitForm (builds all controls), then WireEvents.
        // ════════════════════════════════════════════════════════════════════
        public ModelPredictionForm()
        {
            InitializeComponent();   // generated stub in Designer — sets AutoScaleMode
            InitForm();              // builds all panels, tabs, grids
            WireEvents();            // attaches all event handlers
        }

        // ── Event wiring ───────────────────────────────────────────────────
        private void WireEvents()
        {
            btnLoadModel.Click += OnLoadModel;
            btnLoadInputFile.Click += OnLoadInputFile;
            btnAddRow.Click += OnAddRow;
            btnClearRows.Click += (_, _) => { if (_meta != null) BuildInputGrid(); };
            btnPredict.Click += OnPredict;
            btnForecast.Click += OnForecast;
            btnExportCsv.Click += (_, _) => ExportResults("csv");
            btnExportHtml.Click += (_, _) => ExportResults("html");
        }

        // ════════════════════════════════════════════════════════════════════
        //  LOAD MODEL
        // ════════════════════════════════════════════════════════════════════
        private void OnLoadModel(object? s, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "ML.NET Model (*.zip)|*.zip|All Files|*.*",
                Title = "Select Model"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                _modelPath = dlg.FileName;
                txtModelPath.Text = _modelPath;
                _model = _ml.Model.Load(_modelPath, out _schema);

                var jsonPath = Path.ChangeExtension(_modelPath, ".json");
                _meta = File.Exists(jsonPath)
                    ? JsonSerializer.Deserialize<ModelMetaConfig>(
                        File.ReadAllText(jsonPath))
                    : null;

                bool isTs = _meta?.Task?.Equals("TimeSeries",
                    StringComparison.OrdinalIgnoreCase) ?? false;

                lblModelInfo.Text =
                    $"Task: {_meta?.Task ?? "Unknown"}  |  " +
                    $"Label: {_meta?.LabelColumn ?? "?"}  |  " +
                    $"Features: {_meta?.FeatureColumns?.Length ?? 0}  |  " +
                    $"Trained: {_meta?.TrainedAt ?? "?"}";

                lblModelPath.Text = $"Loaded:  {Path.GetFileName(_modelPath)}";

                if (isTs)
                {
                    SetupForecastFromMeta();
                    tabMain.SelectedTab = tabForecast;
                }
                else
                {
                    BuildInputGrid();
                    tabMain.SelectedTab = tabInput;
                }

                SetStatus($"Model loaded: {Path.GetFileName(_modelPath)}", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load model:\n{ex.Message}",
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Load failed.", false);
            }
        }

        private void SetupForecastFromMeta()
        {
            if (_meta == null) return;
            if (_meta.HorizonSteps > 0)
                nudHorizonCount.Value = Math.Min(
                    _meta.HorizonSteps, nudHorizonCount.Maximum);
            if (!string.IsNullOrWhiteSpace(_meta.Granularity))
            {
                int idx = cmbGranularity.Items.IndexOf(_meta.Granularity);
                if (idx >= 0) cmbGranularity.SelectedIndex = idx;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  TABULAR INPUT GRID
        // ════════════════════════════════════════════════════════════════════
        private void BuildInputGrid()
        {
            dgvInput.DataSource = null;
            dgvInput.Columns.Clear();
            if (_meta?.FeatureColumns == null) return;

            var dt = new DataTable();
            foreach (var col in _meta.FeatureColumns)
                dt.Columns.Add(col, typeof(string));
            dgvInput.DataSource = dt;

            lblColumnHint.Text =
                "Enter values for: " + string.Join("  |  ", _meta.FeatureColumns);
        }

        // ════════════════════════════════════════════════════════════════════
        //  LOAD INPUT FILE
        // ════════════════════════════════════════════════════════════════════
        private void OnLoadInputFile(object? s, EventArgs e)
        {
            if (_model == null) { AlertNoModel(); return; }
            using var dlg = new OpenFileDialog
            { Filter = "Data Files (*.csv;*.xlsx;*.xls)|*.csv;*.xlsx;*.xls|All Files|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var pp = new DataPreprocessor();
                var dt = pp.LoadFile(dlg.FileName);
                if (dt == null || dt.Rows.Count == 0)
                    throw new InvalidDataException("File is empty.");

                dgvInput.DataSource = null;
                dgvInput.DataSource = dt;
                lblColumnHint.Text =
                    $"Loaded {dt.Rows.Count:N0} rows from " +
                    Path.GetFileName(dlg.FileName);
                SetStatus($"Input file loaded: {dt.Rows.Count:N0} rows", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}", "Load Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  ADD ROW
        // ════════════════════════════════════════════════════════════════════
        private void OnAddRow(object? s, EventArgs e)
        {
            if (_model == null) { AlertNoModel(); return; }
            if (dgvInput.DataSource is DataTable dt) dt.Rows.Add();
        }

        // ════════════════════════════════════════════════════════════════════
        //  RUN TABULAR PREDICTION
        // ════════════════════════════════════════════════════════════════════
        private async void OnPredict(object? s, EventArgs e)
        {
            if (_model == null) { AlertNoModel(); return; }
            if (dgvInput.DataSource is not DataTable inputDt || inputDt.Rows.Count == 0)
            {
                MessageBox.Show("Add at least one row of data.", "No Input",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true, "Running prediction...");
            DataTable? result = null;
            Exception? err = null;
            var inputCopy = inputDt.Copy(); // snapshot on UI thread

            await Task.Run(() =>
            {
                try { result = PredictTabular(inputCopy); }
                catch (Exception ex) { err = ex; }
            });

            SetBusy(false, "");
            if (err != null)
            {
                MessageBox.Show($"Prediction failed:\n{err.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ShowOutput(result!,
                $"Predicted {result!.Rows.Count} row(s)  |  " +
                $"Label: {_meta?.LabelColumn ?? "?"}");
            tabMain.SelectedTab = tabOutput;
        }

        private DataTable PredictTabular(DataTable input)
        {
            var tmp = Path.Combine(Path.GetTempPath(),
                $"pred_{Guid.NewGuid():N}.csv");
            WriteCsv(input, tmp);
            try
            {
                var cols = input.Columns.Cast<DataColumn>()
                    .Select((c, i) => new TextLoader.Column(
                        c.ColumnName,
                        double.TryParse(
                            input.Rows.Count > 0
                                ? input.Rows[0][c]?.ToString() : "",
                            out _) ? DataKind.Single : DataKind.String,
                        i))
                    .ToArray();

                var loader = _ml.Data.CreateTextLoader(new TextLoader.Options
                {
                    HasHeader = true,
                    Separators = new[] { ',' },
                    Columns = cols,
                    AllowQuoting = true
                });

                var data = loader.Load(tmp);
                var preds = _model!.Transform(data);

                var result = input.Copy();
                result.Columns.Add("Predicted", typeof(string));
                result.Columns.Add("Score", typeof(string));

                try
                {
                    var labels = preds.GetColumn<string>("PredictedLabel").ToList();
                    var scores = TryFloatCol(preds, "Score");
                    for (int i = 0; i < result.Rows.Count && i < labels.Count; i++)
                    {
                        result.Rows[i]["Predicted"] = labels[i];
                        result.Rows[i]["Score"] =
                            scores.Count > i ? $"{scores[i]:G4}" : "";
                    }
                }
                catch
                {
                    try
                    {
                        var sc = preds.GetColumn<float>("Score").ToList();
                        for (int i = 0; i < result.Rows.Count && i < sc.Count; i++)
                        {
                            result.Rows[i]["Predicted"] = sc[i].ToString("G6");
                            result.Rows[i]["Score"] = sc[i].ToString("G6");
                        }
                    }
                    catch { /* leave empty */ }
                }
                return result;
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        private static List<float> TryFloatCol(IDataView v, string col)
        {
            try { return v.GetColumn<float>(col).ToList(); }
            catch { return new List<float>(); }
        }

        // ════════════════════════════════════════════════════════════════════
        //  RUN TIME-SERIES FORECAST
        // ════════════════════════════════════════════════════════════════════
        private async void OnForecast(object? s, EventArgs e)
        {
            if (_model == null) { AlertNoModel(); return; }

            // Snapshot all UI values on the UI thread before Task.Run
            int horizon;
            string gran;
            DateTime startDate;

            if (rbEndDateMode.Checked)
            {
                gran = GetGran2();
                startDate = dtpStartDate.Value;
                horizon = CalcHorizonFromDates(startDate, dtpEndDate.Value, gran);
                if (horizon <= 0)
                {
                    MessageBox.Show("End date must be after start date.",
                        "Invalid Dates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                horizon = (int)nudHorizonCount.Value;
                gran = cmbGranularity.SelectedItem?.ToString() ?? "Month";
                startDate = DateTime.Today;
            }

            var labelCol = _meta?.LabelColumn ?? "?";

            SetBusy(true, $"Forecasting {horizon} {gran}(s)...");
            DataTable? result = null;
            Exception? err = null;

            await Task.Run(() =>
            {
                try { result = RunSSAForecast(horizon, gran, startDate); }
                catch (Exception ex) { err = ex; }
            });

            SetBusy(false, "");
            if (err != null)
            {
                MessageBox.Show(
                    $"Forecast failed:\n{err.Message}\n\n" +
                    "Ensure the model was trained as Time Series (SSA).",
                    "Forecast Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ShowOutput(result!,
                $"Forecast: {horizon} {gran}(s)  |  Series: {labelCol}");
            tabMain.SelectedTab = tabOutput;
        }

        private DataTable RunSSAForecast(int horizon, string gran, DateTime startDate)
        {
            // Build a dummy IDataView with schema { "Value": float }.
            // The saved SSA model's input schema is { "Value": Single } because
            // DataModelTrainer saved only the ssaModel (after CopyColumns renamed
            // the original label to "Value").  We do NOT use CreateTimeSeriesEngine
            // because that validates the full original schema (including the original
            // column name) and throws "Could not find input column 'QUANTITY_INVOICED'".
            var rows = Enumerable.Repeat(new TsValueRow { Value = 0f }, horizon).ToList();
            var view = _ml.Data.LoadFromEnumerable(rows);
            var out_ = _model!.Transform(view);

            float[] fc, lo, hi;
            try
            {
                fc = out_.GetColumn<float[]>("Forecast")
                         .FirstOrDefault() ?? Array.Empty<float>();
                lo = out_.GetColumn<float[]>("LowerBound")
                         .FirstOrDefault() ?? Array.Empty<float>();
                hi = out_.GetColumn<float[]>("UpperBound")
                         .FirstOrDefault() ?? Array.Empty<float>();
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
                    "Ensure it was trained as Time Series (SSA).");

            var dt = new DataTable();
            dt.Columns.Add("Step", typeof(int));
            dt.Columns.Add("Period", typeof(string));
            dt.Columns.Add("Forecast", typeof(string));
            dt.Columns.Add("LowerBound", typeof(string));
            dt.Columns.Add("UpperBound", typeof(string));

            int steps = Math.Min(fc.Length, horizon);
            for (int i = 0; i < steps; i++)
            {
                string period = gran switch
                {
                    "Year" => startDate.AddYears(i + 1).ToString("yyyy"),
                    "Day" => startDate.AddDays(i + 1).ToString("yyyy-MM-dd"),
                    _ => startDate.AddMonths(i + 1).ToString("yyyy-MM")
                };
                dt.Rows.Add(i + 1, period,
                    fc[i].ToString("G6"),
                    lo.Length > i ? lo[i].ToString("G4") : "",
                    hi.Length > i ? hi[i].ToString("G4") : "");
            }
            return dt;
        }

        private static int CalcHorizonFromDates(DateTime start, DateTime end, string gran)
        {
            if (end <= start) return 0;
            return gran switch
            {
                "Year" => end.Year - start.Year,
                "Day" => (int)(end - start).TotalDays,
                _ => (end.Year - start.Year) * 12 + end.Month - start.Month
            };
        }

        private string GetGran2()
        {
            foreach (Control c in grpForecastMode.Controls)
                if (c is Panel p)
                    foreach (Control cc in p.Controls)
                        if (cc is ComboBox cb && cb.Tag?.ToString() == "gran2")
                            return cb.SelectedItem?.ToString() ?? "Month";
            return "Month";
        }

        // ════════════════════════════════════════════════════════════════════
        //  OUTPUT / EXPORT
        // ════════════════════════════════════════════════════════════════════
        private void ShowOutput(DataTable dt, string info)
        {
            _lastOutput = dt;
            dgvOutput.DataSource = null;
            dgvOutput.DataSource = dt;
            lblOutputInfo.Text = $"Results  |  {info}  |  {dt.Rows.Count:N0} rows";
            btnExportCsv.Enabled = btnExportHtml.Enabled = true;
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

            MessageBox.Show($"Exported to:\n{dlg.FileName}", "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void WriteCsv(DataTable dt, string path)
        {
            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine(string.Join(",",
                dt.Columns.Cast<DataColumn>().Select(c => CsvEsc(c.ColumnName))));
            foreach (DataRow r in dt.Rows)
                sw.WriteLine(string.Join(",",
                    r.ItemArray.Select(v => CsvEsc(v?.ToString() ?? ""))));
        }

        private static void WriteHtml(DataTable dt, string path)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='UTF-8'>" +
                      "<title>Prediction Results</title><style>" +
                      "body{font-family:Segoe UI,sans-serif;margin:24px;color:#222;}" +
                      "h2{color:#2962c0;}" +
                      "table{border-collapse:collapse;width:100%;font-size:13px;}" +
                      "th{background:#2962c0;color:#fff;padding:8px 12px;text-align:left;}" +
                      "tr:nth-child(even){background:#f0f5ff;}" +
                      "td{padding:6px 12px;border-bottom:1px solid #dde;}" +
                      "</style></head><body>");
            sb.Append($"<h2>Prediction Results</h2><p>Generated: " +
                      $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | Rows: {dt.Rows.Count:N0}</p>");
            sb.Append("<table><tr>");
            foreach (DataColumn c in dt.Columns)
                sb.Append($"<th>{HtmlEnc(c.ColumnName)}</th>");
            sb.Append("</tr>");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append("<tr>");
                foreach (var v in r.ItemArray)
                    sb.Append($"<td>{HtmlEnc(v?.ToString() ?? "")}</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></body></html>");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static string CsvEsc(string s) =>
            s.Contains(',') || s.Contains('"') || s.Contains('\n')
                ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        private static string HtmlEnc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
             .Replace("\"", "&quot;");

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS  (Btn is used by Designer build methods too)
        // ════════════════════════════════════════════════════════════════════
        internal System.Windows.Forms.Button Btn(
            string text, Point loc, int w, bool accent)
        {
            var b = new Button
            {
                Text = text,
                Location = loc,
                Width = w,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f)
            };
            if (accent)
            {
                b.BackColor = Color.FromArgb(41, 98, 200);
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
            }
            else
            {
                b.BackColor = Color.FromArgb(226, 233, 248);
                b.ForeColor = Color.FromArgb(40, 60, 120);
                b.FlatAppearance.BorderColor = Color.FromArgb(185, 205, 235);
            }
            return b;
        }

        private void SetBusy(bool busy, string msg)
        {
            if (InvokeRequired) { Invoke(() => SetBusy(busy, msg)); return; }
            tsslProgress.Visible = busy;
            if (msg != "") tsslStatus.Text = msg;
        }

        private void SetStatus(string msg, bool busy)
        {
            if (InvokeRequired) { Invoke(() => SetStatus(msg, busy)); return; }
            tsslStatus.Text = msg;
            tsslProgress.Visible = busy;
        }

        private void AlertNoModel() =>
            MessageBox.Show("Load a model first using the Browse & Load button.",
                "No Model", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        // ════════════════════════════════════════════════════════════════════
        //  SSA HELPER ROWS
        //  [ColumnName("Value")] — NOT [LoadColumn(0)].
        //  LoadFromEnumerable maps by property name / ColumnName attribute,
        //  not by ordinal, so [LoadColumn] has no effect here.
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
    //  Companion JSON config POCO
    // ════════════════════════════════════════════════════════════════════════
    internal class ModelMetaConfig
    {
        public string? Task { get; set; }
        public string? LabelColumn { get; set; }
        public string[]? FeatureColumns { get; set; }
        public string? TrainedAt { get; set; }
        public string? BestModel { get; set; }
        public double PrimaryMetric { get; set; }
        public string? DateColumn { get; set; }
        public int HorizonSteps { get; set; }
        public string? Granularity { get; set; }
        public int WindowSize { get; set; }
        public double MAE { get; set; }
        public double RMSE { get; set; }
    }
}