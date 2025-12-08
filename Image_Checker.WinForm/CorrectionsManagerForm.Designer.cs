namespace Image_Checker.WinForm
{
    partial class CorrectionsManagerForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.gridCorrections = new System.Windows.Forms.DataGridView();
            this.colTimestamp = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFileName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colOriginalLabel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colConfidence = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colCorrectedLabel = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.colDelete = new System.Windows.Forms.DataGridViewButtonColumn();
            this.panelControls = new System.Windows.Forms.Panel();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnDeleteAll = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.cboFilterLabel = new System.Windows.Forms.ComboBox();
            this.lblFilter = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.lblSearch = new System.Windows.Forms.Label();
            this.lblStats = new System.Windows.Forms.Label();
            this.panelPreview = new System.Windows.Forms.Panel();
            this.picturePreview = new System.Windows.Forms.PictureBox();
            this.panelEdit = new System.Windows.Forms.Panel();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.cboEditLabel = new System.Windows.Forms.ComboBox();
            this.lblEdit = new System.Windows.Forms.Label();
            this.lblImageInfo = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridCorrections)).BeginInit();
            this.panelControls.SuspendLayout();
            this.panelPreview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picturePreview)).BeginInit();
            this.panelEdit.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.gridCorrections);
            this.splitContainer.Panel1.Controls.Add(this.panelControls);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.panelPreview);
            this.splitContainer.Panel2MinSize = 300;
            this.splitContainer.Size = new System.Drawing.Size(1400, 800);
            this.splitContainer.SplitterDistance = 400;
            this.splitContainer.TabIndex = 0;
            // 
            // gridCorrections
            // 
            this.gridCorrections.AllowUserToAddRows = false;
            this.gridCorrections.AllowUserToDeleteRows = false;
            this.gridCorrections.AutoGenerateColumns = false;
            this.gridCorrections.BackgroundColor = System.Drawing.Color.White;
            this.gridCorrections.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.gridCorrections.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridCorrections.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTimestamp,
            this.colFileName,
            this.colOriginalLabel,
            this.colConfidence,
            this.colCorrectedLabel,
            this.colDelete});
            this.gridCorrections.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridCorrections.Location = new System.Drawing.Point(0, 120);
            this.gridCorrections.MultiSelect = false;
            this.gridCorrections.Name = "gridCorrections";
            this.gridCorrections.RowHeadersVisible = true;
            this.gridCorrections.RowHeadersWidth = 51;
            this.gridCorrections.RowTemplate.Height = 29;
            this.gridCorrections.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridCorrections.Size = new System.Drawing.Size(1400, 280);
            this.gridCorrections.TabIndex = 1;
            this.gridCorrections.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.GridCorrections_CellClick);
            this.gridCorrections.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.GridCorrections_CellValueChanged);
            this.gridCorrections.SelectionChanged += new System.EventHandler(this.GridCorrections_SelectionChanged);
            // 
            // colTimestamp
            // 
            this.colTimestamp.DataPropertyName = "Timestamp";
            this.colTimestamp.HeaderText = "Timestamp";
            this.colTimestamp.MinimumWidth = 6;
            this.colTimestamp.Name = "colTimestamp";
            this.colTimestamp.ReadOnly = true;
            this.colTimestamp.Width = 150;
            // 
            // colFileName
            // 
            this.colFileName.DataPropertyName = "FileName";
            this.colFileName.HeaderText = "File Name";
            this.colFileName.MinimumWidth = 6;
            this.colFileName.Name = "colFileName";
            this.colFileName.ReadOnly = true;
            this.colFileName.Width = 200;
            // 
            // colOriginalLabel
            // 
            this.colOriginalLabel.DataPropertyName = "OriginalLabel";
            this.colOriginalLabel.HeaderText = "Original Prediction";
            this.colOriginalLabel.MinimumWidth = 6;
            this.colOriginalLabel.Name = "colOriginalLabel";
            this.colOriginalLabel.ReadOnly = true;
            this.colOriginalLabel.Width = 140;
            // 
            // colConfidence
            // 
            this.colConfidence.DataPropertyName = "ConfidenceDisplay";
            this.colConfidence.HeaderText = "Confidence";
            this.colConfidence.MinimumWidth = 6;
            this.colConfidence.Name = "colConfidence";
            this.colConfidence.ReadOnly = true;
            this.colConfidence.Width = 100;
            // 
            // colCorrectedLabel
            // 
            this.colCorrectedLabel.DataPropertyName = "CorrectedLabel";
            this.colCorrectedLabel.HeaderText = "Corrected Label";
            this.colCorrectedLabel.Items.AddRange(new object[] {
            "OK",
            "NG"});
            this.colCorrectedLabel.MinimumWidth = 6;
            this.colCorrectedLabel.Name = "colCorrectedLabel";
            this.colCorrectedLabel.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.colCorrectedLabel.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            this.colCorrectedLabel.Width = 130;
            // 
            // colDelete
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(53)))), ((int)(((byte)(69)))));
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.White;
            this.colDelete.DefaultCellStyle = dataGridViewCellStyle1;
            this.colDelete.HeaderText = "Action";
            this.colDelete.MinimumWidth = 6;
            this.colDelete.Name = "colDelete";
            this.colDelete.ReadOnly = true;
            this.colDelete.Text = "Delete";
            this.colDelete.UseColumnTextForButtonValue = true;
            this.colDelete.Width = 80;
            // 
            // panelControls
            // 
            this.panelControls.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.panelControls.Controls.Add(this.btnClose);
            this.panelControls.Controls.Add(this.btnDeleteAll);
            this.panelControls.Controls.Add(this.btnExport);
            this.panelControls.Controls.Add(this.btnRefresh);
            this.panelControls.Controls.Add(this.cboFilterLabel);
            this.panelControls.Controls.Add(this.lblFilter);
            this.panelControls.Controls.Add(this.txtSearch);
            this.panelControls.Controls.Add(this.lblSearch);
            this.panelControls.Controls.Add(this.lblStats);
            this.panelControls.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelControls.Location = new System.Drawing.Point(0, 0);
            this.panelControls.Name = "panelControls";
            this.panelControls.Padding = new System.Windows.Forms.Padding(10);
            this.panelControls.Size = new System.Drawing.Size(1400, 120);
            this.panelControls.TabIndex = 0;
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(360, 80);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 30);
            this.btnClose.TabIndex = 8;
            this.btnClose.Text = "✖ Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.BtnClose_Click);
            // 
            // btnDeleteAll
            // 
            this.btnDeleteAll.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(53)))), ((int)(((byte)(69)))));
            this.btnDeleteAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteAll.ForeColor = System.Drawing.Color.White;
            this.btnDeleteAll.Location = new System.Drawing.Point(240, 80);
            this.btnDeleteAll.Name = "btnDeleteAll";
            this.btnDeleteAll.Size = new System.Drawing.Size(110, 30);
            this.btnDeleteAll.TabIndex = 7;
            this.btnDeleteAll.Text = "🗑️ Clear All";
            this.btnDeleteAll.UseVisualStyleBackColor = false;
            this.btnDeleteAll.Click += new System.EventHandler(this.BtnDeleteAll_Click);
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(120, 80);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(110, 30);
            this.btnExport.TabIndex = 6;
            this.btnExport.Text = "📊 Export CSV";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.BtnExport_Click);
            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new System.Drawing.Point(10, 80);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(100, 30);
            this.btnRefresh.TabIndex = 5;
            this.btnRefresh.Text = "🔄 Refresh";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.BtnRefresh_Click);
            // 
            // cboFilterLabel
            // 
            this.cboFilterLabel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboFilterLabel.FormattingEnabled = true;
            this.cboFilterLabel.Items.AddRange(new object[] {
            "All",
            "OK",
            "NG"});
            this.cboFilterLabel.Location = new System.Drawing.Point(415, 45);
            this.cboFilterLabel.Name = "cboFilterLabel";
            this.cboFilterLabel.Size = new System.Drawing.Size(120, 28);
            this.cboFilterLabel.TabIndex = 4;
            this.cboFilterLabel.SelectedIndexChanged += new System.EventHandler(this.CboFilterLabel_SelectedIndexChanged);
            // 
            // lblFilter
            // 
            this.lblFilter.Location = new System.Drawing.Point(360, 45);
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new System.Drawing.Size(50, 25);
            this.lblFilter.TabIndex = 3;
            this.lblFilter.Text = "Filter:";
            this.lblFilter.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtSearch
            // 
            this.txtSearch.Location = new System.Drawing.Point(95, 45);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.PlaceholderText = "Search by filename...";
            this.txtSearch.Size = new System.Drawing.Size(250, 27);
            this.txtSearch.TabIndex = 2;
            this.txtSearch.TextChanged += new System.EventHandler(this.TxtSearch_TextChanged);
            // 
            // lblSearch
            // 
            this.lblSearch.Location = new System.Drawing.Point(10, 45);
            this.lblSearch.Name = "lblSearch";
            this.lblSearch.Size = new System.Drawing.Size(80, 25);
            this.lblSearch.TabIndex = 1;
            this.lblSearch.Text = "🔍 Search:";
            this.lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblStats
            // 
            this.lblStats.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblStats.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.lblStats.Location = new System.Drawing.Point(10, 10);
            this.lblStats.Name = "lblStats";
            this.lblStats.Size = new System.Drawing.Size(600, 25);
            this.lblStats.TabIndex = 0;
            this.lblStats.Text = "📊 Total Corrections: 0 | OK: 0 | NG: 0";
            // 
            // panelPreview
            // 
            this.panelPreview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.panelPreview.Controls.Add(this.picturePreview);
            this.panelPreview.Controls.Add(this.panelEdit);
            this.panelPreview.Controls.Add(this.lblImageInfo);
            this.panelPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPreview.Location = new System.Drawing.Point(0, 0);
            this.panelPreview.Name = "panelPreview";
            this.panelPreview.Padding = new System.Windows.Forms.Padding(10);
            this.panelPreview.Size = new System.Drawing.Size(1400, 396);
            this.panelPreview.TabIndex = 0;
            // 
            // picturePreview
            // 
            this.picturePreview.BackColor = System.Drawing.Color.White;
            this.picturePreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picturePreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picturePreview.Location = new System.Drawing.Point(10, 40);
            this.picturePreview.Name = "picturePreview";
            this.picturePreview.Size = new System.Drawing.Size(1380, 296);
            this.picturePreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picturePreview.TabIndex = 2;
            this.picturePreview.TabStop = false;
            // 
            // panelEdit
            // 
            this.panelEdit.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.panelEdit.Controls.Add(this.btnDelete);
            this.panelEdit.Controls.Add(this.btnSave);
            this.panelEdit.Controls.Add(this.cboEditLabel);
            this.panelEdit.Controls.Add(this.lblEdit);
            this.panelEdit.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelEdit.Location = new System.Drawing.Point(10, 336);
            this.panelEdit.Name = "panelEdit";
            this.panelEdit.Padding = new System.Windows.Forms.Padding(10);
            this.panelEdit.Size = new System.Drawing.Size(1380, 50);
            this.panelEdit.TabIndex = 1;
            // 
            // btnDelete
            // 
            this.btnDelete.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(53)))), ((int)(((byte)(69)))));
            this.btnDelete.Enabled = false;
            this.btnDelete.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDelete.ForeColor = System.Drawing.Color.White;
            this.btnDelete.Location = new System.Drawing.Point(355, 8);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(100, 30);
            this.btnDelete.TabIndex = 3;
            this.btnDelete.Text = "🗑️ Delete";
            this.btnDelete.UseVisualStyleBackColor = false;
            this.btnDelete.Click += new System.EventHandler(this.BtnDelete_Click);
            // 
            // btnSave
            // 
            this.btnSave.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(167)))), ((int)(((byte)(69)))));
            this.btnSave.Enabled = false;
            this.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSave.ForeColor = System.Drawing.Color.White;
            this.btnSave.Location = new System.Drawing.Point(225, 8);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(120, 30);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "💾 Save Change";
            this.btnSave.UseVisualStyleBackColor = false;
            this.btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            // 
            // cboEditLabel
            // 
            this.cboEditLabel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboEditLabel.Enabled = false;
            this.cboEditLabel.FormattingEnabled = true;
            this.cboEditLabel.Items.AddRange(new object[] {
            "OK",
            "NG"});
            this.cboEditLabel.Location = new System.Drawing.Point(115, 10);
            this.cboEditLabel.Name = "cboEditLabel";
            this.cboEditLabel.Size = new System.Drawing.Size(100, 28);
            this.cboEditLabel.TabIndex = 1;
            // 
            // lblEdit
            // 
            this.lblEdit.Location = new System.Drawing.Point(10, 12);
            this.lblEdit.Name = "lblEdit";
            this.lblEdit.Size = new System.Drawing.Size(100, 25);
            this.lblEdit.TabIndex = 0;
            this.lblEdit.Text = "Change Label:";
            this.lblEdit.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblImageInfo
            // 
            this.lblImageInfo.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblImageInfo.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblImageInfo.Location = new System.Drawing.Point(10, 10);
            this.lblImageInfo.Name = "lblImageInfo";
            this.lblImageInfo.Size = new System.Drawing.Size(1380, 30);
            this.lblImageInfo.TabIndex = 0;
            this.lblImageInfo.Text = "Select a correction to preview";
            this.lblImageInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // CorrectionsManagerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1400, 800);
            this.Controls.Add(this.splitContainer);
            this.MinimumSize = new System.Drawing.Size(1000, 600);
            this.Name = "CorrectionsManagerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Corrections Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CorrectionsManagerForm_FormClosing);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridCorrections)).EndInit();
            this.panelControls.ResumeLayout(false);
            this.panelControls.PerformLayout();
            this.panelPreview.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picturePreview)).EndInit();
            this.panelEdit.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.DataGridView gridCorrections;
        private System.Windows.Forms.Panel panelControls;
        private System.Windows.Forms.Panel panelPreview;
        private System.Windows.Forms.PictureBox picturePreview;
        private System.Windows.Forms.Label lblImageInfo;
        private System.Windows.Forms.Label lblStats;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ComboBox cboFilterLabel;
        private System.Windows.Forms.ComboBox cboEditLabel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnDeleteAll;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.Label lblFilter;
        private System.Windows.Forms.Panel panelEdit;
        private System.Windows.Forms.Label lblEdit;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTimestamp;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFileName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOriginalLabel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colConfidence;
        private System.Windows.Forms.DataGridViewComboBoxColumn colCorrectedLabel;
        private System.Windows.Forms.DataGridViewButtonColumn colDelete;
    }
}