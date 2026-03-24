// ══════════════════════════════════════════════════════════════════════════════
//  ModelPredictionForm.cs
//  Load a saved .zip model and run predictions.
//  Tabular  → load file or type rows → predict
//  TS       → set horizon + granularity → SSA forecast
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

                // Enable/disable the Forecast tab based on task type.
                // The SSA Forecast tab ONLY works with TimeSeries models.
                // Tabular models (Regression / Classification) use Input Data tab.
                tabForecast.Enabled = isTs;
                tabForecast.ToolTipText = isTs
                    ? ""
                    : "Forecast tab is only available for Time Series (SSA) models.";

                if (isTs)
                {
                    SetupForecastFromMeta();
                    tabMain.SelectedTab = tabForecast;
                    lblModelInfo.Text =
                        $"Task: TimeSeries (SSA)  |  " +
                        $"Label: {_meta?.LabelColumn ?? "?"}  |  " +
                        $"Horizon: {_meta?.HorizonSteps} steps  |  " +
                        $"Granularity: {_meta?.Granularity ?? "Month"}  |  " +
                        $"Trained: {_meta?.TrainedAt ?? "?"}";
                }
                else
                {
                    BuildInputGrid();
                    tabMain.SelectedTab = tabInput;

                    // Disable forecast tab for non-TS models and show a tooltip
                    tabForecast.Enabled = false;
                    MessageBox.Show(
                        $"This is a {_meta?.Task ?? "tabular"} model.\n\n" +
                        "The Time-Series Forecast tab is only available when you load\n" +
                        "the SSA model (bestModel-SSA-*.zip).\n\n" +
                        "Use the Input Data tab to predict quantity for a specific\n" +
                        "PARTY_NAME + INVENTORY_ITEM_ID combination.",
                        "Tabular Model Loaded",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            // Match granularity case-insensitively.
            // If not found keep the default "Month" (SelectedIndex=1 set in Designer).
            if (!string.IsNullOrWhiteSpace(_meta.Granularity))
            {
                for (int i = 0; i < cmbGranularity.Items.Count; i++)
                {
                    if (string.Equals(cmbGranularity.Items[i]?.ToString(),
                        _meta.Granularity, StringComparison.OrdinalIgnoreCase))
                    {
                        cmbGranularity.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                // Default to Month when granularity not stored in JSON
                int monthIdx = 0;
                for (int i = 0; i < cmbGranularity.Items.Count; i++)
                    if (cmbGranularity.Items[i]?.ToString() == "Month") { monthIdx = i; break; }
                cmbGranularity.SelectedIndex = monthIdx;
            }

            // Populate the filter grid from JSON feature columns so user can
            // filter forecast by PARTY_NAME, INVENTORY_ITEM_ID etc.
            BuildFilterGrid();
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILTER GRID  (per-series filtering for SSA forecast)
        // ════════════════════════════════════════════════════════════════════

        // Holds the filter DataGridView and its backing table — declared here
        // so BuildFilterGrid and GetFilterValues can both access them.
        private System.Windows.Forms.DataGridView? _dgvFilter;
        private System.Windows.Forms.Panel? _pnlFilter;

        private void BuildFilterGrid()
        {
            // Remove any existing filter panel from the forecast tab
            if (_pnlFilter != null)
            {
                // Find and remove from the scroll panel inside tabForecast
                foreach (System.Windows.Forms.Control c in tabForecast.Controls)
                {
                    if (c is System.Windows.Forms.Panel scroll)
                    {
                        foreach (System.Windows.Forms.Control inner in scroll.Controls)
                        {
                            if (inner is System.Windows.Forms.Panel pnl)
                            {
                                pnl.Controls.Remove(_pnlFilter);
                                break;
                            }
                        }
                        break;
                    }
                }
                _pnlFilter.Dispose();
                _pnlFilter = null;
            }

            // Show ALL feature columns as filterable — user can filter by any combination.
            // Date column is included but ignored in filters (it's used for aggregation).
            // Label column is excluded (it's what we're predicting).
            var filterCols = _meta?.FeatureColumns?.ToArray();
            if (filterCols == null || filterCols.Length == 0) return;

            // Build grid with explicit column definitions — do NOT use DataSource on a
            // DataGridView before it is parented/visible.  DataSource triggers async
            // column auto-generation and Columns[0] would be empty → IndexOutOfRange.
            _dgvFilter = new System.Windows.Forms.DataGridView
            {
                Height = Math.Min(filterCols.Length * 30 + 36, 280),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = System.Drawing.Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                GridColor = System.Drawing.Color.FromArgb(220, 230, 245),
                ColumnHeadersDefaultCellStyle = { BackColor = System.Drawing.Color.FromArgb(41, 98, 200),
                                                  ForeColor = System.Drawing.Color.White },
                EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter
            };

            // Add columns manually — this is synchronous, no async binding issues
            _dgvFilter.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn
            {
                HeaderText = "Column",
                Name = "ColName",
                ReadOnly = true,
                FillWeight = 40,
                DefaultCellStyle = { BackColor = System.Drawing.Color.FromArgb(240, 245, 255),
                                     ForeColor = System.Drawing.Color.FromArgb(30, 60, 120) }
            });
            _dgvFilter.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn
            {
                HeaderText = "Filter Value  (leave blank = include all rows)",
                Name = "FilterVal",
                ReadOnly = false,
                FillWeight = 60
            });

            // Populate rows
            foreach (var col in filterCols)
                _dgvFilter.Rows.Add(col, "");

            _pnlFilter = new System.Windows.Forms.Panel
            {
                Location = new System.Drawing.Point(12, 430),
                Size = new System.Drawing.Size(760, _dgvFilter.Height + 52),
                BackColor = System.Drawing.Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
            };

            var lbl = new System.Windows.Forms.Label
            {
                Text = "Filter by column values — enter values for any columns you want " +
                            "to filter on (leave blank = all). Supports any number of filters.",
                Location = new System.Drawing.Point(8, 4),
                Size = new System.Drawing.Size(740, 16),
                Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(30, 70, 160)
            };
            var lbl2 = new System.Windows.Forms.Label
            {
                Text = "Example: PARTY_NAME = Acme Corp  +  INVENTORY_ITEM_ID = 12345  \u2192 forecasts qty for that customer+item combination",
                Location = new System.Drawing.Point(8, 22),
                Size = new System.Drawing.Size(740, 14),
                Font = new System.Drawing.Font("Segoe UI", 7.5f),
                ForeColor = System.Drawing.Color.DimGray
            };
            _dgvFilter.Location = new System.Drawing.Point(4, 40);
            _dgvFilter.Width = 750;
            _pnlFilter.Controls.Add(lbl);
            _pnlFilter.Controls.Add(lbl2);
            _pnlFilter.Controls.Add(_dgvFilter);

            // Add to the inner pnl (first Panel inside the scroll Panel inside tabForecast)
            foreach (System.Windows.Forms.Control c in tabForecast.Controls)
            {
                if (c is System.Windows.Forms.Panel scroll)
                {
                    foreach (System.Windows.Forms.Control inner in scroll.Controls)
                    {
                        if (inner is System.Windows.Forms.Panel pnl)
                        {
                            // Expand pnl height if needed
                            if (pnl.Height < _pnlFilter.Bottom + 20)
                                pnl.Height = _pnlFilter.Bottom + 20;
                            pnl.Controls.Add(_pnlFilter);
                            break;
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Returns the filter values the user typed into the filter grid.
        /// Key = column name, Value = filter string (empty = no filter).
        /// </summary>
        private Dictionary<string, string> GetFilterValues()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_dgvFilter == null) return result;

            // Read directly from grid rows (manually-built, no DataSource binding)
            foreach (System.Windows.Forms.DataGridViewRow row in _dgvFilter.Rows)
            {
                string col = row.Cells["ColName"]?.Value?.ToString() ?? "";
                string val = row.Cells["FilterVal"]?.Value?.ToString()?.Trim() ?? "";
                if (col != "" && val != "")
                    result[col] = val;
            }
            return result;
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

            // Guard: only SSA (TimeSeries) models can forecast.
            // If user somehow reaches here with a tabular model, show a clear message.
            bool modelIsTs = _meta?.Task?.Equals("TimeSeries",
                StringComparison.OrdinalIgnoreCase) ?? false;

            if (!modelIsTs)
            {
                MessageBox.Show(
                    "The loaded model is a tabular model\n" +
                    $"(Task: {_meta?.Task ?? "Unknown"})\n\n" +
                    "Time-Series Forecast requires a model trained with\n" +
                    "Task = Time Series (SSA).\n\n" +
                    "Please load the SSA model file (bestModel-SSA-*.zip)\n" +
                    "instead of the tabular model file.",
                    "Wrong Model Type",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

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
            var filterVals = GetFilterValues();   // snapshot on UI thread

            // Build a filter description for the output title
            string filterDesc = filterVals.Count > 0
                ? "  |  Filter: " + string.Join(", ", filterVals.Select(kv => $"{kv.Key}={kv.Value}"))
                : "";

            SetBusy(true, $"Forecasting {horizon} {gran}(s){(filterVals.Count > 0 ? " (filtered)" : "")}...");
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
                MessageBox.Show(
                    $"Forecast failed:\n{err.Message}\n\n" +
                    "Ensure the model was trained as Time Series (SSA).",
                    "Forecast Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ShowOutput(result!,
                $"Forecast: {horizon} {gran}(s)  |  Series: {labelCol}{filterDesc}");
            tabMain.SelectedTab = tabOutput;
        }

        private DataTable RunSSAForecast(int horizon, string gran, DateTime startDate,
            Dictionary<string, string>? filterValues = null)
        {
            // ── Decide: filtered per-series SSA vs global SSA ─────────────────
            //
            // FILTERED: user filled in PARTY_NAME / INVENTORY_ITEM_ID in the filter grid.
            //   → Re-read the original raw CSV (path stored in companion JSON as RawDataPath)
            //   → Keep only rows that match ALL filter conditions
            //   → Aggregate by date period (same logic as training)
            //   → Retrain a new SSA on that mini-series
            //   → Return that forecast
            //
            // GLOBAL: no filters set → use the pre-trained SSA model directly.

            bool hasFilters = filterValues != null && filterValues.Count > 0;
            string? rawDataPath = _meta?.RawDataPath;

            if (hasFilters && rawDataPath != null && File.Exists(rawDataPath)
                && _meta?.DateColumn != null && _meta?.LabelColumn != null)
            {
                return RunFilteredSSAForecast(horizon, gran, startDate,
                    filterValues!, rawDataPath,
                    _meta.DateColumn, _meta.LabelColumn);
            }

            if (hasFilters && (rawDataPath == null || !File.Exists(rawDataPath)))
            {
                throw new InvalidOperationException(
                    "Cannot filter forecast: the original training data file was not found.\n" +
                    $"Expected path: {rawDataPath ?? "(not stored in model JSON)"}\n\n" +
                    "Ensure the model was retrained after the latest update, " +
                    "or run without filters for the global forecast.");
            }

            // ── Global forecast via pre-trained SSA model ──────────────────────
            // Build a dummy IDataView with schema { "Value": float }.
            var rows = Enumerable.Repeat(new TsValueRow { Value = 0f }, horizon).ToList();
            var view = _ml.Data.LoadFromEnumerable(rows);
            var out_ = _model!.Transform(view);

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
                    "Ensure it was trained as Time Series (SSA).");

            return BuildForecastTable(fc, lo, hi, horizon, gran, startDate);
        }

        private DataTable RunFilteredSSAForecast(
            int horizon, string gran, DateTime startDate,
            Dictionary<string, string> filters,
            string rawCsvPath, string dateCol, string labelCol)
        {
            // ── Step 1: Read raw CSV with proper quoted-field handling ─────────
            // Simple Split(',') breaks on values like "Acme Corp, LLC" — use a
            // proper CSV parser that respects quoted fields.
            var allLines = File.ReadAllLines(rawCsvPath);
            if (allLines.Length < 2)
                throw new InvalidOperationException("Raw data file is empty.");

            var headers = ParseCsvLine(allLines[0]);

            int dateIdx = Array.FindIndex(headers,
                h => string.Equals(h, dateCol, StringComparison.OrdinalIgnoreCase));
            int labelIdx = Array.FindIndex(headers,
                h => string.Equals(h, labelCol, StringComparison.OrdinalIgnoreCase));

            if (dateIdx < 0) throw new InvalidOperationException(
                $"Date column '{dateCol}' not found. CSV headers: {string.Join(", ", headers)}");
            if (labelIdx < 0) throw new InvalidOperationException(
                $"Label column '{labelCol}' not found. CSV headers: {string.Join(", ", headers)}");

            // Map each filter column name → its CSV column index
            var filterIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in filters)
            {
                int idx = Array.FindIndex(headers,
                    h => string.Equals(h, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) throw new InvalidOperationException(
                    $"Filter column '{kv.Key}' not found. CSV headers: {string.Join(", ", headers)}");
                filterIdx[kv.Key] = idx;
            }

            // ── Step 2: Filter rows, aggregate quantity by time period ─────────
            string ToPeriodKey(string rawDate)
            {
                rawDate = rawDate.Trim().Trim('"');
                if (DateTime.TryParse(rawDate, out var dt))
                    return gran switch
                    {
                        "Year" => dt.ToString("yyyy"),
                        "Day" => dt.ToString("yyyy-MM-dd"),
                        _ => dt.ToString("yyyy-MM")   // Month
                    };
                // Fallback: use first 10 chars (handles yyyy-MM-dd already)
                return rawDate.Length > 10 ? rawDate[..10] : rawDate;
            }

            // periodSums: period key → summed quantity, periodDates: key → parsed DateTime for sorting
            var periodSums = new Dictionary<string, double>();
            var periodDates = new Dictionary<string, DateTime>();
            int matchedRows = 0;

            for (int li = 1; li < allLines.Length; li++)
            {
                if (string.IsNullOrWhiteSpace(allLines[li])) continue;
                var cells = ParseCsvLine(allLines[li]);

                // ALL filter conditions must match (supports any number of filters)
                bool match = filters.All(kv =>
                {
                    int ci = filterIdx[kv.Key];
                    string cell = ci < cells.Length ? cells[ci] : "";
                    return string.Equals(cell, kv.Value, StringComparison.OrdinalIgnoreCase);
                });
                if (!match) continue;
                matchedRows++;

                // Parse quantity (label)
                string rawQty = labelIdx < cells.Length ? cells[labelIdx] : "";
                if (!double.TryParse(rawQty,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double qty)) continue;

                // Parse date → period key
                string rawDate = dateIdx < cells.Length ? cells[dateIdx] : "";
                string key = ToPeriodKey(rawDate);

                if (periodSums.ContainsKey(key))
                    periodSums[key] += qty;
                else
                {
                    periodSums[key] = qty;
                    if (DateTime.TryParse(rawDate, out var parsedDt))
                        periodDates[key] = parsedDt;
                }
            }

            Console.WriteLine($"   [Filter] {matchedRows:N0} rows matched → " +
                $"{periodSums.Count} {gran} periods");

            if (matchedRows == 0)
                throw new InvalidOperationException(
                    "No rows matched the filters.\n" +
                    "Filters applied:\n" +
                    string.Join("\n", filters.Select(kv => "  * " + kv.Key + " = " + kv.Value)) +
                    "\n\nValues are case-sensitive. Check spelling and spaces.");

            if (periodSums.Count < 4)
                throw new InvalidOperationException(
                    $"Only {periodSums.Count} period(s) after aggregation - SSA needs at least 4. " +
                    $"Total matching rows: {matchedRows:N0}. " +
                    "Try a broader filter or coarser granularity (Month instead of Day).");

            // Sort periods chronologically using parsed DateTime when available,
            // falling back to string sort (which works for yyyy-MM / yyyy-MM-dd / yyyy).
            var sortedPeriods = periodSums.Keys
                .OrderBy(k => periodDates.ContainsKey(k)
                    ? periodDates[k]
                    : DateTime.TryParse(k, out var dt) ? dt : DateTime.MaxValue)
                .ToList();

            var series = sortedPeriods.Select(k => (float)periodSums[k]).ToList();
            var lastPeriod = sortedPeriods.Last();

            // Derive the actual start date from the last observed period so the
            // forecast periods continue from where the data ends (not DateTime.Today).
            DateTime forecastStart = startDate; // user-supplied fallback
            if (periodDates.ContainsKey(lastPeriod))
                forecastStart = periodDates[lastPeriod];
            else if (DateTime.TryParse(lastPeriod, out var lp))
                forecastStart = lp;

            // Print series summary
            Console.WriteLine($"   Series: {sortedPeriods.First()} → {lastPeriod}");
            Console.WriteLine($"   Values (first 6): " +
                string.Join(", ", series.Take(6).Select(v => v.ToString("N0"))));

            // ── Step 3: Train SSA on the filtered+aggregated series ───────────
            var seriesRows = series.Select(v => new TsValueRow { Value = v }).ToList();
            var seriesView = _ml.Data.LoadFromEnumerable(seriesRows);

            int n = series.Count;
            int trainSize = n;  // use all data for training (we're forecasting future, not testing)

            // SSA constraints:
            //   windowSize > horizon
            //   windowSize ≤ seriesLength / 2
            //   seriesLength ≤ trainSize
            int seriesLen = trainSize;
            int windowSize = Math.Max(horizon + 1, seriesLen / 3);  // 1/3 of series is a good default
            if (windowSize > seriesLen / 2)
                windowSize = Math.Max(2, seriesLen / 2);
            if (windowSize <= horizon)
                windowSize = horizon + 1;
            // Final safety clamp
            windowSize = Math.Min(windowSize, trainSize - 1);
            windowSize = Math.Max(windowSize, 2);

            Console.WriteLine($"   SSA params: n={n}, trainSize={trainSize}, " +
                $"window={windowSize}, horizon={horizon}");

            var ssaPipeline = _ml.Forecasting.ForecastBySsa(
                outputColumnName: "Forecast",
                inputColumnName: "Value",
                windowSize: windowSize,
                seriesLength: seriesLen,
                trainSize: trainSize,
                horizon: horizon,
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: "LowerBound",
                confidenceUpperBoundColumn: "UpperBound");

            var ssaModel = ssaPipeline.Fit(seriesView);
            var eng = ssaModel.CreateTimeSeriesEngine<TsValueRow, TsForecastRow>(_ml);
            var forecast = eng.Predict();

            // Show in-sample last-period accuracy
            var transformed = ssaModel.Transform(seriesView);
            var actualLast = series.Last();
            var fcastFirst = transformed.GetColumn<float[]>("Forecast")
                                           .LastOrDefault()?[0] ?? 0f;
            Console.WriteLine($"   Last period actual: {actualLast:N2}, " +
                $"SSA fitted: {fcastFirst:N2}");

            return BuildForecastTable(
                forecast.Forecast, forecast.LowerBound, forecast.UpperBound,
                horizon, gran, forecastStart);
        }

        /// <summary>
        /// Parses one CSV line respecting quoted fields (handles commas inside quotes).
        /// Returns trimmed, unquoted field values.
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');  // escaped quote ""
                        i++;
                    }
                    else inQuote = !inQuote;
                }
                else if (ch == ',' && !inQuote)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else current.Append(ch);
            }
            fields.Add(current.ToString().Trim());
            return fields.ToArray();
        }

        private static DataTable BuildForecastTable(
            float[] fc, float[] lo, float[] hi,
            int horizon, string gran, DateTime startDate)
        {
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
                    fc[i].ToString("N2"),
                    lo.Length > i ? lo[i].ToString("N2") : "",
                    hi.Length > i ? hi[i].ToString("N2") : "");
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
        // Path to the original raw CSV used during training.
        // Used by per-series filtering in RunSSAForecast.
        public string? RawDataPath { get; set; }
    }
}