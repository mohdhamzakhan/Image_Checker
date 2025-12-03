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
        private void InitializeComponent()
        {
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.menuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.menuBuildModel = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSelectModel = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.menuChangeBasePath = new System.Windows.Forms.ToolStripMenuItem();
            this.btnSelectFolder = new System.Windows.Forms.Button();
            this.cbFolderFilter = new System.Windows.Forms.ComboBox();
            this.cbPredFilter = new System.Windows.Forms.ComboBox();
            this.grid = new System.Windows.Forms.DataGridView();
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.lblInfo = new System.Windows.Forms.Label();
            this.cbCorrection = new System.Windows.Forms.ComboBox();
            this.btnCorrect = new System.Windows.Forms.Button();
            this.btnQuickUpdate = new System.Windows.Forms.Button();
            this.btnRetrain = new System.Windows.Forms.Button();
            this.lblCorrectionCount = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblFolderFilter = new System.Windows.Forms.Label();
            this.lblPredFilter = new System.Windows.Forms.Label();
            this.lblSelectLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            this.menuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuFile});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1330, 24);
            this.menuStrip.TabIndex = 16;
            this.menuStrip.Text = "menuStrip1";
            // 
            // menuFile
            // 
            this.menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuBuildModel,
            this.menuSelectModel,
            this.menuSeparator,
            this.menuChangeBasePath});
            this.menuFile.Name = "menuFile";
            this.menuFile.Size = new System.Drawing.Size(61, 20);
            this.menuFile.Text = "Settings";
            // 
            // menuBuildModel
            // 
            this.menuBuildModel.Name = "menuBuildModel";
            this.menuBuildModel.Size = new System.Drawing.Size(200, 22);
            this.menuBuildModel.Text = "🤖 Build New Model...";
            this.menuBuildModel.Click += new System.EventHandler(this.MenuBuildModel_Click);
            // 
            // menuSelectModel
            // 
            this.menuSelectModel.Name = "menuSelectModel";
            this.menuSelectModel.Size = new System.Drawing.Size(200, 22);
            this.menuSelectModel.Text = "📂 Select Existing Model...";
            this.menuSelectModel.Click += new System.EventHandler(this.MenuSelectModel_Click);
            // 
            // menuSeparator
            // 
            this.menuSeparator.Name = "menuSeparator";
            this.menuSeparator.Size = new System.Drawing.Size(197, 6);
            // 
            // menuChangeBasePath
            // 
            this.menuChangeBasePath.Name = "menuChangeBasePath";
            this.menuChangeBasePath.Size = new System.Drawing.Size(200, 22);
            this.menuChangeBasePath.Text = "📁 Change Base Path...";
            this.menuChangeBasePath.Click += new System.EventHandler(this.MenuChangeBasePath_Click);
            // 
            // btnSelectFolder
            // 
            this.btnSelectFolder.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnSelectFolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSelectFolder.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnSelectFolder.ForeColor = System.Drawing.Color.White;
            this.btnSelectFolder.Location = new System.Drawing.Point(20, 35);
            this.btnSelectFolder.Name = "btnSelectFolder";
            this.btnSelectFolder.Size = new System.Drawing.Size(160, 40);
            this.btnSelectFolder.TabIndex = 0;
            this.btnSelectFolder.Text = "📁 Select Folder";
            this.btnSelectFolder.UseVisualStyleBackColor = false;
            this.btnSelectFolder.Click += new System.EventHandler(this.BtnSelectFolder_Click);
            // 
            // lblFolderFilter
            // 
            this.lblFolderFilter.AutoSize = true;
            this.lblFolderFilter.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblFolderFilter.Location = new System.Drawing.Point(200, 23);
            this.lblFolderFilter.Name = "lblFolderFilter";
            this.lblFolderFilter.Size = new System.Drawing.Size(79, 15);
            this.lblFolderFilter.TabIndex = 1;
            this.lblFolderFilter.Text = "Filter by Folder:";
            // 
            // cbFolderFilter
            // 
            this.cbFolderFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbFolderFilter.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cbFolderFilter.FormattingEnabled = true;
            this.cbFolderFilter.Location = new System.Drawing.Point(200, 43);
            this.cbFolderFilter.Name = "cbFolderFilter";
            this.cbFolderFilter.Size = new System.Drawing.Size(200, 25);
            this.cbFolderFilter.TabIndex = 2;
            this.cbFolderFilter.SelectedIndexChanged += new System.EventHandler(this.ApplyFilters);
            // 
            // lblPredFilter
            // 
            this.lblPredFilter.AutoSize = true;
            this.lblPredFilter.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblPredFilter.Location = new System.Drawing.Point(420, 23);
            this.lblPredFilter.Name = "lblPredFilter";
            this.lblPredFilter.Size = new System.Drawing.Size(105, 15);
            this.lblPredFilter.TabIndex = 3;
            this.lblPredFilter.Text = "Filter by Prediction:";
            // 
            // cbPredFilter
            // 
            this.cbPredFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbPredFilter.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cbPredFilter.FormattingEnabled = true;
            this.cbPredFilter.Location = new System.Drawing.Point(420, 43);
            this.cbPredFilter.Name = "cbPredFilter";
            this.cbPredFilter.Size = new System.Drawing.Size(200, 25);
            this.cbPredFilter.TabIndex = 4;
            this.cbPredFilter.SelectedIndexChanged += new System.EventHandler(this.ApplyFilters);
            // 
            // grid
            // 
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.grid.BackgroundColor = System.Drawing.Color.White;
            this.grid.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.Location = new System.Drawing.Point(20, 90);
            this.grid.MultiSelect = false;
            this.grid.Name = "grid";
            this.grid.ReadOnly = true;
            this.grid.RowHeadersWidth = 51;
            this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.grid.Size = new System.Drawing.Size(800, 600);
            this.grid.TabIndex = 5;
            this.grid.SelectionChanged += new System.EventHandler(this.Grid_SelectionChanged);
            // 
            // pictureBox
            // 
            this.pictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox.Location = new System.Drawing.Point(850, 90);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(450, 400);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox.TabIndex = 6;
            this.pictureBox.TabStop = false;
            // 
            // lblInfo
            // 
            this.lblInfo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.lblInfo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblInfo.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblInfo.Location = new System.Drawing.Point(850, 490);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(450, 30);
            this.lblInfo.TabIndex = 7;
            this.lblInfo.Text = "Select an image row to preview";
            this.lblInfo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblSelectLabel
            // 
            this.lblSelectLabel.AutoSize = true;
            this.lblSelectLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblSelectLabel.Location = new System.Drawing.Point(850, 530);
            this.lblSelectLabel.Name = "lblSelectLabel";
            this.lblSelectLabel.Size = new System.Drawing.Size(125, 15);
            this.lblSelectLabel.TabIndex = 8;
            this.lblSelectLabel.Text = "Correct Classification:";
            // 
            // cbCorrection
            // 
            this.cbCorrection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbCorrection.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cbCorrection.FormattingEnabled = true;
            this.cbCorrection.Items.AddRange(new object[] {
            "OK",
            "NG"});
            this.cbCorrection.Location = new System.Drawing.Point(850, 550);
            this.cbCorrection.Name = "cbCorrection";
            this.cbCorrection.Size = new System.Drawing.Size(150, 25);
            this.cbCorrection.TabIndex = 9;
            // 
            // btnCorrect
            // 
            this.btnCorrect.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(140)))), ((int)(((byte)(0)))));
            this.btnCorrect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCorrect.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnCorrect.ForeColor = System.Drawing.Color.White;
            this.btnCorrect.Location = new System.Drawing.Point(1020, 550);
            this.btnCorrect.Name = "btnCorrect";
            this.btnCorrect.Size = new System.Drawing.Size(280, 35);
            this.btnCorrect.TabIndex = 10;
            this.btnCorrect.Text = "✏️ Save Correction";
            this.btnCorrect.UseVisualStyleBackColor = false;
            this.btnCorrect.Click += new System.EventHandler(this.BtnCorrect_Click);
            // 
            // btnQuickUpdate
            // 
            this.btnQuickUpdate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnQuickUpdate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnQuickUpdate.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnQuickUpdate.ForeColor = System.Drawing.Color.White;
            this.btnQuickUpdate.Location = new System.Drawing.Point(850, 600);
            this.btnQuickUpdate.Name = "btnQuickUpdate";
            this.btnQuickUpdate.Size = new System.Drawing.Size(210, 40);
            this.btnQuickUpdate.TabIndex = 11;
            this.btnQuickUpdate.Text = "⚡ Quick Update (Fast)";
            this.btnQuickUpdate.UseVisualStyleBackColor = false;
            this.btnQuickUpdate.Click += new System.EventHandler(this.BtnQuickUpdate_Click);
            // 
            // btnRetrain
            // 
            this.btnRetrain.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(156)))), ((int)(((byte)(39)))), ((int)(((byte)(176)))));
            this.btnRetrain.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRetrain.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.btnRetrain.ForeColor = System.Drawing.Color.White;
            this.btnRetrain.Location = new System.Drawing.Point(1090, 600);
            this.btnRetrain.Name = "btnRetrain";
            this.btnRetrain.Size = new System.Drawing.Size(210, 40);
            this.btnRetrain.TabIndex = 12;
            this.btnRetrain.Text = "🔄 Full Retrain (Slow)";
            this.btnRetrain.UseVisualStyleBackColor = false;
            this.btnRetrain.Click += new System.EventHandler(this.BtnRetrain_Click);
            // 
            // lblCorrectionCount
            // 
            this.lblCorrectionCount.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(248)))), ((int)(((byte)(220)))));
            this.lblCorrectionCount.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblCorrectionCount.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblCorrectionCount.Location = new System.Drawing.Point(850, 650);
            this.lblCorrectionCount.Name = "lblCorrectionCount";
            this.lblCorrectionCount.Size = new System.Drawing.Size(450, 25);
            this.lblCorrectionCount.TabIndex = 13;
            this.lblCorrectionCount.Text = "Corrections pending: 0";
            this.lblCorrectionCount.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(850, 685);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(450, 23);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.progressBar.TabIndex = 14;
            this.progressBar.Visible = false;
            // 
            // lblStatus
            // 
            this.lblStatus.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.lblStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lblStatus.Location = new System.Drawing.Point(850, 718);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(450, 60);
            this.lblStatus.TabIndex = 15;
            this.lblStatus.Text = "Model status will appear here";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;


            // Grid - should expand with form
            this.grid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));

            // PictureBox - should stay on right and expand vertically
            this.pictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Right)));

            // Right panel controls - anchor to right
            this.lblInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSelectLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cbCorrection.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCorrect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnQuickUpdate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRetrain.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lblCorrectionCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));

            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1330, 800);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblCorrectionCount);
            this.Controls.Add(this.btnRetrain);
            this.Controls.Add(this.btnQuickUpdate);
            this.Controls.Add(this.btnCorrect);
            this.Controls.Add(this.cbCorrection);
            this.Controls.Add(this.lblSelectLabel);
            this.Controls.Add(this.lblInfo);
            this.Controls.Add(this.pictureBox);
            this.Controls.Add(this.grid);
            this.Controls.Add(this.cbPredFilter);
            this.Controls.Add(this.lblPredFilter);
            this.Controls.Add(this.cbFolderFilter);
            this.Controls.Add(this.lblFolderFilter);
            this.Controls.Add(this.btnSelectFolder);
            this.Controls.Add(this.menuStrip);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MainMenuStrip = this.menuStrip;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Image Checker - Incremental Learning System";
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuFile;
        private System.Windows.Forms.ToolStripMenuItem menuBuildModel;
        private System.Windows.Forms.ToolStripMenuItem menuSelectModel;
        private System.Windows.Forms.ToolStripSeparator menuSeparator;
        private System.Windows.Forms.ToolStripMenuItem menuChangeBasePath;
        private System.Windows.Forms.Button btnSelectFolder;
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
    }
}