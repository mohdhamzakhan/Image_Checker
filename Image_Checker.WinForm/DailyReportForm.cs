using System.Drawing.Imaging;
using Image_Checker.Services;

namespace Image_Checker.WinForm
{
    /// <summary>
    /// Pick a shift date and a shift (A, B, C, all shifts, calendar day) and see how many images were scanned:
    /// OK / NG split, a second pie (shifts, sources or parts) and scans per hour.
    /// Data comes from the monitored date folders or from inspection_log.csv.
    /// </summary>
    public class DailyReportForm : Form
    {
        private const int Outer = 16, Inner = 8, ControlHeight = 30;

        private static readonly Color Green = Color.FromArgb(46, 125, 50);
        private static readonly Color Red = Color.FromArgb(198, 40, 40);
        private static readonly Color Amber = Color.FromArgb(239, 150, 20);
        private static readonly Color Blue = Color.FromArgb(0, 120, 215);
        private static readonly Color Teal = Color.FromArgb(0, 137, 123);
        private static readonly Color Purple = Color.FromArgb(142, 68, 173);
        private static readonly Color Grey = Color.FromArgb(110, 110, 110);
        private static readonly Color[] Palette = { Blue, Purple, Teal, Color.FromArgb(121, 85, 72), Amber, Grey };

        private readonly List<MonitorProfile> _profiles;
        private CancellationTokenSource? _cts;
        private int _loadId;

        private readonly DateTimePicker dtpDay = new();
        private readonly ComboBox cmbShift = new(), cmbSource = new(), cmbProfile = new();
        private readonly TextBox txtLog = new();
        private readonly Panel pnlMonitor = new(), pnlLog = new();
        private readonly Label lblTitle = new(), lblStatus = new(), lblHint = new();
        private readonly Label lblTotal = new(), lblOk = new(), lblNg = new(), lblOther = new(), lblExtra = new();
        private readonly Label capExtra = new();
        private readonly ProgressBar progress = new();
        private readonly PieChartControl pieResult = new(), pieSecond = new();
        private readonly StackedBarChartControl barHours = new();
        private TableLayoutPanel _report = null!;
        private Button btnShow = null!, btnBrowse = null!, btnSave = null!, btnClose = null!;

        public DailyReportForm(IEnumerable<MonitorProfile> profiles, string? inspectionLogPath = null)
        {
            _profiles = profiles?.ToList() ?? new List<MonitorProfile>();

            Text = "Daily scan report";
            Font = new Font("Segoe UI", 9f);
            MinimumSize = new Size(1000, 700);
            Size = new Size(1250, 820);
            StartPosition = FormStartPosition.CenterParent;

            BuildLayout();

            cmbProfile.Items.Add("All sources");
            foreach (var p in _profiles) cmbProfile.Items.Add(p.Name);
            cmbProfile.SelectedIndex = 0;

            if (!string.IsNullOrWhiteSpace(inspectionLogPath)) txtLog.Text = inspectionLogPath;
            cmbSource.SelectedIndex = (_profiles.Count == 0 && txtLog.Text.Length > 0) ? 1 : 0;

            Shown += async (_, _) => { if (_profiles.Count > 0 || File.Exists(txtLog.Text)) await ShowDayAsync(); };
            FormClosing += (_, _) => _cts?.Cancel();
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
            root.Controls.Add(BuildReport(), 0, 1);
            root.Controls.Add(BuildFooter(), 0, 2);
            Controls.Add(root);
        }

        private Control BuildHeader()
        {
            var grp = new GroupBox
            {
                Text = "1. Choose the shift and the data",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(Inner),
                Margin = new Padding(0, 0, 0, Inner)
            };

            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 6, RowCount = 3 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            dtpDay.Format = DateTimePickerFormat.Long;
            dtpDay.MaxDate = DateTime.Today;
            dtpDay.Value = Shifts.ProductionDate(DateTime.Now);
            dtpDay.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            dtpDay.Margin = new Padding(0, 0, Inner * 2, Inner);

            cmbShift.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbShift.Items.AddRange(new object[]
            {
                "All shifts (06:00 - 06:00)",
                "Shift A (06:00 - 14:30)",
                "Shift B (14:30 - 23:00)",
                "Shift C (23:00 - 06:00)",
                "Calendar day (00:00 - 24:00)"
            });
            cmbShift.SelectedIndex = 0;   // order matches the ShiftKind enum
            cmbShift.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbShift.Margin = new Padding(0, 0, Inner, Inner);

            cmbSource.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSource.Items.AddRange(new object[]
            {
                "Monitored date folders (OK / NG folders)",
                "Inspection log (inspection_log.csv)"
            });
            cmbSource.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbSource.Margin = new Padding(0, 0, Inner * 2, Inner);
            cmbSource.SelectedIndexChanged += (_, _) =>
            {
                pnlMonitor.Visible = cmbSource.SelectedIndex == 0;
                pnlLog.Visible = cmbSource.SelectedIndex == 1;
            };

            lblHint.AutoSize = true;
            lblHint.ForeColor = Grey;
            lblHint.Anchor = AnchorStyles.Left;
            lblHint.Text = "A shift belongs to the date it starts on: Shift C of 26 Nov = 26 Nov 23:00 to 27 Nov 06:00.";

            btnShow = MakeButton("Show report", 130, async (_, _) => await ShowDayAsync(), true);
            btnShow.Margin = new Padding(Inner, 0, 0, Inner);

            t.Controls.Add(HeaderLabel("Shift date"), 0, 0);
            t.Controls.Add(dtpDay, 1, 0);
            t.Controls.Add(HeaderLabel("Shift"), 2, 0);
            t.Controls.Add(cmbShift, 3, 0);
            t.Controls.Add(btnShow, 5, 0);
            t.Controls.Add(HeaderLabel("Data"), 0, 1);
            t.Controls.Add(cmbSource, 1, 1);
            t.Controls.Add(lblHint, 2, 1);
            t.SetColumnSpan(lblHint, 4);

            // mode specific row: only one of the two panels is visible
            var pnlMode = new Panel { Dock = DockStyle.Fill, AutoSize = true };

            var tm = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1 };
            tm.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
            cmbProfile.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProfile.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            tm.Controls.Add(HeaderLabel("Source"), 0, 0);
            tm.Controls.Add(cmbProfile, 1, 0);
            pnlMonitor.Dock = DockStyle.Top;
            pnlMonitor.AutoSize = true;
            pnlMonitor.Controls.Add(tm);

            var tl = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 1 };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            txtLog.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            btnBrowse = MakeButton("Browse...", 100, (_, _) => BrowseLog());
            tl.Controls.Add(HeaderLabel("Log file"), 0, 0);
            tl.Controls.Add(txtLog, 1, 0);
            tl.Controls.Add(btnBrowse, 2, 0);
            pnlLog.Dock = DockStyle.Top;
            pnlLog.AutoSize = true;
            pnlLog.Visible = false;
            pnlLog.Controls.Add(tl);

            pnlMode.Controls.Add(pnlLog);
            pnlMode.Controls.Add(pnlMonitor);
            t.Controls.Add(pnlMode, 0, 2);
            t.SetColumnSpan(pnlMode, 6);

            grp.Controls.Add(t);
            return grp;
        }

        /// <summary>Title, summary cards and charts: this panel is what "Save picture" captures.</summary>
        private Control BuildReport()
        {
            _report = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.White,
                Padding = new Padding(Inner)
            };
            _report.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _report.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _report.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            lblTitle.Margin = new Padding(0, 0, 0, Inner);
            lblTitle.Text = "Daily scan report";

            var cards = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5, RowCount = 1 };
            for (int i = 0; i < 5; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            var capTotal = new Label(); var capOk = new Label(); var capNg = new Label(); var capOther = new Label();
            cards.Controls.Add(Card(lblTotal, capTotal, "Scanned", Blue), 0, 0);
            cards.Controls.Add(Card(lblOk, capOk, "OK", Green), 1, 0);
            cards.Controls.Add(Card(lblNg, capNg, "NG", Red), 2, 0);
            cards.Controls.Add(Card(lblOther, capOther, "Other (CHECK, ERROR...)", Amber), 3, 0);
            cards.Controls.Add(Card(lblExtra, capExtra, "-", Grey), 4, 0);

            var charts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            charts.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            charts.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            foreach (Control c in new Control[] { pieResult, pieSecond, barHours })
            {
                c.Dock = DockStyle.Fill;
                c.Margin = new Padding(0, 0, Inner, Inner);
            }
            charts.Controls.Add(pieResult, 0, 0);
            charts.Controls.Add(pieSecond, 1, 0);
            charts.Controls.Add(barHours, 0, 1);
            charts.SetColumnSpan(barHours, 2);

            _report.Controls.Add(lblTitle, 0, 0);
            _report.Controls.Add(cards, 0, 1);
            _report.Controls.Add(charts, 0, 2);
            return _report;
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
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.ForeColor = Grey;
            lblStatus.Text = "Choose a shift and press Show report.";

            progress.Dock = DockStyle.Fill;
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 30;
            progress.Margin = new Padding(Inner, 8, Inner, 8);
            progress.Visible = false;

            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            btnClose = MakeButton("Close", 90, (_, _) => Close());
            btnSave = MakeButton("Save picture...", 130, (_, _) => SavePicture());
            flow.Controls.Add(btnClose);
            flow.Controls.Add(btnSave);

            t.Controls.Add(lblStatus, 0, 0);
            t.Controls.Add(progress, 1, 0);
            t.Controls.Add(flow, 2, 0);
            return t;
        }

        private static Label HeaderLabel(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, Inner, Inner)
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
                BackColor = accent ? Blue : SystemColors.Control,
                ForeColor = accent ? Color.White : SystemColors.ControlText
            };
            b.FlatAppearance.BorderColor = accent ? Blue : SystemColors.ControlDark;
            b.Click += click;
            return b;
        }

        private static Panel Card(Label value, Label caption, string captionText, Color accent)
        {
            var p = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 88,
                BackColor = Color.FromArgb(244, 247, 250),
                Margin = new Padding(0, 0, Inner, Inner),
                Padding = new Padding(12, 8, 12, 8)
            };
            value.AutoSize = true;
            value.Dock = DockStyle.Top;
            value.Font = new Font("Segoe UI", 20f, FontStyle.Bold);
            value.ForeColor = accent;
            value.Text = "-";
            caption.AutoSize = true;
            caption.Dock = DockStyle.Bottom;
            caption.ForeColor = Color.DimGray;
            caption.Text = captionText;
            p.Controls.Add(value);
            p.Controls.Add(caption);
            return p;
        }

        // ══════════════════════════════════════════════════════════════
        //  ACTIONS
        // ══════════════════════════════════════════════════════════════

        private void BrowseLog()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select inspection_log.csv",
                Filter = "Inspection log (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK) txtLog.Text = dlg.FileName;
        }

        private List<MonitorProfile> SelectedProfiles()
        {
            int i = cmbProfile.SelectedIndex;
            if (i <= 0) return _profiles.Where(p => p.Enabled).ToList();
            return i - 1 < _profiles.Count ? new List<MonitorProfile> { _profiles[i - 1] } : new List<MonitorProfile>();
        }

        private async Task ShowDayAsync()
        {
            DateTime day = dtpDay.Value.Date;
            bool folders = cmbSource.SelectedIndex == 0;
            string logPath = txtLog.Text.Trim();
            var profiles = SelectedProfiles();
            var kind = (ShiftKind)Math.Max(0, cmbShift.SelectedIndex);
            var (start, end, title) = Shifts.Window(day, kind);
            bool splitByShift = kind == ShiftKind.All || kind == ShiftKind.CalendarDay;

            if (folders && profiles.Count == 0)
            {
                Warn("No monitored sources are saved yet.\n\nAdd them in Monitor Folders first, " +
                     "or switch the data to the inspection log.");
                return;
            }
            if (!folders && !File.Exists(logPath))
            {
                Warn("Select the inspection_log.csv file (in the product's Results folder).");
                return;
            }

            _cts?.Cancel();                    // a newer request replaces a running one
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            int id = ++_loadId;

            progress.Visible = true;
            lblStatus.ForeColor = Grey;
            lblStatus.Text = "Counting images...";
            lblTitle.Text = $"Loading {title}, {day:dd MMM yyyy} ...";

            void Report(int n)
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (id == _loadId) lblStatus.Text = $"Counting images... {n:N0} found so far";
                    }));
                }
                catch { /* window closed */ }
            }

            try
            {
                DayStats stats = await Task.Run(() =>
                {
                    if (!folders) return DailyStatsReader.FromInspectionLog(logPath, start, end, title);

                    var scans = new List<DayScan>();
                    for (var d = start.Date; d < end && d <= DateTime.Today; d = d.AddDays(1))
                        scans.Add(DailyStatsReader.ScanDay(profiles, d, Report, ct));
                    return DailyStatsReader.Build(scans, start, end, title, splitByShift);
                }, ct);

                if (id != _loadId) return;     // a newer request is already running
                Render(stats, folders);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (id == _loadId)
                {
                    lblStatus.ForeColor = Red;
                    lblStatus.Text = "Could not build the report: " + ex.Message;
                }
            }
            finally
            {
                if (id == _loadId) progress.Visible = false;
            }
        }

        private void Render(DayStats s, bool folders)
        {
            int total = s.Total, ok = s.Count("OK"), ng = s.Count("NG");

            lblTitle.Text = $"{s.WindowTitle}   |   {s.Start:ddd dd MMM yyyy HH:mm}  to  {s.End:ddd dd MMM yyyy HH:mm}" +
                            (folders ? "   (date folders)" : "   (inspection log)");
            lblTotal.Text = total.ToString("N0");
            lblOk.Text = Describe(ok, total);
            lblNg.Text = Describe(ng, total);
            lblOther.Text = s.Other.ToString("N0");
            if (folders)
            {
                capExtra.Text = "Active sources";
                lblExtra.Text = s.Secondary.Count.ToString();
                if (s.SecondaryTitle.Contains("shift")) { capExtra.Text = "Shifts with scans"; }
            }
            else
            {
                capExtra.Text = "Parts finished / overrides";
                lblExtra.Text = $"{s.Secondary.Values.Sum()} / {s.Overrides}";
            }

            int palette = 0;
            Color Pick(string name)
            {
                if (name.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                    (name.StartsWith("Passed", StringComparison.OrdinalIgnoreCase) && !name.Contains("override")))
                    return Green;
                if (name.Equals("NG", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                    return Red;
                if (name.Equals("CHECK", StringComparison.OrdinalIgnoreCase) || name.Contains("override")) return Amber;
                if (name.Equals("ERROR", StringComparison.OrdinalIgnoreCase)) return Grey;
                if (name.StartsWith("Shift ", StringComparison.OrdinalIgnoreCase))
                    return name.EndsWith("A") ? Blue : name.EndsWith("B") ? Teal : Purple;
                return Palette[palette++ % Palette.Length];
            }

            int Rank(string name) => name.Equals("OK", StringComparison.OrdinalIgnoreCase) ? 0
                                   : name.Equals("NG", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

            pieResult.SetData("Scan results",
                s.Results.OrderBy(kv => Rank(kv.Key)).ThenBy(kv => kv.Key)
                         .Select(kv => new Slice(kv.Key, kv.Value, Pick(kv.Key))));

            var second = s.SecondaryTitle.Contains("shift")
                ? s.Secondary.OrderBy(kv => kv.Key)
                : s.Secondary.OrderByDescending(kv => kv.Value);
            pieSecond.SetData(s.SecondaryTitle, second.Select(kv => new Slice(kv.Key, kv.Value, Pick(kv.Key))));

            barHours.SetData("Scans per hour (clock hour)", s.BucketLabels, new[]
            {
                new BarSeries("OK", Green, s.OkPerBucket),
                new BarSeries("NG", Red, s.NgPerBucket),
                new BarSeries("Other", Amber, s.OtherPerBucket)
            });

            lblStatus.ForeColor = Grey;
            lblStatus.Text = (total == 0 ? "No scans found in this period." : $"{total:N0} scans.") +
                             (s.Note.Length > 0 ? "  " + s.Note : "");
        }

        private static string Describe(int count, int total) =>
            total > 0 ? $"{count:N0}  ({count * 100.0 / total:0.#}%)" : count.ToString("N0");

        private void SavePicture()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Save the report as a picture",
                Filter = "PNG image (*.png)|*.png",
                FileName = $"daily_report_{dtpDay.Value:yyyyMMdd}_{cmbShift.Text.Split(' ')[0]}{(cmbShift.Text.StartsWith("Shift") ? cmbShift.Text.Split(' ')[1] : "")}.png"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var bmp = new Bitmap(_report.Width, _report.Height);
                _report.DrawToBitmap(bmp, new Rectangle(Point.Empty, _report.Size));
                bmp.Save(dlg.FileName, ImageFormat.Png);
                lblStatus.Text = "Saved: " + dlg.FileName;
            }
            catch (Exception ex)
            {
                Warn("Could not save the picture:\n\n" + ex.Message);
            }
        }

        private void Warn(string text) =>
            MessageBox.Show(this, text, "Daily report", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}