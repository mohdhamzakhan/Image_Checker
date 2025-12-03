using Image_Checker.Services;
using Microsoft.ML;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Image_Checker.WinForm
{
    public partial class ModelBuilderForm : Form
    {
        private string _datasetPath;
        private string _outputPath;
        private ConsoleRedirector _consoleRedirector;

        public ModelBuilderForm()
        {
            InitializeComponent();
            _consoleRedirector = new ConsoleRedirector(txtTrainingLog);
        }

        private void BtnSelectDataset_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select dataset folder containing OK and NG subfolders"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _datasetPath = dialog.SelectedPath;
                txtDatasetPath.Text = _datasetPath;
                LogMessage($"📁 Dataset path selected: {_datasetPath}");
                ValidateDataset();
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

        private void ValidateDataset()
        {
            if (string.IsNullOrEmpty(_datasetPath))
                return;

            LogMessage("🔍 Validating dataset structure...");

            var okDir = Path.Combine(_datasetPath, "OK");
            var ngDir = Path.Combine(_datasetPath, "NG");

            bool hasOK = Directory.Exists(okDir);
            bool hasNG = Directory.Exists(ngDir);

            if (hasOK && hasNG)
            {
                LogMessage("✅ Found OK and NG folders");

                int okCount = Directory.GetFiles(okDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Count(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

                int ngCount = Directory.GetFiles(ngDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Count(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

                LogMessage($"📊 OK folder: {okCount} images");
                LogMessage($"📊 NG folder: {ngCount} images");
                LogMessage($"📊 Total: {okCount + ngCount} images");

                lblDatasetInfo.Text = $"✅ Valid dataset\nOK: {okCount} images | NG: {ngCount} images | Total: {okCount + ngCount}";
                lblDatasetInfo.ForeColor = System.Drawing.Color.Green;
                btnStartTraining.Enabled = true;

                if (okCount < 10 || ngCount < 10)
                {
                    LogMessage("⚠️ Warning: Less than 10 images per class. More data recommended for better accuracy.", System.Drawing.Color.Orange);
                }
            }
            else
            {
                LogMessage($"❌ Invalid dataset structure", System.Drawing.Color.Red);
                LogMessage($"   Expected: OK/ and NG/ subfolders", System.Drawing.Color.Red);
                LogMessage($"   Found: OK={hasOK}, NG={hasNG}", System.Drawing.Color.Red);

                lblDatasetInfo.Text = $"❌ Invalid dataset\nRequired: 'OK' and 'NG' subfolders\nFound: OK={hasOK}, NG={hasNG}";
                lblDatasetInfo.ForeColor = System.Drawing.Color.Red;
                btnStartTraining.Enabled = false;
            }
        }

        private async void BtnStartTraining_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_datasetPath))
            {
                MessageBox.Show("Please select a dataset folder.", "Missing Dataset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_outputPath))
            {
                _outputPath = _datasetPath;
                txtOutputPath.Text = _outputPath;
                LogMessage($"💾 Output path not specified, using dataset folder: {_outputPath}");
            }

            // Disable controls
            btnSelectDataset.Enabled = false;
            btnSelectOutput.Enabled = false;
            btnStartTraining.Enabled = false;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;

            LogMessage("", System.Drawing.Color.White);
            LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Cyan);
            LogMessage("🚀 STARTING MODEL TRAINING", System.Drawing.Color.Cyan);
            LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Cyan);

            try
            {
                await Task.Run(() => TrainModel());

                LogMessage("", System.Drawing.Color.White);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Green);
                LogMessage("✅ MODEL TRAINING COMPLETED SUCCESSFULLY!", System.Drawing.Color.Green);
                LogMessage("═══════════════════════════════════════════════", System.Drawing.Color.Green);

                MessageBox.Show("Model training completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                // Re-enable controls
                btnSelectDataset.Enabled = true;
                btnSelectOutput.Enabled = true;
                btnStartTraining.Enabled = true;
                progressBar.Visible = false;
            }
        }

        private void TrainModel()
        {
            LogMessage("📝 Step 1: Creating CSV dataset from image folders...");
            DataValidator.CreateCsv(_datasetPath);
            LogMessage("✅ CSV dataset created successfully");

            var csvPath = Path.Combine(_datasetPath, "images.csv");
            LogMessage($"📄 CSV file: {csvPath}");

            LogMessage("");
            LogMessage("📊 Step 2: Analyzing dataset distribution...");
            DataValidator.PrintLabelDistribution(csvPath);

            LogMessage("");
            LogMessage("🧠 Step 3: Initializing ML.NET context...");
            var mlContext = new MLContext(seed: 42);
            LogMessage("✅ ML Context initialized with seed=42");

            LogMessage("");
            LogMessage("🔧 Step 4: Building and training models...");
            LogMessage("   This may take several minutes depending on dataset size...");

            // Redirect console output to capture training details
            _consoleRedirector.Start();

            try
            {
                var trainer = new ModelTrainer(mlContext, _datasetPath);
                trainer.TrainAndEvaluate();
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _consoleRedirector?.Stop();
            base.OnFormClosing(e);
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