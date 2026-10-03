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

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        /// 
        private void InitializeUsbPortControlButtons()
        {

        }
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
            btnSelectFolder = new Button();
            btnSelectSingleImage = new Button();
            lblFolderFilter = new Label();
            cbFolderFilter = new ComboBox();
            lblPredFilter = new Label();
            cbPredFilter = new ComboBox();
            lblModelInfo = new Label();
            grid = new DataGridView();
            pictureBox = new PictureBox();
            lblInfo = new Label();
            lblSelectLabel = new Label();
            cbCorrection = new ComboBox();
            btnCorrect = new Button();
            btnQuickUpdate = new Button();
            btnRetrain = new Button();
            lblCorrectionCount = new Label();
            progressBar = new ProgressBar();
            lblStatus = new Label();
            lblSingleImageResult = new Label();
            btnMonitorFolder = new Button();
            tooltip = new ToolTip(components);
            lblUsbLightStatus = new Label();
            groupBoxUsbPortControl = new GroupBox();
            lblUsbPortStatus = new Label();
            btnCheckUsbSupport = new Button();
            btnTestUsbPortControl = new Button();
            btnConnectUsbHub = new Button();
            btnTestConnection = new Button();
            btnCyclePort = new Button();
            btnDisconnectUsbHub = new Button();
            menuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)grid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
            groupBoxUsbPortControl.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip
            // 
            menuStrip.GripMargin = new Padding(2);
            menuStrip.Items.AddRange(new ToolStripItem[] { menuFile });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Padding = new Padding(4, 1, 0, 1);
            menuStrip.Size = new Size(1268, 24);
            menuStrip.TabIndex = 16;
            menuStrip.Text = "menuStrip1";
            // 
            // menuFile
            // 
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuBuildModel, menuSelectModel, menuSeparator, menuChangeBasePath, verifyAllSettingToolStripMenuItem, manageCorrectionsToolStripMenuItem, menuEvaluateModel, MenuConnectUsbLight });
            menuFile.Name = "menuFile";
            menuFile.Size = new Size(61, 22);
            menuFile.Text = "Settings";
            // 
            // menuBuildModel
            // 
            menuBuildModel.Name = "menuBuildModel";
            menuBuildModel.Size = new Size(228, 22);
            menuBuildModel.Text = "🤖 Build New Model...";
            menuBuildModel.Click += MenuBuildModel_Click;
            // 
            // menuSelectModel
            // 
            menuSelectModel.Name = "menuSelectModel";
            menuSelectModel.Size = new Size(228, 22);
            menuSelectModel.Text = "📂 Select Base Folder...";
            menuSelectModel.Click += MenuSelectModel_Click;
            // 
            // menuSeparator
            // 
            menuSeparator.Name = "menuSeparator";
            menuSeparator.Size = new Size(225, 6);
            // 
            // menuChangeBasePath
            // 
            menuChangeBasePath.Name = "menuChangeBasePath";
            menuChangeBasePath.Size = new Size(228, 22);
            menuChangeBasePath.Text = "📁 Change Base Path...";
            menuChangeBasePath.Click += MenuChangeBasePath_Click;
            // 
            // verifyAllSettingToolStripMenuItem
            // 
            verifyAllSettingToolStripMenuItem.Name = "verifyAllSettingToolStripMenuItem";
            verifyAllSettingToolStripMenuItem.Size = new Size(228, 22);
            verifyAllSettingToolStripMenuItem.Text = "⚙️ Verify All Settings";
            verifyAllSettingToolStripMenuItem.Click += MenuVerifySetup_Click;
            // 
            // manageCorrectionsToolStripMenuItem
            // 
            manageCorrectionsToolStripMenuItem.Name = "manageCorrectionsToolStripMenuItem";
            manageCorrectionsToolStripMenuItem.Size = new Size(228, 22);
            manageCorrectionsToolStripMenuItem.Text = "📝 Manage Corrections";
            manageCorrectionsToolStripMenuItem.Click += MenuManageCorrections_Click;
            // 
            // menuEvaluateModel
            // 
            menuEvaluateModel.Name = "menuEvaluateModel";
            menuEvaluateModel.Size = new Size(228, 22);
            menuEvaluateModel.Text = "📊 Evaluate Model (OK/NG)...";
            menuEvaluateModel.Click += MenuEvaluateModel_Click;
            // 
            // MenuConnectUsbLight
            // 
            MenuConnectUsbLight.Name = "MenuConnectUsbLight";
            MenuConnectUsbLight.Size = new Size(228, 22);
            MenuConnectUsbLight.Text = "🔌 Connect USB Light";
            MenuConnectUsbLight.Click += MenuConnectUsbLight_Click;
            // 
            // btnSelectFolder
            // 
            btnSelectFolder.BackColor = Color.FromArgb(0, 120, 215);
            btnSelectFolder.FlatStyle = FlatStyle.Flat;
            btnSelectFolder.Font = new Font("Microsoft Sans Serif", 6.666667F, FontStyle.Bold);
            btnSelectFolder.ForeColor = Color.White;
            btnSelectFolder.Location = new Point(19, 39);
            btnSelectFolder.Name = "btnSelectFolder";
            btnSelectFolder.Size = new Size(153, 45);
            btnSelectFolder.TabIndex = 0;
            btnSelectFolder.Text = "📁 Select Folder";
            tooltip.SetToolTip(btnSelectFolder, "Process multiple images from folder structure");
            btnSelectFolder.UseVisualStyleBackColor = false;
            btnSelectFolder.Click += BtnSelectFolder_Click;
            // 
            // btnSelectSingleImage
            // 
            btnSelectSingleImage.BackColor = Color.FromArgb(46, 125, 50);
            btnSelectSingleImage.FlatStyle = FlatStyle.Flat;
            btnSelectSingleImage.Font = new Font("Microsoft Sans Serif", 6.666667F, FontStyle.Bold);
            btnSelectSingleImage.ForeColor = Color.White;
            btnSelectSingleImage.Location = new Point(815, 39);
            btnSelectSingleImage.Name = "btnSelectSingleImage";
            btnSelectSingleImage.Size = new Size(200, 45);
            btnSelectSingleImage.TabIndex = 18;
            btnSelectSingleImage.Text = "🖼️ Predict Single Image";
            tooltip.SetToolTip(btnSelectSingleImage, "Select and predict a single image file");
            btnSelectSingleImage.UseVisualStyleBackColor = false;
            btnSelectSingleImage.Click += BtnSelectSingleImage_Click;
            // 
            // lblFolderFilter
            // 
            lblFolderFilter.AutoSize = true;
            lblFolderFilter.Font = new Font("Microsoft Sans Serif", 6F);
            lblFolderFilter.Location = new Point(191, 31);
            lblFolderFilter.Name = "lblFolderFilter";
            lblFolderFilter.Size = new Size(90, 15);
            lblFolderFilter.TabIndex = 1;
            lblFolderFilter.Text = "Filter by Folder:";
            // 
            // cbFolderFilter
            // 
            cbFolderFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbFolderFilter.Font = new Font("Microsoft Sans Serif", 6.666667F);
            cbFolderFilter.FormattingEnabled = true;
            cbFolderFilter.Location = new Point(191, 54);
            cbFolderFilter.Name = "cbFolderFilter";
            cbFolderFilter.Size = new Size(191, 24);
            cbFolderFilter.TabIndex = 2;
            cbFolderFilter.SelectedIndexChanged += ApplyFilters;
            // 
            // lblPredFilter
            // 
            lblPredFilter.AutoSize = true;
            lblPredFilter.Font = new Font("Microsoft Sans Serif", 6F);
            lblPredFilter.Location = new Point(400, 31);
            lblPredFilter.Name = "lblPredFilter";
            lblPredFilter.Size = new Size(110, 15);
            lblPredFilter.TabIndex = 3;
            lblPredFilter.Text = "Filter by Prediction:";
            // 
            // cbPredFilter
            // 
            cbPredFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPredFilter.Font = new Font("Microsoft Sans Serif", 6.666667F);
            cbPredFilter.FormattingEnabled = true;
            cbPredFilter.Location = new Point(400, 54);
            cbPredFilter.Name = "cbPredFilter";
            cbPredFilter.Size = new Size(191, 24);
            cbPredFilter.TabIndex = 4;
            cbPredFilter.SelectedIndexChanged += ApplyFilters;
            // 
            // lblModelInfo
            // 
            lblModelInfo.AutoSize = true;
            lblModelInfo.Font = new Font("Microsoft Sans Serif", 6F, FontStyle.Italic);
            lblModelInfo.ForeColor = Color.FromArgb(0, 120, 215);
            lblModelInfo.Location = new Point(605, 61);
            lblModelInfo.Margin = new Padding(2, 0, 2, 0);
            lblModelInfo.Name = "lblModelInfo";
            lblModelInfo.Size = new Size(102, 15);
            lblModelInfo.TabIndex = 17;
            lblModelInfo.Text = "No model loaded";
            // 
            // grid
            // 
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.Fixed3D;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = SystemColors.Control;
            dataGridViewCellStyle1.Font = new Font("Microsoft Sans Serif", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Microsoft Sans Serif", 9F);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            grid.DefaultCellStyle = dataGridViewCellStyle2;
            grid.Location = new Point(8, 92);
            grid.MultiSelect = false;
            grid.Name = "grid";
            grid.ReadOnly = true;
            grid.RowHeadersWidth = 51;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Size = new Size(800, 680);
            grid.TabIndex = 5;
            grid.CellFormatting += Grid_CellFormatting;
            grid.SelectionChanged += Grid_SelectionChanged;
            // 
            // pictureBox
            // 
            pictureBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            pictureBox.BorderStyle = BorderStyle.FixedSingle;
            pictureBox.Location = new Point(813, 130);
            pictureBox.Name = "pictureBox";
            pictureBox.Size = new Size(449, 491);
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.TabIndex = 6;
            pictureBox.TabStop = false;
            // 
            // lblInfo
            // 
            lblInfo.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblInfo.BackColor = Color.FromArgb(240, 240, 240);
            lblInfo.BorderStyle = BorderStyle.FixedSingle;
            lblInfo.Font = new Font("Microsoft Sans Serif", 6F);
            lblInfo.Location = new Point(813, 624);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(449, 33);
            lblInfo.TabIndex = 7;
            lblInfo.Text = "Select an image row to preview";
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblSelectLabel
            // 
            lblSelectLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblSelectLabel.AutoSize = true;
            lblSelectLabel.Font = new Font("Microsoft Sans Serif", 6F, FontStyle.Bold);
            lblSelectLabel.Location = new Point(813, 657);
            lblSelectLabel.Name = "lblSelectLabel";
            lblSelectLabel.Size = new Size(147, 15);
            lblSelectLabel.TabIndex = 8;
            lblSelectLabel.Text = "Correct Classification:";
            // 
            // cbCorrection
            // 
            cbCorrection.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cbCorrection.DropDownStyle = ComboBoxStyle.DropDownList;
            cbCorrection.Font = new Font("Microsoft Sans Serif", 6.666667F);
            cbCorrection.FormattingEnabled = true;
            cbCorrection.Location = new Point(813, 686);
            cbCorrection.Name = "cbCorrection";
            cbCorrection.Size = new Size(121, 24);
            cbCorrection.TabIndex = 9;
            // 
            // btnCorrect
            // 
            btnCorrect.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCorrect.BackColor = Color.FromArgb(255, 140, 0);
            btnCorrect.FlatStyle = FlatStyle.Flat;
            btnCorrect.Font = new Font("Microsoft Sans Serif", 6F, FontStyle.Bold);
            btnCorrect.ForeColor = Color.White;
            btnCorrect.Location = new Point(944, 677);
            btnCorrect.Name = "btnCorrect";
            btnCorrect.Size = new Size(318, 33);
            btnCorrect.TabIndex = 10;
            btnCorrect.Text = "✏️ Save Correction";
            btnCorrect.UseVisualStyleBackColor = false;
            btnCorrect.Click += BtnCorrect_Click;
            // 
            // btnQuickUpdate
            // 
            btnQuickUpdate.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnQuickUpdate.BackColor = Color.FromArgb(76, 175, 80);
            btnQuickUpdate.FlatStyle = FlatStyle.Flat;
            btnQuickUpdate.Font = new Font("Microsoft Sans Serif", 6.666667F, FontStyle.Bold);
            btnQuickUpdate.ForeColor = Color.White;
            btnQuickUpdate.Location = new Point(813, 717);
            btnQuickUpdate.Name = "btnQuickUpdate";
            btnQuickUpdate.Size = new Size(220, 45);
            btnQuickUpdate.TabIndex = 11;
            btnQuickUpdate.Text = "⚡ Quick Update (Fast)";
            tooltip.SetToolTip(btnQuickUpdate, "True Incremental Learning:\n• Preserves original model knowledge\n• Learns from corrections\n• Fast (1-3 minutes)\n• Requires images.csv in base folder");
            btnQuickUpdate.UseVisualStyleBackColor = false;
            btnQuickUpdate.Click += BtnQuickUpdate_Click;
            // 
            // btnRetrain
            // 
            btnRetrain.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnRetrain.BackColor = Color.FromArgb(156, 39, 176);
            btnRetrain.FlatStyle = FlatStyle.Flat;
            btnRetrain.Font = new Font("Microsoft Sans Serif", 6.666667F, FontStyle.Bold);
            btnRetrain.ForeColor = Color.White;
            btnRetrain.Location = new Point(1042, 717);
            btnRetrain.Name = "btnRetrain";
            btnRetrain.Size = new Size(220, 45);
            btnRetrain.TabIndex = 12;
            btnRetrain.Text = "🔄 Full Retrain (Slow)";
            tooltip.SetToolTip(btnRetrain, "Full Retrain:\n• Rebuilds model from scratch\n• Most accurate\n• Slow (5-15 minutes)\n• Use when accumulated many corrections");
            btnRetrain.UseVisualStyleBackColor = false;
            btnRetrain.Click += BtnRetrain_Click;
            // 
            // lblCorrectionCount
            // 
            lblCorrectionCount.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblCorrectionCount.BackColor = Color.FromArgb(255, 248, 220);
            lblCorrectionCount.BorderStyle = BorderStyle.FixedSingle;
            lblCorrectionCount.Font = new Font("Microsoft Sans Serif", 6F, FontStyle.Bold);
            lblCorrectionCount.Location = new Point(813, 765);
            lblCorrectionCount.Name = "lblCorrectionCount";
            lblCorrectionCount.Size = new Size(449, 27);
            lblCorrectionCount.TabIndex = 13;
            lblCorrectionCount.Text = "Corrections pending: 0";
            lblCorrectionCount.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // progressBar
            // 
            progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            progressBar.Location = new Point(813, 686);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(449, 25);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TabIndex = 14;
            progressBar.Visible = false;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblStatus.BackColor = Color.FromArgb(245, 245, 245);
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            lblStatus.Font = new Font("Microsoft Sans Serif", 6F, FontStyle.Italic);
            lblStatus.ForeColor = Color.FromArgb(64, 64, 64);
            lblStatus.Location = new Point(813, 705);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(449, 67);
            lblStatus.TabIndex = 15;
            lblStatus.Text = "Model status will appear here";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblSingleImageResult
            // 
            lblSingleImageResult.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblSingleImageResult.BackColor = Color.FromArgb(232, 245, 233);
            lblSingleImageResult.BorderStyle = BorderStyle.FixedSingle;
            lblSingleImageResult.Font = new Font("Microsoft Sans Serif", 7.33333349F, FontStyle.Bold);
            lblSingleImageResult.ForeColor = Color.FromArgb(46, 125, 50);
            lblSingleImageResult.Location = new Point(813, 90);
            lblSingleImageResult.Name = "lblSingleImageResult";
            lblSingleImageResult.Size = new Size(449, 34);
            lblSingleImageResult.TabIndex = 19;
            lblSingleImageResult.Text = "Click 'Predict Single Image' to start";
            lblSingleImageResult.TextAlign = ContentAlignment.MiddleCenter;
            lblSingleImageResult.Visible = false;
            // 
            // btnMonitorFolder
            // 
            btnMonitorFolder.BackColor = Color.FromArgb(156, 39, 176);
            btnMonitorFolder.FlatStyle = FlatStyle.Flat;
            btnMonitorFolder.Font = new Font("Microsoft Sans Serif", 6.666667F, FontStyle.Bold);
            btnMonitorFolder.ForeColor = Color.White;
            btnMonitorFolder.Location = new Point(1020, 39);
            btnMonitorFolder.Margin = new Padding(2, 2, 2, 2);
            btnMonitorFolder.Name = "btnMonitorFolder";
            btnMonitorFolder.Size = new Size(238, 45);
            btnMonitorFolder.TabIndex = 20;
            btnMonitorFolder.Text = "👁️ Monitor Folder (OFF)";
            tooltip.SetToolTip(btnMonitorFolder, "Auto-detect and predict new images in class folders");
            btnMonitorFolder.UseVisualStyleBackColor = false;
            btnMonitorFolder.Click += BtnMonitorFolder_Click;
            // 
            // lblUsbLightStatus
            // 
            lblUsbLightStatus.AutoSize = true;
            lblUsbLightStatus.Font = new Font("Microsoft Sans Serif", 6F, FontStyle.Italic);
            lblUsbLightStatus.ForeColor = Color.Sienna;
            lblUsbLightStatus.Location = new Point(605, 31);
            lblUsbLightStatus.Margin = new Padding(2, 0, 2, 0);
            lblUsbLightStatus.Name = "lblUsbLightStatus";
            lblUsbLightStatus.Size = new Size(71, 15);
            lblUsbLightStatus.TabIndex = 21;
            lblUsbLightStatus.Text = "Light Status";
            // 
            // groupBoxUsbPortControl
            // 
            groupBoxUsbPortControl.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            groupBoxUsbPortControl.Controls.Add(lblUsbPortStatus);
            groupBoxUsbPortControl.Controls.Add(btnCheckUsbSupport);
            groupBoxUsbPortControl.Controls.Add(btnTestUsbPortControl);
            groupBoxUsbPortControl.Controls.Add(btnConnectUsbHub);
            groupBoxUsbPortControl.Controls.Add(btnTestConnection);
            groupBoxUsbPortControl.Controls.Add(btnCyclePort);
            groupBoxUsbPortControl.Controls.Add(btnDisconnectUsbHub);
            groupBoxUsbPortControl.Location = new Point(8, 675);
            groupBoxUsbPortControl.Margin = new Padding(2, 2, 2, 2);
            groupBoxUsbPortControl.Name = "groupBoxUsbPortControl";
            groupBoxUsbPortControl.Padding = new Padding(2, 2, 2, 2);
            groupBoxUsbPortControl.Size = new Size(427, 97);
            groupBoxUsbPortControl.TabIndex = 200;
            groupBoxUsbPortControl.TabStop = false;
            groupBoxUsbPortControl.Text = "USB Port Power Control (Advanced)";
            groupBoxUsbPortControl.Visible = false;
            // 
            // lblUsbPortStatus
            // 
            lblUsbPortStatus.BackColor = Color.FromArgb(240, 240, 240);
            lblUsbPortStatus.Location = new Point(10, 13);
            lblUsbPortStatus.Margin = new Padding(2, 0, 2, 0);
            lblUsbPortStatus.Name = "lblUsbPortStatus";
            lblUsbPortStatus.Size = new Size(367, 26);
            lblUsbPortStatus.TabIndex = 0;
            lblUsbPortStatus.Text = "Status: Not connected";
            lblUsbPortStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnCheckUsbSupport
            // 
            btnCheckUsbSupport.Location = new Point(10, 47);
            btnCheckUsbSupport.Margin = new Padding(2, 2, 2, 2);
            btnCheckUsbSupport.Name = "btnCheckUsbSupport";
            btnCheckUsbSupport.Size = new Size(117, 20);
            btnCheckUsbSupport.TabIndex = 1;
            btnCheckUsbSupport.Text = "✓ Check Support";
            btnCheckUsbSupport.UseVisualStyleBackColor = true;
            btnCheckUsbSupport.Click += BtnCheckUsbSupport_Click;
            // 
            // btnTestUsbPortControl
            // 
            btnTestUsbPortControl.Location = new Point(133, 47);
            btnTestUsbPortControl.Margin = new Padding(2, 2, 2, 2);
            btnTestUsbPortControl.Name = "btnTestUsbPortControl";
            btnTestUsbPortControl.Size = new Size(117, 20);
            btnTestUsbPortControl.TabIndex = 2;
            btnTestUsbPortControl.Text = "🔍 Run Diagnostics";
            btnTestUsbPortControl.UseVisualStyleBackColor = true;
            btnTestUsbPortControl.Click += BtnTestUsbPortControl_Click;
            // 
            // btnConnectUsbHub
            // 
            btnConnectUsbHub.BackColor = Color.FromArgb(220, 255, 220);
            btnConnectUsbHub.Location = new Point(257, 47);
            btnConnectUsbHub.Margin = new Padding(2, 2, 2, 2);
            btnConnectUsbHub.Name = "btnConnectUsbHub";
            btnConnectUsbHub.Size = new Size(117, 20);
            btnConnectUsbHub.TabIndex = 3;
            btnConnectUsbHub.Text = "🔌 Connect Hub";
            btnConnectUsbHub.UseVisualStyleBackColor = false;
            btnConnectUsbHub.Click += BtnConnectUsbHub_Click;
            // 
            // btnTestConnection
            // 
            btnTestConnection.Location = new Point(10, 70);
            btnTestConnection.Margin = new Padding(2, 2, 2, 2);
            btnTestConnection.Name = "btnTestConnection";
            btnTestConnection.Size = new Size(117, 20);
            btnTestConnection.TabIndex = 4;
            btnTestConnection.Text = "🔧 Test Connection";
            btnTestConnection.UseVisualStyleBackColor = true;
            btnTestConnection.Click += BtnTestConnection_Click;
            // 
            // btnCyclePort
            // 
            btnCyclePort.BackColor = Color.FromArgb(255, 248, 220);
            btnCyclePort.Location = new Point(133, 70);
            btnCyclePort.Margin = new Padding(2, 2, 2, 2);
            btnCyclePort.Name = "btnCyclePort";
            btnCyclePort.Size = new Size(117, 20);
            btnCyclePort.TabIndex = 5;
            btnCyclePort.Text = "⚡ Cycle Port";
            btnCyclePort.UseVisualStyleBackColor = false;
            btnCyclePort.Click += BtnCyclePort_Click;
            // 
            // btnDisconnectUsbHub
            // 
            btnDisconnectUsbHub.Location = new Point(257, 70);
            btnDisconnectUsbHub.Margin = new Padding(2, 2, 2, 2);
            btnDisconnectUsbHub.Name = "btnDisconnectUsbHub";
            btnDisconnectUsbHub.Size = new Size(117, 20);
            btnDisconnectUsbHub.TabIndex = 6;
            btnDisconnectUsbHub.Text = "⏹️ Disconnect";
            btnDisconnectUsbHub.UseVisualStyleBackColor = true;
            btnDisconnectUsbHub.Click += BtnDisconnectUsbHub_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            ClientSize = new Size(1268, 780);
            Controls.Add(lblUsbLightStatus);
            Controls.Add(lblSingleImageResult);
            Controls.Add(btnSelectSingleImage);
            Controls.Add(lblModelInfo);
            Controls.Add(lblStatus);
            Controls.Add(progressBar);
            Controls.Add(lblCorrectionCount);
            Controls.Add(btnRetrain);
            Controls.Add(btnQuickUpdate);
            Controls.Add(btnCorrect);
            Controls.Add(cbCorrection);
            Controls.Add(lblSelectLabel);
            Controls.Add(lblInfo);
            Controls.Add(pictureBox);
            Controls.Add(grid);
            Controls.Add(cbPredFilter);
            Controls.Add(lblPredFilter);
            Controls.Add(cbFolderFilter);
            Controls.Add(lblFolderFilter);
            Controls.Add(btnSelectFolder);
            Controls.Add(menuStrip);
            Controls.Add(groupBoxUsbPortControl);
            Controls.Add(btnMonitorFolder);
            Font = new Font("Microsoft Sans Serif", 6F);
            MainMenuStrip = menuStrip;
            MinimumSize = new Size(1284, 733);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MEAI Image Checking System";
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)grid).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
            groupBoxUsbPortControl.ResumeLayout(false);
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
    }
}