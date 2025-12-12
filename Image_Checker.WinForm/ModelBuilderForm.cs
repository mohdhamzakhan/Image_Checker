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
using Tensorflow.Keras;
using static SkiaSharp.SKImageFilter;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Image_Checker.WinForm
{
    public partial class ModelBuilderForm : Form
    {
        private string _datasetPath;
        private string _outputPath;
        private ConsoleRedirector _consoleRedirector;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isTraining;
        private List<string> _detectedLabels = new List<string>();
        // ROI preview state
        private string sampleImagePath;
        private Rectangle roiRect = new Rectangle(220, 140, 200, 200);
        private List<string> roiImagePaths = new List<string>();
        private int roiImageIndex = 0;



        public ModelBuilderForm()
        {
            InitializeComponent();
            _consoleRedirector = new ConsoleRedirector(txtTrainingLog);
            _isTraining = false;
            btnRoiPrev.Click += btnRoiPrev_Click;
            btnRoiNext.Click += btnRoiNext_Click;

            // Sync target image size when ROI dimensions change
            numRoiW.ValueChanged += RoiSizeChanged;
            numRoiH.ValueChanged += RoiSizeChanged;

            // Initialize target image size to match default ROI
            SyncImageSizeWithRoi();
        }

        private void RoiSizeChanged(object sender, EventArgs e)
        {
            SyncImageSizeWithRoi();
        }

        private void SyncImageSizeWithRoi()
        {
            int roiWidth = (int)numRoiW.Value;
            int roiHeight = (int)numRoiH.Value;

            // Update target image size to match ROI
            numImageWidth.Value = roiWidth;
            numImageHeight.Value = roiHeight;

            // Optional: Add visual feedback
            // lblImageSizeInfo.Text = $"Target size auto-synced with ROI: {roiWidth}x{roiHeight}";
        }

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

        private void BtnStopTraining_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null && _isTraining)
            {
                LogMessage("", System.Drawing.Color.White);
                LogMessage("⚠️ STOP REQUESTED - Cancelling training...", System.Drawing.Color.Orange);
                LogMessage("   Please wait while current operation completes...", System.Drawing.Color.Orange);

                _cancellationTokenSource.Cancel();
                btnStopTraining.Enabled = false;
                btnStopTraining.Text = "Stopping...";
            }
        }

        private void BtnSelectOutput_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select folder to save trained model"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _outputPath = dialog.SelectedPath;
                txtOutputPath.Text = _outputPath;
                LogMessage($"💾 Output path selected: {_outputPath}");
            }
        }

        private void RoiValueChanged(object sender, EventArgs e)
        {
            roiRect = new Rectangle(
                (int)numRoiX.Value,
                (int)numRoiY.Value,
                (int)numRoiW.Value,
                (int)numRoiH.Value);

            // redraw box and update cropped preview
            if (picRoiSource.Image != null)
            {
                ShowRoiImage();
                picRoiSource.Invalidate();
            }
        }

        private void ValidateDataset()
        {
            if (string.IsNullOrEmpty(_datasetPath))
                return;

            LogMessage("🔍 Validating dataset structure...");

            // Get all subdirectories as potential class labels
            var subdirectories = Directory.GetDirectories(_datasetPath)
                .Select(d => new DirectoryInfo(d).Name)
                .ToList();

            _detectedLabels.Clear();

            if (subdirectories.Count < 2)
            {
                LogMessage($"❌ Invalid dataset structure", System.Drawing.Color.Red);
                LogMessage($"   Expected: At least 2 class folders", System.Drawing.Color.Red);
                LogMessage($"   Found: {subdirectories.Count} folder(s)", System.Drawing.Color.Red);
                LogMessage($"   Example structure: Dataset/Class1/, Dataset/Class2/, etc.", System.Drawing.Color.Red);

                lblDatasetInfo.Text = $"❌ Invalid dataset\nRequired: At least 2 class folders\nFound: {subdirectories.Count} folder(s)";
                lblDatasetInfo.ForeColor = System.Drawing.Color.Red;
                btnStartTraining.Enabled = false;
                return;
            }

            LogMessage($"✅ Found {subdirectories.Count} class folders:");

            var classInfo = new Dictionary<string, int>();
            int totalImages = 0;
            bool hasInvalidClass = false;

            foreach (var className in subdirectories)
            {
                var classPath = Path.Combine(_datasetPath, className);

                int imageCount = Directory.GetFiles(classPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Count(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

                classInfo[className] = imageCount;
                totalImages += imageCount;
                _detectedLabels.Add(className);

                LogMessage($"   📊 {className}: {imageCount} images");

                if (imageCount < 5)
                {
                    LogMessage($"      ⚠️ Warning: Very few images ({imageCount}). Recommend at least 10 per class.", System.Drawing.Color.Orange);
                    hasInvalidClass = true;
                }
            }

            LogMessage($"📊 Total: {totalImages} images across {subdirectories.Count} classes");

            // Build info text
            var infoBuilder = new StringBuilder();
            infoBuilder.AppendLine($"✅ Valid dataset - {subdirectories.Count} classes");
            foreach (var kvp in classInfo.OrderBy(x => x.Key))
            {
                infoBuilder.AppendLine($"{kvp.Key}: {kvp.Value} images");
            }
            infoBuilder.AppendLine($"Total: {totalImages} images");

            lblDatasetInfo.Text = infoBuilder.ToString().TrimEnd();
            lblDatasetInfo.ForeColor = hasInvalidClass ? System.Drawing.Color.Orange : System.Drawing.Color.Green;
            btnStartTraining.Enabled = true;

            if (hasInvalidClass)
            {
                LogMessage("⚠️ Warning: Some classes have very few images. More data recommended for better accuracy.", System.Drawing.Color.Orange);
            }

            // Show detected classes summary
            LogMessage("");
            LogMessage($"🏷️ Detected Classes: {string.Join(", ", _detectedLabels)}");
            LogMessage($"   The model will be trained to classify images into these {_detectedLabels.Count} categories.");
        }

        private void btnPreviewRoi_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_datasetPath) || !Directory.Exists(_datasetPath))
            {
                MessageBox.Show("Select a dataset folder first.", "ROI Preview",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            roiImagePaths = Directory.GetDirectories(_datasetPath)
                .SelectMany(dir => Directory.GetFiles(dir, "*.*"))
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!roiImagePaths.Any())
            {
                MessageBox.Show("No images found in dataset folders.", "ROI Preview",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            roiImageIndex = 0;   // only here
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
                var safeRoi = new Rectangle(
                    x, y,
                    Math.Min(roiRect.Width, bmp.Width),
                    Math.Min(roiRect.Height, bmp.Height));

                picRoiSource.Image = (Bitmap)bmp.Clone();
                picRoiCrop.Image = bmp.Clone(safeRoi, bmp.PixelFormat);

                lblRoiInfo.Text =
                    $"Image {roiImageIndex + 1} / {roiImagePaths.Count}\r\n" +
                    $"{Path.GetFileName(imgPath)}\r\n" +
                    $"ROI: X={safeRoi.X}, Y={safeRoi.Y}, W={safeRoi.Width}, H={safeRoi.Height}";
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

            float imageAspect = (float)img.Width / img.Height;
            float boxAspect = (float)pb.Width / pb.Height;

            Rectangle drawRect;
            if (imageAspect > boxAspect)
            {
                int drawWidth = pb.Width;
                int drawHeight = (int)(pb.Width / imageAspect);
                int offsetY = (pb.Height - drawHeight) / 2;
                drawRect = new Rectangle(0, offsetY, drawWidth, drawHeight);
            }
            else
            {
                int drawHeight = pb.Height;
                int drawWidth = (int)(pb.Height * imageAspect);
                int offsetX = (pb.Width - drawWidth) / 2;
                drawRect = new Rectangle(offsetX, 0, drawWidth, drawHeight);
            }

            float scaleX = (float)drawRect.Width / img.Width;
            float scaleY = (float)drawRect.Height / img.Height;

            var roiDisplay = new Rectangle(
                drawRect.X + (int)(roiRect.X * scaleX),
                drawRect.Y + (int)(roiRect.Y * scaleY),
                (int)(roiRect.Width * scaleX),
                (int)(roiRect.Height * scaleY));

            using (var pen = new Pen(Color.Lime, 2))
            {
                e.Graphics.DrawRectangle(pen, roiDisplay);
            }
        }



        private async void BtnStartTraining_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_datasetPath))
            {
                MessageBox.Show("Please select a dataset folder.", "Missing Dataset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_detectedLabels.Count < 2)
            {
                MessageBox.Show("Dataset must have at least 2 class folders.", "Invalid Dataset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_outputPath))
            {
                _outputPath = _datasetPath;
                txtOutputPath.Text = _outputPath;
                LogMessage($"💾 Output path not specified, using dataset folder: {_outputPath}");
            }

            // Show confirmation with detected classes
            var confirmMessage = $"Ready to train model with the following classes:\n\n" +
                               $"{string.Join("\n", _detectedLabels.Select(l => $"• {l}"))}\n\n" +
                               $"Total: {_detectedLabels.Count} classes\n\n" +
                               $"Continue?";

            var confirmResult = MessageBox.Show(confirmMessage, "Confirm Training",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            // Create new cancellation token
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _isTraining = true;

            // Update UI for training mode
            btnSelectDataset.Enabled = false;
            btnSelectOutput.Enabled = false;
            btnStartTraining.Enabled = false;
            btnStopTraining.Enabled = true;
            btnStopTraining.Text = "Stop Training";
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;

            LogMessage("", System.Drawing.Color.White);
            LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Cyan);
            LogMessage("🚀 STARTING MODEL TRAINING", System.Drawing.Color.Cyan);
            LogMessage($"   Classes: {string.Join(", ", _detectedLabels)}", System.Drawing.Color.Cyan);
            LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Cyan);

            bool trainingCompleted = false;

            try
            {
                await Task.Run(() => TrainModel(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

                trainingCompleted = true;

                LogMessage("", System.Drawing.Color.White);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Green);
                LogMessage("✅ MODEL TRAINING COMPLETED SUCCESSFULLY!", System.Drawing.Color.Green);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Green);

                MessageBox.Show(
                    $"Model training completed successfully!\n\n" +
                    $"The model can now classify images into:\n" +
                    $"{string.Join("\n", _detectedLabels.Select(l => $"• {l}"))}",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                LogMessage("", System.Drawing.Color.White);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Orange);
                LogMessage("⚠️ TRAINING CANCELLED BY USER", System.Drawing.Color.Orange);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Orange);

                MessageBox.Show("Training was cancelled.", "Training Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                LogMessage("", System.Drawing.Color.White);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Red);
                LogMessage($"❌ TRAINING FAILED: {ex.Message}", System.Drawing.Color.Red);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Red);

                MessageBox.Show($"Training failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isTraining = false;

                // Re-enable controls
                btnSelectDataset.Enabled = true;
                btnSelectOutput.Enabled = true;
                btnStartTraining.Enabled = true;
                btnStopTraining.Enabled = false;
                btnStopTraining.Text = "Stop Training";
                progressBar.Visible = false;

                // Clean up cancellation token
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void TrainModel(CancellationToken cancellationToken)
        {
            // Get training parameters from UI
            int cvFolds = 0, trials = 0;
            int imageWidth = 0, imageHeight = 0;
            bool useSDCA = false, useLBFGS = false, useFastTree = false, useLightGBM = false, useTransfer = false;

            Invoke(new Action(() =>
            {
                cvFolds = (int)numCVFolds.Value;
                trials = (int)numTrials.Value;
                imageWidth = (int)numImageWidth.Value;
                imageHeight = (int)numImageHeight.Value;
                useSDCA = chkSDCA.Checked;
                useLBFGS = chkLBFGS.Checked;
                useFastTree = chkFastTree.Checked;
                useLightGBM = chkLightGBM.Checked;
                useTransfer = chkTransferLearning.Checked;
            }));

            // Check cancellation before each major step
            cancellationToken.ThrowIfCancellationRequested();

            LogMessage("📝 Step 1: Creating CSV dataset from image folders...");
            LogMessage($"   Detected classes: {string.Join(", ", _detectedLabels)}");
            DataValidator.CreateCsv(_datasetPath);
            LogMessage("✅ CSV dataset created successfully");

            cancellationToken.ThrowIfCancellationRequested();

            var csvPath = Path.Combine(_datasetPath, "images.csv");
            LogMessage($"📄 CSV file: {csvPath}");

            LogMessage("");
            LogMessage("📊 Step 2: Analyzing dataset distribution...");
            DataValidator.PrintLabelDistribution(csvPath);

            cancellationToken.ThrowIfCancellationRequested();

            LogMessage("");
            LogMessage("🧠 Step 3: Initializing ML.NET context...");
            var mlContext = new MLContext(seed: 42);
            LogMessage("✅ ML Context initialized with seed=42");

            cancellationToken.ThrowIfCancellationRequested();

            LogMessage("");
            LogMessage("⚙️ Training Configuration:");
            LogMessage($"   • Classes: {string.Join(", ", _detectedLabels)}");
            LogMessage($"   • Cross-Validation Folds: {cvFolds}");
            LogMessage($"   • Tuning Trials: {trials}");
            LogMessage($"   • Selected Algorithms:");
            if (useSDCA) LogMessage("      - SDCA MaxEnt");
            if (useLBFGS) LogMessage("      - L-BFGS MaxEnt");
            if (useFastTree) LogMessage("      - FastTree");
            if (useLightGBM) LogMessage("      - LightGBM");
            if (useTransfer) LogMessage("      - Transfer Learning (MobileNetV2)");

            cancellationToken.ThrowIfCancellationRequested();

            LogMessage("");
            LogMessage("🔧 Step 4: Building and training models...");
            LogMessage("   This may take several minutes depending on dataset size...");
            LogMessage("   Press 'Stop Training' button to cancel at any time.");

            // Redirect console output to capture training details
            _consoleRedirector.Start();

            try
            {
                var trainer = new ModelTrainer(mlContext, _datasetPath, roiRect);

                trainer.TrainAndEvaluate(
                    cvFolds: cvFolds,
                    trials: trials,
                    useSDCA: useSDCA,
                    useLBFGS: useLBFGS,
                    useFastTree: useFastTree,
                    useLightGBM: useLightGBM,
                    useTransferLearning: useTransfer,
                    imageWidth: imageWidth,
                    imageHeight: imageHeight,
                    cancellationToken: cancellationToken
                );

                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                _consoleRedirector.Stop();
            }

            LogMessage("");
            LogMessage("💾 Step 5: Saving model...");

            // Copy model to output directory if different
            if (_outputPath != _datasetPath)
            {
                var modelFiles = Directory.GetFiles(_datasetPath, "bestModel-*.zip");
                if (modelFiles.Any())
                {
                    var latestModel = modelFiles.OrderByDescending(f => File.GetCreationTime(f)).First();
                    var destPath = Path.Combine(_outputPath, Path.GetFileName(latestModel));
                    File.Copy(latestModel, destPath, true);
                    LogMessage($"✅ Model copied to: {destPath}");
                }
            }
            else
            {
                var modelFiles = Directory.GetFiles(_datasetPath, "bestModel-*.zip");
                if (modelFiles.Any())
                {
                    var latestModel = modelFiles.OrderByDescending(f => File.GetCreationTime(f)).First();
                    LogMessage($"✅ Model saved at: {latestModel}");
                }
            }

            LogMessage("");
            LogMessage("🎉 All steps completed successfully!");
            LogMessage($"   Model can classify: {string.Join(", ", _detectedLabels)}");
            PreserveTrainingData();
        }

        private void LogMessage(string message, System.Drawing.Color? color = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => LogMessage(message, color)));
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = string.IsNullOrEmpty(message) ? "\r\n" : $"[{timestamp}] {message}\r\n";

            // Store current selection
            int selectionStart = txtTrainingLog.SelectionStart;
            int selectionLength = txtTrainingLog.SelectionLength;

            // Append text
            txtTrainingLog.SelectionStart = txtTrainingLog.TextLength;
            txtTrainingLog.SelectionLength = 0;

            if (color.HasValue)
            {
                txtTrainingLog.SelectionColor = color.Value;
            }

            txtTrainingLog.AppendText(logEntry);

            // Reset color
            txtTrainingLog.SelectionColor = txtTrainingLog.ForeColor;

            // Scroll to end
            txtTrainingLog.SelectionStart = txtTrainingLog.TextLength;
            txtTrainingLog.ScrollToCaret();
        }

        private void PreserveTrainingData()
        {
            var csvPath = Path.Combine(_datasetPath, "images.csv");

            if (File.Exists(csvPath))
            {
                MessageBox.Show(
                    "✅ Training data preserved!\n\n" +
                    $"File: {csvPath}\n" +
                    $"Classes: {string.Join(", ", _detectedLabels)}\n\n" +
                    "IMPORTANT: Keep this file to enable True Incremental Learning.\n" +
                    "Without it, incremental updates will cause catastrophic forgetting.",
                    "Training Data Preserved",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Stop training if in progress
            if (_isTraining)
            {
                var result = MessageBox.Show(
                    "Training is in progress. Are you sure you want to close?",
                    "Training In Progress",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _cancellationTokenSource?.Cancel();
            }

            _consoleRedirector?.Stop();
            _cancellationTokenSource?.Dispose();
            base.OnFormClosing(e);
        }

        private async void btnApplyRoi_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_datasetPath) || !Directory.Exists(_datasetPath))
            {
                MessageBox.Show("Select a dataset folder first.", "Apply ROI",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Disable button and show progress
            btnApplyRoi.Enabled = false;
            btnApplyRoi.Text = "Processing...";
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = 0;

            LogMessage("");
            LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Cyan);
            LogMessage("✂️ APPLYING ROI AND CREATING CROPPED DATASET", System.Drawing.Color.Cyan);
            LogMessage($"   ROI: X={roiRect.X}, Y={roiRect.Y}, W={roiRect.Width}, H={roiRect.Height}", System.Drawing.Color.Cyan);
            LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Cyan);

            try
            {
                var outputRoot = Path.Combine(Path.GetDirectoryName(_datasetPath)!,
                                              Path.GetFileName(_datasetPath) + "_Cropped");
                Directory.CreateDirectory(outputRoot);

                var classDirs = Directory.GetDirectories(_datasetPath);

                // Count total images first
                int totalImages = 0;
                var classImageCounts = new Dictionary<string, List<string>>();

                foreach (var classDir in classDirs)
                {
                    var images = Directory.GetFiles(classDir, "*.*")
                        .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var className = Path.GetFileName(classDir);
                    classImageCounts[className] = images;
                    totalImages += images.Count;
                }

                LogMessage($"📊 Found {totalImages} images across {classDirs.Length} classes");
                LogMessage("🔄 Starting crop operation...");

                int processedCount = 0;

                await Task.Run(() =>
                {
                    foreach (var kvp in classImageCounts)
                    {
                        var className = kvp.Key;
                        var images = kvp.Value;

                        var outClassDir = Path.Combine(outputRoot, className);
                        Directory.CreateDirectory(outClassDir);

                        Invoke(new Action(() =>
                            LogMessage($"   Processing class: {className} ({images.Count} images)...")));

                        foreach (var imgPath in images)
                        {
                            try
                            {
                                using var bmp = new Bitmap(imgPath);

                                int x = Math.Max(0, Math.Min(roiRect.X, bmp.Width - roiRect.Width));
                                int y = Math.Max(0, Math.Min(roiRect.Y, bmp.Height - roiRect.Height));
                                var safeRoi = new Rectangle(
                                    x,
                                    y,
                                    Math.Min(roiRect.Width, bmp.Width),
                                    Math.Min(roiRect.Height, bmp.Height));

                                using var crop = bmp.Clone(safeRoi, bmp.PixelFormat);

                                // Save directly without resizing since ROI size = target size
                                var outPath = Path.Combine(outClassDir, Path.GetFileName(imgPath));
                                crop.Save(outPath);

                                processedCount++;

                                // Update progress bar
                                int progressPercent = (int)((processedCount / (float)totalImages) * 100);
                                Invoke(new Action(() =>
                                {
                                    progressBar.Value = progressPercent;
                                }));
                            }
                            catch (Exception ex)
                            {
                                Invoke(new Action(() =>
                                    LogMessage($"      ⚠️ Failed to process {Path.GetFileName(imgPath)}: {ex.Message}",
                                        System.Drawing.Color.Orange)));
                            }
                        }
                    }
                });

                LogMessage("");
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Green);
                LogMessage($"✅ SUCCESSFULLY CROPPED {processedCount} IMAGES!", System.Drawing.Color.Green);
                LogMessage($"   Output: {outputRoot}", System.Drawing.Color.Green);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Green);

                MessageBox.Show(
                    $"✅ Successfully cropped {processedCount} images!\n\n" +
                    $"ROI: {roiRect.Width}x{roiRect.Height}\n" +
                    $"Output: {outputRoot}\n\n" +
                    $"The dataset path has been updated to use the cropped images.",
                    "Crop Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Auto-switch to cropped dataset
                _datasetPath = outputRoot;
                txtDatasetPath.Text = outputRoot;
                ValidateDataset();
            }
            catch (Exception ex)
            {
                LogMessage("", System.Drawing.Color.White);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Red);
                LogMessage($"❌ CROP OPERATION FAILED: {ex.Message}", System.Drawing.Color.Red);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Red);

                MessageBox.Show(
                    $"Failed to create cropped dataset:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // Re-enable button and hide progress
                btnApplyRoi.Enabled = true;
                btnApplyRoi.Text = "Apply ROI && Create Cropped Dataset";
                progressBar.Visible = false;
                progressBar.Value = 0;
            }
        }

    }

    /// <summary>
    /// Redirects Console output to a RichTextBox for real-time logging
    /// </summary>
    public class ConsoleRedirector
    {
        private readonly RichTextBox _textBox;
        private readonly TextWriter _originalOut;
        private StringWriter _stringWriter;

        public ConsoleRedirector(RichTextBox textBox)
        {
            _textBox = textBox;
            _originalOut = Console.Out;
        }

        public void Start()
        {
            _stringWriter = new StringWriter();
            var multiWriter = new MultiTextWriter(_originalOut, new ControlWriter(_textBox));
            Console.SetOut(multiWriter);
        }

        public void Stop()
        {
            Console.SetOut(_originalOut);
            _stringWriter?.Dispose();
        }
    }

    /// <summary>
    /// Writes to both console and RichTextBox
    /// </summary>
    public class MultiTextWriter : TextWriter
    {
        private readonly TextWriter[] _writers;

        public MultiTextWriter(params TextWriter[] writers)
        {
            _writers = writers;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            foreach (var writer in _writers)
                writer.Write(value);
        }

        public override void WriteLine(string value)
        {
            foreach (var writer in _writers)
                writer.WriteLine(value);
        }
    }

    /// <summary>
    /// Writes console output to a RichTextBox control
    /// </summary>
    public class ControlWriter : TextWriter
    {
        private readonly RichTextBox _textBox;

        public ControlWriter(RichTextBox textBox)
        {
            _textBox = textBox;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (_textBox.InvokeRequired)
            {
                _textBox.Invoke(new Action(() => Write(value)));
                return;
            }
            _textBox.AppendText(value.ToString());
        }

        public override void WriteLine(string value)
        {
            if (_textBox.InvokeRequired)
            {
                _textBox.Invoke(new Action(() => WriteLine(value)));
                return;
            }
            _textBox.AppendText(value + Environment.NewLine);
            _textBox.SelectionStart = _textBox.TextLength;
            _textBox.ScrollToCaret();
        }
    }
}