using Image_Checker.Services;

namespace Image_Checker.WinForm
{
    public class MonitorProfileEditForm : Form
    {
        private const int OuterMargin = 16, InnerPad = 8, ControlHeight = 30;

        private readonly MonitorProfile _work;
        private readonly TextBox txtName = new(), txtFolder = new(), txtModel = new();
        private readonly CheckBox chkDate = new();
        private readonly ComboBox cmbFormat = new();
        private readonly Label lblPreview = new();
        private readonly Button btnBrowseFolder = new(), btnBrowseModel = new(), btnOk = new(), btnCancel = new();

        public MonitorProfile Result => _work;

        public MonitorProfileEditForm(MonitorProfile source)
        {
            _work = source.Clone();
            BuildLayout();
            LoadValues();
            UpdatePreview();
        }

        private void BuildLayout()
        {
            Text = "Monitor source";
            Size = new Size(760, 440);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var tblMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(OuterMargin)
            };
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ── Group 1: source ──────────────────────────────────────
            var grpSource = new GroupBox { Text = "Source", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(InnerPad) };
            var tblSource = NewTable(3, 3);
            tblSource.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            tblSource.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));
            tblSource.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            AddRow(tblSource, 0, "Name", txtName, null);
            AddRow(tblSource, 1, "Base folder", txtFolder, btnBrowseFolder);
            AddRow(tblSource, 2, "Model", txtModel, btnBrowseModel);
            grpSource.Controls.Add(tblSource);

            btnBrowseFolder.Text = "Browse…";
            btnBrowseModel.Text = "Browse…";
            btnBrowseFolder.Click += (_, _) => BrowseFolder();
            btnBrowseModel.Click += (_, _) => BrowseModel();
            txtFolder.TextChanged += (_, _) => UpdatePreview();

            // ── Group 2: date folders ────────────────────────────────
            var grpDate = new GroupBox { Text = "Folder layout", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(InnerPad) };
            var tblDate = NewTable(3, 2);
            tblDate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            tblDate.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));

            chkDate.Text = "Images are stored in date folders (changes every day at 00:00)";
            chkDate.AutoSize = true;
            chkDate.Margin = new Padding(0, 0, 0, InnerPad);
            chkDate.CheckedChanged += (_, _) => { cmbFormat.Enabled = chkDate.Checked; UpdatePreview(); };
            tblDate.Controls.Add(chkDate, 0, 0);
            tblDate.SetColumnSpan(chkDate, 2);

            cmbFormat.DropDownStyle = ComboBoxStyle.DropDown;     // presets, but the user can type any format
            cmbFormat.Items.AddRange(DateFolderFormat.Presets);
            cmbFormat.Dock = DockStyle.Fill;
            cmbFormat.TextChanged += (_, _) => UpdatePreview();
            AddRow(tblDate, 1, "Date format", cmbFormat, null);

            lblPreview.AutoSize = true;
            lblPreview.MaximumSize = new Size(560, 0);
            lblPreview.Margin = new Padding(0, InnerPad, 0, 0);
            tblDate.Controls.Add(new Label { Text = "Watches", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, InnerPad, 0, 0) }, 0, 2);
            tblDate.Controls.Add(lblPreview, 1, 2);
            grpDate.Controls.Add(tblDate);

            // ── Footer ───────────────────────────────────────────────
            var pnlFooter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, InnerPad, 0, 0)
            };
            StyleButton(btnCancel, "Cancel", (_, _) => DialogResult = DialogResult.Cancel);
            StyleButton(btnOk, "OK", (_, _) => Accept());
            pnlFooter.Controls.Add(btnCancel);
            pnlFooter.Controls.Add(btnOk);

            tblMain.Controls.Add(grpSource, 0, 0);
            tblMain.Controls.Add(grpDate, 0, 1);
            tblMain.Controls.Add(pnlFooter, 0, 2);
            Controls.Add(tblMain);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private static TableLayoutPanel NewTable(int rows, int cols) => new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = cols,
            RowCount = rows
        };

        private static void AddRow(TableLayoutPanel t, int row, string label, Control input, Button? browse)
        {
            t.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, InnerPad, InnerPad)
            }, 0, row);

            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 0, InnerPad, InnerPad);
            t.Controls.Add(input, 1, row);

            if (browse != null)
            {
                browse.Size = new Size(90, ControlHeight);
                browse.Margin = new Padding(0, 0, 0, InnerPad);
                t.Controls.Add(browse, 2, row);
            }
        }

        private static void StyleButton(Button b, string text, EventHandler onClick)
        {
            b.Text = text;
            b.Size = new Size(100, ControlHeight);
            b.Margin = new Padding(InnerPad, 0, 0, 0);
            b.Click += onClick;
        }

        // ── Values ────────────────────────────────────────────────────
        private void LoadValues()
        {
            txtName.Text = _work.Name;
            txtFolder.Text = _work.FolderPath;
            txtModel.Text = _work.ModelPath;
            chkDate.Checked = _work.UseDateFolders;
            cmbFormat.Text = string.IsNullOrWhiteSpace(_work.DateFormat) ? @"yy\MM\dd" : _work.DateFormat;
            cmbFormat.Enabled = chkDate.Checked;
        }

        private void UpdatePreview()
        {
            string folder = txtFolder.Text.Trim();
            string baseText = string.IsNullOrEmpty(folder) ? "<base folder>" : folder;

            if (!chkDate.Checked)
            {
                lblPreview.ForeColor = SystemColors.ControlText;
                lblPreview.Text = $"{baseText}\\OK, {baseText}\\NG … (this folder is scanned directly, no date change)";
                return;
            }

            if (!DateFolderFormat.TryValidate(cmbFormat.Text, out string error))
            {
                lblPreview.ForeColor = Color.Firebrick;
                lblPreview.Text = error;
                return;
            }

            lblPreview.ForeColor = SystemColors.ControlText;
            lblPreview.Text = "Today:     " + Path.Combine(baseText, DateFolderFormat.Build(cmbFormat.Text, DateTime.Today)) +
                              "\nTomorrow: " + Path.Combine(baseText, DateFolderFormat.Build(cmbFormat.Text, DateTime.Today.AddDays(1)));
        }

        // ── Actions ───────────────────────────────────────────────────
        private void BrowseFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = chkDate.Checked
                    ? "Select the BASE folder (the one that contains the date folders)"
                    : "Select the folder to scan (it must contain class sub-folders such as OK / NG)",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(txtFolder.Text) ? txtFolder.Text : ""
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            txtFolder.Text = dlg.SelectedPath;
            if (string.IsNullOrWhiteSpace(txtName.Text))
                txtName.Text = new DirectoryInfo(dlg.SelectedPath).Name;
        }

        private void BrowseModel()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select the model",
                Filter = "Model files (*.zip;*.bin;*.onnx)|*.zip;*.bin;*.onnx|All files (*.*)|*.*",
                FileName = txtModel.Text
            };
            if (dlg.ShowDialog(this) == DialogResult.OK) txtModel.Text = dlg.FileName;
        }

        private void Accept()
        {
            if (string.IsNullOrWhiteSpace(txtFolder.Text)) { Warn("Select the folder."); return; }
            if (string.IsNullOrWhiteSpace(txtModel.Text)) { Warn("Select the model."); return; }
            if (chkDate.Checked && !DateFolderFormat.TryValidate(cmbFormat.Text, out string error)) { Warn(error); return; }

            _work.Name = string.IsNullOrWhiteSpace(txtName.Text)
                ? new DirectoryInfo(txtFolder.Text.Trim()).Name
                : txtName.Text.Trim();
            _work.FolderPath = txtFolder.Text.Trim();
            _work.ModelPath = txtModel.Text.Trim();
            _work.UseDateFolders = chkDate.Checked;
            _work.DateFormat = cmbFormat.Text.Trim();
            DialogResult = DialogResult.OK;
        }

        private void Warn(string text) =>
            MessageBox.Show(this, text, "Monitor source", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}