using Image_Checker.Services;
using System.Text.Json;

namespace Image_Checker.WinForm
{
    public class ScrewPositionEditorForm : Form
    {
        private const int OuterMargin = 16, InnerPad = 8, ControlHeight = 30, Handle = 10;
        private enum Mode { None, Draw, Move, Resize }

        private readonly Panel pnlCanvas = new();
        private readonly ListBox lstPositions = new();
        private readonly TextBox txtName = new();
        private readonly NumericUpDown numWidth = new(), numHeight = new();
        private readonly Label lblInfo = new();
        private readonly Button btnOpen = new(), btnLoad = new(), btnSave = new(), btnApply = new(),
                                btnDelete = new(), btnClose = new();

        private Bitmap? _img;
        private readonly List<ScrewPosition> _pos = new();
        private int _sel = -1;
        private Mode _mode = Mode.None;
        private Point _startImg;
        private Rectangle _orig;
        private bool _updating;

        public ScrewPositionEditorForm()
        {
            BuildLayout();
            WireEvents();
        }

        // ── Layout ────────────────────────────────────────────────
        private void BuildLayout()
        {
            Text = "Screw Positions";
            Size = new Size(1100, 700);
            MinimumSize = new Size(900, 560);
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

            var tblContent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            tblContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tblContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

            // Canvas
            var grpImage = new GroupBox { Text = "Sample photo (drag to draw a box around each screw)", Dock = DockStyle.Fill, Padding = new Padding(InnerPad) };
            pnlCanvas.Dock = DockStyle.Fill;
            pnlCanvas.BackColor = Color.FromArgb(40, 40, 40);
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                         .SetValue(pnlCanvas, true);
            grpImage.Controls.Add(pnlCanvas);

            // Right side
            var grpPos = new GroupBox { Text = "Positions", Dock = DockStyle.Fill, Padding = new Padding(InnerPad) };
            var tblPos = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
            tblPos.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) tblPos.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lstPositions.Dock = DockStyle.Fill;
            lstPositions.IntegralHeight = false;

            var lblName = new Label { Text = "Name of selected", AutoSize = true, Margin = new Padding(0, InnerPad, 0, 6) };
            txtName.Dock = DockStyle.Top;
            btnDelete.Text = "Delete selected";
            btnDelete.Dock = DockStyle.Top;
            btnDelete.Height = ControlHeight;
            btnDelete.Margin = new Padding(0, InnerPad, 0, 0);

            var tblSize = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, InnerPad, 0, 0) };
            tblSize.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tblSize.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tblSize.Controls.Add(new Label { Text = "Train width", AutoSize = true }, 0, 0);
            tblSize.Controls.Add(new Label { Text = "Train height", AutoSize = true }, 1, 0);
            foreach (var n in new[] { numWidth, numHeight })
            { n.Minimum = 32; n.Maximum = 512; n.Value = 128; n.Dock = DockStyle.Fill; n.Margin = new Padding(0, 6, InnerPad, 0); }
            tblSize.Controls.Add(numWidth, 0, 1);
            tblSize.Controls.Add(numHeight, 1, 1);

            lblInfo.AutoSize = true;
            lblInfo.MaximumSize = new Size(250, 0);
            lblInfo.Margin = new Padding(0, InnerPad, 0, 0);
            lblInfo.Text = "Open a photo, then drag on it.\nClick a box to select / move it.\nDrag its bottom-right square to resize.";

            tblPos.Controls.Add(lstPositions, 0, 0);
            tblPos.Controls.Add(lblName, 0, 1);
            tblPos.Controls.Add(txtName, 0, 2);
            tblPos.Controls.Add(btnDelete, 0, 3);
            tblPos.Controls.Add(tblSize, 0, 4);
            tblPos.Controls.Add(lblInfo, 0, 5);
            grpPos.Controls.Add(tblPos);

            tblContent.Controls.Add(grpImage, 0, 0);
            tblContent.Controls.Add(grpPos, 1, 0);

            // Footer (right aligned)
            var pnlFooter = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(0, InnerPad, 0, 0)
            };
            Style(btnClose, "Close", 90);
            Style(btnApply, "Apply to model…", 130);
            Style(btnSave, "Save positions.json…", 160);
            Style(btnLoad, "Load positions.json…", 160);
            Style(btnOpen, "Open photo…", 110);
            pnlFooter.Controls.AddRange(new Control[] { btnClose, btnApply, btnSave, btnLoad, btnOpen });

            tblMain.Controls.Add(tblContent, 0, 0);
            tblMain.Controls.Add(pnlFooter, 0, 1);
            Controls.Add(tblMain);
        }

        private static void Style(Button b, string text, int width)
        {
            b.Text = text;
            b.Size = new Size(width, ControlHeight);
            b.Margin = new Padding(InnerPad, 0, 0, 0);
        }

        private void WireEvents()
        {
            pnlCanvas.Paint += Canvas_Paint;
            pnlCanvas.MouseDown += Canvas_MouseDown;
            pnlCanvas.MouseMove += Canvas_MouseMove;
            pnlCanvas.MouseUp += Canvas_MouseUp;
            pnlCanvas.Resize += (_, _) => pnlCanvas.Invalidate();

            lstPositions.SelectedIndexChanged += (_, _) =>
            {
                if (_updating) return;
                _sel = lstPositions.SelectedIndex;
                SyncName();
                pnlCanvas.Invalidate();
            };
            txtName.TextChanged += (_, _) =>
            {
                if (_updating || _sel < 0) return;
                _pos[_sel].Name = txtName.Text.Trim();
                RefreshList();
                pnlCanvas.Invalidate();
            };
            btnDelete.Click += (_, _) =>
            {
                if (_sel < 0) return;
                _pos.RemoveAt(_sel);
                _sel = -1;
                RefreshList();
                pnlCanvas.Invalidate();
            };
            btnOpen.Click += (_, _) => OpenPhoto();
            btnLoad.Click += (_, _) => LoadJson();
            btnSave.Click += (_, _) => SaveJson();
            btnApply.Click += (_, _) => ApplyToModel();
            btnClose.Click += (_, _) => Close();
            FormClosed += (_, _) => _img?.Dispose();
        }

        // ── Coordinates ───────────────────────────────────────────
        private float Scale => _img == null ? 1f :
            Math.Min((float)pnlCanvas.ClientSize.Width / _img.Width, (float)pnlCanvas.ClientSize.Height / _img.Height);

        private PointF Origin => _img == null ? PointF.Empty :
            new PointF((pnlCanvas.ClientSize.Width - _img.Width * Scale) / 2f,
                       (pnlCanvas.ClientSize.Height - _img.Height * Scale) / 2f);

        private Point ToImage(Point p)
        {
            if (_img == null) return Point.Empty;
            float s = Scale; var o = Origin;
            int x = (int)Math.Round((p.X - o.X) / s), y = (int)Math.Round((p.Y - o.Y) / s);
            return new Point(Math.Max(0, Math.Min(_img.Width, x)), Math.Max(0, Math.Min(_img.Height, y)));
        }

        private RectangleF ToScreen(Rectangle r)
        {
            float s = Scale; var o = Origin;
            return new RectangleF(o.X + r.X * s, o.Y + r.Y * s, r.Width * s, r.Height * s);
        }

        // ── Mouse ─────────────────────────────────────────────────
        private void Canvas_MouseDown(object? sender, MouseEventArgs e)
        {
            if (_img == null || e.Button != MouseButtons.Left) return;
            var ip = ToImage(e.Location);

            // resize handle of the selected box
            if (_sel >= 0)
            {
                var sr = ToScreen(_pos[_sel].Rect);
                var h = new RectangleF(sr.Right - Handle, sr.Bottom - Handle, Handle * 2, Handle * 2);
                if (h.Contains(e.Location)) { _mode = Mode.Resize; _orig = _pos[_sel].Rect; return; }
            }

            // select / move an existing box (topmost first)
            for (int i = _pos.Count - 1; i >= 0; i--)
            {
                if (_pos[i].Rect.Contains(ip))
                {
                    _sel = i; _mode = Mode.Move; _startImg = ip; _orig = _pos[i].Rect;
                    RefreshList(); pnlCanvas.Invalidate();
                    return;
                }
            }

            // draw a new box
            var p = new ScrewPosition { Name = NextName(), X = ip.X, Y = ip.Y, W = 0, H = 0 };
            _pos.Add(p);
            _sel = _pos.Count - 1;
            _mode = Mode.Draw; _startImg = ip;
            RefreshList();
        }

        private void Canvas_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_img == null) return;
            var ip = ToImage(e.Location);

            if (_mode == Mode.Draw && _sel >= 0)
            {
                var p = _pos[_sel];
                p.X = Math.Min(_startImg.X, ip.X); p.Y = Math.Min(_startImg.Y, ip.Y);
                p.W = Math.Abs(ip.X - _startImg.X); p.H = Math.Abs(ip.Y - _startImg.Y);
            }
            else if (_mode == Mode.Move && _sel >= 0)
            {
                var p = _pos[_sel];
                p.X = Math.Max(0, Math.Min(_img.Width - _orig.Width, _orig.X + ip.X - _startImg.X));
                p.Y = Math.Max(0, Math.Min(_img.Height - _orig.Height, _orig.Y + ip.Y - _startImg.Y));
            }
            else if (_mode == Mode.Resize && _sel >= 0)
            {
                var p = _pos[_sel];
                p.W = Math.Max(10, ip.X - p.X);
                p.H = Math.Max(10, ip.Y - p.Y);
            }
            else
            {
                // cursor hint
                bool onHandle = false;
                if (_sel >= 0)
                {
                    var sr = ToScreen(_pos[_sel].Rect);
                    onHandle = new RectangleF(sr.Right - Handle, sr.Bottom - Handle, Handle * 2, Handle * 2).Contains(e.Location);
                }
                pnlCanvas.Cursor = onHandle ? Cursors.SizeNWSE
                                 : _pos.Any(q => q.Rect.Contains(ip)) ? Cursors.SizeAll : Cursors.Cross;
                return;
            }
            pnlCanvas.Invalidate();
        }

        private void Canvas_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_mode == Mode.Draw && _sel >= 0 && (_pos[_sel].W < 10 || _pos[_sel].H < 10))
            {
                _pos.RemoveAt(_sel);   // a simple click, not a box
                _sel = -1;
            }
            _mode = Mode.None;
            RefreshList();
            pnlCanvas.Invalidate();
        }

        // ── Painting ──────────────────────────────────────────────
        private void Canvas_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            if (_img == null)
            {
                TextRenderer.DrawText(g, "Click “Open photo…”", Font, pnlCanvas.ClientRectangle, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            float s = Scale; var o = Origin;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(_img, o.X, o.Y, _img.Width * s, _img.Height * s);

            for (int i = 0; i < _pos.Count; i++)
            {
                var r = ToScreen(_pos[i].Rect);
                bool sel = i == _sel;
                using var pen = new Pen(sel ? Color.Red : Color.Lime, 2f);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);

                using var br = new SolidBrush(sel ? Color.Red : Color.Lime);
                g.FillRectangle(br, r.X, r.Y - 18, Math.Max(28, _pos[i].Name.Length * 9 + 8), 18);
                g.DrawString(_pos[i].Name, Font, Brushes.Black, r.X + 3, r.Y - 17);

                if (sel) g.FillRectangle(Brushes.White, r.Right - Handle / 2f, r.Bottom - Handle / 2f, Handle, Handle);
            }
        }

        // ── Helpers ───────────────────────────────────────────────
        private string NextName()
        {
            int n = 1;
            while (_pos.Any(p => p.Name.Equals("S" + n, StringComparison.OrdinalIgnoreCase))) n++;
            return "S" + n;
        }

        private void RefreshList()
        {
            _updating = true;
            lstPositions.Items.Clear();
            foreach (var p in _pos) lstPositions.Items.Add($"{p.Name}   ({p.X}, {p.Y}, {p.W}×{p.H})");
            if (_sel >= 0 && _sel < lstPositions.Items.Count) lstPositions.SelectedIndex = _sel;
            _updating = false;
            SyncName();
        }

        private void SyncName()
        {
            _updating = true;
            txtName.Text = _sel >= 0 && _sel < _pos.Count ? _pos[_sel].Name : "";
            txtName.Enabled = _sel >= 0;
            _updating = false;
        }

        private bool Validate(out string error)
        {
            error = "";
            if (_pos.Count == 0) { error = "Draw at least one box."; return false; }
            foreach (var p in _pos)
            {
                if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Contains('_') || p.Name.Any(char.IsWhiteSpace))
                { error = $"Invalid name '{p.Name}'. Use letters/digits only (no underscore or spaces), e.g. S1."; return false; }
                if (p.W < 10 || p.H < 10) { error = $"Box {p.Name} is too small."; return false; }
            }
            var dup = _pos.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) { error = $"Name '{dup.Key}' is used twice."; return false; }
            return true;
        }

        private ScrewDatasetBuilder.Config BuildConfig() => new()
        {
            ImageWidth = (int)numWidth.Value,
            ImageHeight = (int)numHeight.Value,
            Positions = _pos.Select(p => new ScrewPosition { Name = p.Name, X = p.X, Y = p.Y, W = p.W, H = p.H }).ToList()
        };

        // ── Files ─────────────────────────────────────────────────
        private void OpenPhoto()
        {
            using var dlg = new OpenFileDialog { Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp", Title = "Select a sample photo" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                using var fs = new FileStream(dlg.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var tmp = Image.FromStream(fs);
                _img?.Dispose();
                _img = new Bitmap(tmp);
                Text = $"Screw Positions - {Path.GetFileName(dlg.FileName)} ({_img.Width}×{_img.Height})";
                pnlCanvas.Invalidate();
            }
            catch (Exception ex) { Warn("Cannot open the photo: " + ex.Message); }
        }

        private void LoadJson()
        {
            using var dlg = new OpenFileDialog { Filter = "positions.json|*.json", Title = "Open positions.json" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var cfg = JsonSerializer.Deserialize<ScrewDatasetBuilder.Config>(File.ReadAllText(dlg.FileName),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cfg == null) return;
                _pos.Clear(); _pos.AddRange(cfg.Positions);
                numWidth.Value = Math.Max(32, Math.Min(512, cfg.ImageWidth));
                numHeight.Value = Math.Max(32, Math.Min(512, cfg.ImageHeight));
                _sel = -1;
                RefreshList(); pnlCanvas.Invalidate();
            }
            catch (Exception ex) { Warn("Cannot read the file: " + ex.Message); }
        }

        private void SaveJson()
        {
            if (!Validate(out string err)) { Warn(err); return; }
            using var dlg = new SaveFileDialog
            {
                Filter = "positions.json|positions.json",
                FileName = "positions.json",
                Title = "Save into the ScrewRaw folder"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            File.WriteAllText(dlg.FileName,
                JsonSerializer.Serialize(BuildConfig(), new JsonSerializerOptions { WriteIndented = true }));
            MessageBox.Show(this, $"Saved {_pos.Count} position(s):\n{dlg.FileName}", "Saved",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // For a model that already exists: writes Positions into its .json
        private void ApplyToModel()
        {
            if (!Validate(out string err)) { Warn(err); return; }
            using var dlg = new OpenFileDialog { Filter = "Model (*.zip;*.bin;*.onnx)|*.zip;*.bin;*.onnx", Title = "Select the model to update" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            ScrewDatasetBuilder.PatchModelJson(dlg.FileName, BuildConfig());
            MessageBox.Show(this, "Positions written to the model's .json.", "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Warn(string text) =>
            MessageBox.Show(this, text, "Screw Positions", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}