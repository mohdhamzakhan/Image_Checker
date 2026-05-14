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
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "Model Prediction";
            this.ClientSize = new System.Drawing.Size(1100, 740);
        }

        // ════════════════════════════════════════════════════════════════════
        //  InitForm  –  builds every control programmatically
        // ════════════════════════════════════════════════════════════════════
        private void InitForm()
        {
            this.SuspendLayout();
            this.Text = "Model Prediction";
            this.Size = new System.Drawing.Size(1160, 800);
            this.MinimumSize = new System.Drawing.Size(900, 650);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Font = new System.Drawing.Font("Segoe UI", 9f);
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
            var outer = new System.Windows.Forms.Panel
            { Dock = System.Windows.Forms.DockStyle.Fill, AutoScroll = true };

            var pnl = new System.Windows.Forms.Panel
            { Location = new System.Drawing.Point(0, 0), Size = new System.Drawing.Size(900, 680) };

            // ── Forecast settings group ───────────────────────────────────
            grpForecastMode = new System.Windows.Forms.GroupBox
            {
                Text = "Forecast Settings",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(870, 180),
                Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
            };

            grpForecastMode.Controls.Add(new System.Windows.Forms.Label
            { Text = "Steps to forecast:", Location = new System.Drawing.Point(14, 36), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9f) });

            nudHorizonCount = new System.Windows.Forms.NumericUpDown
            { Location = new System.Drawing.Point(140, 32), Width = 80, Minimum = 1, Maximum = 2000, Value = 12, Font = new System.Drawing.Font("Segoe UI", 9f) };

            lblGranularity = new System.Windows.Forms.Label
            { Text = "Granularity:", Location = new System.Drawing.Point(240, 36), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9f) };

            cmbGranularity = new System.Windows.Forms.ComboBox
            { Location = new System.Drawing.Point(320, 32), Width = 110, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList, Font = new System.Drawing.Font("Segoe UI", 9f) };
            cmbGranularity.Items.AddRange(new object[] { "Day", "Month", "Year" });
            cmbGranularity.SelectedIndex = 1;

            lblStartDate = new System.Windows.Forms.Label
            { Text = "Forecast start date:", Location = new System.Drawing.Point(14, 76), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9f) };

            dtpStartDate = new System.Windows.Forms.DateTimePicker
            { Location = new System.Drawing.Point(140, 72), Width = 160, Format = System.Windows.Forms.DateTimePickerFormat.Short, Value = System.DateTime.Today, Font = new System.Drawing.Font("Segoe UI", 9f) };

            grpForecastMode.Controls.Add(new System.Windows.Forms.Label
            { Text = "Step 1 = one period AFTER this date  (e.g. today → next month for Month)", Location = new System.Drawing.Point(320, 76), AutoSize = true, ForeColor = System.Drawing.Color.DimGray, Font = new System.Drawing.Font("Segoe UI", 8f) });

            chkClampNeg = new System.Windows.Forms.CheckBox
            { Text = "Clamp negative values to 0  (quantities cannot be negative)", Location = new System.Drawing.Point(14, 114), AutoSize = true, Checked = true, Font = new System.Drawing.Font("Segoe UI", 9f), ForeColor = System.Drawing.Color.FromArgb(0, 100, 0) };
            chkClampNeg.CheckedChanged += (s2, e2) => _clampNegative = chkClampNeg.Checked;

            grpForecastMode.Controls.Add(new System.Windows.Forms.Label
            { Text = "Leave filters blank for a global forecast, or fill in values to forecast for a specific customer/item.", Location = new System.Drawing.Point(14, 148), AutoSize = true, ForeColor = System.Drawing.Color.SteelBlue, Font = new System.Drawing.Font("Segoe UI", 8.5f) });

            grpForecastMode.Controls.AddRange(new System.Windows.Forms.Control[]
            { nudHorizonCount, lblGranularity, cmbGranularity, lblStartDate, dtpStartDate, chkClampNeg });

            // ── Filter section ────────────────────────────────────────────
            var grpFilter = new System.Windows.Forms.GroupBox
            {
                Text = "Filter by Customer / Item  (optional — leave blank for global forecast)",
                Location = new System.Drawing.Point(12, 204),
                Size = new System.Drawing.Size(870, 320),
                Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
            };

            lblFilterHint = new System.Windows.Forms.Label
            {
                Text = "Dropdowns are populated from your training data. Select a PARTY_NAME + INVENTORY_ITEM_ID combination.",
                Location = new System.Drawing.Point(8, 28),
                Size = new System.Drawing.Size(850, 18),
                ForeColor = System.Drawing.Color.FromArgb(50, 80, 150),
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };

            pnlFilterGrid = new System.Windows.Forms.Panel
            {
                Location = new System.Drawing.Point(8, 50),
                Size = new System.Drawing.Size(854, 260),
                AutoScroll = true,
                BackColor = System.Drawing.Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
            };

            grpFilter.Controls.Add(lblFilterHint);
            grpFilter.Controls.Add(pnlFilterGrid);

            // ── Run button ────────────────────────────────────────────────
            btnForecast = Btn("Run Forecast", new System.Drawing.Point(12, 534), 200, true);
            btnForecast.Height = 44;
            btnForecast.Font = new System.Drawing.Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold);

            pnl.Controls.AddRange(new System.Windows.Forms.Control[]
                { grpForecastMode, grpFilter, btnForecast });
            outer.Controls.Add(pnl);
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

            tabOutput.Controls.Add(dgvOutput);
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