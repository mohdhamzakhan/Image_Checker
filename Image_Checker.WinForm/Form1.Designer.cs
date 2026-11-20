namespace Image_Checker.WinForm
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeUsbPortControlButtons()
        {

        }

        /// <summary>
        /// Layout: nothing is positioned by absolute coordinates any more. TableLayoutPanels size and
        /// stack everything (toolbar / grid + side panel / status bar), so controls cannot overlap and
        /// the window can be resized or maximised. Fonts are normal 9pt Segoe UI.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
            menuStrip = new MenuStrip();
            menuFile = new ToolStripMenuItem();
            menuBuildModel = new ToolStripMenuItem();
            menuSelectModel = new ToolStripMenuItem();
            menuSeparator = new ToolStripSeparator();
            menuChangeBasePath = new ToolStripMenuItem();
            verifyAllSettingToolStripMenuItem = new ToolStripMenuItem();
            manageCorrectionsToolStripMenuItem = new ToolStripMenuItem();
            menuEvaluateModel = new ToolStripMenuItem();
            MenuConnectUsbLight = new ToolStripMenuItem();
            tlpRoot = new TableLayoutPanel();
            flowToolbar = new FlowLayoutPanel();
            btnSelectFolder = new Button();
            pnlFolderFilter = new TableLayoutPanel();
            lblFolderFilter = new Label();
            cbFolderFilter = new ComboBox();
            pnlPredFilter = new TableLayoutPanel();
            lblPredFilter = new Label();
            cbPredFilter = new ComboBox();
            pnlHeaderLabels = new TableLayoutPanel();
            lblUsbLightStatus = new Label();
            lblModelInfo = new Label();
            btnSelectSingleImage = new Button();
            btnMonitorFolder = new Button();
            tlpContent = new TableLayoutPanel();
            tlpLeft = new TableLayoutPanel();
            grid = new DataGridView();
            groupBoxUsbPortControl = new GroupBox();
            tlpUsb = new TableLayoutPanel();
            lblUsbPortStatus = new Label();
            btnCheckUsbSupport = new Button();
            btnTestUsbPortControl = new Button();
            btnConnectUsbHub = new Button();
            btnTestConnection = new Button();
            btnCyclePort = new Button();
            btnDisconnectUsbHub = new Button();
            tlpRight = new TableLayoutPanel();
            lblSingleImageResult = new Label();
            pictureBox = new PictureBox();
            lblInfo = new Label();
            lblSelectLabel = new Label();
            tlpCorrect = new TableLayoutPanel();
            cbCorrection = new ComboBox();
            btnCorrect = new Button();
            tlpUpdate = new TableLayoutPanel();
            btnQuickUpdate = new Button();
            btnRetrain = new Button();
            lblCorrectionCount = new Label();
            progressBar = new ProgressBar();
            lblStatus = new Label();
            tooltip = new ToolTip(components);
            screwPositionToolStripMenuItem = new ToolStripMenuItem();
            menuStrip.SuspendLayout();
            tlpRoot.SuspendLayout();
            flowToolbar.SuspendLayout();
            pnlFolderFilter.SuspendLayout();
            pnlPredFilter.SuspendLayout();
            pnlHeaderLabels.SuspendLayout();
            tlpContent.SuspendLayout();
            tlpLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)grid).BeginInit();
            groupBoxUsbPortControl.SuspendLayout();
            tlpUsb.SuspendLayout();
            tlpRight.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
            tlpCorrect.SuspendLayout();
            tlpUpdate.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip
            // 
            menuStrip.ImageScalingSize = new Size(24, 24);
            menuStrip.Items.AddRange(new ToolStripItem[] { menuFile });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Padding = new Padding(9, 3, 0, 3);
            menuStrip.Size = new Size(1800, 35);
            menuStrip.TabIndex = 16;
            menuStrip.Text = "menuStrip1";
            // 
            // menuFile
            // 
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuBuildModel, menuSelectModel, menuSeparator, menuChangeBasePath, verifyAllSettingToolStripMenuItem, manageCorrectionsToolStripMenuItem, menuEvaluateModel, MenuConnectUsbLight, screwPositionToolStripMenuItem });
            menuFile.Name = "menuFile";
            menuFile.Size = new Size(92, 29);
            menuFile.Text = "Settings";
            // 
            // menuBuildModel
            // 
            menuBuildModel.Name = "menuBuildModel";
            menuBuildModel.Size = new Size(348, 34);
            menuBuildModel.Text = "🤖 Build New Model...";
            menuBuildModel.Click += MenuBuildModel_Click;
            // 
            // menuSelectModel
            // 
            menuSelectModel.Name = "menuSelectModel";
            menuSelectModel.Size = new Size(348, 34);
            menuSelectModel.Text = "📂 Select Base Folder...";
            menuSelectModel.Click += MenuSelectModel_Click;
            // 
            // menuSeparator
            // 
            menuSeparator.Name = "menuSeparator";
            menuSeparator.Size = new Size(345, 6);
            // 
            // menuChangeBasePath
            // 
            menuChangeBasePath.Name = "menuChangeBasePath";
            menuChangeBasePath.Size = new Size(348, 34);
            menuChangeBasePath.Text = "📁 Change Base Path...";
            menuChangeBasePath.Click += MenuChangeBasePath_Click;
            // 
            // verifyAllSettingToolStripMenuItem
            // 
            verifyAllSettingToolStripMenuItem.Name = "verifyAllSettingToolStripMenuItem";
            verifyAllSettingToolStripMenuItem.Size = new Size(348, 34);
            verifyAllSettingToolStripMenuItem.Text = "⚙️ Verify All Settings";
            verifyAllSettingToolStripMenuItem.Click += MenuVerifySetup_Click;
            // 
            // manageCorrectionsToolStripMenuItem
            // 
            manageCorrectionsToolStripMenuItem.Name = "manageCorrectionsToolStripMenuItem";
            manageCorrectionsToolStripMenuItem.Size = new Size(348, 34);
            manageCorrectionsToolStripMenuItem.Text = "📝 Manage Corrections";
            manageCorrectionsToolStripMenuItem.Click += MenuManageCorrections_Click;
            // 
            // menuEvaluateModel
            // 
            menuEvaluateModel.Name = "menuEvaluateModel";
            menuEvaluateModel.Size = new Size(348, 34);
            menuEvaluateModel.Text = "📊 Evaluate Model (OK/NG)...";
            menuEvaluateModel.Click += MenuEvaluateModel_Click;
            // 
            // MenuConnectUsbLight
            // 
            MenuConnectUsbLight.Name = "MenuConnectUsbLight";
            MenuConnectUsbLight.Size = new Size(348, 34);
            MenuConnectUsbLight.Text = "🔌 Connect USB Light";
            MenuConnectUsbLight.Click += MenuConnectUsbLight_Click;
            // 
            // tlpRoot
            // 
            tlpRoot.ColumnCount = 1;
            tlpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpRoot.Controls.Add(flowToolbar, 0, 0);
            tlpRoot.Controls.Add(tlpContent, 0, 1);
            tlpRoot.Controls.Add(lblStatus, 0, 2);
            tlpRoot.Dock = DockStyle.Fill;
            tlpRoot.Location = new Point(0, 35);
            tlpRoot.Margin = new Padding(4, 4, 4, 4);
            tlpRoot.Name = "tlpRoot";
            tlpRoot.Padding = new Padding(12, 12, 12, 12);
            tlpRoot.RowCount = 3;
            tlpRoot.RowStyles.Add(new RowStyle());
            tlpRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpRoot.RowStyles.Add(new RowStyle());
            tlpRoot.Size = new Size(1800, 1075);
            tlpRoot.TabIndex = 0;
            // 
            // flowToolbar
            // 
            flowToolbar.AutoSize = true;
            flowToolbar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowToolbar.Controls.Add(btnSelectFolder);
            flowToolbar.Controls.Add(pnlFolderFilter);
            flowToolbar.Controls.Add(pnlPredFilter);
            flowToolbar.Controls.Add(pnlHeaderLabels);
            flowToolbar.Controls.Add(btnSelectSingleImage);
            flowToolbar.Controls.Add(btnMonitorFolder);
            flowToolbar.Dock = DockStyle.Fill;
            flowToolbar.Location = new Point(12, 12);
            flowToolbar.Margin = new Padding(0, 0, 0, 9);
            flowToolbar.Name = "flowToolbar";
            flowToolbar.Size = new Size(1776, 61);
            flowToolbar.TabIndex = 1;
            // 
            // btnSelectFolder
            // 
            btnSelectFolder.BackColor = Color.FromArgb(0, 120, 215);
            btnSelectFolder.FlatStyle = FlatStyle.Flat;
            btnSelectFolder.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSelectFolder.ForeColor = Color.White;
            btnSelectFolder.Location = new Point(0, 0);
            btnSelectFolder.Margin = new Padding(0, 0, 18, 0);
            btnSelectFolder.Name = "btnSelectFolder";
            btnSelectFolder.Size = new Size(210, 57);
            btnSelectFolder.TabIndex = 0;
            btnSelectFolder.Text = "📁 Select Folder";
            tooltip.SetToolTip(btnSelectFolder, "Process multiple images from folder structure");
            btnSelectFolder.UseVisualStyleBackColor = false;
            btnSelectFolder.Click += BtnSelectFolder_Click;
            // 
            // pnlFolderFilter
            // 
            pnlFolderFilter.AutoSize = true;
            pnlFolderFilter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            pnlFolderFilter.ColumnCount = 1;
            pnlFolderFilter.ColumnStyles.Add(new ColumnStyle());
            pnlFolderFilter.Controls.Add(lblFolderFilter, 0, 0);
            pnlFolderFilter.Controls.Add(cbFolderFilter, 0, 1);
            pnlFolderFilter.Location = new Point(228, 0);
            pnlFolderFilter.Margin = new Padding(0, 0, 18, 0);
            pnlFolderFilter.Name = "pnlFolderFilter";
            pnlFolderFilter.RowCount = 2;
            pnlFolderFilter.RowStyles.Add(new RowStyle());
            pnlFolderFilter.RowStyles.Add(new RowStyle());
            pnlFolderFilter.Size = new Size(253, 61);
            pnlFolderFilter.TabIndex = 1;
            // 
            // lblFolderFilter
            // 
            lblFolderFilter.AutoSize = true;
            lblFolderFilter.Location = new Point(0, 0);
            lblFolderFilter.Margin = new Padding(0, 0, 0, 3);
            lblFolderFilter.Name = "lblFolderFilter";
            lblFolderFilter.Size = new Size(134, 25);
            lblFolderFilter.TabIndex = 1;
            lblFolderFilter.Text = "Filter by Folder:";
            // 
            // cbFolderFilter
            // 
            cbFolderFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbFolderFilter.FormattingEnabled = true;
            cbFolderFilter.Location = new Point(0, 28);
            cbFolderFilter.Margin = new Padding(0);
            cbFolderFilter.Name = "cbFolderFilter";
            cbFolderFilter.Size = new Size(253, 33);
            cbFolderFilter.TabIndex = 2;
            cbFolderFilter.SelectedIndexChanged += ApplyFilters;
            // 
            // pnlPredFilter
            // 
            pnlPredFilter.AutoSize = true;
            pnlPredFilter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            pnlPredFilter.ColumnCount = 1;
            pnlPredFilter.ColumnStyles.Add(new ColumnStyle());
            pnlPredFilter.Controls.Add(lblPredFilter, 0, 0);
            pnlPredFilter.Controls.Add(cbPredFilter, 0, 1);
            pnlPredFilter.Location = new Point(499, 0);
            pnlPredFilter.Margin = new Padding(0, 0, 18, 0);
            pnlPredFilter.Name = "pnlPredFilter";
            pnlPredFilter.RowCount = 2;
            pnlPredFilter.RowStyles.Add(new RowStyle());
            pnlPredFilter.RowStyles.Add(new RowStyle());
            pnlPredFilter.Size = new Size(253, 61);
            pnlPredFilter.TabIndex = 2;
            // 
            // lblPredFilter
            // 
            lblPredFilter.AutoSize = true;
            lblPredFilter.Location = new Point(0, 0);
            lblPredFilter.Margin = new Padding(0, 0, 0, 3);
            lblPredFilter.Name = "lblPredFilter";
            lblPredFilter.Size = new Size(163, 25);
            lblPredFilter.TabIndex = 3;
            lblPredFilter.Text = "Filter by Prediction:";
            // 
            // cbPredFilter
            // 
            cbPredFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPredFilter.FormattingEnabled = true;
            cbPredFilter.Location = new Point(0, 28);
            cbPredFilter.Margin = new Padding(0);
            cbPredFilter.Name = "cbPredFilter";
            cbPredFilter.Size = new Size(253, 33);
            cbPredFilter.TabIndex = 4;
            cbPredFilter.SelectedIndexChanged += ApplyFilters;
            // 
            // pnlHeaderLabels
            // 
            pnlHeaderLabels.AutoSize = true;
            pnlHeaderLabels.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            pnlHeaderLabels.ColumnCount = 1;
            pnlHeaderLabels.ColumnStyles.Add(new ColumnStyle());
            pnlHeaderLabels.Controls.Add(lblUsbLightStatus, 0, 0);
            pnlHeaderLabels.Controls.Add(lblModelInfo, 0, 1);
            pnlHeaderLabels.Location = new Point(770, 0);
            pnlHeaderLabels.Margin = new Padding(0, 0, 18, 0);
            pnlHeaderLabels.Name = "pnlHeaderLabels";
            pnlHeaderLabels.RowCount = 2;
            pnlHeaderLabels.RowStyles.Add(new RowStyle());
            pnlHeaderLabels.RowStyles.Add(new RowStyle());
            pnlHeaderLabels.Size = new Size(149, 56);
            pnlHeaderLabels.TabIndex = 3;
            // 
            // lblUsbLightStatus
            // 
            lblUsbLightStatus.AutoSize = true;
            lblUsbLightStatus.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            lblUsbLightStatus.ForeColor = Color.Sienna;
            lblUsbLightStatus.Location = new Point(0, 0);
            lblUsbLightStatus.Margin = new Padding(0, 0, 0, 6);
            lblUsbLightStatus.Name = "lblUsbLightStatus";
            lblUsbLightStatus.Size = new Size(104, 25);
            lblUsbLightStatus.TabIndex = 21;
            lblUsbLightStatus.Text = "Light Status";
            // 
            // lblModelInfo
            // 
            lblModelInfo.AutoSize = true;
            lblModelInfo.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            lblModelInfo.ForeColor = Color.FromArgb(0, 120, 215);
            lblModelInfo.Location = new Point(0, 31);
            lblModelInfo.Margin = new Padding(0);
            lblModelInfo.Name = "lblModelInfo";
            lblModelInfo.Size = new Size(149, 25);
            lblModelInfo.TabIndex = 17;
            lblModelInfo.Text = "No model loaded";
            // 
            // btnSelectSingleImage
            // 
            btnSelectSingleImage.BackColor = Color.FromArgb(46, 125, 50);
            btnSelectSingleImage.FlatStyle = FlatStyle.Flat;
            btnSelectSingleImage.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSelectSingleImage.ForeColor = Color.White;
            btnSelectSingleImage.Location = new Point(937, 0);
            btnSelectSingleImage.Margin = new Padding(0, 0, 18, 0);
            btnSelectSingleImage.Name = "btnSelectSingleImage";
            btnSelectSingleImage.Size = new Size(285, 57);
            btnSelectSingleImage.TabIndex = 18;
            btnSelectSingleImage.Text = "🖼️ Predict Single Image";
            tooltip.SetToolTip(btnSelectSingleImage, "Select and predict a single image file");
            btnSelectSingleImage.UseVisualStyleBackColor = false;
            btnSelectSingleImage.Click += BtnSelectSingleImage_Click;
            // 
            // btnMonitorFolder
            // 
            btnMonitorFolder.BackColor = Color.FromArgb(156, 39, 176);
            btnMonitorFolder.FlatStyle = FlatStyle.Flat;
            btnMonitorFolder.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnMonitorFolder.ForeColor = Color.White;
            btnMonitorFolder.Location = new Point(1240, 0);
            btnMonitorFolder.Margin = new Padding(0);
            btnMonitorFolder.Name = "btnMonitorFolder";
            btnMonitorFolder.Size = new Size(315, 57);
            btnMonitorFolder.TabIndex = 20;
            btnMonitorFolder.Text = "👁️ Monitor Folder (OFF)";
            tooltip.SetToolTip(btnMonitorFolder, "Auto-detect and predict new images in class folders");
            btnMonitorFolder.UseVisualStyleBackColor = false;
            btnMonitorFolder.Click += BtnMonitorFolder_Click;
            // 
            // tlpContent
            // 
            tlpContent.ColumnCount = 2;
            tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 600F));
            tlpContent.Controls.Add(tlpLeft, 0, 0);
            tlpContent.Controls.Add(tlpRight, 1, 0);
            tlpContent.Dock = DockStyle.Fill;
            tlpContent.Location = new Point(12, 82);
            tlpContent.Margin = new Padding(0);
            tlpContent.Name = "tlpContent";
            tlpContent.RowCount = 1;
            tlpContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpContent.Size = new Size(1776, 820);
            tlpContent.TabIndex = 2;
            // 
            // tlpLeft
            // 
            tlpLeft.ColumnCount = 1;
            tlpLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpLeft.Controls.Add(grid, 0, 0);
            tlpLeft.Controls.Add(groupBoxUsbPortControl, 0, 1);
            tlpLeft.Dock = DockStyle.Fill;
            tlpLeft.Location = new Point(0, 0);
            tlpLeft.Margin = new Padding(0);
            tlpLeft.Name = "tlpLeft";
            tlpLeft.RowCount = 2;
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpLeft.RowStyles.Add(new RowStyle());
            tlpLeft.Size = new Size(1176, 820);
            tlpLeft.TabIndex = 0;
            // 
            // grid
            // 
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.Fixed3D;
            dataGridViewCellStyle5.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = SystemColors.Control;
            dataGridViewCellStyle5.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle5.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle6.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = SystemColors.Window;
            dataGridViewCellStyle6.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle6.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle6.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle6.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle6.WrapMode = DataGridViewTriState.False;
            grid.DefaultCellStyle = dataGridViewCellStyle6;
            grid.Dock = DockStyle.Fill;
            grid.Location = new Point(0, 0);
            grid.Margin = new Padding(0);
            grid.MultiSelect = false;
            grid.Name = "grid";
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.RowHeadersWidth = 62;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Size = new Size(1176, 578);
            grid.TabIndex = 5;
            grid.CellFormatting += Grid_CellFormatting;
            grid.SelectionChanged += Grid_SelectionChanged;
            // 
            // groupBoxUsbPortControl
            // 
            groupBoxUsbPortControl.AutoSize = true;
            groupBoxUsbPortControl.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupBoxUsbPortControl.Controls.Add(tlpUsb);
            groupBoxUsbPortControl.Dock = DockStyle.Fill;
            groupBoxUsbPortControl.Location = new Point(0, 590);
            groupBoxUsbPortControl.Margin = new Padding(0, 12, 0, 0);
            groupBoxUsbPortControl.Name = "groupBoxUsbPortControl";
            groupBoxUsbPortControl.Padding = new Padding(12, 6, 12, 12);
            groupBoxUsbPortControl.Size = new Size(1176, 230);
            groupBoxUsbPortControl.TabIndex = 200;
            groupBoxUsbPortControl.TabStop = false;
            groupBoxUsbPortControl.Text = "USB Port Power Control (Advanced)";
            groupBoxUsbPortControl.Visible = false;
            // 
            // tlpUsb
            // 
            tlpUsb.AutoSize = true;
            tlpUsb.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tlpUsb.ColumnCount = 3;
            tlpUsb.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tlpUsb.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            tlpUsb.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            tlpUsb.Controls.Add(lblUsbPortStatus, 0, 0);
            tlpUsb.Controls.Add(btnCheckUsbSupport, 0, 1);
            tlpUsb.Controls.Add(btnTestUsbPortControl, 1, 1);
            tlpUsb.Controls.Add(btnConnectUsbHub, 2, 1);
            tlpUsb.Controls.Add(btnTestConnection, 0, 2);
            tlpUsb.Controls.Add(btnCyclePort, 1, 2);
            tlpUsb.Controls.Add(btnDisconnectUsbHub, 2, 2);
            tlpUsb.Dock = DockStyle.Top;
            tlpUsb.Location = new Point(12, 30);
            tlpUsb.Margin = new Padding(4, 4, 4, 4);
            tlpUsb.Name = "tlpUsb";
            tlpUsb.RowCount = 3;
            tlpUsb.RowStyles.Add(new RowStyle());
            tlpUsb.RowStyles.Add(new RowStyle());
            tlpUsb.RowStyles.Add(new RowStyle());
            tlpUsb.Size = new Size(1152, 188);
            tlpUsb.TabIndex = 0;
            // 
            // lblUsbPortStatus
            // 
            lblUsbPortStatus.AutoSize = true;
            tlpUsb.SetColumnSpan(lblUsbPortStatus, 3);
            lblUsbPortStatus.Location = new Point(4, 4);
            lblUsbPortStatus.Margin = new Padding(4, 4, 4, 9);
            lblUsbPortStatus.Name = "lblUsbPortStatus";
            lblUsbPortStatus.Size = new Size(186, 25);
            lblUsbPortStatus.TabIndex = 0;
            lblUsbPortStatus.Text = "Status: Not connected";
            // 
            // btnCheckUsbSupport
            // 
            btnCheckUsbSupport.Dock = DockStyle.Fill;
            btnCheckUsbSupport.Location = new Point(4, 42);
            btnCheckUsbSupport.Margin = new Padding(4, 4, 4, 4);
            btnCheckUsbSupport.MinimumSize = new Size(0, 48);
            btnCheckUsbSupport.Name = "btnCheckUsbSupport";
            btnCheckUsbSupport.Size = new Size(375, 48);
            btnCheckUsbSupport.TabIndex = 1;
            btnCheckUsbSupport.Text = "✓ Check Support";
            btnCheckUsbSupport.UseVisualStyleBackColor = true;
            btnCheckUsbSupport.Click += BtnCheckUsbSupport_Click;
            // 
            // btnTestUsbPortControl
            // 
            btnTestUsbPortControl.Dock = DockStyle.Fill;
            btnTestUsbPortControl.Location = new Point(387, 42);
            btnTestUsbPortControl.Margin = new Padding(4, 4, 4, 4);
            btnTestUsbPortControl.MinimumSize = new Size(0, 48);
            btnTestUsbPortControl.Name = "btnTestUsbPortControl";
            btnTestUsbPortControl.Size = new Size(375, 48);
            btnTestUsbPortControl.TabIndex = 2;
            btnTestUsbPortControl.Text = "🔍 Run Diagnostics";
            btnTestUsbPortControl.UseVisualStyleBackColor = true;
            btnTestUsbPortControl.Click += BtnTestUsbPortControl_Click;
            // 
            // btnConnectUsbHub
            // 
            btnConnectUsbHub.BackColor = Color.FromArgb(220, 255, 220);
            btnConnectUsbHub.Dock = DockStyle.Fill;
            btnConnectUsbHub.Location = new Point(770, 42);
            btnConnectUsbHub.Margin = new Padding(4, 4, 4, 4);
            btnConnectUsbHub.MinimumSize = new Size(0, 48);
            btnConnectUsbHub.Name = "btnConnectUsbHub";
            btnConnectUsbHub.Size = new Size(378, 48);
            btnConnectUsbHub.TabIndex = 3;
            btnConnectUsbHub.Text = "🔌 Connect Hub";
            btnConnectUsbHub.UseVisualStyleBackColor = false;
            btnConnectUsbHub.Click += BtnConnectUsbHub_Click;
            // 
            // btnTestConnection
            // 
            btnTestConnection.Dock = DockStyle.Fill;
            btnTestConnection.Location = new Point(4, 98);
            btnTestConnection.Margin = new Padding(4, 4, 4, 4);
            btnTestConnection.MinimumSize = new Size(0, 48);
            btnTestConnection.Name = "btnTestConnection";
            btnTestConnection.Size = new Size(375, 86);
            btnTestConnection.TabIndex = 4;
            btnTestConnection.Text = "🔧 Test Connection";
            btnTestConnection.UseVisualStyleBackColor = true;
            btnTestConnection.Click += BtnTestConnection_Click;
            // 
            // btnCyclePort
            // 
            btnCyclePort.BackColor = Color.FromArgb(255, 248, 220);
            btnCyclePort.Dock = DockStyle.Fill;
            btnCyclePort.Location = new Point(387, 98);
            btnCyclePort.Margin = new Padding(4, 4, 4, 4);
            btnCyclePort.MinimumSize = new Size(0, 48);
            btnCyclePort.Name = "btnCyclePort";
            btnCyclePort.Size = new Size(375, 86);
            btnCyclePort.TabIndex = 5;
            btnCyclePort.Text = "⚡ Cycle Port";
            btnCyclePort.UseVisualStyleBackColor = false;
            btnCyclePort.Click += BtnCyclePort_Click;
            // 
            // btnDisconnectUsbHub
            // 
            btnDisconnectUsbHub.Dock = DockStyle.Fill;
            btnDisconnectUsbHub.Location = new Point(770, 98);
            btnDisconnectUsbHub.Margin = new Padding(4, 4, 4, 4);
            btnDisconnectUsbHub.MinimumSize = new Size(0, 48);
            btnDisconnectUsbHub.Name = "btnDisconnectUsbHub";
            btnDisconnectUsbHub.Size = new Size(378, 86);
            btnDisconnectUsbHub.TabIndex = 6;
            btnDisconnectUsbHub.Text = "⏹️ Disconnect";
            btnDisconnectUsbHub.UseVisualStyleBackColor = true;
            btnDisconnectUsbHub.Click += BtnDisconnectUsbHub_Click;
            // 
            // tlpRight
            // 
            tlpRight.ColumnCount = 1;
            tlpRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpRight.Controls.Add(lblSingleImageResult, 0, 0);
            tlpRight.Controls.Add(pictureBox, 0, 1);
            tlpRight.Controls.Add(lblInfo, 0, 2);
            tlpRight.Controls.Add(lblSelectLabel, 0, 3);
            tlpRight.Controls.Add(tlpCorrect, 0, 4);
            tlpRight.Controls.Add(tlpUpdate, 0, 5);
            tlpRight.Controls.Add(lblCorrectionCount, 0, 6);
            tlpRight.Controls.Add(progressBar, 0, 7);
            tlpRight.Dock = DockStyle.Fill;
            tlpRight.Location = new Point(1176, 0);
            tlpRight.Margin = new Padding(0);
            tlpRight.Name = "tlpRight";
            tlpRight.Padding = new Padding(15, 0, 0, 0);
            tlpRight.RowCount = 8;
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.Size = new Size(600, 820);
            tlpRight.TabIndex = 1;
            // 
            // lblSingleImageResult
            // 
            lblSingleImageResult.BackColor = Color.FromArgb(232, 245, 233);
            lblSingleImageResult.BorderStyle = BorderStyle.FixedSingle;
            lblSingleImageResult.Dock = DockStyle.Fill;
            lblSingleImageResult.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblSingleImageResult.ForeColor = Color.FromArgb(46, 125, 50);
            lblSingleImageResult.Location = new Point(15, 0);
            lblSingleImageResult.Margin = new Padding(0, 0, 0, 9);
            lblSingleImageResult.MinimumSize = new Size(2, 77);
            lblSingleImageResult.Name = "lblSingleImageResult";
            lblSingleImageResult.Size = new Size(585, 77);
            lblSingleImageResult.TabIndex = 19;
            lblSingleImageResult.Text = "Click 'Predict Single Image' to start";
            lblSingleImageResult.TextAlign = ContentAlignment.MiddleCenter;
            lblSingleImageResult.Visible = false;
            // 
            // pictureBox
            // 
            pictureBox.BorderStyle = BorderStyle.FixedSingle;
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.Location = new Point(15, 86);
            pictureBox.Margin = new Padding(0);
            pictureBox.MinimumSize = new Size(2, 179);
            pictureBox.Name = "pictureBox";
            pictureBox.Size = new Size(585, 406);
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 6;
            pictureBox.TabStop = false;
            // 
            // lblInfo
            // 
            lblInfo.BackColor = Color.FromArgb(240, 240, 240);
            lblInfo.BorderStyle = BorderStyle.FixedSingle;
            lblInfo.Dock = DockStyle.Fill;
            lblInfo.Location = new Point(15, 501);
            lblInfo.Margin = new Padding(0, 9, 0, 0);
            lblInfo.MinimumSize = new Size(2, 53);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(585, 53);
            lblInfo.TabIndex = 7;
            lblInfo.Text = "Select an image row to preview";
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblSelectLabel
            // 
            lblSelectLabel.AutoSize = true;
            lblSelectLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSelectLabel.Location = new Point(15, 569);
            lblSelectLabel.Margin = new Padding(0, 15, 0, 4);
            lblSelectLabel.Name = "lblSelectLabel";
            lblSelectLabel.Size = new Size(196, 25);
            lblSelectLabel.TabIndex = 8;
            lblSelectLabel.Text = "Correct Classification:";
            // 
            // tlpCorrect
            // 
            tlpCorrect.AutoSize = true;
            tlpCorrect.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tlpCorrect.ColumnCount = 2;
            tlpCorrect.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            tlpCorrect.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            tlpCorrect.Controls.Add(cbCorrection, 0, 0);
            tlpCorrect.Controls.Add(btnCorrect, 1, 0);
            tlpCorrect.Dock = DockStyle.Fill;
            tlpCorrect.Location = new Point(15, 598);
            tlpCorrect.Margin = new Padding(0);
            tlpCorrect.Name = "tlpCorrect";
            tlpCorrect.RowCount = 1;
            tlpCorrect.RowStyles.Add(new RowStyle());
            tlpCorrect.Size = new Size(585, 51);
            tlpCorrect.TabIndex = 9;
            // 
            // cbCorrection
            // 
            cbCorrection.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cbCorrection.DropDownStyle = ComboBoxStyle.DropDownList;
            cbCorrection.FormattingEnabled = true;
            cbCorrection.Location = new Point(0, 9);
            cbCorrection.Margin = new Padding(0, 0, 9, 0);
            cbCorrection.Name = "cbCorrection";
            cbCorrection.Size = new Size(213, 33);
            cbCorrection.TabIndex = 9;
            // 
            // btnCorrect
            // 
            btnCorrect.BackColor = Color.FromArgb(255, 140, 0);
            btnCorrect.Dock = DockStyle.Fill;
            btnCorrect.FlatStyle = FlatStyle.Flat;
            btnCorrect.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCorrect.ForeColor = Color.White;
            btnCorrect.Location = new Point(222, 0);
            btnCorrect.Margin = new Padding(0);
            btnCorrect.MinimumSize = new Size(0, 51);
            btnCorrect.Name = "btnCorrect";
            btnCorrect.Size = new Size(363, 51);
            btnCorrect.TabIndex = 10;
            btnCorrect.Text = "✏️ Save Correction";
            btnCorrect.UseVisualStyleBackColor = false;
            btnCorrect.Click += BtnCorrect_Click;
            // 
            // tlpUpdate
            // 
            tlpUpdate.AutoSize = true;
            tlpUpdate.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tlpUpdate.ColumnCount = 2;
            tlpUpdate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpUpdate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpUpdate.Controls.Add(btnQuickUpdate, 0, 0);
            tlpUpdate.Controls.Add(btnRetrain, 1, 0);
            tlpUpdate.Dock = DockStyle.Fill;
            tlpUpdate.Location = new Point(15, 661);
            tlpUpdate.Margin = new Padding(0, 12, 0, 0);
            tlpUpdate.Name = "tlpUpdate";
            tlpUpdate.RowCount = 1;
            tlpUpdate.RowStyles.Add(new RowStyle());
            tlpUpdate.Size = new Size(585, 57);
            tlpUpdate.TabIndex = 11;
            // 
            // btnQuickUpdate
            // 
            btnQuickUpdate.BackColor = Color.FromArgb(76, 175, 80);
            btnQuickUpdate.Dock = DockStyle.Fill;
            btnQuickUpdate.FlatStyle = FlatStyle.Flat;
            btnQuickUpdate.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnQuickUpdate.ForeColor = Color.White;
            btnQuickUpdate.Location = new Point(0, 0);
            btnQuickUpdate.Margin = new Padding(0, 0, 6, 0);
            btnQuickUpdate.MinimumSize = new Size(0, 57);
            btnQuickUpdate.Name = "btnQuickUpdate";
            btnQuickUpdate.Size = new Size(286, 57);
            btnQuickUpdate.TabIndex = 11;
            btnQuickUpdate.Text = "⚡ Quick Update (Fast)";
            tooltip.SetToolTip(btnQuickUpdate, "True Incremental Learning:\n• Preserves original model knowledge\n• Learns from corrections\n• Fast (1-3 minutes)\n• Requires images.csv in base folder");
            btnQuickUpdate.UseVisualStyleBackColor = false;
            btnQuickUpdate.Click += BtnQuickUpdate_Click;
            // 
            // btnRetrain
            // 
            btnRetrain.BackColor = Color.FromArgb(156, 39, 176);
            btnRetrain.Dock = DockStyle.Fill;
            btnRetrain.FlatStyle = FlatStyle.Flat;
            btnRetrain.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnRetrain.ForeColor = Color.White;
            btnRetrain.Location = new Point(298, 0);
            btnRetrain.Margin = new Padding(6, 0, 0, 0);
            btnRetrain.MinimumSize = new Size(0, 57);
            btnRetrain.Name = "btnRetrain";
            btnRetrain.Size = new Size(287, 57);
            btnRetrain.TabIndex = 12;
            btnRetrain.Text = "🔄 Full Retrain (Slow)";
            tooltip.SetToolTip(btnRetrain, "Full Retrain:\n• Rebuilds model from scratch\n• Most accurate\n• Slow (5-15 minutes)\n• Use when accumulated many corrections");
            btnRetrain.UseVisualStyleBackColor = false;
            btnRetrain.Click += BtnRetrain_Click;
            // 
            // lblCorrectionCount
            // 
            lblCorrectionCount.BackColor = Color.FromArgb(255, 248, 220);
            lblCorrectionCount.BorderStyle = BorderStyle.FixedSingle;
            lblCorrectionCount.Dock = DockStyle.Fill;
            lblCorrectionCount.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCorrectionCount.Location = new Point(15, 730);
            lblCorrectionCount.Margin = new Padding(0, 12, 0, 0);
            lblCorrectionCount.MinimumSize = new Size(2, 44);
            lblCorrectionCount.Name = "lblCorrectionCount";
            lblCorrectionCount.Size = new Size(585, 44);
            lblCorrectionCount.TabIndex = 13;
            lblCorrectionCount.Text = "Corrections pending: 0";
            lblCorrectionCount.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // progressBar
            // 
            progressBar.Dock = DockStyle.Fill;
            progressBar.Location = new Point(15, 786);
            progressBar.Margin = new Padding(0, 12, 0, 0);
            progressBar.MinimumSize = new Size(0, 27);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(585, 34);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TabIndex = 14;
            progressBar.Visible = false;
            // 
            // lblStatus
            // 
            lblStatus.BackColor = Color.FromArgb(245, 245, 245);
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.ForeColor = Color.FromArgb(64, 64, 64);
            lblStatus.Location = new Point(12, 914);
            lblStatus.Margin = new Padding(0, 12, 0, 0);
            lblStatus.MinimumSize = new Size(2, 149);
            lblStatus.Name = "lblStatus";
            lblStatus.Padding = new Padding(9, 6, 9, 6);
            lblStatus.Size = new Size(1776, 149);
            lblStatus.TabIndex = 15;
            lblStatus.Text = "Model status will appear here";
            // 
            // screwPositionToolStripMenuItem
            // 
            screwPositionToolStripMenuItem.Name = "screwPositionToolStripMenuItem";
            screwPositionToolStripMenuItem.Size = new Size(348, 34);
            screwPositionToolStripMenuItem.Text = "🔩 Screw Position";
            screwPositionToolStripMenuItem.Click += screwPositionToolStripMenuItem_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(144F, 144F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            ClientSize = new Size(1800, 1110);
            Controls.Add(tlpRoot);
            Controls.Add(menuStrip);
            Font = new Font("Segoe UI", 9F);
            MainMenuStrip = menuStrip;
            Margin = new Padding(4, 4, 4, 4);
            MinimumSize = new Size(1489, 992);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MEAI Image Checking System";
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            tlpRoot.ResumeLayout(false);
            tlpRoot.PerformLayout();
            flowToolbar.ResumeLayout(false);
            flowToolbar.PerformLayout();
            pnlFolderFilter.ResumeLayout(false);
            pnlFolderFilter.PerformLayout();
            pnlPredFilter.ResumeLayout(false);
            pnlPredFilter.PerformLayout();
            pnlHeaderLabels.ResumeLayout(false);
            pnlHeaderLabels.PerformLayout();
            tlpContent.ResumeLayout(false);
            tlpLeft.ResumeLayout(false);
            tlpLeft.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)grid).EndInit();
            groupBoxUsbPortControl.ResumeLayout(false);
            groupBoxUsbPortControl.PerformLayout();
            tlpUsb.ResumeLayout(false);
            tlpUsb.PerformLayout();
            tlpRight.ResumeLayout(false);
            tlpRight.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
            tlpCorrect.ResumeLayout(false);
            tlpUpdate.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuFile;
        private System.Windows.Forms.ToolStripMenuItem menuBuildModel;
        private System.Windows.Forms.ToolStripMenuItem menuSelectModel;
        private System.Windows.Forms.ToolStripSeparator menuSeparator;
        private System.Windows.Forms.ToolStripMenuItem menuChangeBasePath;
        private System.Windows.Forms.Button btnSelectFolder;
        private System.Windows.Forms.Button btnSelectSingleImage;
        private System.Windows.Forms.ComboBox cbFolderFilter;
        private System.Windows.Forms.ComboBox cbPredFilter;
        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.ComboBox cbCorrection;
        private System.Windows.Forms.Button btnCorrect;
        private System.Windows.Forms.Button btnQuickUpdate;
        private System.Windows.Forms.Button btnRetrain;
        private System.Windows.Forms.Label lblCorrectionCount;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblFolderFilter;
        private System.Windows.Forms.Label lblPredFilter;
        private System.Windows.Forms.Label lblSelectLabel;
        private System.Windows.Forms.Label lblSingleImageResult;
        private Label lblModelInfo;
        private ToolStripMenuItem verifyAllSettingToolStripMenuItem;
        private ToolTip tooltip;
        private ToolStripMenuItem manageCorrectionsToolStripMenuItem;
        private ToolStripMenuItem menuEvaluateModel;
        private System.Windows.Forms.Button btnMonitorFolder;
        private ToolStripMenuItem MenuConnectUsbLight;
        private Label lblUsbLightStatus;
        private Button button1;

        private System.Windows.Forms.Button btnCheckUsbSupport;
        private System.Windows.Forms.Button btnTestUsbPortControl;
        private System.Windows.Forms.Button btnConnectUsbHub;
        private System.Windows.Forms.Button btnTestConnection;
        private System.Windows.Forms.Button btnCyclePort;
        private System.Windows.Forms.Button btnDisconnectUsbHub;
        private System.Windows.Forms.GroupBox groupBoxUsbPortControl;
        private System.Windows.Forms.Label lblUsbPortStatus;

        // layout containers
        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.FlowLayoutPanel flowToolbar;
        private System.Windows.Forms.TableLayoutPanel pnlFolderFilter;
        private System.Windows.Forms.TableLayoutPanel pnlPredFilter;
        private System.Windows.Forms.TableLayoutPanel pnlHeaderLabels;
        private System.Windows.Forms.TableLayoutPanel tlpContent;
        private System.Windows.Forms.TableLayoutPanel tlpLeft;
        private System.Windows.Forms.TableLayoutPanel tlpUsb;
        private System.Windows.Forms.TableLayoutPanel tlpRight;
        private System.Windows.Forms.TableLayoutPanel tlpCorrect;
        private System.Windows.Forms.TableLayoutPanel tlpUpdate;
        private ToolStripMenuItem screwPositionToolStripMenuItem;
    }
}