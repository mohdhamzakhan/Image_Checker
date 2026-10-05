// ══════════════════════════════════════════════════════════════════════════════
//  ModelPredictionForm.Designer.cs  –  Fully qualified types, no partial duplication
// ══════════════════════════════════════════════════════════════════════════════

namespace Image_Checker.Forms
{
    partial class ModelPredictionForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        // ── Field declarations ───────────────────────────────────────────────
        // Header
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblModelPath, lblModelInfo;
        private System.Windows.Forms.Button btnLoadModel;
        private System.Windows.Forms.TextBox txtModelPath;

        // Tabs
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabInput, tabForecast, tabOutput;

        // Input tab
        private System.Windows.Forms.Panel pnlInputGrid;
        private System.Windows.Forms.Label lblColumnHint;
        private System.Windows.Forms.Button btnPredict, btnAddRow, btnClearRows;

        // Forecast tab
        private System.Windows.Forms.GroupBox grpForecastMode;
        private System.Windows.Forms.Label lblHorizonCount, lblGranularity;
        private System.Windows.Forms.NumericUpDown nudHorizonCount;
        private System.Windows.Forms.ComboBox cmbGranularity;
        private System.Windows.Forms.Label lblStartDate, lblEndDate;
        private System.Windows.Forms.DateTimePicker dtpStartDate, dtpEndDate;
        private System.Windows.Forms.CheckBox chkClampNeg;
        private System.Windows.Forms.Panel pnlFilterGrid;
        private System.Windows.Forms.Label lblFilterHint;
        private System.Windows.Forms.Button btnForecast;

        // Output tab
        private System.Windows.Forms.DataGridView dgvOutput;
        private System.Windows.Forms.SplitContainer splitResults;
        private System.Windows.Forms.DataVisualization.Charting.Chart chtOutput;
        private System.Windows.Forms.ComboBox cmbChartType;
        private System.Windows.Forms.CheckedListBox clbChartSeries;
        private System.Windows.Forms.Label lblOutputInfo;
        private System.Windows.Forms.Button btnExportCsv, btnExportHtml;

        // Status
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel tsslStatus;
        private System.Windows.Forms.ToolStripProgressBar tsslProgress;

        // ════════════════════════════════════════════════════════════════════
        //  InitializeComponent  – minimal stub; real build is in InitForm()
        // ════════════════════════════════════════════════════════════════════
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Text = "Model Prediction";
            // ClientSize is set once, in InitForm() (after Font is set there),
            // to avoid two conflicting size declarations racing each other.
        }

        // ════════════════════════════════════════════════════════════════════
        //  InitForm  –  builds every control programmatically
        // ════════════════════════════════════════════════════════════════════
        private void InitForm()
        {
            this.SuspendLayout();
            this.Text = "Model Prediction";
            // Font set before sizing, so AutoScale (Dpi mode) and layout
            // both see the real font this form actually uses.
            this.Font = new System.Drawing.Font("Segoe UI", 9f);
            this.ClientSize = new System.Drawing.Size(1160, 800);
            this.MinimumSize = new System.Drawing.Size(900, 650);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 252);

            BuildStatusBar();
            BuildHeader();
            BuildTabs();

            this.Controls.Add(tabMain);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(statusStrip);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // ── HEADER ───────────────────────────────────────────────────────────
        private void BuildHeader()
        {
            pnlHeader = new System.Windows.Forms.Panel
            {
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 88,
                BackColor = System.Drawing.Color.White
            };
            pnlHeader.Paint += (s, e) => e.Graphics.DrawLine(
                new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 220, 240)),
                0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);

            var tbl = new System.Windows.Forms.TableLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                Padding = new System.Windows.Forms.Padding(14, 8, 14, 6)
            };
            tbl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tbl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));
            tbl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 160));
            tbl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tbl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            tbl.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));

            var lCap = new System.Windows.Forms.Label
            {
                Text = "Model (.zip):",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                Anchor = System.Windows.Forms.AnchorStyles.Left
            };

            txtModelPath = new System.Windows.Forms.TextBox
            {
                ReadOnly = true,
                BackColor = System.Drawing.Color.FromArgb(248, 249, 252),
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Margin = new System.Windows.Forms.Padding(8, 0, 8, 0)
            };

            btnLoadModel = Btn("Browse & Load", System.Drawing.Point.Empty, 150, true);
            btnLoadModel.Dock = System.Windows.Forms.DockStyle.Fill;
            btnLoadModel.Margin = new System.Windows.Forms.Padding(0);
            btnLoadModel.Height = 28;

            lblModelPath = new System.Windows.Forms.Label
            {
                AutoSize = true,
                ForeColor = System.Drawing.Color.FromArgb(60, 120, 185),
                Font = new System.Drawing.Font("Segoe UI", 8f),
                Anchor = System.Windows.Forms.AnchorStyles.Left,
                Margin = new System.Windows.Forms.Padding(0, 2, 0, 0)
            };

            lblModelInfo = new System.Windows.Forms.Label
            {
                AutoSize = true,
                ForeColor = System.Drawing.Color.FromArgb(30, 70, 160),
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                Anchor = System.Windows.Forms.AnchorStyles.Left,
                Margin = new System.Windows.Forms.Padding(8, 2, 0, 0)
            };

            tbl.Controls.Add(lCap, 0, 0);
            tbl.Controls.Add(txtModelPath, 1, 0);
            tbl.Controls.Add(btnLoadModel, 2, 0);
            tbl.Controls.Add(lblModelPath, 0, 1);
            tbl.Controls.Add(lblModelInfo, 1, 1);
            tbl.SetColumnSpan(lblModelInfo, 2);
            pnlHeader.Controls.Add(tbl);
        }

        // ── TABS ─────────────────────────────────────────────────────────────
        private void BuildTabs()
        {
            tabMain = new System.Windows.Forms.TabControl
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Padding = new System.Drawing.Point(14, 5),
                Font = new System.Drawing.Font("Segoe UI", 9.5f)
            };

            tabInput = new System.Windows.Forms.TabPage("Prediction  (Regression)")
            { BackColor = System.Drawing.Color.FromArgb(245, 247, 252) };
            tabForecast = new System.Windows.Forms.TabPage("Time-Series Forecast  (SSA)")
            { BackColor = System.Drawing.Color.FromArgb(245, 247, 252) };
            tabOutput = new System.Windows.Forms.TabPage("Results")
            { BackColor = System.Drawing.Color.FromArgb(245, 247, 252) };

            BuildInputTab();
            BuildForecastTab();
            BuildOutputTab();

            tabMain.TabPages.AddRange(new System.Windows.Forms.TabPage[]
                { tabInput, tabForecast, tabOutput });
        }

        // ── INPUT TAB ────────────────────────────────────────────────────────
        private void BuildInputTab()
        {
            lblColumnHint = new System.Windows.Forms.Label
            {
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 32,
                Padding = new System.Windows.Forms.Padding(10, 8, 0, 0),
                Text = "Select a regression model to enable prediction. Values are pre-populated from training data.",
                ForeColor = System.Drawing.Color.FromArgb(50, 80, 150),
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                BackColor = System.Drawing.Color.FromArgb(232, 241, 255)
            };

            pnlInputGrid = new System.Windows.Forms.Panel
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                AutoScroll = true,
                BackColor = System.Drawing.Color.White
            };
            // Empty-state placeholder: BuildInputGrid() clears this panel's
            // Controls the moment a model loads, so this never needs removing
            // by hand — it just disappears the first time real fields appear.
            pnlInputGrid.Controls.Add(new System.Windows.Forms.Label
            {
                Text = "Load a model above to see its input fields here.",
                AutoSize = false,
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 120,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                ForeColor = System.Drawing.Color.Silver,
                Font = new System.Drawing.Font("Segoe UI", 11f)
            });

            var btnBar = new System.Windows.Forms.FlowLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Bottom,
                Height = 48,
                Padding = new System.Windows.Forms.Padding(8, 8, 0, 0),
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                BackColor = System.Drawing.Color.FromArgb(232, 239, 255)
            };

            btnClearRows = Btn("Clear Values", System.Drawing.Point.Empty, 120, false);
            btnAddRow = Btn("Reset", System.Drawing.Point.Empty, 100, false);
            btnPredict = Btn("Predict Quantity", System.Drawing.Point.Empty, 180, true);
            btnPredict.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
            btnPredict.Height = 34;

            btnBar.Controls.AddRange(new System.Windows.Forms.Control[]
                { btnClearRows, btnAddRow, btnPredict });

            tabInput.Controls.Add(pnlInputGrid);
            tabInput.Controls.Add(btnBar);
            tabInput.Controls.Add(lblColumnHint);
        }

        // ── FORECAST TAB ─────────────────────────────────────────────────────
        private void BuildForecastTab()
        {
            // Previously this tab put everything inside a fixed-size "pnl"
            // panel (Size = 900x680, no Anchor/Dock) sitting inside this
            // AutoScroll "outer" panel. A fixed-size child never resizes no
            // matter how big its parent gets — that's why the Forecast
            // Settings box stayed exactly the same size however much the
            // window was enlarged. Controls now go directly into "outer"
            // with Anchor = Top|Left|Right, so their WIDTH actually tracks
            // the window instead of being frozen at its initial size.
            var outer = new System.Windows.Forms.Panel
            { Dock = System.Windows.Forms.DockStyle.Fill, AutoScroll = true };

            // ── Forecast settings group ───────────────────────────────────
            // Built with a TableLayoutPanel instead of hand-placed Point()
            // coordinates: column widths are computed from each cell's real
            // preferred size, so a label can never silently run into the
            // control next to it the way fixed pixel gaps could.
            grpForecastMode = new System.Windows.Forms.GroupBox
            {
                Text = "Forecast Settings",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(870, 190),
                Anchor = System.Windows.Forms.AnchorStyles.Top
                       | System.Windows.Forms.AnchorStyles.Left
                       | System.Windows.Forms.AnchorStyles.Right,
                Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
            };

            var tblForecast = new System.Windows.Forms.TableLayoutPanel
            {
                Location = new System.Drawing.Point(10, 26),
                Size = new System.Drawing.Size(850, 150),
                Anchor = System.Windows.Forms.AnchorStyles.Top
                       | System.Windows.Forms.AnchorStyles.Left
                       | System.Windows.Forms.AnchorStyles.Right,
                ColumnCount = 4,
                RowCount = 4
            };
            tblForecast.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tblForecast.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tblForecast.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            tblForecast.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));
            for (int r = 0; r < 4; r++)
                tblForecast.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));

            var cellMargin = new System.Windows.Forms.Padding(0, 6, 14, 6);
            var font9 = new System.Drawing.Font("Segoe UI", 9f);

            var lblSteps = new System.Windows.Forms.Label
            { Text = "Steps to forecast:", AutoSize = true, Font = font9, Margin = cellMargin, Anchor = System.Windows.Forms.AnchorStyles.Left };

            nudHorizonCount = new System.Windows.Forms.NumericUpDown
            { Width = 80, Minimum = 1, Maximum = 2000, Value = 12, Font = font9, Margin = cellMargin, Anchor = System.Windows.Forms.AnchorStyles.Left };

            lblGranularity = new System.Windows.Forms.Label
            { Text = "Granularity:", AutoSize = true, Font = font9, Margin = cellMargin, Anchor = System.Windows.Forms.AnchorStyles.Left };

            cmbGranularity = new System.Windows.Forms.ComboBox
            { Width = 120, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList, Font = font9, Margin = cellMargin, Anchor = System.Windows.Forms.AnchorStyles.Left };
            cmbGranularity.Items.AddRange(new object[] { "Day", "Month", "Year" });
            cmbGranularity.SelectedIndex = 1;

            lblStartDate = new System.Windows.Forms.Label
            { Text = "Forecast start date:", AutoSize = true, Font = font9, Margin = cellMargin, Anchor = System.Windows.Forms.AnchorStyles.Left };

            dtpStartDate = new System.Windows.Forms.DateTimePicker
            { Width = 170, Format = System.Windows.Forms.DateTimePickerFormat.Short, Value = System.DateTime.Today, Font = font9, Margin = cellMargin, Anchor = System.Windows.Forms.AnchorStyles.Left };

            var lblStepHint = new System.Windows.Forms.Label
            {
                Text = "Step 1 = one period AFTER this date  (e.g. today → next month for Month)",
                AutoSize = true,
                ForeColor = System.Drawing.Color.DimGray,
                Font = new System.Drawing.Font("Segoe UI", 8f),
                Margin = cellMargin,
                Anchor = System.Windows.Forms.AnchorStyles.Left
            };

            chkClampNeg = new System.Windows.Forms.CheckBox
            {
                Text = "Clamp negative values to 0  (quantities cannot be negative)",
                AutoSize = true,
                Checked = true,
                Font = font9,
                ForeColor = System.Drawing.Color.FromArgb(0, 100, 0),
                Margin = cellMargin,
                Anchor = System.Windows.Forms.AnchorStyles.Left
            };
            chkClampNeg.CheckedChanged += (s2, e2) => _clampNegative = chkClampNeg.Checked;

            var lblFilterGuidance = new System.Windows.Forms.Label
            {
                Text = "Leave filters blank for a global forecast, or fill in values to forecast for a specific customer/item.",
                AutoSize = true,
                ForeColor = System.Drawing.Color.SteelBlue,
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                Margin = cellMargin,
                Anchor = System.Windows.Forms.AnchorStyles.Left
            };

            tblForecast.Controls.Add(lblSteps, 0, 0);
            tblForecast.Controls.Add(nudHorizonCount, 1, 0);
            tblForecast.Controls.Add(lblGranularity, 2, 0);
            tblForecast.Controls.Add(cmbGranularity, 3, 0);

            tblForecast.Controls.Add(lblStartDate, 0, 1);
            tblForecast.Controls.Add(dtpStartDate, 1, 1);
            tblForecast.Controls.Add(lblStepHint, 2, 1);
            tblForecast.SetColumnSpan(lblStepHint, 2);

            tblForecast.Controls.Add(chkClampNeg, 0, 2);
            tblForecast.SetColumnSpan(chkClampNeg, 4);

            tblForecast.Controls.Add(lblFilterGuidance, 0, 3);
            tblForecast.SetColumnSpan(lblFilterGuidance, 4);

            grpForecastMode.Controls.Add(tblForecast);

            // ── Filter section ────────────────────────────────────────────
            var grpFilter = new System.Windows.Forms.GroupBox
            {
                Text = "Filter by Customer / Item  (optional — leave blank for global forecast)",
                Location = new System.Drawing.Point(12, 212),
                Size = new System.Drawing.Size(870, 320),
                Anchor = System.Windows.Forms.AnchorStyles.Top
                       | System.Windows.Forms.AnchorStyles.Left
                       | System.Windows.Forms.AnchorStyles.Right,
                Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
            };

            lblFilterHint = new System.Windows.Forms.Label
            {
                Text = "Dropdowns are populated from your training data. Select a PARTY_NAME + INVENTORY_ITEM_ID combination.",
                Location = new System.Drawing.Point(8, 28),
                Size = new System.Drawing.Size(850, 18),
                Anchor = System.Windows.Forms.AnchorStyles.Top
                       | System.Windows.Forms.AnchorStyles.Left
                       | System.Windows.Forms.AnchorStyles.Right,
                ForeColor = System.Drawing.Color.FromArgb(50, 80, 150),
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };

            pnlFilterGrid = new System.Windows.Forms.Panel
            {
                Location = new System.Drawing.Point(8, 50),
                Size = new System.Drawing.Size(854, 260),
                Anchor = System.Windows.Forms.AnchorStyles.Top
                       | System.Windows.Forms.AnchorStyles.Left
                       | System.Windows.Forms.AnchorStyles.Right,
                AutoScroll = true,
                BackColor = System.Drawing.Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
            };

            grpFilter.Controls.Add(lblFilterHint);
            grpFilter.Controls.Add(pnlFilterGrid);

            // ── Run button ────────────────────────────────────────────────
            btnForecast = Btn("Run Forecast", new System.Drawing.Point(12, 544), 200, true);
            btnForecast.Height = 44;
            btnForecast.Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold);
            btnForecast.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;

            outer.Controls.AddRange(new System.Windows.Forms.Control[]
                { grpForecastMode, grpFilter, btnForecast });
            tabForecast.Controls.Add(outer);
        }

        // ── OUTPUT TAB ───────────────────────────────────────────────────────
        private void BuildOutputTab()
        {
            lblOutputInfo = new System.Windows.Forms.Label
            {
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 30,
                Padding = new System.Windows.Forms.Padding(10, 7, 0, 0),
                Text = "Results will appear here.",
                ForeColor = System.Drawing.Color.FromArgb(50, 80, 150),
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                BackColor = System.Drawing.Color.FromArgb(232, 241, 255)
            };

            dgvOutput = new System.Windows.Forms.DataGridView
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                BackgroundColor = System.Drawing.Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                GridColor = System.Drawing.Color.FromArgb(220, 230, 245),
                ScrollBars = System.Windows.Forms.ScrollBars.Both,
                RowHeadersVisible = false
            };
            dgvOutput.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(41, 98, 200);
            dgvOutput.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
            dgvOutput.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold);
            dgvOutput.EnableHeadersVisualStyles = false;
            dgvOutput.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(240, 245, 255);
            dgvOutput.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(175, 210, 255);
            dgvOutput.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;

            var pBtns = new System.Windows.Forms.FlowLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Bottom,
                Height = 48,
                Padding = new System.Windows.Forms.Padding(8, 8, 0, 0),
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                BackColor = System.Drawing.Color.FromArgb(232, 239, 255)
            };

            btnExportCsv = Btn("Export CSV", System.Drawing.Point.Empty, 140, true);
            btnExportHtml = Btn("Export HTML", System.Drawing.Point.Empty, 140, false);
            btnExportCsv.Enabled = btnExportHtml.Enabled = false;
            pBtns.Controls.AddRange(new System.Windows.Forms.Control[] { btnExportCsv, btnExportHtml });

            // ── Grid / Chart split ───────────────────────────────────────
            // Top: the existing results grid. Bottom: a trend chart whose
            // type and plotted value(s) the user picks, built from whatever
            // numeric columns the current result table has (Forecast,
            // Lower/Upper bound for a time-series run, or the predicted
            // value for a single regression run).
            splitResults = new System.Windows.Forms.SplitContainer
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Horizontal,
                SplitterWidth = 6
                // Deliberately NOT setting FixedPanel, Panel2MinSize or
                // SplitterDistance here. All three are validated against the
                // control's CURRENT size, which at construction time (before
                // this control has been parented and Dock=Fill applied) is
                // just its small built-in default — smaller than a 220px
                // Panel2MinSize needs, so setting any of them here throws
                // "SplitterDistance must be between Panel1MinSize and
                // Width - Panel2MinSize" even without an explicit
                // SplitterDistance value, because Panel2MinSize alone is
                // already inconsistent with that tiny default size.
                // All three are set together, safely, once the form has its
                // real size — see ConfigureResultsSplitter() in the .cs file,
                // called from the Form's Load event.
            };
            splitResults.Panel1.Controls.Add(dgvOutput);

            var chartBar = new System.Windows.Forms.Panel
            {
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 34,
                BackColor = System.Drawing.Color.FromArgb(240, 244, 252)
            };
            var lblChartType = new System.Windows.Forms.Label
            {
                Text = "Chart type:",
                AutoSize = true,
                Location = new System.Drawing.Point(10, 9),
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };
            cmbChartType = new System.Windows.Forms.ComboBox
            {
                Location = new System.Drawing.Point(80, 5),
                Width = 110,
                DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList,
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };
            cmbChartType.Items.AddRange(new object[] { "Line", "Spline", "Column", "Area", "Scatter" });
            cmbChartType.SelectedIndex = 0;
            cmbChartType.SelectedIndexChanged += (s2, e2) => RenderChart();

            var lblChartValue = new System.Windows.Forms.Label
            {
                Text = "Plot:",
                AutoSize = true,
                Location = new System.Drawing.Point(206, 9),
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };
            clbChartSeries = new System.Windows.Forms.CheckedListBox
            {
                Location = new System.Drawing.Point(240, 3),
                Size = new System.Drawing.Size(320, 28),
                CheckOnClick = true,
                MultiColumn = true,
                ColumnWidth = 110,
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
            };
            // ItemCheck fires BEFORE the checked state is actually applied,
            // so defer the redraw until right after this event finishes.
            clbChartSeries.ItemCheck += (s2, e2) => BeginInvoke((Action)RenderChart);

            chartBar.Controls.AddRange(new System.Windows.Forms.Control[]
                { lblChartType, cmbChartType, lblChartValue, clbChartSeries });

            chtOutput = new System.Windows.Forms.DataVisualization.Charting.Chart
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                BackColor = System.Drawing.Color.White
            };
            var area = new System.Windows.Forms.DataVisualization.Charting.ChartArea("main");
            area.AxisX.MajorGrid.LineColor = System.Drawing.Color.FromArgb(230, 235, 245);
            area.AxisY.MajorGrid.LineColor = System.Drawing.Color.FromArgb(230, 235, 245);
            area.AxisX.LabelStyle.Angle = -45;
            chtOutput.ChartAreas.Add(area);
            var legend = new System.Windows.Forms.DataVisualization.Charting.Legend("legend")
            { Docking = System.Windows.Forms.DataVisualization.Charting.Docking.Top };
            chtOutput.Legends.Add(legend);

            var chartHost = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Fill };
            chartHost.Controls.Add(chtOutput);
            chartHost.Controls.Add(chartBar);

            splitResults.Panel2.Controls.Add(chartHost);

            tabOutput.Controls.Add(splitResults);
            tabOutput.Controls.Add(pBtns);
            tabOutput.Controls.Add(lblOutputInfo);
        }

        // ── STATUS BAR ───────────────────────────────────────────────────────
        private void BuildStatusBar()
        {
            statusStrip = new System.Windows.Forms.StatusStrip { SizingGrip = false };
            tsslStatus = new System.Windows.Forms.ToolStripStatusLabel
            {
                Text = "Ready - load a model to begin.",
                Spring = true,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            tsslProgress = new System.Windows.Forms.ToolStripProgressBar
            {
                Visible = false,
                Width = 200,
                Style = System.Windows.Forms.ProgressBarStyle.Marquee
            };
            statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
                { tsslStatus, tsslProgress });
        }

        // ── Unused label fields kept to avoid missing-member errors ──────────
        private System.Windows.Forms.RadioButton rbHorizonMode, rbEndDateMode;
    }
}