// ─────────────────────────────────────────────────────────────────────────────
//  ModelBuilderForm.CNN.cs
//
//  Partial class — CNN training logic + ONNX test panel event handlers.
//  All controls are declared in ModelBuilderForm.Designer.cs (the single
//  Designer file — ModelBuilderForm.CNN.Designer.cs is NOT needed).
//
//  Integration in ModelBuilderForm.cs constructor:
//    InitializeComponent();
//    InitCnnTab();          ← wires events & tooltips for CNN controls
//
//  Integration in OnFormClosing():
//    DisposeCnnResources(); ← already added in ModelBuilderForm.cs
// ─────────────────────────────────────────────────────────────────────────────

using Image_Checker.Services;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Image_Checker.WinForm
{
    public partial class ModelBuilderForm
    {
        // ── CNN tab state ──────────────────────────────────────────────────────
        private string? _loadedOnnxPath;
        private string? _testImagePath;
        private OnnxImageChecker? _onnxChecker;
        private TorchImageChecker? _torchChecker;

        // ══════════════════════════════════════════════════════════════════════
        //  INIT — called from constructor after InitializeComponent()
        // ══════════════════════════════════════════════════════════════════════
        private void InitCnnTab()
        {
            // Populate architecture dropdown (controls already created by Designer)
            cmbCnnArchitecture.Items.Clear();
            cmbCnnArchitecture.Items.AddRange(new object[] { "MiniCNN", "ResidualCNN" });
            cmbCnnArchitecture.SelectedIndex = 0;

            // Tooltips
            var tips = new ToolTip { AutoPopDelay = 8000 };
            tips.SetToolTip(cmbCnnArchitecture,
                "MiniCNN     — 3 conv blocks, global avg pool. Fast, works well for most tasks.\r\n" +
                "ResidualCNN — skip connections, deeper. Better for subtle or complex defects.");
            tips.SetToolTip(chkExportOnnx,
                "Exports the trained model to ONNX using TorchSharp native export.\r\n" +
                "No Python or external tools required.");
            tips.SetToolTip(chkCnnUseGpu,
                "Uses CUDA GPU if available.\r\n" +
                "Requires the TorchSharp-cuda-windows NuGet package.");
            tips.SetToolTip(numCnnEarlyStop,
                "Stop training when val accuracy has not improved\r\n" +
                "for this many consecutive epochs.");
            tips.SetToolTip(numCnnLR,
                "Adam learning rate. 0.001 is a solid default.\r\n" +
                "Lower (0.0001) for fine-tuning; higher (0.01) for quick warm-up.");

            // Wire button events
            btnStartCnnTraining.Click += BtnStartCnnTraining_Click;
            btnBrowseOnnx.Click += BtnBrowseOnnx_Click;
            btnBrowseTestImage.Click += BtnBrowseTestImage_Click;
            btnTestOnnx.Click += BtnTestOnnx_Click;
            // One-Class wiring
            btnBrowseOneClassOk.Click += BtnBrowseOneClassOk_Click;
            btnStartOneClass.Click += BtnStartOneClass_Click;
            btnBrowseOneClassModel.Click += BtnBrowseOneClassModel_Click;
            btnBrowseOneClassTestImage.Click += BtnBrowseOneClassTestImage_Click;
            btnTestOneClass.Click += BtnTestOneClass_Click;
            btnAdjustThreshold.Click += BtnAdjustThreshold_Click;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  START CNN TRAINING
        // ══════════════════════════════════════════════════════════════════════
        private async void BtnStartCnnTraining_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_datasetPath))
            {
                MessageBox.Show("Select a dataset folder first (Step 1).",
                    "No Dataset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_detectedLabels.Count < 2)
            {
                MessageBox.Show("Dataset must contain at least 2 class folders.",
                    "Invalid Dataset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var config = new CnnTrainer.CnnConfig
            {
                Architecture = cmbCnnArchitecture.SelectedItem?.ToString() ?? "MiniCNN",
                ImageWidth = (int)numImageWidth.Value,
                ImageHeight = (int)numImageHeight.Value,
                Epochs = (int)numCnnEpochs.Value,
                BatchSize = (int)numCnnBatch.Value,
                LearningRate = (float)numCnnLR.Value,
                Augment = chkCnnAugment.Checked,
                EarlyStopPatience = (int)numCnnEarlyStop.Value,
                ExportOnnx = chkExportOnnx.Checked,
                UseGpu = chkCnnUseGpu.Checked
            };

            if (MessageBox.Show(
                    $"Start CNN training?\n\n" +
                    $"Architecture : {config.Architecture}\n" +
                    $"Image size   : {config.ImageWidth}×{config.ImageHeight}\n" +
                    $"Epochs       : {config.Epochs}  (early stop: {config.EarlyStopPatience})\n" +
                    $"Batch size   : {config.BatchSize}\n" +
                    $"Learning rate: {config.LearningRate:F5}\n" +
                    $"Augmentation : {(config.Augment ? "ON" : "OFF")}\n" +
                    $"Export ONNX  : {(config.ExportOnnx ? "YES" : "NO")}\n" +
                    $"GPU / CUDA   : {(config.UseGpu ? "YES" : "NO")}\n\n" +
                    $"Classes ({_detectedLabels.Count}): {string.Join(", ", _detectedLabels)}",
                    "Confirm CNN Training",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _isTraining = true;
            SetCnnUiState(training: true);

            LogMessage("");
            LogMessage("═══════════════════════════════════════════════", Color.Cyan);
            LogMessage("🧠 STARTING CNN TRAINING", Color.Cyan);
            LogMessage($"   Architecture : {config.Architecture}", Color.Cyan);
            LogMessage($"   Device       : {(config.UseGpu ? "GPU (CUDA)" : "CPU")}", Color.Cyan);
            LogMessage($"   Classes      : {string.Join(", ", _detectedLabels)}", Color.Cyan);
            LogMessage("═══════════════════════════════════════════════", Color.Cyan);

            CnnTrainer.CnnResult? result = null;

            try
            {
                var ct = _cancellationTokenSource.Token;
                var roi = roiRect;

                result = await Task.Run(() =>
                    new CnnTrainer(_datasetPath, config, roi, msg => LogMessage(msg))
                        .Train(ct), ct);

                LogMessage("");
                LogMessage("═══════════════════════════════════════════════", Color.Green);
                LogMessage("✅ CNN TRAINING COMPLETE!", Color.Green);
                LogMessage($"   Train Acc : {result.TrainAccuracy:P2}   Loss: {result.TrainLoss:F4}", Color.Green);
                LogMessage($"   Val   Acc : {result.ValAccuracy:P2}   Loss: {result.ValLoss:F4}", Color.Green);
                LogMessage($"   Model     : {Path.GetFileName(result.ModelPath)}", Color.Green);
                if (!string.IsNullOrEmpty(result.OnnxPath))
                    LogMessage($"   ONNX      : {Path.GetFileName(result.OnnxPath)}", Color.Green);
                LogMessage("═══════════════════════════════════════════════", Color.Green);

                // Auto-populate ONNX test panel
                if (!string.IsNullOrEmpty(result.OnnxPath))
                {
                    _loadedOnnxPath = result.OnnxPath;
                    txtOnnxModelPath.Text = Path.GetFileName(result.OnnxPath);
                    try
                    {
                        _onnxChecker?.Dispose();
                        _onnxChecker = new OnnxImageChecker(result.OnnxPath);
                        var info = _onnxChecker.GetModelInfo();
                        lblOnnxResult.Text =
                            $"Model ready: {Path.GetFileName(result.OnnxPath)}\n" +
                            $"Classes: {string.Join(", ", info.ClassNames)}\n\n" +
                            "Browse a test image then click Run Inference.";
                        LogMessage($"   ONNX session ready — classes: {string.Join(", ", info.ClassNames)}", Color.Lime);
                    }
                    catch (Exception ex) { LogMessage($"   ⚠️ ONNX session pre-load failed: {ex.Message}", Color.Orange); }
                }

                MessageBox.Show(
                    $"CNN training complete!\n\n" +
                    $"Val Accuracy : {result.ValAccuracy:P2}\n" +
                    $"Val Loss     : {result.ValLoss:F4}\n\n" +
                    (string.IsNullOrEmpty(result.OnnxPath)
                        ? $"TorchScript model:\n{result.ModelPath}"
                        : $"ONNX model:\n{result.OnnxPath}"),
                    "CNN Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                LogMessage("⚠️ CNN TRAINING CANCELLED", Color.Orange);
                MessageBox.Show("CNN training was cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ CNN TRAINING FAILED: {ex.Message}", Color.Red);
                if (ex.InnerException != null) LogMessage($"   Inner: {ex.InnerException.Message}", Color.Red);
                MessageBox.Show($"CNN training failed:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isTraining = false;
                SetCnnUiState(training: false);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ONNX TEST — browse model
        // ══════════════════════════════════════════════════════════════════════
        private void BtnBrowseOnnx_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select TorchSharp Model",
                Filter = "TorchSharp Models (*.bin)|*.bin|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            // Dispose old checkers
            _onnxChecker?.Dispose();
            _onnxChecker = null;
            _torchChecker?.Dispose();
            _torchChecker = null;

            _loadedOnnxPath = dlg.FileName;  // reusing field name — still holds the .bin path
            txtOnnxModelPath.Text = Path.GetFileName(dlg.FileName);
            lblOnnxResult.Text = "Loading model…";

            LogMessage($"\n📂 TorchSharp model: {Path.GetFileName(dlg.FileName)}");

            try
            {
                _torchChecker = new TorchImageChecker(dlg.FileName);

                // Read sidecar .json for display info
                var jsonPath = Path.ChangeExtension(dlg.FileName, ".json");
                string classInfo = "(no sidecar found)";
                if (File.Exists(jsonPath))
                {
                    var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
                    if (json.RootElement.TryGetProperty("ClassNames", out var names))
                        classInfo = string.Join(", ", names.EnumerateArray().Select(x => x.GetString()));
                }

                LogMessage($"   Classes: {classInfo}");

                lblOnnxResult.Text =
                    $"Model: {Path.GetFileName(dlg.FileName)}\n" +
                    $"Classes: {classInfo}\n\n" +
                    "Browse a test image then click Run Inference.";
            }
            catch (Exception ex)
            {
                lblOnnxResult.Text = $"Error loading model: {ex.Message}";
                LogMessage($"❌ Model load failed: {ex.Message}", Color.Red);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ONNX TEST — browse test image
        // ══════════════════════════════════════════════════════════════════════
        private void BtnBrowseTestImage_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select Test Image",
                Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All (*.*)|*.*"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _testImagePath = dlg.FileName;
            var old = picOnnxTest.Image;
            try { picOnnxTest.Image = Image.FromFile(dlg.FileName); }
            catch { picOnnxTest.Image = null; }
            old?.Dispose();

            lblOnnxResult.Text = $"Image: {Path.GetFileName(dlg.FileName)}\nClick \"Run Inference\" to classify.";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ONNX TEST — run inference
        // ══════════════════════════════════════════════════════════════════════
        private void BtnTestOnnx_Click(object sender, EventArgs e)
        {
            if (_torchChecker == null || string.IsNullOrEmpty(_loadedOnnxPath))
            {
                MessageBox.Show("Browse a model (.bin) first.", "No Model",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_testImagePath) || !File.Exists(_testImagePath))
            {
                MessageBox.Show("Browse a test image first.", "No Image",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTestOnnx.Enabled = false;
            btnTestOnnx.Text = "Running…";

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var pred = _torchChecker.Predict(_testImagePath);
                sw.Stop();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Prediction  : {pred.Label}");
                sb.AppendLine($"Confidence  : {pred.Confidence:P1}");
                sb.AppendLine($"Inference   : {sw.ElapsedMilliseconds} ms");
                sb.AppendLine();
                sb.AppendLine("All scores:");
                foreach (var kvp in pred.ScoreMap.OrderByDescending(x => x.Value))
                    sb.AppendLine($"  {kvp.Key,-22} {kvp.Value:P2}");

                lblOnnxResult.Text = sb.ToString();

                LogMessage($"\n🔍 Inference: {Path.GetFileName(_testImagePath)}");
                LogMessage($"   Result : {pred.Label}  ({pred.Confidence:P1})  [{sw.ElapsedMilliseconds} ms]");
                foreach (var kvp in pred.ScoreMap.OrderByDescending(x => x.Value))
                    LogMessage($"   {kvp.Key,-24}: {kvp.Value:P2}");
            }
            catch (Exception ex)
            {
                lblOnnxResult.Text = $"Error: {ex.Message}";
                LogMessage($"❌ Inference failed: {ex.Message}", Color.Red);
            }
            finally
            {
                btnTestOnnx.Enabled = true;
                btnTestOnnx.Text = "▶  Run Inference";
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ONE-CLASS TAB HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        private string? _oneClassModelPath;
        private string? _oneClassTestImagePath;
        private OneClassChecker? _oneClassChecker;

        private void BtnBrowseOneClassOk_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Select folder containing OK images (subfolders allowed)"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtOneClassOkFolder.Text = dlg.SelectedPath;
        }

        private async void BtnStartOneClass_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOneClassOkFolder.Text))
            {
                MessageBox.Show("Select the OK images folder first.",
                    "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputPath = string.IsNullOrEmpty(txtOutputPath.Text)
                ? txtOneClassOkFolder.Text
                : txtOutputPath.Text;

            float sensitivity = (float)numOneClassSensitivity.Value * 0.1f;

            var config = new OneClassTrainer.Config
            {
                ImageWidth = (int)numOneClassImgW.Value,
                ImageHeight = (int)numOneClassImgH.Value,
                LatentDim = (int)numOneClassLatent.Value,
                Epochs = (int)numOneClassEpochs.Value,
                BatchSize = (int)numOneClassBatch.Value,
                Sensitivity = sensitivity,
                Augment = chkOneClassAugment.Checked,
                UseGpu = chkOneClassGpu.Checked,
                EarlyStopPatience = 5
            };

            if (MessageBox.Show(
                    $"Start one-class training?\n\n" +
                    $"OK folder   : {txtOneClassOkFolder.Text}\n" +
                    $"Image size  : {config.ImageWidth}×{config.ImageHeight}\n" +
                    $"Latent dim  : {config.LatentDim}\n" +
                    $"Epochs      : {config.Epochs}\n" +
                    $"Sensitivity : {sensitivity:F1}",
                    "Confirm", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            SetCnnUiState(training: true);

            LogMessage("", Color.White);
            LogMessage("══════════════════════════════════════", Color.Cyan);
            LogMessage("🔵 STARTING ONE-CLASS TRAINING", Color.Cyan);
            LogMessage("══════════════════════════════════════", Color.Cyan);

            try
            {
                var ct = _cancellationTokenSource.Token;
                var roi = roiRect;
                var trainer = new OneClassTrainer(
                    txtOneClassOkFolder.Text, outputPath,
                    config, roi, msg => LogMessage(msg));

                var result = await Task.Run(() => trainer.Train(ct), ct);

                // Auto-populate test panel
                _oneClassModelPath = result.ModelPath;
                txtOneClassModelPath.Text = Path.GetFileName(result.ModelPath);
                numOneClassThresholdAdj.Value =
                    (decimal)Math.Max((double)numOneClassThresholdAdj.Minimum,
                        Math.Min((double)numOneClassThresholdAdj.Maximum,
                            (double)result.Threshold));

                _oneClassChecker?.Dispose();
                _oneClassChecker = new OneClassChecker(result.ModelPath);

                lblOneClassResult.Text =
                    $"Model ready\n" +
                    $"Threshold: {result.Threshold:F6}\n" +
                    $"Train: {result.TrainCount}  Val: {result.ValCount}\n\n" +
                    "Browse a test image to classify.";

                LogMessage("══════════════════════════════════════", Color.Green);
                LogMessage("✅ ONE-CLASS TRAINING COMPLETE", Color.Green);
                LogMessage($"   Threshold : {result.Threshold:F6}", Color.Green);
                LogMessage($"   Model     : {Path.GetFileName(result.ModelPath)}", Color.Green);
                LogMessage("══════════════════════════════════════", Color.Green);

                MessageBox.Show(
                    $"✅ Training complete!\n\n" +
                    $"Threshold : {result.Threshold:F6}\n" +
                    $"Model     : {Path.GetFileName(result.ModelPath)}\n\n" +
                    $"💡 If you see too many false NGs, increase Sensitivity and retrain,\n" +
                    $"   or raise the threshold with the Adjust control.",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                LogMessage("⚠️ One-class training cancelled.", Color.Orange);
            }
            catch (Exception ex)
            {
                LogMessage($"❌ One-class training failed: {ex.Message}", Color.Red);
                MessageBox.Show($"Training failed:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetCnnUiState(training: false);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void BtnBrowseOneClassModel_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select One-Class Model",
                Filter = "One-Class Models (*.bin)|*.bin|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            // Verify it is a one-class model via sidecar
            var jsonPath = Path.ChangeExtension(dlg.FileName, ".json");
            if (File.Exists(jsonPath))
            {
                var doc = System.Text.Json.JsonDocument.Parse(
                    File.ReadAllText(jsonPath));
                if (!doc.RootElement.TryGetProperty("ModelType", out var mt) ||
                    mt.GetString() != "OneClassAutoencoder")
                {
                    if (MessageBox.Show(
                            "This .bin file does not appear to be a one-class model.\n\n" +
                            "Load it anyway?",
                            "Model Type Mismatch",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning) != DialogResult.Yes)
                        return;
                }
                else
                {
                    // Pre-fill threshold from sidecar
                    if (doc.RootElement.TryGetProperty("Threshold", out var thr))
                    {
                        float t = thr.GetSingle();
                        numOneClassThresholdAdj.Value =
                            (decimal)Math.Max((double)numOneClassThresholdAdj.Minimum,
                                Math.Min((double)numOneClassThresholdAdj.Maximum, t));
                    }
                }
            }

            _oneClassModelPath = dlg.FileName;
            txtOneClassModelPath.Text = Path.GetFileName(dlg.FileName);

            _oneClassChecker?.Dispose();
            _oneClassChecker = null;

            try
            {
                _oneClassChecker = new OneClassChecker(dlg.FileName);
                lblOneClassResult.Text =
                    $"Model loaded.\nThreshold: {numOneClassThresholdAdj.Value:F6}\n\n" +
                    "Browse a test image to classify.";
                LogMessage($"📂 One-class model loaded: {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Load failed: {ex.Message}", Color.Red);
            }
        }

        private void BtnBrowseOneClassTestImage_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select Test Image",
                Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _oneClassTestImagePath = dlg.FileName;
            var old = picOneClassTest.Image;
            try { picOneClassTest.Image = Image.FromFile(dlg.FileName); }
            catch { picOneClassTest.Image = null; }
            old?.Dispose();

            lblOneClassResult.Text =
                $"Image: {Path.GetFileName(dlg.FileName)}\n" +
                "Click Run Inference to classify.";
        }

        private void BtnTestOneClass_Click(object sender, EventArgs e)
        {
            if (_oneClassChecker == null || string.IsNullOrEmpty(_oneClassModelPath))
            {
                MessageBox.Show("Browse a one-class model first.",
                    "No Model", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_oneClassTestImagePath) ||
                !File.Exists(_oneClassTestImagePath))
            {
                MessageBox.Show("Browse a test image first.",
                    "No Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTestOneClass.Enabled = false;
            btnTestOneClass.Text = "Running…";

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var pred = _oneClassChecker.Predict(_oneClassTestImagePath);
                sw.Stop();

                // Result colours: green = OK, red = NG
                bool isNg = pred.IsAnomaly;
                lblOneClassResult.BackColor = isNg
                    ? Color.FromArgb(60, 20, 20)
                    : Color.FromArgb(20, 40, 20);
                lblOneClassResult.ForeColor = isNg
                    ? Color.FromArgb(255, 100, 100)
                    : Color.LightGreen;

                lblOneClassResult.Text =
                    $"Result      : {pred.Label}\n" +
                    $"Confidence  : {pred.Confidence:P1}\n" +
                    $"Recon error : {pred.ReconError:F6}\n" +
                    $"Threshold   : {pred.Threshold:F6}\n" +
                    $"Inference   : {sw.ElapsedMilliseconds} ms\n\n" +
                    (isNg
                        ? "⚠️ ANOMALY — does not look like OK"
                        : "✅ NORMAL — matches OK pattern");

                LogMessage(
                    $"\n🔍 One-Class: {Path.GetFileName(_oneClassTestImagePath)}");
                LogMessage(
                    $"   {pred.Label}  err={pred.ReconError:F6}  " +
                    $"thr={pred.Threshold:F6}  ({sw.ElapsedMilliseconds} ms)",
                    isNg ? Color.Red : Color.Lime);
            }
            catch (Exception ex)
            {
                lblOneClassResult.Text = $"Error: {ex.Message}";
                LogMessage($"❌ Inference failed: {ex.Message}", Color.Red);
            }
            finally
            {
                btnTestOneClass.Enabled = true;
                btnTestOneClass.Text = "▶  Run Inference";
            }
        }

        private void BtnAdjustThreshold_Click(object sender, EventArgs e)
        {
            if (_oneClassChecker == null || string.IsNullOrEmpty(_oneClassModelPath))
            {
                MessageBox.Show("Load a one-class model first.",
                    "No Model", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            float newThr = (float)numOneClassThresholdAdj.Value;
            _oneClassChecker.AdjustThreshold(newThr, _oneClassModelPath);

            lblOneClassResult.Text =
                $"Threshold updated to {newThr:F6}\n\n" +
                "Run inference again to test the new value.";

            LogMessage($"📏 Threshold adjusted → {newThr:F6}");
            MessageBox.Show(
                $"Threshold updated to {newThr:F6}\n\n" +
                "Saved to sidecar .json file.",
                "Threshold Updated", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UI STATE
        // ══════════════════════════════════════════════════════════════════════
        private void SetCnnUiState(bool training)
        {
            btnStartCnnTraining.Enabled = !training;
            btnBrowseOnnx.Enabled = !training;
            btnBrowseTestImage.Enabled = !training;
            btnTestOnnx.Enabled = !training;
            btnStartTraining.Enabled = !training;
            btnStopTraining.Enabled = training;
            btnStopTraining.Text = training ? "Stop Training" : "Stop Training";
            progressBar.Visible = training;
            progressBar.Style = ProgressBarStyle.Marquee;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CLEANUP — called from OnFormClosing in ModelBuilderForm.cs
        // ══════════════════════════════════════════════════════════════════════
        private void DisposeCnnResources()
        {
            _onnxChecker?.Dispose();
            _onnxChecker = null;
            _torchChecker?.Dispose();
            _torchChecker = null;
            if (picOnnxTest != null)
            {
                var old = picOnnxTest.Image;
                picOnnxTest.Image = null;
                old?.Dispose();
            }
        }
    }
}