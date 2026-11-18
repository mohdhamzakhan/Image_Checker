using Image_Checker.Services;
using System.ComponentModel;

namespace Image_Checker.WinForm
{
    public class MonitorProfilesForm : Form
    {
        private const int OuterMargin = 16, InnerPad = 8, ControlHeight = 30;

        private readonly BindingList<MonitorProfile> _profiles;
        private readonly DataGridView grdProfiles = new();
        private readonly Button btnAdd = new(), btnEdit = new(), btnRemove = new(), btnOk = new(), btnCancel = new();

        public IReadOnlyList<MonitorProfile> Profiles => _profiles.ToList();

        public MonitorProfilesForm(IEnumerable<MonitorProfile> existing)
        {
            _profiles = new BindingList<MonitorProfile>(existing.ToList());
            BuildLayout();
            grdProfiles.DataSource = _profiles;
        }

        // ── Layout ────────────────────────────────────────────────────
        private void BuildLayout()
        {
            Text = "Monitor Folders";
            Size = new Size(900, 480);
            MinimumSize = new Size(700, 360);
            StartPosition = FormStartPosition.CenterParent;

            var tblMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(OuterMargin)
            };
            tblMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Group: profiles grid + side buttons
            var grpProfiles = new GroupBox { Text = "Folders and their models", Dock = DockStyle.Fill, Padding = new Padding(InnerPad) };
            var tblGroup = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            tblGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tblGroup.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            ConfigureGrid();

            var pnlSide = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            StyleButton(btnAdd, "Add…", (_, _) => AddProfile());
            StyleButton(btnEdit, "Edit…", (_, _) => EditSelected());
            StyleButton(btnRemove, "Remove", (_, _) => RemoveSelected());
            pnlSide.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnRemove });

            tblGroup.Controls.Add(grdProfiles, 0, 0);
            tblGroup.Controls.Add(pnlSide, 1, 0);
            grpProfiles.Controls.Add(tblGroup);

            // Footer: right-aligned buttons
            var pnlFooter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(0, InnerPad, 0, 0)
            };
            StyleButton(btnCancel, "Cancel", (_, _) => DialogResult = DialogResult.Cancel);
            StyleButton(btnOk, "Save", (_, _) => SaveAndClose());
            pnlFooter.Controls.Add(btnCancel);
            pnlFooter.Controls.Add(btnOk);

            tblMain.Controls.Add(grpProfiles, 0, 0);
            tblMain.Controls.Add(pnlFooter, 0, 1);
            Controls.Add(tblMain);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void ConfigureGrid()
        {
            grdProfiles.Dock = DockStyle.Fill;
            grdProfiles.AutoGenerateColumns = false;
            grdProfiles.AllowUserToAddRows = false;
            grdProfiles.AllowUserToDeleteRows = false;
            grdProfiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grdProfiles.MultiSelect = false;
            grdProfiles.RowHeadersVisible = false;
            grdProfiles.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            grdProfiles.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(MonitorProfile.Enabled), HeaderText = "On", FillWeight = 8 });
            grdProfiles.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MonitorProfile.Name), HeaderText = "Name", FillWeight = 20 });
            grdProfiles.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MonitorProfile.FolderPath), HeaderText = "Folder", FillWeight = 36, ReadOnly = true });
            grdProfiles.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MonitorProfile.ModelPath), HeaderText = "Model (.zip)", FillWeight = 36, ReadOnly = true });
            grdProfiles.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(MonitorProfile.LayoutText),
                HeaderText = "Date folders",
                FillWeight = 18,
                ReadOnly = true
            });
            grdProfiles.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditSelected(); };
        }

        private static void StyleButton(Button b, string text, EventHandler onClick)
        {
            b.Text = text;
            b.Size = new Size(100, ControlHeight);
            b.Margin = new Padding(0, 0, InnerPad, InnerPad);
            b.Click += onClick;
        }

        // ── Actions ───────────────────────────────────────────────────
        private void AddProfile()
        {
            using var dlg = new MonitorProfileEditForm(new MonitorProfile());
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _profiles.Add(dlg.Result);
        }

        private void EditSelected()
        {
            if (grdProfiles.CurrentRow?.DataBoundItem is not MonitorProfile p) return;
            using var dlg = new MonitorProfileEditForm(p);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var r = dlg.Result;
            p.Name = r.Name;
            p.FolderPath = r.FolderPath;
            p.ModelPath = r.ModelPath;
            p.UseDateFolders = r.UseDateFolders;
            p.DateFormat = r.DateFormat;
            grdProfiles.Refresh();
        }

        private void RemoveSelected()
        {
            if (grdProfiles.CurrentRow?.DataBoundItem is MonitorProfile p)
                _profiles.Remove(p);
        }

        private bool PickFolder(MonitorProfile p)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select the folder to monitor (it must contain class sub-folders, e.g. OK / NG)",
                SelectedPath = Directory.Exists(p.FolderPath) ? p.FolderPath : "",
                UseDescriptionForTitle = true
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return false;
            p.FolderPath = dlg.SelectedPath;
            return true;
        }

        private bool PickModel(MonitorProfile p)
        {
            using var dlg = new OpenFileDialog
            {
                Title = $"Select the model for '{new DirectoryInfo(p.FolderPath).Name}'",
                Filter = "Model files (*.zip;*.bin;*.onnx)|*.zip;*.bin;*.onnx|All files (*.*)|*.*",
                FileName = p.ModelPath
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return false;
            p.ModelPath = dlg.FileName;
            return true;
        }

        private void SaveAndClose()
        {
            var duplicate = _profiles.GroupBy(p => p.FolderPath, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                MessageBox.Show(this, $"The folder is listed twice:\n{duplicate.Key}", "Duplicate folder",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>Fallback used right before monitoring starts.</summary>
    public static class MonitorPathResolver
    {
        /// <summary>
        /// Makes sure folder and model exist; asks the user to pick them otherwise.
        /// Returns false if the user cancelled (that profile should be skipped).
        /// </summary>
        public static bool EnsureAvailable(IWin32Window owner, MonitorProfile p)
        {
            if (!Directory.Exists(p.FolderPath))
            {
                using var dlg = new FolderBrowserDialog
                {
                    Description = $"Folder for '{p.Name}' was not found:\n{p.FolderPath}\n\nSelect the base folder again.",
                    UseDescriptionForTitle = true
                };
                if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                p.FolderPath = dlg.SelectedPath;
            }

            if (!File.Exists(p.ModelPath))
            {
                using var dlg = new OpenFileDialog
                {
                    Title = $"Model for '{p.Name}' was not found – select it",
                    Filter = "Model files (*.zip;*.bin;*.onnx)|*.zip;*.bin;*.onnx|All files (*.*)|*.*"
                };
                if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
                p.ModelPath = dlg.FileName;
            }
            return true;
        }
    }
}