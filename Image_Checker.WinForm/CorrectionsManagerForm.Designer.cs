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
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            splitContainer = new SplitContainer();
            gridCorrections = new DataGridView();
            colDelete = new DataGridViewButtonColumn();
            panelControls = new Panel();
            btnClose = new Button();
            btnDeleteAll = new Button();
            btnExport = new Button();
            btnRefresh = new Button();
            cboFilterLabel = new ComboBox();
            lblFilter = new Label();
            txtSearch = new TextBox();
            lblSearch = new Label();
            lblStats = new Label();
            panelPreview = new Panel();
            picturePreview = new PictureBox();
            panelEdit = new Panel();
            btnDelete = new Button();
            btnSave = new Button();
            cboEditLabel = new ComboBox();
            lblEdit = new Label();
            lblImageInfo = new Label();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridCorrections).BeginInit();
            panelControls.SuspendLayout();
            panelPreview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picturePreview).BeginInit();
            panelEdit.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer
            // 
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.FixedPanel = FixedPanel.Panel2;
            splitContainer.Location = new Point(0, 0);
            splitContainer.Name = "splitContainer";
            splitContainer.Orientation = Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            splitContainer.Panel1.Controls.Add(gridCorrections);
            splitContainer.Panel1.Controls.Add(panelControls);
            // 
            // splitContainer.Panel2
            // 
            splitContainer.Panel2.Controls.Add(panelPreview);
            splitContainer.Panel2MinSize = 300;
            splitContainer.Size = new Size(1400, 780);
            splitContainer.SplitterDistance = 380;
            splitContainer.TabIndex = 0;
            // 
            // gridCorrections
            // 
            gridCorrections.AllowUserToAddRows = false;
            gridCorrections.AllowUserToDeleteRows = false;
            gridCorrections.BackgroundColor = Color.White;
            gridCorrections.BorderStyle = BorderStyle.None;
            gridCorrections.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridCorrections.Columns.AddRange(new DataGridViewColumn[] { colDelete });
            gridCorrections.Dock = DockStyle.Fill;
            gridCorrections.Location = new Point(0, 120);
            gridCorrections.MultiSelect = false;
            gridCorrections.Name = "gridCorrections";
            gridCorrections.RowHeadersWidth = 51;
            gridCorrections.RowTemplate.Height = 29;
            gridCorrections.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridCorrections.Size = new Size(1400, 260);
            gridCorrections.TabIndex = 1;
            gridCorrections.CellClick += GridCorrections_CellClick;
            gridCorrections.CellValueChanged += GridCorrections_CellValueChanged;
            gridCorrections.SelectionChanged += GridCorrections_SelectionChanged;
            // 
            // colDelete
            // 
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(220, 53, 69);
            dataGridViewCellStyle1.ForeColor = Color.White;
            colDelete.DefaultCellStyle = dataGridViewCellStyle1;
            colDelete.HeaderText = "Action";
            colDelete.MinimumWidth = 6;
            colDelete.Name = "colDelete";
            colDelete.ReadOnly = true;
            colDelete.Text = "Delete";
            colDelete.UseColumnTextForButtonValue = true;
            colDelete.Width = 80;
            // 
            // panelControls
            // 
            panelControls.BackColor = Color.FromArgb(240, 240, 240);
            panelControls.Controls.Add(btnClose);
            panelControls.Controls.Add(btnDeleteAll);
            panelControls.Controls.Add(btnExport);
            panelControls.Controls.Add(btnRefresh);
            panelControls.Controls.Add(cboFilterLabel);
            panelControls.Controls.Add(lblFilter);
            panelControls.Controls.Add(txtSearch);
            panelControls.Controls.Add(lblSearch);
            panelControls.Controls.Add(lblStats);
            panelControls.Dock = DockStyle.Top;
            panelControls.Location = new Point(0, 0);
            panelControls.Name = "panelControls";
            panelControls.Padding = new Padding(10, 10, 10, 10);
            panelControls.Size = new Size(1400, 120);
            panelControls.TabIndex = 0;
            // 
            // btnClose
            // 
            btnClose.Location = new Point(360, 80);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(100, 30);
            btnClose.TabIndex = 8;
            btnClose.Text = "✖ Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += BtnClose_Click;
            // 
            // btnDeleteAll
            // 
            btnDeleteAll.BackColor = Color.FromArgb(220, 53, 69);
            btnDeleteAll.FlatStyle = FlatStyle.Flat;
            btnDeleteAll.ForeColor = Color.White;
            btnDeleteAll.Location = new Point(240, 80);
            btnDeleteAll.Name = "btnDeleteAll";
            btnDeleteAll.Size = new Size(110, 30);
            btnDeleteAll.TabIndex = 7;
            btnDeleteAll.Text = "🗑️ Clear All";
            btnDeleteAll.UseVisualStyleBackColor = false;
            btnDeleteAll.Click += BtnDeleteAll_Click;
            // 
            // btnExport
            // 
            btnExport.Location = new Point(120, 80);
            btnExport.Name = "btnExport";
            btnExport.Size = new Size(110, 30);
            btnExport.TabIndex = 6;
            btnExport.Text = "📊 Export CSV";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += BtnExport_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(10, 80);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(100, 30);
            btnRefresh.TabIndex = 5;
            btnRefresh.Text = "🔄 Refresh";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += BtnRefresh_Click;
            // 
            // cboFilterLabel
            // 
            cboFilterLabel.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFilterLabel.FormattingEnabled = true;
            cboFilterLabel.Items.AddRange(new object[] { "All", "OK", "NG" });
            cboFilterLabel.Location = new Point(415, 45);
            cboFilterLabel.Name = "cboFilterLabel";
            cboFilterLabel.Size = new Size(120, 23);
            cboFilterLabel.TabIndex = 4;
            cboFilterLabel.SelectedIndexChanged += CboFilterLabel_SelectedIndexChanged;
            // 
            // lblFilter
            // 
            lblFilter.Location = new Point(360, 45);
            lblFilter.Name = "lblFilter";
            lblFilter.Size = new Size(50, 25);
            lblFilter.TabIndex = 3;
            lblFilter.Text = "Filter:";
            lblFilter.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtSearch
            // 
            txtSearch.Location = new Point(95, 45);
            txtSearch.Name = "txtSearch";
            txtSearch.PlaceholderText = "Search by filename...";
            txtSearch.Size = new Size(250, 31);
            txtSearch.TabIndex = 2;
            txtSearch.TextChanged += TxtSearch_TextChanged;
            // 
            // lblSearch
            // 
            lblSearch.Location = new Point(10, 45);
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new Size(80, 25);
            lblSearch.TabIndex = 1;
            lblSearch.Text = "🔍 Search:";
            lblSearch.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblStats
            // 
            lblStats.Font = new Font("Segoe UI", 6.666667F, FontStyle.Bold);
            lblStats.ForeColor = Color.FromArgb(0, 120, 215);
            lblStats.Location = new Point(10, 10);
            lblStats.Name = "lblStats";
            lblStats.Size = new Size(600, 25);
            lblStats.TabIndex = 0;
            lblStats.Text = "📊 Total Corrections: 0 | OK: 0 | NG: 0";
            // 
            // panelPreview
            // 
            panelPreview.BackColor = Color.FromArgb(250, 250, 250);
            panelPreview.Controls.Add(picturePreview);
            panelPreview.Controls.Add(panelEdit);
            panelPreview.Controls.Add(lblImageInfo);
            panelPreview.Dock = DockStyle.Fill;
            panelPreview.Location = new Point(0, 0);
            panelPreview.Name = "panelPreview";
            panelPreview.Padding = new Padding(10, 10, 10, 10);
            panelPreview.Size = new Size(1400, 396);
            panelPreview.TabIndex = 0;
            // 
            // picturePreview
            // 
            picturePreview.BackColor = Color.White;
            picturePreview.BorderStyle = BorderStyle.FixedSingle;
            picturePreview.Dock = DockStyle.Fill;
            picturePreview.Location = new Point(10, 40);
            picturePreview.Name = "picturePreview";
            picturePreview.Size = new Size(1380, 296);
            picturePreview.SizeMode = PictureBoxSizeMode.Zoom;
            picturePreview.TabIndex = 2;
            picturePreview.TabStop = false;
            // 
            // panelEdit
            // 
            panelEdit.BackColor = Color.FromArgb(240, 240, 240);
            panelEdit.Controls.Add(btnDelete);
            panelEdit.Controls.Add(btnSave);
            panelEdit.Controls.Add(cboEditLabel);
            panelEdit.Controls.Add(lblEdit);
            panelEdit.Dock = DockStyle.Bottom;
            panelEdit.Location = new Point(10, 336);
            panelEdit.Name = "panelEdit";
            panelEdit.Padding = new Padding(10, 10, 10, 10);
            panelEdit.Size = new Size(1380, 50);
            panelEdit.TabIndex = 1;
            // 
            // btnDelete
            // 
            btnDelete.BackColor = Color.FromArgb(220, 53, 69);
            btnDelete.Enabled = false;
            btnDelete.FlatStyle = FlatStyle.Flat;
            btnDelete.ForeColor = Color.White;
            btnDelete.Location = new Point(355, 8);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(100, 30);
            btnDelete.TabIndex = 3;
            btnDelete.Text = "🗑️ Delete";
            btnDelete.UseVisualStyleBackColor = false;
            btnDelete.Click += BtnDelete_Click;
            // 
            // btnSave
            // 
            btnSave.BackColor = Color.FromArgb(40, 167, 69);
            btnSave.Enabled = false;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.ForeColor = Color.White;
            btnSave.Location = new Point(225, 8);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(120, 30);
            btnSave.TabIndex = 2;
            btnSave.Text = "💾 Save Change";
            btnSave.UseVisualStyleBackColor = false;
            btnSave.Click += BtnSave_Click;
            // 
            // cboEditLabel
            // 
            cboEditLabel.DropDownStyle = ComboBoxStyle.DropDownList;
            cboEditLabel.Enabled = false;
            cboEditLabel.FormattingEnabled = true;
            cboEditLabel.Items.AddRange(new object[] { "OK", "NG" });
            cboEditLabel.Location = new Point(115, 10);
            cboEditLabel.Name = "cboEditLabel";
            cboEditLabel.Size = new Size(100, 23);
            cboEditLabel.TabIndex = 1;
            // 
            // lblEdit
            // 
            lblEdit.Location = new Point(10, 12);
            lblEdit.Name = "lblEdit";
            lblEdit.Size = new Size(100, 25);
            lblEdit.TabIndex = 0;
            lblEdit.Text = "Change Label:";
            lblEdit.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblImageInfo
            // 
            lblImageInfo.Dock = DockStyle.Top;
            lblImageInfo.Font = new Font("Segoe UI", 6F, FontStyle.Bold);
            lblImageInfo.Location = new Point(10, 10);
            lblImageInfo.Name = "lblImageInfo";
            lblImageInfo.Size = new Size(1380, 30);
            lblImageInfo.TabIndex = 0;
            lblImageInfo.Text = "Select a correction to preview";
            lblImageInfo.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // CorrectionsManagerForm
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1400, 780);
            Controls.Add(splitContainer);
            MinimumSize = new Size(998, 594);
            Name = "CorrectionsManagerForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Corrections Manager";
            FormClosing += CorrectionsManagerForm_FormClosing;
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridCorrections).EndInit();
            panelControls.ResumeLayout(false);
            panelControls.PerformLayout();
            panelPreview.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)picturePreview).EndInit();
            panelEdit.ResumeLayout(false);
            ResumeLayout(false);

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