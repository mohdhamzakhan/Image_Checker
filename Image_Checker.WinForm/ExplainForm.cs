using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Image_Checker.Services;

namespace Image_Checker.WinForm
{
    public sealed class ExplainForm : Form
    {
        private static readonly Color[] Palette =
        {
            Color.FromArgb(255, 90, 0), Color.FromArgb(0, 150, 150),
            Color.FromArgb(150, 70, 200), Color.FromArgb(200, 150, 0)
        };

        // snapshot built on the worker thread, shown on the UI thread
        private sealed class ClassView
        {
            public string Name = "";
            public List<HotSpot> Spots = new List<HotSpot>();
            public List<string[]> Rows = new List<string[]>();
            public string Reading = "";
        }

        private sealed class View
        {
            public string Headline = "";
            public string[] ClassNames = new string[0];
            public int TopIndex;
            public List<KeyValuePair<string, string>> Summary = new List<KeyValuePair<string, string>>();
            public string Scores = "", ImageStats = "", Full = "";
            public Dictionary<int, ClassView> Classes = new Dictionary<int, ClassView>();
        }

        private readonly string _imagePath;
        private readonly string? _folderLabel;
        private readonly Rectangle _roi;
        private readonly string _basePath;
        private readonly Func<IImageScorer> _scorerFactory;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task? _worker;
        private ExplainResult? _result;
        private View? _view;
        private Bitmap? _uiCrop;
        private Bitmap? _spotImage;
        private bool _updatingCombo;
        private Font _bold = null!;

        private readonly Label _headline = new Label();
        private readonly PictureBox _picOriginal = new PictureBox();
        private readonly PictureBox _picCrop = new PictureBox();
        private readonly PictureBox _picSpots = new PictureBox();
        private readonly Label _capSpots = new Label();
        private readonly ComboBox _cmbClass = new ComboBox();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly Label _status = new Label();
        private readonly TabControl _tabs = new TabControl();
        private readonly RichTextBox _rtbSummary = new RichTextBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _txtReading = new TextBox();
        private readonly RichTextBox _rtbScores = new RichTextBox();
        private readonly RichTextBox _rtbStats = new RichTextBox();
        private readonly RichTextBox _rtbFull = new RichTextBox();
        private readonly Button _copy = new Button();
        private readonly Button _close = new Button();

        public ExplainForm(string imagePath, string? folderLabel, Rectangle roi, string basePath,
                           Func<IImageScorer> scorerFactory)
        {
            _imagePath = imagePath;
            _folderLabel = folderLabel;
            _roi = roi;
            _basePath = basePath;
            _scorerFactory = scorerFactory;

            BuildLayout();

            Load += (s, e) => StartWork();
            FormClosing += (s, e) => _cts.Cancel();
            FormClosed += (s, e) => ReleaseImages();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Layout
        // ─────────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            Text = "Why? - " + Path.GetFileName(_imagePath);
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1100, 840);
            MinimumSize = new Size(900, 700);
            Font = new Font("Segoe UI", 9F);
            _bold = new Font(Font, FontStyle.Bold);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(16)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));   // headline
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));    // pictures
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));   // toggle + status
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));    // tabs
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));   // buttons

            _headline.Dock = DockStyle.Fill;
            _headline.Font = new Font("Segoe UI", 12.5F, FontStyle.Bold);
            _headline.TextAlign = ContentAlignment.MiddleLeft;
            _headline.Text = "Analysing...";
            root.Controls.Add(_headline, 0, 0);

            // pictures
            var images = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
            for (int i = 0; i < 3; i++) images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            images.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            images.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            ConfigurePicture(_picOriginal);
            ConfigurePicture(_picCrop);
            ConfigurePicture(_picSpots);
            _picOriginal.DoubleClick += (s, e) => ShowLarge(_picOriginal.Image, "Your photo");
            _picCrop.DoubleClick += (s, e) => ShowLarge(_picCrop.Image, "What the model sees");
            _picSpots.DoubleClick += (s, e) => ShowLarge(_picSpots.Image, "Spots");
            images.Controls.Add(_picOriginal, 0, 0);
            images.Controls.Add(_picCrop, 1, 0);
            images.Controls.Add(_picSpots, 2, 0);
            images.Controls.Add(Caption("1. Your photo\n(yellow box = the part given to the model)"), 0, 1);
            images.Controls.Add(Caption("2. What the model sees\n(the cropped area)"), 1, 1);
            _capSpots.Dock = DockStyle.Fill;
            _capSpots.TextAlign = ContentAlignment.TopCenter;
            _capSpots.ForeColor = Color.DimGray;
            _capSpots.Text = "3. Where the model finds its answer\n(computing...)";
            images.Controls.Add(_capSpots, 2, 1);
            root.Controls.Add(images, 0, 1);

            // toggle + status row
            var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
            var lblShow = new Label
            {
                Text = "Picture 3 shows the spots for:",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 8, 0),
                Font = _bold
            };
            _cmbClass.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbClass.Dock = DockStyle.Fill;
            _cmbClass.Margin = new Padding(0, 4, 8, 4);
            _cmbClass.Enabled = false;
            _cmbClass.SelectedIndexChanged += (s, e) =>
            {
                if (!_updatingCombo && _cmbClass.SelectedIndex >= 0) ShowClass(_cmbClass.SelectedIndex);
            };
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Text = "Starting...";
            _progress.Dock = DockStyle.Fill;
            _progress.Margin = new Padding(8, 10, 0, 10);
            bar.Controls.Add(lblShow, 0, 0);
            bar.Controls.Add(_cmbClass, 1, 0);
            bar.Controls.Add(_status, 2, 0);
            bar.Controls.Add(_progress, 3, 0);
            root.Controls.Add(bar, 0, 2);

            // tabs
            _tabs.Dock = DockStyle.Fill;
            _tabs.TabPages.Add(MakeTab("Summary", ConfigureRtb(_rtbSummary, false)));
            _tabs.TabPages.Add(MakeTab("Spots", BuildSpotsTab()));
            _tabs.TabPages.Add(MakeTab("Scores", ConfigureRtb(_rtbScores, true)));
            _tabs.TabPages.Add(MakeTab("Image vs training", ConfigureRtb(_rtbStats, true)));
            _tabs.TabPages.Add(MakeTab("Full report", ConfigureRtb(_rtbFull, true)));
            root.Controls.Add(_tabs, 0, 3);

            // buttons
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            _close.Text = "Close";
            _close.Size = new Size(110, 30);
            _close.Click += (s, e) => Close();
            _copy.Text = "Copy report";
            _copy.Size = new Size(110, 30);
            _copy.Click += (s, e) => CopyReport();
            buttons.Controls.Add(_close);
            buttons.Controls.Add(_copy);
            root.Controls.Add(buttons, 0, 4);

            Controls.Add(root);
            CancelButton = _close;
        }

        private Control BuildSpotsTab()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));

            t.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.DimGray,
                Text = "Each row is an area of the crop. The tool covers it with a grey patch and runs the model again: " +
                       "the bigger the drop, the more the model relied on that area."
            }, 0, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.ReadOnly = true;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            _grid.Columns.Add("no", "#");
            _grid.Columns.Add("where", "Where in the crop");
            _grid.Columns.Add("now", "Score now");
            _grid.Columns.Add("without", "Score if covered");
            _grid.Columns.Add("change", "Change");
            _grid.Columns.Add("flip", "Answer flips?");
            _grid.Columns[0].FillWeight = 8;
            t.Controls.Add(_grid, 0, 1);

            _txtReading.Dock = DockStyle.Fill;
            _txtReading.Multiline = true;
            _txtReading.ReadOnly = true;
            _txtReading.WordWrap = true;
            _txtReading.ScrollBars = ScrollBars.Vertical;
            _txtReading.BackColor = Color.White;
            _txtReading.Font = new Font("Segoe UI", 10F);
            _txtReading.Margin = new Padding(0, 8, 0, 0);
            t.Controls.Add(_txtReading, 0, 2);
            return t;
        }

        private static TabPage MakeTab(string title, Control content)
        {
            var page = new TabPage(title) { Padding = new Padding(8) };
            content.Dock = DockStyle.Fill;
            page.Controls.Add(content);
            return page;
        }

        private static RichTextBox ConfigureRtb(RichTextBox box, bool mono)
        {
            box.Dock = DockStyle.Fill;
            box.ReadOnly = true;
            box.BackColor = Color.White;
            box.BorderStyle = BorderStyle.None;
            box.Font = mono ? new Font("Consolas", 10F) : new Font("Segoe UI", 10F);
            return box;
        }

        private static void ConfigurePicture(PictureBox pb)
        {
            pb.Dock = DockStyle.Fill;
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            pb.BorderStyle = BorderStyle.FixedSingle;
            pb.BackColor = Color.Gainsboro;
        }

        private static Label Caption(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = Color.DimGray
            };
        }

        // ─────────────────────────────────────────────────────────────────
        //  Background work
        // ─────────────────────────────────────────────────────────────────

        private void StartWork()
        {
            _status.Text = "Loading the image and the model...";
            CancellationToken token = _cts.Token;
            _worker = Task.Run(() => Work(token));
        }

        private void Work(CancellationToken token)
        {
            IImageScorer? scorer = null;
            try
            {
                scorer = _scorerFactory();

                // 1) fast: crop, statistics, one prediction
                ExplainResult res = ExplainSession.Prepare(_imagePath, _roi, _folderLabel, scorer, null);
                _result = res;
                Bitmap uiCrop = new Bitmap(res.Crop);   // the UI gets its own copy (GDI+ bitmaps are not thread-safe)
                View v1 = BuildView(res);
                Post(() => ShowPrepared(res, uiCrop, v1));
                if (token.IsCancellationRequested) return;

                // 2) compare with the training images
                try
                {
                    Post(() => _status.Text = "Comparing with the training images...");
                    TrainingStats training = TrainingStatsBuilder.GetCached(_basePath, _roi);
                    res.Input.KnownLabels = training.Labels.ToList();
                    ExplainSession.Refine(res, training);
                    View v2 = BuildView(res);
                    Post(() => ApplyView(v2));
                }
                catch { /* a bonus: carry on without it */ }
                if (token.IsCancellationRequested) return;

                // 3) spots for every class
                Post(() => _status.Text = "Finding the spots the model relied on...");
                bool ok = ExplainSession.AddOcclusion(res, scorer, 8,
                    () => token.IsCancellationRequested,
                    (done, total) => Post(() => ShowProgress(done, total)));
                if (ok)
                {
                    View v3 = BuildView(res);
                    Post(() => { ApplyView(v3); _progress.Value = 100; _status.Text = "Done."; });
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                Post(() => ShowError(message));
            }
            finally
            {
                try { if (scorer != null) scorer.Dispose(); } catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Building the text (worker thread: reads the result, produces plain data)
        // ─────────────────────────────────────────────────────────────────

        private View BuildView(ExplainResult res)
        {
            Explanation ex = res.Explanation;
            string[] names = ex.ClassNames;
            var v = new View { Headline = ex.Headline, ClassNames = names, TopIndex = ex.TopIndex };

            // verdict
            string band = ex.TopScore >= 0.90f ? "very sure"
                        : (ex.TopScore >= 0.70f && ex.Margin >= 0.30f) ? "fairly sure" : "unsure - a close call";
            var verdict = new StringBuilder();
            verdict.Append("The model answers ").Append(ex.TopLabel).Append(" with ")
                   .Append(MismatchExplainer.Pct(ex.TopScore)).Append(" (").Append(band).Append("). ");
            if (!string.IsNullOrWhiteSpace(_folderLabel)) verdict.Append("The folder says ").Append(_folderLabel!.Trim()).Append(". ");
            switch (ex.Kind)
            {
                case "FalseAlarm": verdict.Append("If the part is really good, this is a false alarm."); break;
                case "CriticalMiss": verdict.Append("If the part is really defective, the model missed it."); break;
                case "Mismatch": verdict.Append("The model disagrees with the folder."); break;
                case "Match": verdict.Append("The model agrees with the folder."); break;
            }
            v.Summary.Add(new KeyValuePair<string, string>("Verdict", verdict.ToString().Trim()));

            // reasons
            if (ex.Reasons.Count > 0)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < ex.Reasons.Count; i++)
                    sb.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + ex.Reasons[i]).AppendLine();
                v.Summary.Add(new KeyValuePair<string, string>(ex.IsMismatch ? "Why it may differ from the folder" : "Notes", sb.ToString().TrimEnd()));
            }

            // next steps
            string next;
            switch (ex.Kind)
            {
                case "FalseAlarm":
                    next = "1. Open the Spots tab and look at the circles in picture 3.\n" +
                           "2. If they sit on glare, a mark, a shadow or the background, the model reacted to the wrong thing: " +
                           "choose OK, press Save Correction, then Quick Update.\n" +
                           "3. If they sit on a real flaw, the folder label may be wrong: move the image to NG.";
                    break;
                case "CriticalMiss":
                    next = "1. Inspect the product by eye before it is used.\n" +
                           "2. Switch picture 3 to NG to see which parts looked defective to the model.\n" +
                           "3. If a real flaw is there, save the correction as NG and retrain so the model learns it.";
                    break;
                case "Mismatch":
                    next = "1. Check whether the folder label is right.\n" +
                           "2. Switch picture 3 between the classes and compare where the model finds each one.\n" +
                           "3. Save a correction if the model is wrong.";
                    break;
                default:
                    next = "No action needed. If the circles in picture 3 sit on something that is not a defect, " +
                           "the model may be right for the wrong reason: add similar images to the training set.";
                    break;
            }
            v.Summary.Add(new KeyValuePair<string, string>("What to check next", next));

            // per class spots
            foreach (KeyValuePair<int, List<HotSpot>> kv in res.SpotsByClass)
            {
                int k = kv.Key;
                var cv = new ClassView { Name = names[k], Spots = kv.Value.ToList(), Reading = ExplainSession.Describe(res, k) };
                foreach (HotSpot s in cv.Spots)
                    cv.Rows.Add(new[]
                    {
                        "#" + s.Rank, s.Where, MismatchExplainer.Pct(s.ScoreBefore), MismatchExplainer.Pct(s.ScoreWithout),
                        s.DropText, s.NewTopIndex >= 0 ? "Yes -> " + names[s.NewTopIndex] : "no"
                    });
                v.Classes[k] = cv;
            }

            // scores
            var sc = new StringBuilder();
            sc.AppendLine(ex.Bars).AppendLine();
            sc.AppendLine("Gap between 1st and 2nd choice: " + MismatchExplainer.Pct(ex.Margin)).AppendLine();
            sc.AppendLine("How to read it:");
            sc.AppendLine("  90% or more                  = very sure");
            sc.AppendLine("  70% or more and a 30-pt gap  = fairly sure");
            sc.AppendLine("  anything else                = a close call that can flip with small changes");
            if (res.Baseline.IsOutOfDistribution)
                sc.AppendLine().AppendLine("The model's own check also says this image looks unlike its training images" +
                    (string.IsNullOrEmpty(res.Baseline.OodReason) ? "." : " (" + res.Baseline.OodReason + ")."));
            v.Scores = sc.ToString();

            v.ImageStats = BuildStats(res);

            // full report
            var full = new StringBuilder();
            full.AppendLine(ex.Headline).AppendLine();
            foreach (var kv in v.Summary) full.AppendLine(kv.Key.ToUpperInvariant()).AppendLine(kv.Value).AppendLine();
            foreach (var kv in v.Classes)
                full.AppendLine("SPOTS FOR " + kv.Value.Name.ToUpperInvariant()).AppendLine(kv.Value.Reading).AppendLine();
            full.AppendLine("SCORES").AppendLine(v.Scores).AppendLine();
            full.AppendLine("IMAGE VS TRAINING").AppendLine(v.ImageStats).AppendLine();
            full.AppendLine("File: " + _imagePath).AppendLine();
            full.AppendLine("These are hints built from the model's scores and image statistics, not the model's internal reasoning.");
            full.AppendLine("The spots show what the answer depends on, not proof that a defect is or is not there.");
            v.Full = full.ToString();
            return v;
        }

        private static string BuildStats(ExplainResult res)
        {
            ImageStats s = res.Stats;
            var sb = new StringBuilder();
            sb.AppendLine("Crop the model saw: " + s.Width + " x " + s.Height).AppendLine();

            TrainingStats? tr = res.Input.Training;
            if (tr == null)
            {
                sb.AppendLine("Brightness " + s.Brightness.ToString("0.0", CultureInfo.InvariantCulture));
                sb.AppendLine("Contrast   " + s.Contrast.ToString("0.0", CultureInfo.InvariantCulture));
                sb.AppendLine("Sharpness  " + s.Sharpness.ToString("0.0", CultureInfo.InvariantCulture));
                sb.AppendLine().AppendLine("Comparing with the training images...");
                return sb.ToString();
            }

            List<string> labels = tr.Labels.OrderBy(l => l).ToList();
            sb.Append("Metric".PadRight(13)).Append("This crop".PadRight(11));
            foreach (string l in labels) sb.Append(("vs " + l + " images").PadRight(18));
            sb.AppendLine();

            foreach (StatMetric m in new[] { StatMetric.Brightness, StatMetric.Contrast, StatMetric.Sharpness })
            {
                double value = s.Get(m);
                sb.Append(m.ToString().PadRight(13)).Append(value.ToString("0.0", CultureInfo.InvariantCulture).PadRight(11));
                foreach (string l in labels)
                {
                    double pt = tr.Percentile(l, m, value);
                    string cell = double.IsNaN(pt) ? "n/a (few images)"
                        : MismatchExplainer.Ord(pt) + " pct" + ((pt <= 0.10 || pt >= 0.90) ? " *" : "");
                    sb.Append(cell.PadRight(18));
                }
                sb.AppendLine();
            }

            sb.AppendLine().AppendLine("A percentile near 50 means typical for that class. * = unusual (below 10 or above 90).");
            sb.AppendLine("An image that is unusual for its own folder's class but typical for another class is a common");
            sb.AppendLine("reason for a wrong answer: the model may be reacting to lighting or focus, not to the part.");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        //  UI updates (UI thread only)
        // ─────────────────────────────────────────────────────────────────

        private void Post(Action action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(action);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void ShowPrepared(ExplainResult res, Bitmap uiCrop, View v)
        {
            _uiCrop = uiCrop;
            _picOriginal.Image = res.OriginalWithRoi;   // only drawn by the UI after this point
            _picCrop.Image = uiCrop;
            ApplyView(v);
        }

        private void ApplyView(View v)
        {
            _view = v;
            _headline.Text = v.Headline;

            _rtbSummary.Clear();
            foreach (KeyValuePair<string, string> kv in v.Summary)
            {
                AppendStyled(_rtbSummary, kv.Key + "\n", _bold, Color.FromArgb(25, 60, 168));
                AppendStyled(_rtbSummary, kv.Value + "\n\n", _rtbSummary.Font, Color.Black);
            }
            _rtbSummary.SelectionStart = 0;
            _rtbSummary.ScrollToCaret();

            _rtbScores.Text = v.Scores;
            _rtbStats.Text = v.ImageStats;
            _rtbFull.Text = v.Full;

            if (v.Classes.Count > 0) PopulateClasses(v);
        }

        private static void AppendStyled(RichTextBox box, string text, Font font, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionFont = font;
            box.SelectionColor = color;
            box.AppendText(text);
        }

        private void PopulateClasses(View v)
        {
            int keep = _cmbClass.SelectedIndex;
            _updatingCombo = true;
            _cmbClass.Items.Clear();
            for (int i = 0; i < v.ClassNames.Length; i++)
            {
                string label = v.ClassNames[i];
                if (i == v.TopIndex) label += "  (model's answer)";
                if (!string.IsNullOrWhiteSpace(_folderLabel) &&
                    string.Equals(v.ClassNames[i].Trim(), _folderLabel!.Trim(), StringComparison.OrdinalIgnoreCase))
                    label += "  (folder)";
                _cmbClass.Items.Add(label);
            }
            _cmbClass.SelectedIndex = (keep >= 0 && keep < _cmbClass.Items.Count) ? keep : v.TopIndex;
            _cmbClass.Enabled = true;
            _updatingCombo = false;
            ShowClass(_cmbClass.SelectedIndex);
        }

        private void ShowClass(int k)
        {
            ClassView? cv;
            if (_view == null || _uiCrop == null || !_view.Classes.TryGetValue(k, out cv)) return;

            Bitmap img = ExplainImaging.RenderSpots(_uiCrop, cv.Spots, Palette[k % Palette.Length]);
            Bitmap? old = _spotImage;
            _spotImage = img;
            _picSpots.Image = img;
            if (old != null) old.Dispose();

            _capSpots.Text = "3. Where the model finds " + cv.Name + "\n(label = points the " + cv.Name + " score falls if that area is covered)";

            _grid.Columns[2].HeaderText = cv.Name + " now";
            _grid.Columns[3].HeaderText = cv.Name + " if covered";
            _grid.Rows.Clear();
            foreach (string[] row in cv.Rows) _grid.Rows.Add(row.Cast<object>().ToArray());

            _txtReading.Text = cv.Reading;
        }

        private void ShowProgress(int done, int total)
        {
            _progress.Value = Math.Max(0, Math.Min(100, done * 100 / Math.Max(1, total)));
            _status.Text = "Finding the spots... " + done + "/" + total;
        }

        private void ShowError(string message)
        {
            _status.Text = "Could not explain this image.";
            _headline.Text = "⚠️ No explanation available";
            _rtbSummary.Text = "The explanation failed:\n\n" + message + "\n\n" +
                "The explanation works with ML.NET models and needs the image file and the model file to still exist.";
        }

        private void ShowLarge(Image? img, string title)
        {
            if (img == null) return;
            var f = new Form
            {
                Text = title + " - " + Path.GetFileName(_imagePath),
                StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(820, 820)
            };
            var pb = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = img };
            f.Controls.Add(pb);
            f.FormClosed += (s, e) => pb.Image = null;   // the image belongs to this window's owner
            f.Show(this);
        }

        private void CopyReport()
        {
            try
            {
                Clipboard.SetText(_view != null ? _view.Full : _headline.Text);
                _status.Text = "Report copied to the clipboard.";
            }
            catch (Exception ex)
            {
                _status.Text = "Could not copy: " + ex.Message;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Clean-up
        // ─────────────────────────────────────────────────────────────────

        private void ReleaseImages()
        {
            _picOriginal.Image = null;
            _picCrop.Image = null;
            _picSpots.Image = null;
            if (_spotImage != null) { _spotImage.Dispose(); _spotImage = null; }
            if (_uiCrop != null) { _uiCrop.Dispose(); _uiCrop = null; }

            Task? worker = _worker;
            if (worker == null) DisposeResult();
            else worker.ContinueWith(_ => DisposeResult());
        }

        private void DisposeResult()
        {
            try { if (_result != null) _result.Dispose(); } catch { }
            try { _cts.Dispose(); } catch { }
        }
    }
}