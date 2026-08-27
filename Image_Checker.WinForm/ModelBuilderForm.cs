using Image_Checker.Services;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Image_Checker.WinForm
{
    public partial class ModelBuilderForm : Form
    {
        // ── fields ─────────────────────────────────────────────────────────────
        private string _datasetPath;
        private string _outputPath;
        private ConsoleRedirector _consoleRedirector;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isTraining;
        private List<string> _detectedLabels = new List<string>();
        private string sampleImagePath;
        private Rectangle roiRect = new Rectangle(220, 140, 200, 200);
        private List<string> roiImagePaths = new List<string>();
        private int roiImageIndex = 0;

        public ModelBuilderForm()
        {
            InitializeComponent();

            // Wire CNN button events and tooltips
            InitCnnTab();

            _consoleRedirector = new ConsoleRedirector(txtTrainingLog);
            _isTraining = false;
            btnRoiPrev.Click += btnRoiPrev_Click;
            btnRoiNext.Click += btnRoiNext_Click;
            numRoiW.ValueChanged += RoiSizeChanged;
            numRoiH.ValueChanged += RoiSizeChanged;
            SyncImageSizeWithRoi();
        }

        private void RoiSizeChanged(object sender, EventArgs e) => SyncImageSizeWithRoi();

        private void SyncImageSizeWithRoi()
        {
            numImageWidth.Value = numRoiW.Value;
            numImageHeight.Value = numRoiH.Value;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DATASET
        // ══════════════════════════════════════════════════════════════════════

        private void BtnSelectDataset_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select dataset folder containing subfolders for each class (e.g., OK/NG, Cats/Dogs, etc.)"
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _datasetPath = dialog.SelectedPath;
                txtDatasetPath.Text = _datasetPath;
                LogMessage($"📁 Dataset path selected: {_datasetPath}");
                ValidateDataset();
            }
        }

        private void ValidateDataset()
        {
            if (string.IsNullOrEmpty(_datasetPath)) return;

            LogMessage("🔍 Validating dataset structure...");

            var subdirs = Directory.GetDirectories(_datasetPath)
                .Select(d => new DirectoryInfo(d).Name).ToList();

            _detectedLabels.Clear();

            if (subdirs.Count < 2)
            {
                LogMessage("❌ Invalid dataset structure", System.Drawing.Color.Red);
                LogMessage($"   Found: {subdirs.Count} folder(s) — need at least 2", System.Drawing.Color.Red);
                lblDatasetInfo.Text = $"❌ Invalid dataset\nRequired: At least 2 class folders\nFound: {subdirs.Count} folder(s)";
                lblDatasetInfo.ForeColor = System.Drawing.Color.Red;
                btnStartTraining.Enabled = false;
                return;
            }

            var classInfo = new Dictionary<string, int>();
            int totalImages = 0;
            bool hasWarn = false;

            foreach (var cls in subdirs)
            {
                int cnt = Directory.GetFiles(Path.Combine(_datasetPath, cls), "*.*", SearchOption.TopDirectoryOnly)
                    .Count(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                classInfo[cls] = cnt;
                totalImages += cnt;
                _detectedLabels.Add(cls);
                LogMessage($"   📊 {cls}: {cnt} images");
                if (cnt < 5) { LogMessage($"      ⚠️ Very few images ({cnt})", System.Drawing.Color.Orange); hasWarn = true; }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"✅ Valid dataset - {subdirs.Count} classes");
            foreach (var kvp in classInfo.OrderBy(x => x.Key)) sb.AppendLine($"{kvp.Key}: {kvp.Value} images");
            sb.AppendLine($"Total: {totalImages} images");

            lblDatasetInfo.Text = sb.ToString().TrimEnd();
            lblDatasetInfo.ForeColor = hasWarn ? System.Drawing.Color.Orange : System.Drawing.Color.Green;
            btnStartTraining.Enabled = true;

            LogMessage($"🏷️ Classes: {string.Join(", ", _detectedLabels)}");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  OUTPUT
        // ══════════════════════════════════════════════════════════════════════

        private void BtnSelectOutput_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog { Description = "Select folder to save trained model" };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _outputPath = dialog.SelectedPath;
                txtOutputPath.Text = _outputPath;
                LogMessage($"💾 Output path selected: {_outputPath}");
            }
        }

        private string _oneClassPreviewImagePath = string.Empty;

        private void BtnLoadPreview_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _oneClassPreviewImagePath = ofd.FileName;
                    UpdateOneClassPreview();
                }
            }
        }

        private void PreviewSize_ValueChanged(object? sender, EventArgs e)
        {
            UpdateOneClassPreview();
        }

        private void UpdateOneClassPreview()
        {
            if (string.IsNullOrEmpty(_oneClassPreviewImagePath) || !System.IO.File.Exists(_oneClassPreviewImagePath))
                return;

            int w = (int)numOneClassImgW.Value;
            int h = (int)numOneClassImgH.Value;

            try
            {
                using (Image original = Image.FromFile(_oneClassPreviewImagePath))
                {
                    // Force the image into the exact target dimensions to reveal distortion
                    Bitmap resized = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(original, 0, 0, w, h);
                    }

                    // Dispose old image to prevent memory leaks in WinForms
                    if (picOneClassPreview.Image != null)
                    {
                        picOneClassPreview.Image.Dispose();
                    }

                    picOneClassPreview.Image = resized;
                    lblOneClassPreviewSize.Text = $"Model View: {w}x{h}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating preview: {ex.Message}", "Preview Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ROI
        // ══════════════════════════════════════════════════════════════════════

        private void RoiValueChanged(object sender, EventArgs e)
        {
            roiRect = new Rectangle(
                (int)numRoiX.Value, (int)numRoiY.Value,
                (int)numRoiW.Value, (int)numRoiH.Value);
            if (picRoiSource.Image != null) { ShowRoiImage(); picRoiSource.Invalidate(); }
        }
        private void BtnGoToImage_Click(object sender, EventArgs e)
        {
            if (!roiImagePaths.Any())
            {
                MessageBox.Show("Load images first using Preview ROI.");
                return;
            }

            if (!int.TryParse(txtImageIndex.Text, out int imageNumber))
            {
                MessageBox.Show("Enter a valid image number.");
                return;
            }

            // User enters 1-based index
            imageNumber--;

            if (imageNumber < 0 || imageNumber >= roiImagePaths.Count)
            {
                MessageBox.Show(
                    $"Image number must be between 1 and {roiImagePaths.Count}");
                return;
            }

            roiImageIndex = imageNumber;
            ShowRoiImage();
        }
        private void btnPreviewRoi_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_datasetPath) || !Directory.Exists(_datasetPath))
            {
                MessageBox.Show("Select a dataset folder first.", "ROI Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            roiImagePaths = Directory.GetDirectories(_datasetPath)
                .SelectMany(dir => Directory.GetFiles(dir, "*.*"))
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (!roiImagePaths.Any())
            {
                MessageBox.Show("No images found.", "ROI Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            roiImageIndex = 0;
            ShowRoiImage();
        }

        private void ShowRoiImage()
        {
            var imgPath = roiImagePaths[roiImageIndex];
            sampleImagePath = imgPath;
            using (var bmp = new Bitmap(imgPath))
            {
                int x = Math.Max(0, Math.Min(roiRect.X, bmp.Width - roiRect.Width));
                int y = Math.Max(0, Math.Min(roiRect.Y, bmp.Height - roiRect.Height));
                var safe = new Rectangle(x, y,
                    Math.Min(roiRect.Width, bmp.Width), Math.Min(roiRect.Height, bmp.Height));

                var oldSrc = picRoiSource.Image;
                var oldCrop = picRoiCrop.Image;
                picRoiSource.Image = (Bitmap)bmp.Clone();
                picRoiCrop.Image = bmp.Clone(safe, bmp.PixelFormat);
                oldSrc?.Dispose(); oldCrop?.Dispose();

                lblRoiInfo.Text =
                    $"Image {roiImageIndex + 1} / {roiImagePaths.Count}\r\n" +
                    $"{Path.GetFileName(imgPath)}\r\n" +
                    $"ROI: X={safe.X}, Y={safe.Y}, W={safe.Width}, H={safe.Height}";
            }
            picRoiSource.Invalidate();
        }

        private void btnRoiPrev_Click(object sender, EventArgs e)
        {
            if (!roiImagePaths.Any()) return;
            roiImageIndex = (roiImageIndex - 1 + roiImagePaths.Count) % roiImagePaths.Count;
            ShowRoiImage();
        }

        private void btnRoiNext_Click(object sender, EventArgs e)
        {
            if (!roiImagePaths.Any()) return;
            roiImageIndex = (roiImageIndex + 1) % roiImagePaths.Count;
            ShowRoiImage();
        }

        private void picRoiSource_Paint(object sender, PaintEventArgs e)
        {
            if (picRoiSource.Image == null) return;
            var img = picRoiSource.Image;
            var pb = picRoiSource;
            float ia = (float)img.Width / img.Height;
            float ba = (float)pb.Width / pb.Height;
            Rectangle draw;
            if (ia > ba) { int h = (int)(pb.Width / ia); draw = new Rectangle(0, (pb.Height - h) / 2, pb.Width, h); }
            else { int w = (int)(pb.Height * ia); draw = new Rectangle((pb.Width - w) / 2, 0, w, pb.Height); }
            float sx = (float)draw.Width / img.Width;
            float sy = (float)draw.Height / img.Height;
            using var pen = new System.Drawing.Pen(System.Drawing.Color.Lime, 2);
            e.Graphics.DrawRectangle(pen, new Rectangle(
                draw.X + (int)(roiRect.X * sx), draw.Y + (int)(roiRect.Y * sy),
                (int)(roiRect.Width * sx), (int)(roiRect.Height * sy)));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ML.NET TRAINING
        // ══════════════════════════════════════════════════════════════════════

        private bool ValidateAlgorithmSelection()
        {
            if ((chkSDCA.Checked || chkLBFGS.Checked) && !chkFastTree.Checked && !chkLightGBM.Checked && !chkTransferLearning.Checked)
                return MessageBox.Show(
                    "⚠️ WARNING: Linear classifiers (SDCA/LBFGS) almost always predict the same class for image data.\n\nRECOMMENDED: Enable FastTree or LightGBM.\n\nContinue anyway?",
                    "Poor Algorithm Choice", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            return true;
        }

        private async void BtnStartTraining_Click(object sender, EventArgs e)
        {
            if (!ValidateAlgorithmSelection()) return;
            if (string.IsNullOrEmpty(_datasetPath)) { MessageBox.Show("Please select a dataset folder.", "Missing Dataset", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (_detectedLabels.Count < 2) { MessageBox.Show("Dataset must have at least 2 class folders.", "Invalid Dataset", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (string.IsNullOrEmpty(_outputPath)) { _outputPath = _datasetPath; txtOutputPath.Text = _outputPath; }

            if (MessageBox.Show(
                    $"Train ML.NET model?\n\nClasses: {string.Join(", ", _detectedLabels)}",
                    "Confirm Training", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _isTraining = true;

            btnSelectDataset.Enabled = false;
            btnSelectOutput.Enabled = false;
            btnStartTraining.Enabled = false;
            btnStopTraining.Enabled = true;
            btnStopTraining.Text = "Stop Training";
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;

            LogMessage("", System.Drawing.Color.White);
            LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Cyan);
            LogMessage("🚀 STARTING ML.NET TRAINING", System.Drawing.Color.Cyan);
            LogMessage($"   Classes: {string.Join(", ", _detectedLabels)}", System.Drawing.Color.Cyan);
            LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Cyan);

            try
            {
                await Task.Run(() => TrainModel(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Green);
                LogMessage("✅ MODEL TRAINING COMPLETED SUCCESSFULLY!", System.Drawing.Color.Green);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Green);
                MessageBox.Show($"Training complete!\n\nClasses: {string.Join(", ", _detectedLabels.Select(l => "• " + l))}",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                LogMessage("⚠️ TRAINING CANCELLED", System.Drawing.Color.Orange);
                MessageBox.Show("Training was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ TRAINING FAILED: {ex.Message}", System.Drawing.Color.Red);
                MessageBox.Show($"Training failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isTraining = false;
                btnSelectDataset.Enabled = true;
                btnSelectOutput.Enabled = true;
                btnStartTraining.Enabled = true;
                btnStopTraining.Enabled = false;
                btnStopTraining.Text = "Stop Training";
                progressBar.Visible = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void BtnStopTraining_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null && _isTraining)
            {
                LogMessage("⚠️ STOP REQUESTED...", System.Drawing.Color.Orange);
                _cancellationTokenSource.Cancel();
                btnStopTraining.Enabled = false;
                btnStopTraining.Text = "Stopping...";
            }
        }

        private void TrainModel(CancellationToken ct)
        {
            int cvFolds = 0, trials = 0, imageWidth = 0, imageHeight = 0;
            bool useSDCA = false, useLBFGS = false, useFastTree = false, useLightGBM = false, useTransfer = false;

            Invoke(new Action(() =>
            {
                cvFolds = (int)numCVFolds.Value; trials = (int)numTrials.Value;
                imageWidth = (int)numImageWidth.Value; imageHeight = (int)numImageHeight.Value;
                useSDCA = chkSDCA.Checked; useLBFGS = chkLBFGS.Checked;
                useFastTree = chkFastTree.Checked; useLightGBM = chkLightGBM.Checked;
                useTransfer = chkTransferLearning.Checked;
            }));

            ct.ThrowIfCancellationRequested();
            LogMessage("📝 Creating CSV dataset...");
            DataValidator.CreateCsv(_datasetPath);

            ct.ThrowIfCancellationRequested();
            DataValidator.PrintLabelDistribution(Path.Combine(_datasetPath, "images.csv"));

            ct.ThrowIfCancellationRequested();
            var mlContext = new MLContext(seed: 42);

            _consoleRedirector.Start();
            try
            {
                new ModelTrainer(mlContext, _datasetPath, roiRect).TrainAndEvaluate(
                    cvFolds, trials, useSDCA, useLBFGS, useFastTree, useLightGBM, useTransfer,
                    imageWidth, imageHeight, ct);
                ct.ThrowIfCancellationRequested();
            }
            finally { _consoleRedirector.Stop(); }

            // Copy model to output path if different
            var modelFiles = Directory.GetFiles(_datasetPath, "bestModel-*.zip");
            if (modelFiles.Any())
            {
                var latest = modelFiles.OrderByDescending(File.GetCreationTime).First();
                if (_outputPath != _datasetPath)
                {
                    var dest = Path.Combine(_outputPath, Path.GetFileName(latest));
                    File.Copy(latest, dest, true);
                    LogMessage($"✅ Model copied to: {dest}");
                }
                else LogMessage($"✅ Model saved at: {latest}");
            }

            PreserveTrainingData();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SHARED HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void LogMessage(string message, System.Drawing.Color? color = null)
        {
            if (InvokeRequired) { Invoke(new Action(() => LogMessage(message, color))); return; }
            var entry = string.IsNullOrEmpty(message) ? "\r\n" : $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";
            txtTrainingLog.SelectionStart = txtTrainingLog.TextLength;
            txtTrainingLog.SelectionLength = 0;
            if (color.HasValue) txtTrainingLog.SelectionColor = color.Value;
            txtTrainingLog.AppendText(entry);
            txtTrainingLog.SelectionColor = txtTrainingLog.ForeColor;
            txtTrainingLog.SelectionStart = txtTrainingLog.TextLength;
            txtTrainingLog.ScrollToCaret();
        }

        private void PreserveTrainingData()
        {
            var csvPath = Path.Combine(_datasetPath, "images.csv");
            if (File.Exists(csvPath))
                MessageBox.Show(
                    $"✅ Training data preserved!\n\nFile: {csvPath}\nClasses: {string.Join(", ", _detectedLabels)}\n\n" +
                    "Keep this file to enable Incremental Learning.",
                    "Training Data Preserved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ROI CROP DATASET
        // ══════════════════════════════════════════════════════════════════════

        private async void btnApplyRoi_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_datasetPath) || !Directory.Exists(_datasetPath))
            {
                MessageBox.Show("Select a dataset folder first.", "Apply ROI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnApplyRoi.Enabled = false;
            btnApplyRoi.Text = "Processing...";
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;

            LogMessage("✂️ APPLYING ROI AND CREATING CROPPED DATASET", System.Drawing.Color.Cyan);

            try
            {
                var outputRoot = Path.Combine(Path.GetDirectoryName(_datasetPath)!, Path.GetFileName(_datasetPath) + "_Cropped");
                Directory.CreateDirectory(outputRoot);

                var classImageCounts = new Dictionary<string, List<string>>();
                int totalImages = 0;
                foreach (var classDir in Directory.GetDirectories(_datasetPath))
                {
                    var images = Directory.GetFiles(classDir, "*.*")
                        .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)).ToList();
                    classImageCounts[Path.GetFileName(classDir)] = images;
                    totalImages += images.Count;
                }

                int processed = 0;
                await Task.Run(() =>
                {
                    foreach (var kvp in classImageCounts)
                    {
                        Directory.CreateDirectory(Path.Combine(outputRoot, kvp.Key));
                        foreach (var imgPath in kvp.Value)
                        {
                            try
                            {
                                using var bmp = new Bitmap(imgPath);
                                int x = Math.Max(0, Math.Min(roiRect.X, bmp.Width - roiRect.Width));
                                int y = Math.Max(0, Math.Min(roiRect.Y, bmp.Height - roiRect.Height));
                                var safe = new Rectangle(x, y, Math.Min(roiRect.Width, bmp.Width), Math.Min(roiRect.Height, bmp.Height));
                                using var crop = bmp.Clone(safe, bmp.PixelFormat);
                                crop.Save(Path.Combine(outputRoot, kvp.Key, Path.GetFileName(imgPath)));
                                processed++;
                                int pct = (int)((processed / (float)totalImages) * 100);
                                Invoke(new Action(() => progressBar.Value = pct));
                            }
                            catch (Exception ex)
                            {
                                Invoke(new Action(() => LogMessage($"⚠️ {Path.GetFileName(imgPath)}: {ex.Message}", System.Drawing.Color.Orange)));
                            }
                        }
                    }
                });

                LogMessage($"✅ Cropped {processed} images → {outputRoot}", System.Drawing.Color.Green);
                _datasetPath = outputRoot; txtDatasetPath.Text = outputRoot;
                ValidateDataset();
            }
            catch (Exception ex)
            {
                LogMessage($"❌ CROP FAILED: {ex.Message}", System.Drawing.Color.Red);
                MessageBox.Show($"Failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnApplyRoi.Enabled = true;
                btnApplyRoi.Text = "🖼️ Apply ROI and Create Cropped Dataset";
                progressBar.Visible = false;
                progressBar.Value = 0;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FORM CLOSING
        // ══════════════════════════════════════════════════════════════════════

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isTraining)
            {
                if (MessageBox.Show("Training is in progress. Close anyway?", "Training In Progress",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                {
                    e.Cancel = true; return;
                }
                _cancellationTokenSource?.Cancel();
            }

            DisposeCnnResources();              // CNN partial class cleanup
            picRoiSource.Image?.Dispose();
            picRoiCrop.Image?.Dispose();
            _consoleRedirector?.Stop();
            _cancellationTokenSource?.Dispose();
            base.OnFormClosing(e);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CONSOLE REDIRECT HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    public class ConsoleRedirector
    {
        private readonly System.IO.TextWriter _orig;
        private readonly RichTextBox _tb;
        private System.IO.StringWriter _sw;
        public ConsoleRedirector(RichTextBox tb) { _tb = tb; _orig = Console.Out; }
        public void Start() { _sw = new System.IO.StringWriter(); Console.SetOut(new MultiTextWriter(_orig, new ControlWriter(_tb))); }
        public void Stop() { Console.SetOut(_orig); _sw?.Dispose(); }
    }

    public class MultiTextWriter : System.IO.TextWriter
    {
        private readonly System.IO.TextWriter[] _w;
        public MultiTextWriter(params System.IO.TextWriter[] w) => _w = w;
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        public override void Write(char v) { foreach (var w in _w) w.Write(v); }
        public override void WriteLine(string v) { foreach (var w in _w) w.WriteLine(v); }
    }


    public class ControlWriter : System.IO.TextWriter
    {
        private readonly RichTextBox _tb;
        public ControlWriter(RichTextBox tb) => _tb = tb;
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        public override void Write(char v)
        {
            if (_tb.InvokeRequired) { _tb.Invoke(new Action(() => Write(v))); return; }
            _tb.AppendText(v.ToString());
        }
        public override void WriteLine(string v)
        {
            if (_tb.InvokeRequired) { _tb.Invoke(new Action(() => WriteLine(v))); return; }
            _tb.AppendText(v + Environment.NewLine);
            _tb.SelectionStart = _tb.TextLength; _tb.ScrollToCaret();
        }
    }

}