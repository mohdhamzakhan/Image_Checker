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
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
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
            menuStrip.Dock = DockStyle.Top;
            menuStrip.Items.AddRange(new ToolStripItem[] { menuFile });
            menuStrip.Name = "menuStrip";
            menuStrip.TabIndex = 16;
            menuStrip.Text = "menuStrip1";
            // 
            // menuFile
            // 
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuBuildModel, menuSelectModel, menuSeparator, menuChangeBasePath, verifyAllSettingToolStripMenuItem, manageCorrectionsToolStripMenuItem, menuEvaluateModel, MenuConnectUsbLight });
            menuFile.Name = "menuFile";
            menuFile.Text = "Settings";
            // 
            // menuBuildModel
            // 
            menuBuildModel.Name = "menuBuildModel";
            menuBuildModel.Text = "🤖 Build New Model...";
            menuBuildModel.Click += MenuBuildModel_Click;
            // 
            // menuSelectModel
            // 
            menuSelectModel.Name = "menuSelectModel";
            menuSelectModel.Text = "📂 Select Base Folder...";
            menuSelectModel.Click += MenuSelectModel_Click;
            // 
            // menuSeparator
            // 
            menuSeparator.Name = "menuSeparator";
            // 
            // menuChangeBasePath
            // 
            menuChangeBasePath.Name = "menuChangeBasePath";
            menuChangeBasePath.Text = "📁 Change Base Path...";
            menuChangeBasePath.Click += MenuChangeBasePath_Click;
            // 
            // verifyAllSettingToolStripMenuItem
            // 
            verifyAllSettingToolStripMenuItem.Name = "verifyAllSettingToolStripMenuItem";
            verifyAllSettingToolStripMenuItem.Text = "⚙️ Verify All Settings";
            verifyAllSettingToolStripMenuItem.Click += MenuVerifySetup_Click;
            // 
            // manageCorrectionsToolStripMenuItem
            // 
            manageCorrectionsToolStripMenuItem.Name = "manageCorrectionsToolStripMenuItem";
            manageCorrectionsToolStripMenuItem.Text = "📝 Manage Corrections";
            manageCorrectionsToolStripMenuItem.Click += MenuManageCorrections_Click;
            // 
            // menuEvaluateModel
            // 
            menuEvaluateModel.Name = "menuEvaluateModel";
            menuEvaluateModel.Text = "📊 Evaluate Model (OK/NG)...";
            menuEvaluateModel.Click += MenuEvaluateModel_Click;
            // 
            // MenuConnectUsbLight
            // 
            MenuConnectUsbLight.Name = "MenuConnectUsbLight";
            MenuConnectUsbLight.Text = "🔌 Connect USB Light";
            MenuConnectUsbLight.Click += MenuConnectUsbLight_Click;
            // 
            // tlpRoot  (3 rows: toolbar | content | status bar)
            // 
            tlpRoot.ColumnCount = 1;
            tlpRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpRoot.Controls.Add(flowToolbar, 0, 0);
            tlpRoot.Controls.Add(tlpContent, 0, 1);
            tlpRoot.Controls.Add(lblStatus, 0, 2);
            tlpRoot.Dock = DockStyle.Fill;
            tlpRoot.Name = "tlpRoot";
            tlpRoot.Padding = new Padding(8);
            tlpRoot.RowCount = 3;
            tlpRoot.RowStyles.Add(new RowStyle());
            tlpRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpRoot.RowStyles.Add(new RowStyle());
            tlpRoot.TabIndex = 0;
            // 
            // flowToolbar  (wraps onto a second line on a narrow window instead of overlapping)
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
            flowToolbar.Margin = new Padding(0, 0, 0, 6);
            flowToolbar.Name = "flowToolbar";
            flowToolbar.TabIndex = 1;
            // 
            // btnSelectFolder
            // 
            btnSelectFolder.BackColor = Color.FromArgb(0, 120, 215);
            btnSelectFolder.FlatStyle = FlatStyle.Flat;
            btnSelectFolder.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSelectFolder.ForeColor = Color.White;
            btnSelectFolder.Margin = new Padding(0, 0, 12, 0);
            btnSelectFolder.Name = "btnSelectFolder";
            btnSelectFolder.Size = new Size(140, 38);
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
            pnlFolderFilter.Margin = new Padding(0, 0, 12, 0);
            pnlFolderFilter.Name = "pnlFolderFilter";
            pnlFolderFilter.RowCount = 2;
            pnlFolderFilter.RowStyles.Add(new RowStyle());
            pnlFolderFilter.RowStyles.Add(new RowStyle());
            pnlFolderFilter.TabIndex = 1;
            // 
            // lblFolderFilter
            // 
            lblFolderFilter.AutoSize = true;
            lblFolderFilter.Margin = new Padding(0, 0, 0, 2);
            lblFolderFilter.Name = "lblFolderFilter";
            lblFolderFilter.TabIndex = 1;
            lblFolderFilter.Text = "Filter by Folder:";
            // 
            // cbFolderFilter
            // 
            cbFolderFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbFolderFilter.FormattingEnabled = true;
            cbFolderFilter.Margin = new Padding(0);
            cbFolderFilter.Name = "cbFolderFilter";
            cbFolderFilter.Size = new Size(170, 25);
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
            pnlPredFilter.Margin = new Padding(0, 0, 12, 0);
            pnlPredFilter.Name = "pnlPredFilter";
            pnlPredFilter.RowCount = 2;
            pnlPredFilter.RowStyles.Add(new RowStyle());
            pnlPredFilter.RowStyles.Add(new RowStyle());
            pnlPredFilter.TabIndex = 2;
            // 
            // lblPredFilter
            // 
            lblPredFilter.AutoSize = true;
            lblPredFilter.Margin = new Padding(0, 0, 0, 2);
            lblPredFilter.Name = "lblPredFilter";
            lblPredFilter.TabIndex = 3;
            lblPredFilter.Text = "Filter by Prediction:";
            // 
            // cbPredFilter
            // 
            cbPredFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPredFilter.FormattingEnabled = true;
            cbPredFilter.Margin = new Padding(0);
            cbPredFilter.Name = "cbPredFilter";
            cbPredFilter.Size = new Size(170, 25);
            cbPredFilter.TabIndex = 4;
            cbPredFilter.SelectedIndexChanged += ApplyFilters;
            // 
            // pnlHeaderLabels  (light + model status, stacked)
            // 
            pnlHeaderLabels.AutoSize = true;
            pnlHeaderLabels.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            pnlHeaderLabels.ColumnCount = 1;
            pnlHeaderLabels.ColumnStyles.Add(new ColumnStyle());
            pnlHeaderLabels.Controls.Add(lblUsbLightStatus, 0, 0);
            pnlHeaderLabels.Controls.Add(lblModelInfo, 0, 1);
            pnlHeaderLabels.Margin = new Padding(0, 0, 12, 0);
            pnlHeaderLabels.Name = "pnlHeaderLabels";
            pnlHeaderLabels.RowCount = 2;
            pnlHeaderLabels.RowStyles.Add(new RowStyle());
            pnlHeaderLabels.RowStyles.Add(new RowStyle());
            pnlHeaderLabels.TabIndex = 3;
            // 
            // lblUsbLightStatus
            // 
            lblUsbLightStatus.AutoSize = true;
            lblUsbLightStatus.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            lblUsbLightStatus.ForeColor = Color.Sienna;
            lblUsbLightStatus.Margin = new Padding(0, 0, 0, 4);
            lblUsbLightStatus.Name = "lblUsbLightStatus";
            lblUsbLightStatus.TabIndex = 21;
            lblUsbLightStatus.Text = "Light Status";
            // 
            // lblModelInfo
            // 
            lblModelInfo.AutoSize = true;
            lblModelInfo.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            lblModelInfo.ForeColor = Color.FromArgb(0, 120, 215);
            lblModelInfo.Margin = new Padding(0);
            lblModelInfo.Name = "lblModelInfo";
            lblModelInfo.TabIndex = 17;
            lblModelInfo.Text = "No model loaded";
            // 
            // btnSelectSingleImage
            // 
            btnSelectSingleImage.BackColor = Color.FromArgb(46, 125, 50);
            btnSelectSingleImage.FlatStyle = FlatStyle.Flat;
            btnSelectSingleImage.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnSelectSingleImage.ForeColor = Color.White;
            btnSelectSingleImage.Margin = new Padding(0, 0, 12, 0);
            btnSelectSingleImage.Name = "btnSelectSingleImage";
            btnSelectSingleImage.Size = new Size(190, 38);
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
            btnMonitorFolder.Margin = new Padding(0);
            btnMonitorFolder.Name = "btnMonitorFolder";
            btnMonitorFolder.Size = new Size(210, 38);
            btnMonitorFolder.TabIndex = 20;
            btnMonitorFolder.Text = "👁️ Monitor Folder (OFF)";
            tooltip.SetToolTip(btnMonitorFolder, "Auto-detect and predict new images in class folders");
            btnMonitorFolder.UseVisualStyleBackColor = false;
            btnMonitorFolder.Click += BtnMonitorFolder_Click;
            // 
            // tlpContent  (left: results grid | right: preview + corrections)
            // 
            tlpContent.ColumnCount = 2;
            tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400F));
            tlpContent.Controls.Add(tlpLeft, 0, 0);
            tlpContent.Controls.Add(tlpRight, 1, 0);
            tlpContent.Dock = DockStyle.Fill;
            tlpContent.Margin = new Padding(0);
            tlpContent.Name = "tlpContent";
            tlpContent.RowCount = 1;
            tlpContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpContent.TabIndex = 2;
            // 
            // tlpLeft
            // 
            tlpLeft.ColumnCount = 1;
            tlpLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpLeft.Controls.Add(grid, 0, 0);
            tlpLeft.Controls.Add(groupBoxUsbPortControl, 0, 1);
            tlpLeft.Dock = DockStyle.Fill;
            tlpLeft.Margin = new Padding(0);
            tlpLeft.Name = "tlpLeft";
            tlpLeft.RowCount = 2;
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpLeft.RowStyles.Add(new RowStyle());
            tlpLeft.TabIndex = 0;
            // 
            // grid
            // 
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.Fixed3D;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            grid.DefaultCellStyle = dataGridViewCellStyle2;
            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.MultiSelect = false;
            grid.Name = "grid";
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.TabIndex = 5;
            grid.CellFormatting += Grid_CellFormatting;
            grid.SelectionChanged += Grid_SelectionChanged;
            // 
            // groupBoxUsbPortControl  (hidden by default; takes its own row, so it never covers the grid)
            // 
            groupBoxUsbPortControl.AutoSize = true;
            groupBoxUsbPortControl.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            groupBoxUsbPortControl.Controls.Add(tlpUsb);
            groupBoxUsbPortControl.Dock = DockStyle.Fill;
            groupBoxUsbPortControl.Margin = new Padding(0, 8, 0, 0);
            groupBoxUsbPortControl.Name = "groupBoxUsbPortControl";
            groupBoxUsbPortControl.Padding = new Padding(8, 4, 8, 8);
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
            tlpUsb.Name = "tlpUsb";
            tlpUsb.RowCount = 3;
            tlpUsb.RowStyles.Add(new RowStyle());
            tlpUsb.RowStyles.Add(new RowStyle());
            tlpUsb.RowStyles.Add(new RowStyle());
            tlpUsb.TabIndex = 0;
            // 
            // lblUsbPortStatus
            // 
            lblUsbPortStatus.AutoSize = true;
            lblUsbPortStatus.Margin = new Padding(3, 3, 3, 6);
            lblUsbPortStatus.Name = "lblUsbPortStatus";
            lblUsbPortStatus.TabIndex = 0;
            lblUsbPortStatus.Text = "Status: Not connected";
            tlpUsb.SetColumnSpan(lblUsbPortStatus, 3);
            // 
            // btnCheckUsbSupport
            // 
            btnCheckUsbSupport.Dock = DockStyle.Fill;
            btnCheckUsbSupport.MinimumSize = new Size(0, 32);
            btnCheckUsbSupport.Name = "btnCheckUsbSupport";
            btnCheckUsbSupport.TabIndex = 1;
            btnCheckUsbSupport.Text = "✓ Check Support";
            btnCheckUsbSupport.UseVisualStyleBackColor = true;
            btnCheckUsbSupport.Click += BtnCheckUsbSupport_Click;
            // 
            // btnTestUsbPortControl
            // 
            btnTestUsbPortControl.Dock = DockStyle.Fill;
            btnTestUsbPortControl.MinimumSize = new Size(0, 32);
            btnTestUsbPortControl.Name = "btnTestUsbPortControl";
            btnTestUsbPortControl.TabIndex = 2;
            btnTestUsbPortControl.Text = "🔍 Run Diagnostics";
            btnTestUsbPortControl.UseVisualStyleBackColor = true;
            btnTestUsbPortControl.Click += BtnTestUsbPortControl_Click;
            // 
            // btnConnectUsbHub
            // 
            btnConnectUsbHub.BackColor = Color.FromArgb(220, 255, 220);
            btnConnectUsbHub.Dock = DockStyle.Fill;
            btnConnectUsbHub.MinimumSize = new Size(0, 32);
            btnConnectUsbHub.Name = "btnConnectUsbHub";
            btnConnectUsbHub.TabIndex = 3;
            btnConnectUsbHub.Text = "🔌 Connect Hub";
            btnConnectUsbHub.UseVisualStyleBackColor = false;
            btnConnectUsbHub.Click += BtnConnectUsbHub_Click;
            // 
            // btnTestConnection
            // 
            btnTestConnection.Dock = DockStyle.Fill;
            btnTestConnection.MinimumSize = new Size(0, 32);
            btnTestConnection.Name = "btnTestConnection";
            btnTestConnection.TabIndex = 4;
            btnTestConnection.Text = "🔧 Test Connection";
            btnTestConnection.UseVisualStyleBackColor = true;
            btnTestConnection.Click += BtnTestConnection_Click;
            // 
            // btnCyclePort
            // 
            btnCyclePort.BackColor = Color.FromArgb(255, 248, 220);
            btnCyclePort.Dock = DockStyle.Fill;
            btnCyclePort.MinimumSize = new Size(0, 32);
            btnCyclePort.Name = "btnCyclePort";
            btnCyclePort.TabIndex = 5;
            btnCyclePort.Text = "⚡ Cycle Port";
            btnCyclePort.UseVisualStyleBackColor = false;
            btnCyclePort.Click += BtnCyclePort_Click;
            // 
            // btnDisconnectUsbHub
            // 
            btnDisconnectUsbHub.Dock = DockStyle.Fill;
            btnDisconnectUsbHub.MinimumSize = new Size(0, 32);
            btnDisconnectUsbHub.Name = "btnDisconnectUsbHub";
            btnDisconnectUsbHub.TabIndex = 6;
            btnDisconnectUsbHub.Text = "⏹️ Disconnect";
            btnDisconnectUsbHub.UseVisualStyleBackColor = true;
            btnDisconnectUsbHub.Click += BtnDisconnectUsbHub_Click;
            // 
            // tlpRight  (single-image result | preview | info | correction | update buttons | count | progress)
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
            tlpRight.Margin = new Padding(0);
            tlpRight.Name = "tlpRight";
            tlpRight.Padding = new Padding(10, 0, 0, 0);
            tlpRight.RowCount = 8;
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.RowStyles.Add(new RowStyle());
            tlpRight.TabIndex = 1;
            // 
            // lblSingleImageResult  (hidden until a single image is predicted; a hidden row takes no space)
            // 
            lblSingleImageResult.BackColor = Color.FromArgb(232, 245, 233);
            lblSingleImageResult.BorderStyle = BorderStyle.FixedSingle;
            lblSingleImageResult.Dock = DockStyle.Fill;
            lblSingleImageResult.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblSingleImageResult.ForeColor = Color.FromArgb(46, 125, 50);
            lblSingleImageResult.Margin = new Padding(0, 0, 0, 6);
            lblSingleImageResult.MinimumSize = new Size(0, 52);
            lblSingleImageResult.Name = "lblSingleImageResult";
            lblSingleImageResult.TabIndex = 19;
            lblSingleImageResult.Text = "Click 'Predict Single Image' to start";
            lblSingleImageResult.TextAlign = ContentAlignment.MiddleCenter;
            lblSingleImageResult.Visible = false;
            // 
            // pictureBox
            // 
            pictureBox.BorderStyle = BorderStyle.FixedSingle;
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.Margin = new Padding(0);
            pictureBox.MinimumSize = new Size(0, 120);
            pictureBox.Name = "pictureBox";
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 6;
            pictureBox.TabStop = false;
            // 
            // lblInfo
            // 
            lblInfo.BackColor = Color.FromArgb(240, 240, 240);
            lblInfo.BorderStyle = BorderStyle.FixedSingle;
            lblInfo.Dock = DockStyle.Fill;
            lblInfo.Margin = new Padding(0, 6, 0, 0);
            lblInfo.MinimumSize = new Size(0, 36);
            lblInfo.Name = "lblInfo";
            lblInfo.TabIndex = 7;
            lblInfo.Text = "Select an image row to preview";
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblSelectLabel
            // 
            lblSelectLabel.AutoSize = true;
            lblSelectLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSelectLabel.Margin = new Padding(0, 10, 0, 3);
            lblSelectLabel.Name = "lblSelectLabel";
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
            tlpCorrect.Margin = new Padding(0);
            tlpCorrect.Name = "tlpCorrect";
            tlpCorrect.RowCount = 1;
            tlpCorrect.RowStyles.Add(new RowStyle());
            tlpCorrect.TabIndex = 9;
            // 
            // cbCorrection
            // 
            cbCorrection.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cbCorrection.DropDownStyle = ComboBoxStyle.DropDownList;
            cbCorrection.FormattingEnabled = true;
            cbCorrection.Margin = new Padding(0, 0, 6, 0);
            cbCorrection.Name = "cbCorrection";
            cbCorrection.TabIndex = 9;
            // 
            // btnCorrect
            // 
            btnCorrect.BackColor = Color.FromArgb(255, 140, 0);
            btnCorrect.Dock = DockStyle.Fill;
            btnCorrect.FlatStyle = FlatStyle.Flat;
            btnCorrect.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCorrect.ForeColor = Color.White;
            btnCorrect.Margin = new Padding(0);
            btnCorrect.MinimumSize = new Size(0, 34);
            btnCorrect.Name = "btnCorrect";
            btnCorrect.TabIndex = 10;
            btnCorrect.Text = "✏️ Save Correction";
            btnCorrect.UseVisualStyleBackColor = false;
            btnCorrect.Click += BtnCorrect_Click;
            // 
            // tlpUpdate  (the two update buttons share one row)
            // 
            tlpUpdate.AutoSize = true;
            tlpUpdate.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tlpUpdate.ColumnCount = 2;
            tlpUpdate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpUpdate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tlpUpdate.Controls.Add(btnQuickUpdate, 0, 0);
            tlpUpdate.Controls.Add(btnRetrain, 1, 0);
            tlpUpdate.Dock = DockStyle.Fill;
            tlpUpdate.Margin = new Padding(0, 8, 0, 0);
            tlpUpdate.Name = "tlpUpdate";
            tlpUpdate.RowCount = 1;
            tlpUpdate.RowStyles.Add(new RowStyle());
            tlpUpdate.TabIndex = 11;
            // 
            // btnQuickUpdate
            // 
            btnQuickUpdate.BackColor = Color.FromArgb(76, 175, 80);
            btnQuickUpdate.Dock = DockStyle.Fill;
            btnQuickUpdate.FlatStyle = FlatStyle.Flat;
            btnQuickUpdate.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnQuickUpdate.ForeColor = Color.White;
            btnQuickUpdate.Margin = new Padding(0, 0, 4, 0);
            btnQuickUpdate.MinimumSize = new Size(0, 38);
            btnQuickUpdate.Name = "btnQuickUpdate";
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
            btnRetrain.Margin = new Padding(4, 0, 0, 0);
            btnRetrain.MinimumSize = new Size(0, 38);
            btnRetrain.Name = "btnRetrain";
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
            lblCorrectionCount.Margin = new Padding(0, 8, 0, 0);
            lblCorrectionCount.MinimumSize = new Size(0, 30);
            lblCorrectionCount.Name = "lblCorrectionCount";
            lblCorrectionCount.TabIndex = 13;
            lblCorrectionCount.Text = "Corrections pending: 0";
            lblCorrectionCount.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // progressBar  (hidden until a long operation runs; a hidden row takes no space)
            // 
            progressBar.Dock = DockStyle.Fill;
            progressBar.Margin = new Padding(0, 8, 0, 0);
            progressBar.MinimumSize = new Size(0, 18);
            progressBar.Name = "progressBar";
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TabIndex = 14;
            progressBar.Visible = false;
            // 
            // lblStatus  (full-width status bar: long messages and alerts have room)
            // 
            lblStatus.BackColor = Color.FromArgb(245, 245, 245);
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.ForeColor = Color.FromArgb(64, 64, 64);
            lblStatus.Margin = new Padding(0, 8, 0, 0);
            lblStatus.MinimumSize = new Size(0, 100);
            lblStatus.Name = "lblStatus";
            lblStatus.Padding = new Padding(6, 4, 6, 4);
            lblStatus.TabIndex = 15;
            lblStatus.Text = "Model status will appear here";
            lblStatus.TextAlign = ContentAlignment.TopLeft;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            ClientSize = new Size(1200, 740);
            Controls.Add(tlpRoot);
            Controls.Add(menuStrip);
            Font = new Font("Segoe UI", 9F);
            MainMenuStrip = menuStrip;
            MinimumSize = new Size(1000, 680);
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
            tlpCorrect.PerformLayout();
            tlpUpdate.ResumeLayout(false);
            tlpUpdate.PerformLayout();
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
    }
}