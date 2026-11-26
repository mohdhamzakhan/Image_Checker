using Image_Checker.Services;

namespace Image_Checker.WinForm
{
    /// <summary>
    /// Finds duplicate / near-identical training images, shows them side by side, and moves the checked ones
    /// to "&lt;dataset&gt;_duplicates" (a backup next to the dataset, not a permanent delete).
    /// </summary>
    public class DuplicateCleanerForm : Form
    {
        private const int Outer = 16, Inner = 8, ControlHeight = 30;
        private static readonly int[] Distances = { 0, 10, 24 };

        private readonly TextBox txtFolder = new();
        private readonly ComboBox cmbMatch = new();
        private readonly DataGridView grid = new();
        private readonly PictureBox picKeep = new(), picDup = new();
        private readonly Label lblStatus = new();
        private readonly ProgressBar progress = new();
        private Button btnBrowse = null!, btnScan = null!, btnCancel = null!, btnMove = null!,
                       btnClose = null!, btnAll = null!, btnNone = null!, btnConflicts = null!;

        private DuplicateReport? _report;
        private CancellationTokenSource? _cts;

        public DuplicateCleanerForm(string? startFolder = null)
        {
            Text = "Remove duplicate training images";
            Font = new Font("Segoe UI", 9f);
            MinimumSize = new Size(1000, 640);
            Size = new Size(1200, 720);
            StartPosition = FormStartPosition.CenterParent;

            BuildLayout();
            cmbMatch.SelectedIndex = 1;
            if (!string.IsNullOrEmpty(startFolder)) txtFolder.Text = startFolder;
        }

        // ══════════════════════════════════════════════════════════════
        //  LAYOUT
        // ══════════════════════════════════════════════════════════════

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(Outer)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildContent(), 0, 1);
            root.Controls.Add(BuildFooter(), 0, 2);
            Controls.Add(root);
        }

        private Control BuildHeader()
        {
            var grp = new GroupBox
            {
                Text = "1. Dataset (the folder that contains the OK / NG class folders)",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(Inner),
                Margin = new Padding(0, 0, 0, Inner)
            };

            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 6, RowCount = 1 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtFolder.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtFolder.Margin = new Padding(0, 0, Inner, 0);

            cmbMatch.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMatch.Items.AddRange(new object[]
            {
                "Identical pictures only",
                "Almost identical (recommended)",
                "Very similar (check carefully)"
            });
            cmbMatch.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbMatch.Margin = new Padding(0, 0, Inner, 0);

            btnBrowse = MakeButton("Browse...", 100, (_, _) => BrowseFolder());
            btnScan = MakeButton("Scan", 90, BtnScan_Click, true);

            t.Controls.Add(Lbl("Folder"), 0, 0);
            t.Controls.Add(txtFolder, 1, 0);
            t.Controls.Add(btnBrowse, 2, 0);
            t.Controls.Add(Lbl("Match"), 3, 0);
            t.Controls.Add(cmbMatch, 4, 0);
            t.Controls.Add(btnScan, 5, 0);
            grp.Controls.Add(t);
            return grp;
        }

        private Control BuildContent()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            // list
            var grpList = new GroupBox
            {
                Text = "2. Duplicates found (checked = will be moved to the backup folder)",
                Dock = DockStyle.Fill,
                Padding = new Padding(Inner),
                Margin = new Padding(0, 0, Inner, 0)
            };
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Move", FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Class", FillWeight = 12, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kept image", FillWeight = 34, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Duplicate", FillWeight = 34, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Diff", FillWeight = 10, ReadOnly = true });
            grid.SelectionChanged += (_, _) => ShowSelected();
            grpList.Controls.Add(grid);

            // preview
            var grpPrev = new GroupBox { Text = "Preview", Dock = DockStyle.Fill, Padding = new Padding(Inner) };
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            p.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            foreach (var pb in new[] { picKeep, picDup })
            {
                pb.Dock = DockStyle.Fill;
                pb.SizeMode = PictureBoxSizeMode.Zoom;
                pb.BorderStyle = BorderStyle.FixedSingle;
                pb.BackColor = Color.Gainsboro;
            }
            p.Controls.Add(Lbl("Kept (stays in the dataset)"), 0, 0);
            p.Controls.Add(picKeep, 0, 1);
            p.Controls.Add(Lbl("Duplicate (will be moved)"), 0, 2);
            p.Controls.Add(picDup, 0, 3);
            grpPrev.Controls.Add(p);

            t.Controls.Add(grpList, 0, 0);
            t.Controls.Add(grpPrev, 1, 0);
            return t;
        }

        private Control BuildFooter()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, Inner, 0, 0)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Text = "Select the dataset folder and press Scan.";
            progress.Dock = DockStyle.Fill;
            progress.Margin = new Padding(Inner, 8, Inner, 8);

            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            btnClose = MakeButton("Close", 90, (_, _) => Close());
            btnMove = MakeButton("Move checked", 130, BtnMove_Click, true);
            btnNone = MakeButton("Uncheck all", 100, (_, _) => SetAllChecked(false));
            btnAll = MakeButton("Check all", 90, (_, _) => SetAllChecked(true));
            btnConflicts = MakeButton("Show conflicts", 120, (_, _) => ShowConflicts());
            btnCancel = MakeButton("Cancel scan", 100, (_, _) => _cts?.Cancel());
            btnConflicts.Enabled = false;
            btnCancel.Enabled = false;
            btnMove.Enabled = false;
            flow.Controls.AddRange(new Control[] { btnClose, btnMove, btnNone, btnAll, btnConflicts, btnCancel });

            t.Controls.Add(lblStatus, 0, 0);
            t.Controls.Add(progress, 1, 0);
            t.Controls.Add(flow, 2, 0);
            return t;
        }

        private static Label Lbl(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, Inner, 0)
        };

        private static Button MakeButton(string text, int width, EventHandler click, bool accent = false)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(width, ControlHeight),
                Margin = new Padding(Inner, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = accent ? Color.FromArgb(0, 120, 215) : SystemColors.Control,
                ForeColor = accent ? Color.White : SystemColors.ControlText
            };
            b.FlatAppearance.BorderColor = accent ? Color.FromArgb(0, 120, 215) : SystemColors.ControlDark;
            b.Click += click;
            return b;
        }

        // ══════════════════════════════════════════════════════════════
        //  ACTIONS
        // ══════════════════════════════════════════════════════════════

        private void BrowseFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select the dataset folder (it contains the OK / NG class folders)",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(txtFolder.Text) ? txtFolder.Text : ""
            };
            if (dlg.ShowDialog(this) == DialogResult.OK) txtFolder.Text = dlg.SelectedPath;
        }

        private async void BtnScan_Click(object? sender, EventArgs e)
        {
            string root = txtFolder.Text.Trim();
            if (!Directory.Exists(root))
            {
                Warn("Select the dataset folder (the one that contains the OK / NG class folders).");
                return;
            }

            int maxDistance = Distances[Math.Max(0, cmbMatch.SelectedIndex)];
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            SetBusy(true);
            grid.Rows.Clear();
            ClearPreview();
            lblStatus.Text = "Scanning...";

            try
            {
                var report = await Task.Run(() => DuplicateFinder.Scan(root, maxDistance, (done, total) =>
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            progress.Maximum = Math.Max(1, total);
                            progress.Value = Math.Min(done, progress.Maximum);
                        }));
                    }
                    catch { /* window closed */ }
                }, ct), ct);

                _report = report;
                foreach (var d in report.Duplicates)
                {
                    int idx = grid.Rows.Add(true, d.ClassName, Path.GetFileName(d.Keep),
                                            Path.GetFileName(d.Duplicate), d.Distance);
                    grid.Rows[idx].Tag = d;
                }

                lblStatus.Text = $"{report.Duplicates.Count} duplicate(s) in {report.Scanned} images" +
                                 (report.Unreadable > 0 ? $"  ({report.Unreadable} unreadable)" : "") +
                                 (report.Conflicts.Count > 0
                                     ? $"  -  {report.Conflicts.Count} picture(s) appear in TWO classes: review them!"
                                     : "");
                btnConflicts.Enabled = report.Conflicts.Count > 0;
                btnMove.Enabled = report.Duplicates.Count > 0;
                if (grid.Rows.Count > 0) grid.Rows[0].Selected = true;
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Scan failed: " + ex.Message;
                Warn("Scan failed:\n\n" + ex.Message);
            }
            finally
            {
                SetBusy(false);
                progress.Value = 0;
            }
        }

        private void BtnMove_Click(object? sender, EventArgs e)
        {
            grid.EndEdit();
            string root = txtFolder.Text.Trim();
            var rows = grid.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells[0].Value is true && r.Tag is DuplicateItem).ToList();
            if (rows.Count == 0) { Warn("No duplicate is checked."); return; }

            string target = DuplicateFinder.DuplicatesFolder(root);
            if (MessageBox.Show(this,
                    $"Move {rows.Count} duplicate image(s) to:\n\n{target}\n\n" +
                    "They are not deleted: you can move them back. images.csv is updated.\n\nContinue?",
                    "Move duplicates", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            ClearPreview();   // release the picture files before moving them
            int moved = DuplicateFinder.MoveDuplicates(root, rows.Select(r => (DuplicateItem)r.Tag!).ToList());

            foreach (var r in rows.Where(r => !File.Exists(((DuplicateItem)r.Tag!).Duplicate)).ToList())
                grid.Rows.Remove(r);

            lblStatus.Text = $"Moved {moved} image(s) to {target}";
            btnMove.Enabled = grid.Rows.Count > 0;
            MessageBox.Show(this,
                $"Moved {moved} duplicate image(s).\n\nBackup folder:\n{target}\n\n" +
                "Retrain the model now so it learns from the cleaned dataset.",
                "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowConflicts()
        {
            if (_report == null || _report.Conflicts.Count == 0) return;
            var lines = _report.Conflicts.Take(15).Select(c =>
                $"{c.ClassA}: {Path.GetFileName(c.FileA)}\n   looks the same as  {c.ClassB}: {Path.GetFileName(c.FileB)}");
            MessageBox.Show(this,
                "The same picture is in two different classes, so one of the labels is wrong:\n\n" +
                string.Join("\n\n", lines) +
                (_report.Conflicts.Count > 15 ? $"\n\n... and {_report.Conflicts.Count - 15} more." : "") +
                "\n\nOpen both folders and move or delete the wrongly labelled image yourself.",
                "Conflicting labels", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private void SetAllChecked(bool value)
        {
            foreach (DataGridViewRow r in grid.Rows) r.Cells[0].Value = value;
        }

        private void SetBusy(bool busy)
        {
            btnScan.Enabled = !busy;
            btnBrowse.Enabled = !busy;
            btnCancel.Enabled = busy;
            if (busy) btnMove.Enabled = false;
        }

        private void ShowSelected()
        {
            if (grid.CurrentRow?.Tag is not DuplicateItem item) return;
            SetPicture(picKeep, item.Keep);
            SetPicture(picDup, item.Duplicate);
        }

        private void ClearPreview()
        {
            SetPicture(picKeep, null);
            SetPicture(picDup, null);
        }

        private static void SetPicture(PictureBox pb, string? path)
        {
            var old = pb.Image;
            try { pb.Image = path == null ? null : ExplainImaging.LoadCopy(path); }
            catch { pb.Image = null; }
            old?.Dispose();
        }

        private void Warn(string text) =>
            MessageBox.Show(this, text, "Duplicates", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            ClearPreview();
            base.OnFormClosing(e);
        }
    }
}