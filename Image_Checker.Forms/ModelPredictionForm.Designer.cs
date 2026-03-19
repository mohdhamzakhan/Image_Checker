// ══════════════════════════════════════════════════════════════════════════════
//  ModelPredictionForm.Designer.cs
//
//  NO using-directives — every type is fully qualified.
//  This eliminates every possible ambiguity from global/implicit usings
//  (e.g. System.Net.Mime.MediaTypeNames.Font vs System.Drawing.Font).
// ══════════════════════════════════════════════════════════════════════════════

namespace Image_Checker.Forms
{
    partial class ModelPredictionForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        // ── Field declarations ───────────────────────────────────────────────
        private System.Windows.Forms.Panel pnlHeader;
        private System.Windows.Forms.Label lblModelPath, lblModelInfo;
        private System.Windows.Forms.Button btnLoadModel;
        private System.Windows.Forms.TextBox txtModelPath;

        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabInput, tabForecast, tabOutput;

        private System.Windows.Forms.Label lblInputInstructions;
        private System.Windows.Forms.DataGridView dgvInput;
        private System.Windows.Forms.Button btnAddRow, btnClearRows,
                                                   btnLoadInputFile, btnPredict;
        private System.Windows.Forms.Label lblColumnHint;

        private System.Windows.Forms.GroupBox grpForecastMode;
        private System.Windows.Forms.RadioButton rbHorizonMode, rbEndDateMode;
        private System.Windows.Forms.Label lblHorizonCount;
        private System.Windows.Forms.NumericUpDown nudHorizonCount;
        private System.Windows.Forms.Label lblGranularity;
        private System.Windows.Forms.ComboBox cmbGranularity;
        private System.Windows.Forms.Label lblEndDate, lblStartDate;
        private System.Windows.Forms.DateTimePicker dtpEndDate, dtpStartDate;
        private System.Windows.Forms.Button btnForecast;
        private System.Windows.Forms.Label lblForecastNote;

        private System.Windows.Forms.DataGridView dgvOutput;
        private System.Windows.Forms.Label lblOutputInfo;
        private System.Windows.Forms.Button btnExportCsv, btnExportHtml;

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel tsslStatus;
        private System.Windows.Forms.ToolStripProgressBar tsslProgress;

        private System.Windows.Forms.ToolTip toolTip;

        // ════════════════════════════════════════════════════════════════════
        //  InitializeComponent  — minimal WinForms entry point
        //  All real UI construction is in InitForm() called by the constructor.
        // ════════════════════════════════════════════════════════════════════
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "Model Prediction";
            this.ClientSize = new System.Drawing.Size(1100, 740);
        }

        // ════════════════════════════════════════════════════════════════════
        //  InitForm  — builds every control
        //  Called from the constructor AFTER InitializeComponent().
        // ════════════════════════════════════════════════════════════════════
        private void InitForm()
        {
            this.SuspendLayout();

            this.Text = "\U0001F52E  Model Prediction";
            this.Size = new System.Drawing.Size(1100, 780);
            this.MinimumSize = new System.Drawing.Size(900, 650);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Font = new System.Drawing.Font("Segoe UI", 9f);
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 252);

            this.toolTip = new System.Windows.Forms.ToolTip
            { AutoPopDelay = 8000, InitialDelay = 300 };

            BuildHeader();
            BuildStatusBar();
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
                Height = 80,
                BackColor = System.Drawing.Color.White
            };
            pnlHeader.Paint += (s, e) =>
                e.Graphics.DrawLine(
                    new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 220, 240)),
                    0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);

            var tbl = new System.Windows.Forms.TableLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                Padding = new System.Windows.Forms.Padding(12, 8, 12, 6)
            };
            tbl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.AutoSize));
            tbl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.Percent, 100));
            tbl.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(
                System.Windows.Forms.SizeType.AutoSize));
            tbl.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));
            tbl.RowStyles.Add(new System.Windows.Forms.RowStyle(
                System.Windows.Forms.SizeType.AutoSize));

            var lCap = new System.Windows.Forms.Label
            {
                Text = "Model File (.zip):",
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 9f,
                    System.Drawing.FontStyle.Bold),
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

            // Btn() is defined in ModelPredictionForm.cs
            btnLoadModel = Btn("Browse & Load", System.Drawing.Point.Empty, 150, true);
            btnLoadModel.Dock = System.Windows.Forms.DockStyle.Fill;
            btnLoadModel.Margin = new System.Windows.Forms.Padding(0);

            lblModelPath = new System.Windows.Forms.Label
            {
                AutoSize = true,
                ForeColor = System.Drawing.Color.FromArgb(60, 120, 185),
                Font = new System.Drawing.Font("Segoe UI", 8f),
                Anchor = System.Windows.Forms.AnchorStyles.Left,
                Margin = new System.Windows.Forms.Padding(0, 4, 0, 0)
            };
            lblModelInfo = new System.Windows.Forms.Label
            {
                AutoSize = true,
                ForeColor = System.Drawing.Color.FromArgb(30, 70, 160),
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                Anchor = System.Windows.Forms.AnchorStyles.Left,
                Margin = new System.Windows.Forms.Padding(8, 4, 0, 0)
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
                Padding = new System.Drawing.Point(16, 5),
                Font = new System.Drawing.Font("Segoe UI", 9f)
            };
            tabInput = new System.Windows.Forms.TabPage("Input Data")
            { BackColor = System.Drawing.Color.FromArgb(245, 247, 252) };
            tabForecast = new System.Windows.Forms.TabPage("Time-Series Forecast")
            { BackColor = System.Drawing.Color.FromArgb(245, 247, 252) };
            tabOutput = new System.Windows.Forms.TabPage("Results")
            { BackColor = System.Drawing.Color.FromArgb(245, 247, 252) };

            BuildInputTab();
            BuildForecastTab();
            BuildOutputTab();

            tabMain.TabPages.AddRange(
                new System.Windows.Forms.TabPage[] { tabInput, tabForecast, tabOutput });
        }

        // ── INPUT TAB ────────────────────────────────────────────────────────
        private void BuildInputTab()
        {
            lblInputInstructions = new System.Windows.Forms.Label
            {
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 52,
                Padding = new System.Windows.Forms.Padding(8, 6, 8, 0),
                Text = "Load a model first, then either:\r\n" +
                            "  * Click 'Load File' to load a CSV/Excel file for batch prediction, or\r\n" +
                            "  * Click 'Add Row' and type values for single-row prediction.",
                ForeColor = System.Drawing.Color.FromArgb(50, 80, 150),
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                BackColor = System.Drawing.Color.FromArgb(232, 241, 255)
            };
            lblColumnHint = new System.Windows.Forms.Label
            {
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 24,
                Padding = new System.Windows.Forms.Padding(8, 4, 0, 0),
                Text = "Columns will appear here after loading a model.",
                ForeColor = System.Drawing.Color.Gray,
                Font = new System.Drawing.Font("Segoe UI", 8.5f,
                    System.Drawing.FontStyle.Italic)
            };
            dgvInput = new System.Windows.Forms.DataGridView
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode =
                    System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode =
                    System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                BackgroundColor = System.Drawing.Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                GridColor = System.Drawing.Color.FromArgb(220, 230, 245),
                ScrollBars = System.Windows.Forms.ScrollBars.Both
            };
            ApplyGridStyle(dgvInput);

            var btnPanel = new System.Windows.Forms.FlowLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Bottom,
                Height = 44,
                Padding = new System.Windows.Forms.Padding(6, 6, 0, 0),
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                BackColor = System.Drawing.Color.FromArgb(232, 239, 255)
            };
            btnLoadInputFile = Btn("Load File (CSV/XLSX)", System.Drawing.Point.Empty, 180, false);
            btnAddRow = Btn("Add Row", System.Drawing.Point.Empty, 100, false);
            btnClearRows = Btn("Clear All", System.Drawing.Point.Empty, 100, false);
            btnPredict = Btn("Run Prediction", System.Drawing.Point.Empty, 140, true);
            btnPredict.Font = new System.Drawing.Font("Segoe UI", 9.5f,
                System.Drawing.FontStyle.Bold);
            btnPanel.Controls.AddRange(new System.Windows.Forms.Control[]
            { btnLoadInputFile, btnAddRow, btnClearRows, btnPredict });

            tabInput.Controls.Add(dgvInput);
            tabInput.Controls.Add(btnPanel);
            tabInput.Controls.Add(lblColumnHint);
            tabInput.Controls.Add(lblInputInstructions);
        }

        // ── FORECAST TAB ─────────────────────────────────────────────────────
        private void BuildForecastTab()
        {
            var scroll = new System.Windows.Forms.Panel
            { Dock = System.Windows.Forms.DockStyle.Fill, AutoScroll = true };
            var pnl = new System.Windows.Forms.Panel
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(800, 480)
            };

            grpForecastMode = new System.Windows.Forms.GroupBox
            {
                Text = "Forecast Range",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(760, 310),
                Font = new System.Drawing.Font("Segoe UI", 9f,
                    System.Drawing.FontStyle.Bold)
            };

            rbHorizonMode = new System.Windows.Forms.RadioButton
            {
                Text = "Forecast by number of steps",
                Location = new System.Drawing.Point(12, 28),
                AutoSize = true,
                Checked = true
            };
            rbEndDateMode = new System.Windows.Forms.RadioButton
            {
                Text = "Forecast to a specific end date",
                Location = new System.Drawing.Point(12, 54),
                AutoSize = true
            };

            // Steps panel
            var pSteps = new System.Windows.Forms.Panel
            { Location = new System.Drawing.Point(24, 86), Size = new System.Drawing.Size(700, 52) };

            lblHorizonCount = new System.Windows.Forms.Label
            { Text = "Number of steps:", Location = new System.Drawing.Point(0, 6), AutoSize = true };
            nudHorizonCount = new System.Windows.Forms.NumericUpDown
            {
                Location = new System.Drawing.Point(130, 2),
                Width = 90,
                Minimum = 1,
                Maximum = 2000,
                Value = 12
            };
            lblGranularity = new System.Windows.Forms.Label
            { Text = "Granularity:", Location = new System.Drawing.Point(240, 6), AutoSize = true };
            cmbGranularity = new System.Windows.Forms.ComboBox
            {
                Location = new System.Drawing.Point(320, 2),
                Width = 120,
                DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
            };
            cmbGranularity.Items.AddRange(new object[] { "Day", "Month", "Year" });
            cmbGranularity.SelectedIndex = 1;
            var lEx = new System.Windows.Forms.Label
            {
                Text = "e.g. 12 steps at Month = forecast 12 months ahead",
                Location = new System.Drawing.Point(0, 30),
                AutoSize = true,
                ForeColor = System.Drawing.Color.DimGray,
                Font = new System.Drawing.Font("Segoe UI", 8f)
            };
            pSteps.Controls.AddRange(new System.Windows.Forms.Control[]
            { lblHorizonCount, nudHorizonCount, lblGranularity, cmbGranularity, lEx });

            // End-date panel
            var pDate = new System.Windows.Forms.Panel
            { Location = new System.Drawing.Point(24, 150), Size = new System.Drawing.Size(700, 90) };

            lblStartDate = new System.Windows.Forms.Label
            { Text = "Series starts from:", Location = new System.Drawing.Point(0, 6), AutoSize = true };
            dtpStartDate = new System.Windows.Forms.DateTimePicker
            {
                Location = new System.Drawing.Point(150, 2),
                Width = 150,
                Format = System.Windows.Forms.DateTimePickerFormat.Short,
                Value = System.DateTime.Today.AddYears(-1)
            };
            var lGranLbl = new System.Windows.Forms.Label
            { Text = "Step granularity:", Location = new System.Drawing.Point(320, 6), AutoSize = true };
            var cmbGran2 = new System.Windows.Forms.ComboBox
            {
                Location = new System.Drawing.Point(430, 2),
                Width = 110,
                DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList,
                Tag = "gran2"
            };
            cmbGran2.Items.AddRange(new object[] { "Day", "Month", "Year" });
            cmbGran2.SelectedIndex = 1;
            lblEndDate = new System.Windows.Forms.Label
            { Text = "Forecast until:", Location = new System.Drawing.Point(0, 44), AutoSize = true };
            dtpEndDate = new System.Windows.Forms.DateTimePicker
            {
                Location = new System.Drawing.Point(150, 40),
                Width = 150,
                Format = System.Windows.Forms.DateTimePickerFormat.Short,
                Value = System.DateTime.Today.AddYears(1)
            };
            var lCalc = new System.Windows.Forms.Label
            {
                Text = "Steps calculated automatically from the date range.",
                Location = new System.Drawing.Point(0, 68),
                AutoSize = true,
                ForeColor = System.Drawing.Color.DimGray,
                Font = new System.Drawing.Font("Segoe UI", 8f)
            };
            pDate.Controls.AddRange(new System.Windows.Forms.Control[]
            { lblStartDate, dtpStartDate, lGranLbl, cmbGran2,
              lblEndDate, dtpEndDate, lCalc });

            grpForecastMode.Controls.AddRange(new System.Windows.Forms.Control[]
            { rbHorizonMode, rbEndDateMode, pSteps, pDate });

            lblForecastNote = new System.Windows.Forms.Label
            {
                Location = new System.Drawing.Point(12, 332),
                Size = new System.Drawing.Size(760, 40),
                Text = "The model must have been trained with Task = Time Series (SSA)." +
                            "  For tabular models use the Input Data tab instead.",
                ForeColor = System.Drawing.Color.SteelBlue,
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };

            btnForecast = Btn("Run Forecast", new System.Drawing.Point(12, 382), 160, true);
            btnForecast.Height = 36;
            btnForecast.Font = new System.Drawing.Font("Segoe UI", 10f,
                System.Drawing.FontStyle.Bold);

            pnl.Controls.AddRange(new System.Windows.Forms.Control[]
            { grpForecastMode, lblForecastNote, btnForecast });
            scroll.Controls.Add(pnl);
            tabForecast.Controls.Add(scroll);
        }

        // ── OUTPUT TAB ────────────────────────────────────────────────────────
        private void BuildOutputTab()
        {
            lblOutputInfo = new System.Windows.Forms.Label
            {
                Dock = System.Windows.Forms.DockStyle.Top,
                Height = 28,
                Padding = new System.Windows.Forms.Padding(8, 6, 0, 0),
                Text = "Prediction results will appear here.",
                ForeColor = System.Drawing.Color.FromArgb(50, 80, 150),
                Font = new System.Drawing.Font("Segoe UI", 8.5f),
                BackColor = System.Drawing.Color.FromArgb(232, 241, 255)
            };
            dgvOutput = new System.Windows.Forms.DataGridView
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode =
                    System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode =
                    System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                BackgroundColor = System.Drawing.Color.White,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
                GridColor = System.Drawing.Color.FromArgb(220, 230, 245),
                ScrollBars = System.Windows.Forms.ScrollBars.Both
            };
            ApplyGridStyle(dgvOutput);

            var pBtns = new System.Windows.Forms.FlowLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Bottom,
                Height = 44,
                Padding = new System.Windows.Forms.Padding(6, 6, 0, 0),
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                BackColor = System.Drawing.Color.FromArgb(232, 239, 255)
            };
            btnExportCsv = Btn("Export CSV", System.Drawing.Point.Empty, 130, true);
            btnExportHtml = Btn("Export HTML", System.Drawing.Point.Empty, 130, false);
            btnExportCsv.Enabled = btnExportHtml.Enabled = false;
            pBtns.Controls.AddRange(new System.Windows.Forms.Control[]
            { btnExportCsv, btnExportHtml });

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

        // ── Grid styling (used only in Designer build methods) ───────────────
        private static void ApplyGridStyle(System.Windows.Forms.DataGridView g)
        {
            g.ColumnHeadersDefaultCellStyle.BackColor =
                System.Drawing.Color.FromArgb(41, 98, 200);
            g.ColumnHeadersDefaultCellStyle.ForeColor =
                System.Drawing.Color.White;
            g.ColumnHeadersDefaultCellStyle.Font =
                new System.Drawing.Font("Segoe UI", 8.5f,
                    System.Drawing.FontStyle.Bold);
            g.EnableHeadersVisualStyles = false;
            g.AlternatingRowsDefaultCellStyle.BackColor =
                System.Drawing.Color.FromArgb(240, 245, 255);
            g.DefaultCellStyle.SelectionBackColor =
                System.Drawing.Color.FromArgb(175, 210, 255);
            g.DefaultCellStyle.SelectionForeColor =
                System.Drawing.Color.Black;
        }
    }
}