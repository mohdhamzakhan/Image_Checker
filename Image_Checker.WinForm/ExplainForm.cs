using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Image_Checker.Services;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Image_Checker.WinForm
{
    /// <summary>
    /// "Why is the model's answer different from the folder?"
    /// Shows the photo (ROI outlined), the crop the model actually sees, a heatmap of where the model
    /// reacted, the class scores, and plain-language reasons. Everything heavy runs in the background
    /// with its own model instance, so the main window (and folder monitoring) keeps running.
    /// </summary>
    public sealed class ExplainForm : Form
    {
        private readonly string _imagePath;
        private readonly string? _folderLabel;
        private readonly Rectangle _roi;
        private readonly string _basePath;
        private readonly Func<IImageScorer> _scorerFactory;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task? _worker;
        private ExplainResult? _result;
        private string _reportText = "";

        private readonly Label _headline = new Label();
        private readonly PictureBox _picOriginal = new PictureBox();
        private readonly PictureBox _picCrop = new PictureBox();
        private readonly PictureBox _picHeat = new PictureBox();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly Label _status = new Label();
        private readonly TextBox _report = new TextBox();
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
        //  Layout (code only: no Designer file to keep in sync)
        // ─────────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            Text = "Why? - " + Path.GetFileName(_imagePath);
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1040, 760);
            MinimumSize = new Size(820, 600);
            Font = new Font("Segoe UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));   // headline
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 52f));    // images
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));   // status + progress
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 48f));    // report
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));   // buttons

            _headline.Dock = DockStyle.Fill;
            _headline.Font = new Font("Segoe UI", 12.5F, FontStyle.Bold);
            _headline.TextAlign = ContentAlignment.MiddleLeft;
            _headline.Text = "Analysing...";
            root.Controls.Add(_headline, 0, 0);

            // three pictures side by side, each with a caption
            var images = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2
            };
            for (int i = 0; i < 3; i++) images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            images.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            images.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

            ConfigurePicture(_picOriginal);
            ConfigurePicture(_picCrop);
            ConfigurePicture(_picHeat);
            images.Controls.Add(_picOriginal, 0, 0);
            images.Controls.Add(_picCrop, 1, 0);
            images.Controls.Add(_picHeat, 2, 0);
            images.Controls.Add(Caption("1. Your photo\n(yellow box = the part given to the model)"), 0, 1);
            images.Controls.Add(Caption("2. What the model sees\n(the cropped area)"), 1, 1);
            images.Controls.Add(Caption("3. Where the model looked\n(red = pushed toward its answer, blue = against)"), 2, 1);
            root.Controls.Add(images, 0, 1);

            var statusRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320f));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _status.Dock = DockStyle.Fill;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.Text = "Starting...";
            _progress.Dock = DockStyle.Fill;
            _progress.Minimum = 0;
            _progress.Maximum = 100;
            _progress.Margin = new Padding(3, 8, 3, 8);
            statusRow.Controls.Add(_status, 0, 0);
            statusRow.Controls.Add(_progress, 1, 0);
            root.Controls.Add(statusRow, 0, 2);

            _report.Dock = DockStyle.Fill;
            _report.Multiline = true;
            _report.ReadOnly = true;
            _report.ScrollBars = ScrollBars.Vertical;
            _report.WordWrap = true;
            _report.BackColor = Color.White;
            _report.Font = new Font("Consolas", 10F);
            root.Controls.Add(_report, 0, 3);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
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

                // 1) fast part: crop, statistics, one prediction, first explanation
                ExplainResult res = ExplainSession.Prepare(_imagePath, _roi, _folderLabel, scorer, null);
                _result = res;
                string text1 = ComposeReport(res);
                string headline1 = res.Explanation.Headline;
                Post(() => ShowPrepared(res, headline1, text1));
                if (token.IsCancellationRequested) return;

                // 2) compare with the training images (reads a sample the first time, then cached)
                try
                {
                    Post(() => _status.Text = "Comparing with the training images...");
                    TrainingStats training = TrainingStatsBuilder.GetCached(_basePath, _roi);
                    res.Input.KnownLabels = training.Labels.ToList();
                    ExplainSession.Refine(res, training);
                    string text2 = ComposeReport(res);
                    Post(() => ShowText(text2));
                }
                catch
                {
                    // the comparison is a bonus: carry on without it
                }
                if (token.IsCancellationRequested) return;

                // 3) heatmap: hide each patch in turn and watch the score
                Post(() => _status.Text = "Finding where the model looked...");
                bool ok = ExplainSession.AddHeatmap(res, scorer, 8,
                    () => token.IsCancellationRequested,
                    (done, total) => Post(() => ShowProgress(done, total)));
                if (ok)
                {
                    string text3 = ComposeReport(res);
                    Post(() => ShowHeatmap(res, text3));
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

        private static string ComposeReport(ExplainResult res)
        {
            var sb = new StringBuilder();
            sb.Append(res.Explanation.BodyText.TrimEnd());
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Image statistics of the crop the model saw:");
            sb.AppendLine("  size " + res.Stats.Width + "x" + res.Stats.Height +
                          "   brightness " + res.Stats.Brightness.ToString("0.0") +
                          "   contrast " + res.Stats.Contrast.ToString("0.0") +
                          "   sharpness " + res.Stats.Sharpness.ToString("0.0"));
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────
        //  UI updates (always on the UI thread)
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

        private void ShowPrepared(ExplainResult res, string headline, string text)
        {
            _picOriginal.Image = res.OriginalWithRoi;
            _picCrop.Image = res.Crop;
            _headline.Text = headline;
            ShowText(text);
        }

        private void ShowText(string text)
        {
            _reportText = text;
            _report.Text = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            _report.SelectionStart = 0;
            _report.SelectionLength = 0;
        }

        private void ShowProgress(int done, int total)
        {
            _progress.Value = Math.Max(0, Math.Min(100, done * 100 / Math.Max(1, total)));
            _status.Text = "Finding where the model looked... " + done + "/" + total;
        }

        private void ShowHeatmap(ExplainResult res, string text)
        {
            _picHeat.Image = res.Heatmap;
            _progress.Value = 100;
            _status.Text = "Done.";
            ShowText(text);
        }

        private void ShowError(string message)
        {
            _status.Text = "Could not explain this image.";
            _headline.Text = "⚠️ No explanation available";
            ShowText("The explanation failed:\n\n" + message + "\n\n" +
                     "The explanation works with ML.NET models and needs the image file and the model file to still exist.");
        }

        private void CopyReport()
        {
            try
            {
                Clipboard.SetText(_headline.Text + Environment.NewLine + Environment.NewLine + _reportText +
                                  Environment.NewLine + "File: " + _imagePath);
                _status.Text = "Report copied to the clipboard.";
            }
            catch (Exception ex)
            {
                _status.Text = "Could not copy: " + ex.Message;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Clean-up: the worker may still be finishing a prediction, so images are released after it ends
        // ─────────────────────────────────────────────────────────────────

        private void ReleaseImages()
        {
            _picOriginal.Image = null;
            _picCrop.Image = null;
            _picHeat.Image = null;

            Task? worker = _worker;
            if (worker == null)
            {
                DisposeResult();
            }
            else
            {
                worker.ContinueWith(_ => DisposeResult());
            }
        }

        private void DisposeResult()
        {
            try { if (_result != null) _result.Dispose(); } catch { }
            try { _cts.Dispose(); } catch { }
        }
    }
}