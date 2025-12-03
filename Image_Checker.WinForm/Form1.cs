using Image_Checker.DataModels;
using Image_Checker.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Image_Checker.WinForm
{
    public partial class Form1 : Form
    {
        private Predictor _predictor;
        private List<ImageResult> _results = new();
        private string _basePath;
        private string _modelPath;
        private IncrementalModelTrainer _incrementalTrainer;
        private const string CONFIG_FILE = "app_config.txt";

        public Form1()
        {
            InitializeComponent();
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    var lines = File.ReadAllLines(CONFIG_FILE);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            if (parts[0].Trim() == "BasePath")
                                _basePath = parts[1].Trim();
                            else if (parts[0].Trim() == "ModelPath")
                                _modelPath = parts[1].Trim();
                        }
                    }
                }

                // If config doesn't exist or is incomplete, prompt user
                if (string.IsNullOrEmpty(_basePath) || string.IsNullOrEmpty(_modelPath))
                {
                    ShowSetupDialog();
                }
                else
                {
                    LoadModel();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Configuration load failed: {ex.Message}";
            }
        }

        private void ShowSetupDialog()
        {
            var result = MessageBox.Show(
                "No model configuration found.\n\n" +
                "Would you like to:\n" +
                "• YES: Build a new model\n" +
                "• NO: Select an existing model",
                "Initial Setup",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                OpenModelBuilder();
            }
            else if (result == DialogResult.No)
            {
                SelectExistingModel();
            }
        }

        private void SelectExistingModel()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "ML.NET Model (*.zip)|*.zip",
                Title = "Select Trained Model"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _modelPath = dialog.FileName;
                _basePath = Path.GetDirectoryName(_modelPath);
                SaveConfiguration();
                InitializeTrainer();
                LoadModel();
            }
        }

        private void SaveConfiguration()
        {
            File.WriteAllLines(CONFIG_FILE, new[]
            {
                $"BasePath={_basePath}",
                $"ModelPath={_modelPath}"
            });
        }

        private void InitializeTrainer()
        {
            if (!string.IsNullOrEmpty(_basePath))
            {
                _incrementalTrainer = new IncrementalModelTrainer(new Microsoft.ML.MLContext(), _basePath);
            }
        }

        private void LoadModel()
        {
            try
            {
                if (string.IsNullOrEmpty(_modelPath) || !File.Exists(_modelPath))
                {
                    lblStatus.Text = "❌ Model file not found. Please configure.";
                    return;
                }

                _predictor = new Predictor(_modelPath);
                lblStatus.Text = $"✅ Model loaded: {Path.GetFileName(_modelPath)}";
                InitializeTrainer();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Model load failed: {ex.Message}";
            }
        }

        // === Menu Actions ===
        private void MenuBuildModel_Click(object sender, EventArgs e)
        {
            OpenModelBuilder();
        }

        private void OpenModelBuilder()
        {
            var builderForm = new ModelBuilderForm();
            builderForm.ShowDialog();

            // After building, prompt to load the new model
            var result = MessageBox.Show(
                "Model training completed. Load the new model?",
                "Load Model",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                SelectExistingModel();
            }
        }

        private void MenuSelectModel_Click(object sender, EventArgs e)
        {
            SelectExistingModel();
        }

        private void MenuChangeBasePath_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select base folder for corrections and incremental training"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _basePath = dialog.SelectedPath;
                SaveConfiguration();
                InitializeTrainer();
                MessageBox.Show($"Base path updated to:\n{_basePath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // === Folder selection and prediction ===
        private void BtnSelectFolder_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select folder containing subfolders with images to classify"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                ProcessImages(dialog.SelectedPath);
            }
        }

        private void ProcessImages(string rootFolder)
        {
            if (_predictor == null)
            {
                MessageBox.Show("Model not loaded. Please configure a model first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            lblStatus.Text = "🔍 Scanning folders...";
            Application.DoEvents();

            var imageFiles = Directory.GetDirectories(rootFolder)
                .SelectMany(sub => Directory.GetFiles(sub, "*.*", SearchOption.TopDirectoryOnly))
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!imageFiles.Any())
            {
                MessageBox.Show("No images found in subfolders.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                lblStatus.Text = "❌ No images found";
                return;
            }

            _results.Clear();
            lblStatus.Text = $"📊 Found {imageFiles.Count} images. Starting prediction...";
            Application.DoEvents();

            int processed = 0;
            var startTime = DateTime.Now;

            foreach (var file in imageFiles)
            {
                string subfolder = new DirectoryInfo(Path.GetDirectoryName(file)).Name;
                string pred = _predictor.Predict(file);
                _results.Add(new ImageResult
                {
                    FileName = Path.GetFileName(file),
                    ImagePath = file,
                    Subfolder = subfolder,
                    PredictedLabel = pred,
                    CorrectedLabel = pred
                });

                processed++;
                if (processed % 10 == 0)
                {
                    lblStatus.Text = $"⚙️ Processing... {processed}/{imageFiles.Count} ({(processed * 100 / imageFiles.Count)}%)";
                    Application.DoEvents();
                }
            }

            var duration = DateTime.Now - startTime;
            lblStatus.Text = $"💾 Saving results...";
            Application.DoEvents();

            SaveResults(rootFolder);
            PopulateGrid(_results);
            PopulateFilters();
            UpdateCorrectionCount();

            var okCount = _results.Count(r => r.PredictedLabel == "OK");
            var ngCount = _results.Count(r => r.PredictedLabel == "NG");

            lblStatus.Text = $"✅ Processed {imageFiles.Count} images in {duration.TotalSeconds:F1}s\n" +
                           $"   OK: {okCount} | NG: {ngCount}";
        }

        private void SaveResults(string folder)
        {
            string csvPath = Path.Combine(folder, "predictions.csv");
            using var sw = new StreamWriter(csvPath);
            sw.WriteLine("FileName,ImagePath,SubFolder,Prediction");
            foreach (var r in _results)
                sw.WriteLine($"{r.FileName},{r.ImagePath},{r.Subfolder},{r.PredictedLabel}");
        }

        // === Filtering ===
        private void ApplyFilters(object sender, EventArgs e)
        {
            var subSel = cbFolderFilter.SelectedItem?.ToString();
            var predSel = cbPredFilter.SelectedItem?.ToString();

            var filtered = _results.Where(r =>
                (subSel == "All" || subSel == null || r.Subfolder == subSel) &&
                (predSel == "All" || predSel == null || r.PredictedLabel == predSel)
            ).ToList();

            PopulateGrid(filtered);
        }

        private void PopulateGrid(List<ImageResult> results)
        {
            grid.DataSource = results
                .Select(r => new
                {
                    r.FileName,
                    r.ImagePath,
                    r.Subfolder,
                    Prediction = r.PredictedLabel,
                    Corrected = r.CorrectedLabel
                }).ToList();
        }

        private void PopulateFilters()
        {
            cbFolderFilter.Items.Clear();
            cbPredFilter.Items.Clear();

            cbFolderFilter.Items.Add("All");
            foreach (var f in _results.Select(r => r.Subfolder).Distinct())
                cbFolderFilter.Items.Add(f);
            cbFolderFilter.SelectedIndex = 0;

            cbPredFilter.Items.Add("All");
            foreach (var p in _results.Select(r => r.PredictedLabel).Distinct())
                cbPredFilter.Items.Add(p);
            cbPredFilter.SelectedIndex = 0;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (grid.SelectedRows.Count > 0)
            {
                var path = grid.SelectedRows[0].Cells["ImagePath"].Value.ToString();
                if (File.Exists(path))
                {
                    pictureBox.Image?.Dispose();
                    pictureBox.Image = Image.FromFile(path);
                    lblInfo.Text = Path.GetFileName(path);
                }
            }
        }

        // === Correction ===
        private void BtnCorrect_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_basePath))
            {
                MessageBox.Show("Base path not configured. Please select a base folder from Settings menu.",
                    "Configuration Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a row to correct.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var row = grid.SelectedRows[0];
            var path = row.Cells["ImagePath"].Value.ToString();
            var result = _results.FirstOrDefault(r => r.ImagePath == path);
            var newLabel = cbCorrection.SelectedItem?.ToString();

            if (result == null || string.IsNullOrEmpty(newLabel))
            {
                MessageBox.Show("Select a valid label (OK/NG).", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (result.PredictedLabel == newLabel)
            {
                MessageBox.Show("The selected label is the same as the prediction. No correction needed.",
                    "No Change", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            result.CorrectedLabel = newLabel;

            string correctionPath = Path.Combine(_basePath, "corrections.csv");
            bool exists = File.Exists(correctionPath);
            using var sw = new StreamWriter(correctionPath, append: true);
            if (!exists)
                sw.WriteLine("FileName,ImagePath,SubFolder,PredictedLabel,CorrectedLabel");
            sw.WriteLine($"{result.FileName},{result.ImagePath},{result.Subfolder},{result.PredictedLabel},{result.CorrectedLabel}");

            MessageBox.Show($"✅ Correction saved\n{result.FileName} → {result.CorrectedLabel}",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateCorrectionCount();

            PopulateGrid(_results.Where(r =>
            {
                var subSel = cbFolderFilter.SelectedItem?.ToString();
                var predSel = cbPredFilter.SelectedItem?.ToString();
                return (subSel == "All" || subSel == null || r.Subfolder == subSel) &&
                       (predSel == "All" || predSel == null || r.PredictedLabel == predSel);
            }).ToList());
        }

        private void UpdateCorrectionCount()
        {
            if (string.IsNullOrEmpty(_basePath))
                return;

            var correctionPath = Path.Combine(_basePath, "corrections.csv");
            if (File.Exists(correctionPath))
            {
                int count = File.ReadLines(correctionPath).Count() - 1;
                lblCorrectionCount.Text = $"Corrections pending: {count}";
                lblCorrectionCount.BackColor = count > 0
                    ? Color.FromArgb(255, 248, 220)
                    : Color.FromArgb(220, 255, 220);
            }
            else
            {
                lblCorrectionCount.Text = "Corrections pending: 0";
                lblCorrectionCount.BackColor = Color.FromArgb(220, 255, 220);
            }
        }

        // === Quick Incremental Update ===
        private async void BtnQuickUpdate_Click(object sender, EventArgs e)
        {
            if (_incrementalTrainer == null)
            {
                MessageBox.Show("Trainer not initialized. Please configure base path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var correctionPath = Path.Combine(_basePath, "corrections.csv");
            if (!File.Exists(correctionPath))
            {
                MessageBox.Show("No corrections to apply.", "No Corrections", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Count corrections
            int correctionCount = File.ReadLines(correctionPath).Count() - 1;
            lblStatus.Text = $"⚡ Starting incremental update with {correctionCount} corrections...";
            Application.DoEvents();

            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            btnQuickUpdate.Enabled = false;
            btnRetrain.Enabled = false;

            // Capture console output
            var consoleOutput = new System.Text.StringBuilder();
            var originalOut = Console.Out;
            var stringWriter = new StringWriter(consoleOutput);
            var multiWriter = new MultiTextWriter(originalOut, stringWriter);

            try
            {
                Console.SetOut(multiWriter);

                string newModelPath = await Task.Run(() =>
                    _incrementalTrainer.IncrementalTrain(_modelPath));

                _modelPath = newModelPath;
                SaveConfiguration();

                lblStatus.Text = "📥 Reloading updated model...";
                Application.DoEvents();

                LoadModel();

                var archivePath = Path.Combine(_basePath, $"corrections_archive_{DateTime.Now:yyyyMMddHHmmss}.csv");
                File.Copy(correctionPath, archivePath);
                File.Delete(correctionPath);

                lblStatus.Text = $"✅ Incremental update completed!\n" +
                               $"   Model: {Path.GetFileName(newModelPath)}\n" +
                               $"   Applied {correctionCount} corrections";
                UpdateCorrectionCount();

                MessageBox.Show($"Quick update completed successfully!\n\n" +
                              $"• Corrections applied: {correctionCount}\n" +
                              $"• New model: {Path.GetFileName(newModelPath)}\n" +
                              $"• Training log has been captured",
                              "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Optionally show detailed log
                if (MessageBox.Show("Would you like to see the detailed training log?",
                    "Training Log", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var logForm = new Form
                    {
                        Text = "Incremental Training Log",
                        Width = 800,
                        Height = 600,
                        StartPosition = FormStartPosition.CenterParent
                    };
                    var txtLog = new TextBox
                    {
                        Multiline = true,
                        ScrollBars = ScrollBars.Vertical,
                        Dock = DockStyle.Fill,
                        Font = new Font("Consolas", 9),
                        Text = consoleOutput.ToString()
                    };
                    logForm.Controls.Add(txtLog);
                    logForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Incremental update failed: {ex.Message}";
                MessageBox.Show($"Update failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Console.SetOut(originalOut);
                stringWriter.Dispose();
                progressBar.Visible = false;
                btnQuickUpdate.Enabled = true;
                btnRetrain.Enabled = true;
            }
        }

        // Helper class for capturing console output
        private class MultiTextWriter : TextWriter
        {
            private readonly TextWriter[] _writers;

            public MultiTextWriter(params TextWriter[] writers)
            {
                _writers = writers;
            }

            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

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

        // === Full Retrain ===
        private async void BtnRetrain_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_basePath))
            {
                MessageBox.Show("Base path not configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var result = MessageBox.Show(
                "Full retrain will take several minutes and processes the entire dataset.\n\n" +
                "Quick Update is much faster and recommended for small corrections.\n\n" +
                "Continue with full retrain?",
                "Confirm Full Retrain",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            lblStatus.Text = "🔄 Full retraining (this may take several minutes)...";
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            btnQuickUpdate.Enabled = false;
            btnRetrain.Enabled = false;

            await Task.Run(() =>
            {
                try
                {
                    var trainer = new ModelTrainer(new Microsoft.ML.MLContext(), _basePath);
                    trainer.TrainAndEvaluate();
                }
                catch (Exception ex)
                {
                    Invoke(new Action(() =>
                    {
                        lblStatus.Text = $"❌ Full retrain failed: {ex.Message}";
                        MessageBox.Show($"Retrain failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });

            progressBar.Visible = false;
            lblStatus.Text = "✅ Full retrain completed! Reloading model...";
            btnQuickUpdate.Enabled = true;
            btnRetrain.Enabled = true;

            LoadModel();
            MessageBox.Show("Full retrain completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    public class ImageResult
    {
        public string? FileName { get; set; }
        public string? ImagePath { get; set; }
        public string? Subfolder { get; set; }
        public string? PredictedLabel { get; set; }
        public string? CorrectedLabel { get; set; }
    }
}