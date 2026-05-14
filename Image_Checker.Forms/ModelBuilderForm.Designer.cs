// ══════════════════════════════════════════════════════════════════════════════
//  ModelBuilderForm.Designer.cs  –  Professional redesign.
// ══════════════════════════════════════════════════════════════════════════════

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Image_Checker.Forms
{
    partial class ModelBuilderForm
    {
        private System.ComponentModel.IContainer components = null;
        private ToolTip tip;

        // top bar
        private Panel pnlTop;
        private Label lblFileCaption, lblFileStats;
        private TextBox txtFilePath;
        private Button btnBrowse, btnLoadFile;

        // tabs
        private TabControl tabMain;
        private TabPage tpPreview, tpColumns, tpCleaning,
                           tpTransform, tpReduction, tpTraining, tpResults;

        // preview
        private DataGridView dgvPreview;
        private Label lblPreviewHint;

        // columns tab
        private ComboBox cmbLabel, cmbTask;
        private NumericUpDown nudTestPct, nudSeed;
        private Label lblTestPctSuffix;
        private CheckedListBox clbFeatures, clbIgnore;
        private Button btnCheckAll, btnUncheckAll;
        private GroupBox grpTS;
        private ComboBox cmbTsDate, cmbTsGran;
        private NumericUpDown nudTsHorizon, nudTsWindow, nudTsSeries, nudTsConf;

        // per-column grids
        private DataGridView dgvCleaning, dgvTransform, dgvReduction;
        private Button btnCleanApplyAll, btnTransformApplyAll;
        private CheckBox chkDuplicates;
        private GroupBox grpGlobalReduction;
        private RadioButton rbNoPCA, rbPCA, rbNoSample, rbRandSample, rbStratSample;
        private NumericUpDown nudPCAComp, nudSamplePct;

        // training
        private Panel pnlAlgoScroll;
        private CheckBox chkSDCA, chkLBFGS, chkFastTree, chkFastForest,
                         chkLightGBM, chkPerceptron, chkLinearSGD;
        private Label lblAlgoTitle;
        private RichTextBox rtbAlgoDesc, rtbLog;
        private Button btnTrain, btnCancelTrain;

        // results
        private Label lblResultTitle;
        private DataGridView dgvResults;
        private RichTextBox rtbResultLog;
        private Button btnSaveModel, btnExportReport;

        // status
        private StatusStrip ss;
        private ToolStripStatusLabel tssl;
        private ToolStripProgressBar tsspb;

        // ─────────────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            SuspendLayout();
            tip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 400 };
            Text = "📊  Data Model Builder";
            Size = new Size(1300, 870);
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(242, 245, 252);

            BuildTopBar();
            BuildStatusBar();
            BuildTabs();

            Controls.Add(tabMain);
            Controls.Add(pnlTop);
            Controls.Add(ss);
            ResumeLayout(false);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TOP BAR
        // ═════════════════════════════════════════════════════════════════════
        private void BuildTopBar()
        {
            pnlTop = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White };
            pnlTop.Paint += PaintBottomLine;

            lblFileCaption = L("Data File  (CSV / XLSX / XLS):", 12, 10,
                bold: true, color: Color.FromArgb(30, 50, 110));

            txtFilePath = new TextBox
            {
                Location = new Point(230, 7),
                Width = 300,
                ReadOnly = true,
                BackColor = Color.FromArgb(247, 249, 253),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };

            btnBrowse = Btn("📂  Browse", new Point(0, 5), 120, false);
            btnLoadFile = Btn("▶  Load File", new Point(0, 5), 130, true);
            btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLoadFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            lblFileStats = L("", 230, 32, color: Color.FromArgb(50, 110, 185));
            lblFileStats.Font = new Font("Segoe UI", 8f);

            pnlTop.Resize += (s, e) =>
            {
                int centerY = (pnlTop.Height - txtFilePath.Height) / 2;
                txtFilePath.Top = centerY;
                btnBrowse.Top = centerY;
                btnLoadFile.Top = centerY;
                lblFileCaption.Top = centerY + 3;

                btnLoadFile.Left = pnlTop.Width - btnLoadFile.Width - 10;
                btnBrowse.Left = btnLoadFile.Left - btnBrowse.Width - 10;
                txtFilePath.Width = btnBrowse.Left - txtFilePath.Left - 10;
            };

            pnlTop.Controls.AddRange(new Control[]
                { lblFileCaption, txtFilePath, btnBrowse, btnLoadFile, lblFileStats });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  STATUS BAR
        // ═════════════════════════════════════════════════════════════════════
        private void BuildStatusBar()
        {
            ss = new StatusStrip { SizingGrip = false, BackColor = Color.White };
            tssl = new ToolStripStatusLabel
            {
                Text = "Ready – load a CSV or Excel file to begin.",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            tsspb = new ToolStripProgressBar
            { Visible = false, Width = 220, Style = ProgressBarStyle.Marquee };
            ss.Items.AddRange(new ToolStripItem[] { tssl, tsspb });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TABS
        // ═════════════════════════════════════════════════════════════════════
        private void BuildTabs()
        {
            tabMain = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(20, 6),
                Font = new Font("Segoe UI", 9f)
            };

            tpPreview = MkTab("👁  Preview");
            tpColumns = MkTab("🏷  Columns & Task");
            tpCleaning = MkTab("🧹  Cleaning");
            tpTransform = MkTab("🔄  Transform");
            tpReduction = MkTab("📉  Reduction");
            tpTraining = MkTab("🤖  Training");
            tpResults = MkTab("📊  Results");

            BuildPreviewTab();
            BuildColumnsTab();
            BuildCleaningTab();
            BuildTransformTab();
            BuildReductionTab();
            BuildTrainingTab();
            BuildResultsTab();

            tabMain.TabPages.AddRange(new[]
            { tpPreview, tpColumns, tpCleaning, tpTransform,
              tpReduction, tpTraining, tpResults });
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PREVIEW TAB
        // ═════════════════════════════════════════════════════════════════════
        private void BuildPreviewTab()
        {
            lblPreviewHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(10, 6, 0, 0),
                Text = "Showing first 200 rows.  Load a file to begin.",
                BackColor = Color.FromArgb(226, 238, 255),
                ForeColor = Color.FromArgb(40, 80, 165),
                Font = new Font("Segoe UI", 8.5f)
            };
            dgvPreview = StyledGrid(readOnly: true);
            dgvPreview.Dock = DockStyle.Fill;
            dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

            tpPreview.Controls.Add(dgvPreview);
            tpPreview.Controls.Add(lblPreviewHint);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  COLUMNS TAB
        // ═════════════════════════════════════════════════════════════════════
        private void BuildColumnsTab()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(242, 245, 252)
            };
            split.Layout += (s, e) => { if (split.Width > 0) split.SplitterDistance = 380; };

            // ── LEFT: settings ───────────────────────────────────────────
            var left = new Panel
            { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            var hdr = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(41, 98, 200) };
            hdr.Controls.Add(new Label
            {
                Text = "Model Configuration",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            });
            left.Controls.Add(hdr);

            var lContent = new Panel
            {
                Location = new Point(0, 44),
                Width = 380,
                Height = 700,
                BackColor = Color.White,
                Padding = new Padding(18, 14, 18, 14)
            };

            int y = 14;
            lContent.Controls.Add(FieldLabel("Prediction Target (Label Column)", 18, y)); y += 22;
            cmbLabel = new ComboBox { Location = new Point(18, y), Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
            lContent.Controls.Add(cmbLabel); y += 36;

            lContent.Controls.Add(Divider(18, y)); y += 14;
            lContent.Controls.Add(FieldLabel("Task Type", 18, y)); y += 22;
            cmbTask = new ComboBox { Location = new Point(18, y), Width = 330, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTask.Items.AddRange(new object[]
            { "Auto Detect", "Binary Classification", "Multiclass Classification",
              "Regression", "Time Series (SSA)" });
            cmbTask.SelectedIndex = 0;
            lContent.Controls.Add(cmbTask); y += 36;

            lContent.Controls.Add(Divider(18, y)); y += 14;
            lContent.Controls.Add(FieldLabel("Train / Test Split", 18, y)); y += 22;
            nudTestPct = new NumericUpDown { Location = new Point(18, y), Width = 72, Minimum = 5, Maximum = 50, Value = 20 };
            lblTestPctSuffix = new Label
            { Location = new Point(96, y + 3), AutoSize = true, Text = "% of data used for testing", ForeColor = Color.FromArgb(90, 100, 130) };
            lContent.Controls.Add(nudTestPct);
            lContent.Controls.Add(lblTestPctSuffix); y += 36;

            lContent.Controls.Add(FieldLabel("Random Seed", 18, y)); y += 22;
            nudSeed = new NumericUpDown { Location = new Point(18, y), Width = 110, Minimum = 0, Maximum = 99999, Value = 42 };
            lContent.Controls.Add(nudSeed); y += 40;

            lContent.Controls.Add(Divider(18, y)); y += 14;

            // ── Time-Series group ─────────────────────────────────────────
            grpTS = new GroupBox
            {
                Text = "⏱  Time-Series Options",
                Location = new Point(14, y),
                Size = new Size(348, 296),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(41, 98, 200),
                Visible = false
            };

            int ty = 26;
            void TSRow(string lbl, Control ctrl, string tooltip = "")
            {
                grpTS.Controls.Add(new Label
                {
                    Text = lbl,
                    Location = new Point(12, ty + 3),
                    Size = new Size(130, 18),
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = Color.FromArgb(50, 60, 90)
                });
                ctrl.Location = new Point(150, ty); ctrl.Width = 175;
                grpTS.Controls.Add(ctrl);
                if (tooltip != "") tip.SetToolTip(ctrl, tooltip);
                ty += 30;
            }

            cmbTsDate = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            grpTS.Controls.Add(new Label { Text = "Date Column:", Location = new Point(12, ty + 3), Size = new Size(130, 18), Font = new Font("Segoe UI", 8.5f) });
            cmbTsDate.Location = new Point(150, ty); cmbTsDate.Width = 175;
            grpTS.Controls.Add(cmbTsDate); ty += 30;

            cmbTsGran = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTsGran.Items.AddRange(new object[] { "Day", "Month", "Year" });
            cmbTsGran.SelectedIndex = 1;
            grpTS.Controls.Add(new Label { Text = "Step Granularity:", Location = new Point(12, ty + 3), Size = new Size(130, 18), Font = new Font("Segoe UI", 8.5f) });
            cmbTsGran.Location = new Point(150, ty); cmbTsGran.Width = 175;
            grpTS.Controls.Add(cmbTsGran); ty += 30;

            nudTsHorizon = new NumericUpDown { Minimum = 1, Maximum = 2000, Value = 12 };
            nudTsWindow = new NumericUpDown { Minimum = 2, Maximum = 500, Value = 24 };
            nudTsSeries = new NumericUpDown { Minimum = 10, Maximum = 5000, Value = 100 };
            nudTsConf = new NumericUpDown { Minimum = 50, Maximum = 99, Value = 95 };
            TSRow("Forecast Horizon:", nudTsHorizon, "Future periods to predict.");
            TSRow("Window Size:", nudTsWindow, "SSA window — must exceed horizon.");
            TSRow("Series Length:", nudTsSeries, "Historical points for SSA.");
            TSRow("Confidence %:", nudTsConf, "Prediction interval confidence.");

            lContent.Controls.Add(grpTS);
            lContent.Height = y + 340;
            left.Controls.Add(lContent);
            split.Panel1.Controls.Add(left);

            // ── RIGHT: feature + ignore checklists ───────────────────────
            var rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 440,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(242, 245, 252)
            };

            var pFeat = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var hFeat = ColPaneHeader("Feature Columns", "Check = include as model input",
                Color.FromArgb(28, 148, 80));
            var pBtns = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.FromArgb(242, 245, 252) };
            btnCheckAll = SmBtn("✔  Select All", new Point(8, 4), 130);
            btnUncheckAll = SmBtn("✖  Clear All", new Point(146, 4), 130);
            pBtns.Controls.AddRange(new Control[] { btnCheckAll, btnUncheckAll });
            clbFeatures = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.White
            };
            pFeat.Controls.Add(clbFeatures); pFeat.Controls.Add(pBtns); pFeat.Controls.Add(hFeat);
            rightSplit.Panel1.Controls.Add(pFeat);

            var pIgn = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            var hIgn = ColPaneHeader("Ignore Columns", "☑ = excluded from all processing", Color.FromArgb(190, 60, 40));
            clbIgnore = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                BackColor = Color.White
            };
            pIgn.Controls.Add(clbIgnore); pIgn.Controls.Add(hIgn);
            rightSplit.Panel2.Controls.Add(pIgn);

            split.Panel2.Controls.Add(rightSplit);
            tpColumns.Controls.Add(split);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  CLEANING TAB
        // ═════════════════════════════════════════════════════════════════════
        private void BuildCleaningTab()
        {
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White };
            toolbar.Paint += PaintBottomLine;

            chkDuplicates = new CheckBox
            {
                Text = "Remove exact duplicate rows",
                Location = new Point(14, 14),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9f)
            };
            btnCleanApplyAll = Btn("Apply to All Columns ▼", new Point(300, 10), 190, false);
            tip.SetToolTip(btnCleanApplyAll, "Bulk-set the same missing/outlier strategy on every column.");

            toolbar.Controls.AddRange(new Control[]
            {
                chkDuplicates, btnCleanApplyAll,
                new Label
                {
                    Text = "ℹ  Each row below represents one column. Configure cleaning individually per column.",
                    Location = new Point(510, 15), AutoSize = true,
                    ForeColor = Color.FromArgb(80, 120, 185), Font = new Font("Segoe UI", 8.5f)
                }
            });

            dgvCleaning = BuildCleanGrid();
            tpCleaning.Controls.Add(dgvCleaning);
            tpCleaning.Controls.Add(toolbar);
        }

        private DataGridView BuildCleanGrid()
        {
            var g = StyledGrid(false);
            g.Dock = DockStyle.Fill; g.RowTemplate.Height = 30;
            g.AllowUserToAddRows = g.AllowUserToDeleteRows = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.EditMode = DataGridViewEditMode.EditOnEnter;

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColName",
                HeaderText = "Column Name",
                ReadOnly = true,
                Width = 180,
                DefaultCellStyle = { BackColor = Color.FromArgb(237, 243, 255), Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColType", HeaderText = "Type", ReadOnly = true, Width = 85 });
            g.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Missing",
                HeaderText = "Missing Value Strategy",
                Width = 200,
                FlatStyle = FlatStyle.Flat,
                DataSource = new[] { "Mean (average)", "Median (middle)", "Mode (most frequent)", "Delete Row", "None – leave as is" }
            });
            g.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "OutlierMethod",
                HeaderText = "Outlier Detection",
                Width = 175,
                FlatStyle = FlatStyle.Flat,
                DataSource = new[] { "None", "IQR  (Q1 – k×IQR  to  Q3 + k×IQR)", "Z-Score  ( |z| > threshold )" }
            });
            g.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "OutlierAction",
                HeaderText = "Outlier Action",
                Width = 160,
                FlatStyle = FlatStyle.Flat,
                DataSource = new[] { "Cap to boundary value", "Remove the row" }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "IQRk", HeaderText = "IQR k", Width = 70 });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "ZThresh", HeaderText = "Z Threshold", Width = 100 });
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            return g;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TRANSFORM TAB
        // ═════════════════════════════════════════════════════════════════════
        private void BuildTransformTab()
        {
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.White };
            toolbar.Paint += PaintBottomLine;
            btnTransformApplyAll = Btn("Apply to All Columns ▼", new Point(14, 10), 190, false);
            toolbar.Controls.AddRange(new Control[]
            {
                btnTransformApplyAll,
                new Label
                {
                    Text = "ℹ  Normalization applies to numeric columns.  Encoding applies to text/categorical columns.",
                    Location = new Point(220, 15), AutoSize = true,
                    ForeColor = Color.FromArgb(80, 120, 185), Font = new Font("Segoe UI", 8.5f)
                }
            });
            dgvTransform = BuildTransformGrid();
            tpTransform.Controls.Add(dgvTransform);
            tpTransform.Controls.Add(toolbar);
        }

        private DataGridView BuildTransformGrid()
        {
            var g = StyledGrid(false);
            g.Dock = DockStyle.Fill; g.RowTemplate.Height = 30;
            g.AllowUserToAddRows = g.AllowUserToDeleteRows = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.EditMode = DataGridViewEditMode.EditOnEnter;

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColName",
                HeaderText = "Column Name",
                ReadOnly = true,
                Width = 180,
                DefaultCellStyle = { BackColor = Color.FromArgb(237, 243, 255), Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColType", HeaderText = "Type", ReadOnly = true, Width = 85 });
            g.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Norm",
                HeaderText = "Normalization  (numeric)",
                Width = 240,
                FlatStyle = FlatStyle.Flat,
                DataSource = new[] { "None – raw values", "Min-Max  →  [0, 1]", "Z-Score  →  (x − μ) / σ",
                    "Decimal Scaling  →  ÷ 10ᵏ", "Log Transform  →  ln(x + 1)" }
            });
            g.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Enc",
                HeaderText = "Encoding  (categorical)",
                Width = 250,
                FlatStyle = FlatStyle.Flat,
                DataSource = new[] { "One-Hot Encoding  (binary dummy columns)", "Label Encoding  (integer index)", "None – keep as text" }
            });
            g.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Bin", HeaderText = "Binning", Width = 75, TrueValue = true, FalseValue = false });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "BinCount", HeaderText = "Bin Count", Width = 90 });
            return g;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  REDUCTION TAB
        // ═════════════════════════════════════════════════════════════════════
        private void BuildReductionTab()
        {
            var hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(12, 6, 0, 0),
                Text = "ℹ  Per-column: choose include / exclude strategy. Global options below apply after per-column settings.",
                BackColor = Color.FromArgb(226, 238, 255),
                ForeColor = Color.FromArgb(40, 80, 165),
                Font = new Font("Segoe UI", 8.5f)
            };

            dgvReduction = BuildReductionGrid();
            dgvReduction.Dock = DockStyle.Fill;

            grpGlobalReduction = new GroupBox
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                Text = "Global Options  (applied after per-column settings)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(41, 98, 200),
                BackColor = Color.White
            };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 10,
                RowCount = 2,
                Padding = new Padding(12, 8, 12, 8),
                AutoSize = true
            };
            for (int i = 0; i < 8; i++) tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ── PCA row ───────────────────────────────────────────────────
            rbNoPCA = new RadioButton { AutoSize = true, Checked = true };
            rbPCA = new RadioButton { AutoSize = true };
            nudPCAComp = new NumericUpDown { Width = 70, Minimum = 1, Maximum = 500, Value = 10, Enabled = false };
            rbPCA.CheckedChanged += (s, e) => nudPCAComp.Enabled = rbPCA.Checked;

            var lblNoPCA = MkClickLabel("None", rbNoPCA);
            var lblPCA = MkClickLabel("PCA – Principal Component Analysis", rbPCA);

            tbl.Controls.Add(new Label { Text = "Dimensionality Reduction:", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Anchor = AnchorStyles.Left }, 0, 0);
            tbl.Controls.Add(rbNoPCA, 1, 0); tbl.Controls.Add(lblNoPCA, 2, 0);
            tbl.Controls.Add(rbPCA, 3, 0); tbl.Controls.Add(lblPCA, 4, 0);
            tbl.Controls.Add(new Label { Text = "Components:", AutoSize = true, Anchor = AnchorStyles.Right }, 8, 0);
            tbl.Controls.Add(nudPCAComp, 9, 0);

            // ── Sampling row ──────────────────────────────────────────────
            rbNoSample = new RadioButton { AutoSize = true, Checked = true };
            rbRandSample = new RadioButton { AutoSize = true };
            rbStratSample = new RadioButton { AutoSize = true };
            nudSamplePct = new NumericUpDown { Width = 70, Minimum = 10, Maximum = 99, Value = 80 };

            tbl.Controls.Add(new Label { Text = "Data Sampling:", AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Anchor = AnchorStyles.Left }, 0, 1);
            tbl.Controls.Add(rbNoSample, 1, 1); tbl.Controls.Add(MkClickLabel("No sampling", rbNoSample), 2, 1);
            tbl.Controls.Add(rbRandSample, 3, 1); tbl.Controls.Add(MkClickLabel("Simple Random", rbRandSample), 4, 1);
            tbl.Controls.Add(rbStratSample, 5, 1); tbl.Controls.Add(MkClickLabel("Stratified (per class)", rbStratSample), 6, 1);
            tbl.Controls.Add(new Label { Text = "Keep %:", AutoSize = true, Anchor = AnchorStyles.Right }, 8, 1);
            tbl.Controls.Add(nudSamplePct, 9, 1);

            grpGlobalReduction.Controls.Add(tbl);

            tpReduction.Controls.Clear();
            tpReduction.Controls.Add(dgvReduction);
            tpReduction.Controls.Add(grpGlobalReduction);
            tpReduction.Controls.Add(hint);
        }

        private static Label MkClickLabel(string text, RadioButton rb) =>
            new Label { Text = text, AutoSize = true }.Also(l => l.Click += (s, e) => rb.Checked = true);

        private DataGridView BuildReductionGrid()
        {
            var g = StyledGrid(false);
            g.Dock = DockStyle.Fill; g.RowTemplate.Height = 30;
            g.AllowUserToAddRows = g.AllowUserToDeleteRows = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.EditMode = DataGridViewEditMode.EditOnEnter;

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColName",
                HeaderText = "Column Name",
                ReadOnly = true,
                Width = 180,
                DefaultCellStyle = { BackColor = Color.FromArgb(237, 243, 255), Font = new Font("Segoe UI", 9f, FontStyle.Bold) }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColType", HeaderText = "Type", ReadOnly = true, Width = 85 });
            g.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = "Include", Width = 80, TrueValue = true, FalseValue = false });
            g.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name = "Strategy",
                HeaderText = "Feature Selection Strategy",
                Width = 270,
                FlatStyle = FlatStyle.Flat,
                DataSource = new[] { "Always include", "Variance Filter  (drop near-zero variance)",
                    "Top-N Correlation  (with label column)", "Exclude this column" }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = "VarThresh", HeaderText = "Variance Threshold", Width = 160 });
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            return g;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TRAINING TAB
        // ═════════════════════════════════════════════════════════════════════
        private void BuildTrainingTab()
        {
            var outer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                BorderStyle = BorderStyle.None
            };
            outer.Layout += (s, e) => { if (outer.Width > 0) outer.SplitterDistance = 340; };

            // ── LEFT: algorithm list ──────────────────────────────────────
            pnlAlgoScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            var ah = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(41, 98, 200) };
            ah.Controls.Add(new Label
            {
                Text = "Select Algorithms",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(14, 12),
                AutoSize = true
            });
            pnlAlgoScroll.Controls.Add(ah);

            int ay = 52;
            void Sec(string t) { pnlAlgoScroll.Controls.Add(new Label { Text = t, Location = new Point(12, ay), Size = new Size(308, 18), ForeColor = Color.FromArgb(85, 108, 175), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }); ay += 24; }
            CheckBox AC(string t, bool on) { var c = new CheckBox { Text = t, Checked = on, Location = new Point(12, ay), Size = new Size(314, 24), Font = new Font("Segoe UI", 9f) }; pnlAlgoScroll.Controls.Add(c); ay += 26; return c; }

            Sec("── Classification  /  General ─────────────────");
            chkSDCA = AC("SDCA  (Stochastic Dual Coordinate Ascent)", true);
            chkLBFGS = AC("LBFGS  (Limited-memory BFGS)", true);
            chkFastTree = AC("FastTree  (Gradient Boosted Trees)", true);
            chkFastForest = AC("FastForest  (Random Forest)", true);
            chkLightGBM = AC("LightGBM  (Fast Gradient Boosting)", true);
            chkPerceptron = AC("Averaged Perceptron  (binary only)", false);
            ay += 6;
            Sec("── Regression Only ──────────────────────────────");
            chkLinearSGD = AC("Linear SGD  (fast linear baseline)", true);
            ay += 14;

            pnlAlgoScroll.Controls.Add(new Label
            {
                Text = "ℹ  Hover any algorithm to see full details →",
                Location = new Point(12, ay),
                Size = new Size(308, 36),
                ForeColor = Color.SteelBlue,
                Font = new Font("Segoe UI", 8.5f)
            });
            ay += 46;

            btnTrain = new Button
            {
                Text = "▶  Start Training",
                Location = new Point(12, ay),
                Size = new Size(210, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(41, 98, 200),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTrain.FlatAppearance.BorderSize = 0;
            pnlAlgoScroll.Controls.Add(btnTrain); ay += 50;
            btnCancelTrain = Btn("⏹  Cancel", new Point(12, ay), 130, false);
            btnCancelTrain.Enabled = false;
            pnlAlgoScroll.Controls.Add(btnCancelTrain);
            outer.Panel1.Controls.Add(pnlAlgoScroll);

            // ── RIGHT: info card + log ────────────────────────────────────
            var rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel1,
                BorderStyle = BorderStyle.None
            };
            rightSplit.Layout += (s, e) =>
            {
                try { if (rightSplit.Height > 340 + rightSplit.SplitterWidth + 40) rightSplit.SplitterDistance = 300; }
                catch { }
            };

            var infoPanel = new Panel
            { Dock = DockStyle.Fill, BackColor = Color.FromArgb(235, 243, 255), Padding = new Padding(14, 12, 14, 10) };
            lblAlgoTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 60, 168),
                Text = "Select an algorithm on the left to see details"
            };
            rtbAlgoDesc = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(235, 243, 255),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            infoPanel.Controls.Add(rtbAlgoDesc);
            infoPanel.Controls.Add(lblAlgoTitle);
            rightSplit.Panel1.Controls.Add(infoPanel);

            var logHdr = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(22, 28, 42) };
            logHdr.Controls.Add(new Label
            { Text = "  Training Log", ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Location = new Point(4, 8), AutoSize = true });
            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(14, 16, 22),
                ForeColor = Color.FromArgb(168, 208, 168),
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            rightSplit.Panel2.Controls.Add(rtbLog);
            rightSplit.Panel2.Controls.Add(logHdr);

            outer.Panel2.Controls.Add(rightSplit);
            tpTraining.Controls.Add(outer);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  RESULTS TAB
        // ═════════════════════════════════════════════════════════════════════
        private void BuildResultsTab()
        {
            lblResultTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(14, 10, 0, 0),
                Text = "Train a model first — results will appear here.",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 60, 168),
                BackColor = Color.FromArgb(226, 238, 255)
            };

            var pMetrics = new Panel { Dock = DockStyle.Top, Height = 220 };
            dgvResults = StyledGrid(readOnly: true);
            dgvResults.Dock = DockStyle.Fill;
            dgvResults.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            pMetrics.Controls.Add(dgvResults);

            rtbResultLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9f),
                BackColor = Color.FromArgb(248, 250, 255),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            var pBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(10, 8, 0, 0),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.White
            };
            pBtns.Paint += PaintTopLine;
            btnSaveModel = Btn("💾  Save Best Model", Point.Empty, 195, true);
            btnExportReport = Btn("📄  Export Prediction Report", Point.Empty, 225, false);
            btnSaveModel.Enabled = btnExportReport.Enabled = false;
            pBtns.Controls.AddRange(new Control[] { btnSaveModel, btnExportReport });

            tpResults.Controls.Add(rtbResultLog);
            tpResults.Controls.Add(pMetrics);
            tpResults.Controls.Add(pBtns);
            tpResults.Controls.Add(lblResultTitle);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  FACTORY HELPERS
        // ═════════════════════════════════════════════════════════════════════
        private Button Btn(string text, Point loc, int w, bool accent)
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
            if (accent) { b.BackColor = Color.FromArgb(41, 98, 200); b.ForeColor = Color.White; b.FlatAppearance.BorderSize = 0; }
            else { b.BackColor = Color.FromArgb(224, 232, 248); b.ForeColor = Color.FromArgb(38, 58, 118); b.FlatAppearance.BorderColor = Color.FromArgb(180, 200, 234); }
            return b;
        }

        private static Button SmBtn(string text, Point loc, int w) =>
            new Button
            {
                Text = text,
                Location = loc,
                Width = w,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = Color.FromArgb(224, 232, 248),
                ForeColor = Color.FromArgb(38, 58, 118),
                FlatAppearance = { BorderColor = Color.FromArgb(180, 200, 234) }
            };

        private static TabPage MkTab(string t) =>
            new TabPage(t) { UseVisualStyleBackColor = false, BackColor = Color.FromArgb(242, 245, 252) };

        private static Label L(string text, int x, int y, bool bold = false, Color? color = null)
        {
            var l = new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 9f, bold ? FontStyle.Bold : FontStyle.Regular) };
            if (color.HasValue) l.ForeColor = color.Value;
            return l;
        }

        private static Label FieldLabel(string text, int x, int y) =>
            new Label { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Color.FromArgb(45, 72, 155) };

        private static Panel Divider(int x, int y) =>
            new Panel { Location = new Point(x, y), Size = new Size(340, 1), BackColor = Color.FromArgb(215, 225, 245) };

        private static Panel ColPaneHeader(string title, string subtitle, Color accent)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.White };
            p.Paint += (s, e) =>
            {
                e.Graphics.FillRectangle(new SolidBrush(accent), 0, 0, 4, 68);
                e.Graphics.DrawLine(new Pen(Color.FromArgb(215, 225, 245)), 0, 67, ((Control)s!).Width, 67);
            };
            p.Controls.Add(new Label { Text = title, Location = new Point(14, 12), AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(25, 40, 90) });
            p.Controls.Add(new Label { Text = subtitle, Location = new Point(14, 36), AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(90, 105, 140) });
            return p;
        }

        private static DataGridView StyledGrid(bool readOnly)
        {
            var g = new DataGridView
            {
                ReadOnly = readOnly,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(210, 222, 244),
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Both,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
            };
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(41, 98, 200);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 98, 200);
            g.ColumnHeadersHeight = 34;
            g.EnableHeadersVisualStyles = false;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 249, 255);
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(198, 220, 255);
            g.DefaultCellStyle.SelectionForeColor = Color.Black;
            g.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
            g.DefaultCellStyle.Padding = new Padding(4, 0, 0, 0);
            return g;
        }

        private static void PaintBottomLine(object? s, PaintEventArgs e)
        {
            var c = (Control)s!;
            e.Graphics.DrawLine(new Pen(Color.FromArgb(210, 222, 244)), 0, c.Height - 1, c.Width, c.Height - 1);
        }

        private static void PaintTopLine(object? s, PaintEventArgs e)
        {
            var c = (Control)s!;
            e.Graphics.DrawLine(new Pen(Color.FromArgb(210, 222, 244)), 0, 0, c.Width, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { components?.Dispose(); tip?.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Small extension helper — lets us call .Also() inline
    // ════════════════════════════════════════════════════════════════════════
    internal static class ControlExt
    {
        public static T Also<T>(this T obj, Action<T> action) { action(obj); return obj; }
    }
}