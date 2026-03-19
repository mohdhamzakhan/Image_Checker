using Image_Checker.Services;
using System.IO;
using System.Text;

namespace Image_Checker.WinForm
{
    public partial class Form1 : Form
    {
        private Predictor _predictor;
        private List<ImageResult> _results = new();
        private string _basePath;
        private string _modelPath;
        private IncrementalModelTrainer _incrementalTrainer;
        private CorrectionManager _correctionManager;
        private const string CONFIG_FILE = "app_config.txt";
        private Button btnManageCorrections;
        private SingleImagePredictor _singlePredictor;
        private FileSystemWatcher _folderWatcher;
        private string _monitoredFolder;
        private System.Windows.Forms.Timer _processingTimer;
        private Queue<string> _newImageQueue = new Queue<string>();
        private bool _isProcessingQueue = false;
        // Add these fields at the top of Form1 class
        private UsbLightController _usbLight;
        //private Label lblUsbLightStatus; // You'll need to add this to your form designer
        private UsbPortPowerController _usbPortController;

        private int _abnormalCount = 0;
        private int _criticalMissCount = 0;
        private int _falseAlarmCount = 0;
        public Form1()
        {
            InitializeComponent();
            LoadConfiguration();

            _usbLight = new UsbLightController();
        }

        // Add USB light connection menu/button
        private void MenuConnectUsbLight_Click(object sender, EventArgs e)
        {
            ConnectUsbLight();
        }

        private void MenuCorrections_Click(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void MenuManageCorrections_Click(object sender, EventArgs e)
        {
            OpenCorrectionsManager();
        }

        private void BtnManageCorrections_Click(object sender, EventArgs e)
        {
            OpenCorrectionsManager();
        }

        private void BtnTestUsbPortControl_Click(object sender, EventArgs e)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("STARTING USB PORT POWER CONTROL TEST");
            Console.WriteLine(new string('=', 60) + "\n");

            // Run full diagnostics
            UsbPortPowerController.RunDiagnostics();

            MessageBox.Show(
                "Diagnostic test complete!\n\n" +
                "Check the console output for detailed results.\n\n" +
                "If successful, you can proceed to test actual port control.",
                "Diagnostics Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void BtnCheckUsbSupport_Click(object sender, EventArgs e)
        {
            bool supported = UsbPortPowerController.IsSupported();

            if (supported)
            {
                MessageBox.Show(
                    "✅ USB Port Power Control MAY be supported!\n\n" +
                    "Hubs detected on your system.\n\n" +
                    "Note: Final confirmation requires actual testing.\n" +
                    "Not all hubs support per-port power control.",
                    "Possibly Supported",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    "❌ USB Port Power Control is NOT supported\n\n" +
                    "No USB hubs detected on your system.\n\n" +
                    "Recommended alternatives:\n" +
                    "• Arduino + Relay (most reliable)\n" +
                    "• USB smart plugs\n" +
                    "• DevCon tool for device disable/enable",
                    "Not Supported",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void BtnConnectUsbHub_Click(object sender, EventArgs e)
        {
            if (_usbPortController != null && _usbPortController.IsConnected)
            {
                MessageBox.Show(
                    "Already connected to USB hub.\n\n" +
                    $"Device: {_usbPortController.DevicePath}",
                    "Already Connected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _usbPortController = new UsbPortPowerController();

            if (_usbPortController.AutoConnect())
            {
                MessageBox.Show(
                    $"✅ Connected to USB hub!\n\n" +
                    $"Device: {_usbPortController.DevicePath}\n\n" +
                    "You can now test port control.",
                    "Connection Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    "❌ Failed to connect to USB hub\n\n" +
                    "Make sure you're running as Administrator!\n\n" +
                    "Right-click the application → Run as Administrator",
                    "Connection Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                _usbPortController?.Dispose();
                _usbPortController = null;
            }
        }

        private void BtnTestConnection_Click(object sender, EventArgs e)
        {
            if (_usbPortController == null || !_usbPortController.IsConnected)
            {
                MessageBox.Show(
                    "Not connected to USB hub.\n\n" +
                    "Click 'Connect to USB Hub' first.",
                    "Not Connected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            bool valid = _usbPortController.TestConnection();

            if (valid)
            {
                MessageBox.Show(
                    "✅ Connection test PASSED!\n\n" +
                    $"Device: {_usbPortController.DevicePath}\n\n" +
                    "Connection is valid and ready for use.\n" +
                    "Check console for detailed info.",
                    "Test Passed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    "❌ Connection test FAILED\n\n" +
                    "Connection is not valid.\n" +
                    "Try reconnecting.",
                    "Test Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BtnCyclePort_Click(object sender, EventArgs e)
        {
            if (_usbPortController == null || !_usbPortController.IsConnected)
            {
                MessageBox.Show(
                    "Not connected to USB hub.\n\n" +
                    "Click 'Connect to USB Hub' first.",
                    "Not Connected",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Get port number from user
            var inputDialog = new Form
            {
                Text = "Enter USB Port Number",
                Width = 400,
                Height = 200,
                StartPosition = FormStartPosition.CenterParent
            };

            var label = new Label
            {
                Text = "Enter the USB port number to cycle (1-8):\n\n" +
                       "⚠️ WARNING: This will disconnect any device on that port!\n" +
                       "The device should reconnect automatically.",
                AutoSize = false,
                Width = 350,
                Height = 80,
                Left = 20,
                Top = 20
            };

            var textBox = new TextBox
            {
                Text = "1",
                Left = 20,
                Top = 110,
                Width = 100
            };

            var buttonOk = new Button
            {
                Text = "Cycle Port",
                Left = 130,
                Top = 105,
                DialogResult = DialogResult.OK
            };

            var buttonCancel = new Button
            {
                Text = "Cancel",
                Left = 230,
                Top = 105,
                DialogResult = DialogResult.Cancel
            };

            inputDialog.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            inputDialog.AcceptButton = buttonOk;
            inputDialog.CancelButton = buttonCancel;

            if (inputDialog.ShowDialog() == DialogResult.OK)
            {
                if (int.TryParse(textBox.Text, out int portNumber))
                {
                    if (portNumber < 1 || portNumber > 8)
                    {
                        MessageBox.Show(
                            "Invalid port number.\n\n" +
                            "Please enter a number between 1 and 8.",
                            "Invalid Input",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    // Final confirmation
                    var confirm = MessageBox.Show(
                        $"⚠️ CONFIRM PORT CYCLE\n\n" +
                        $"You are about to cycle USB port {portNumber}.\n\n" +
                        $"This will:\n" +
                        $"• Disconnect any device on port {portNumber}\n" +
                        $"• The device should reconnect in 2-3 seconds\n\n" +
                        $"Do NOT cycle ports with:\n" +
                        $"• Keyboard or mouse\n" +
                        $"• Critical storage devices\n\n" +
                        $"Continue?",
                        "Confirm Port Cycle",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (confirm == DialogResult.Yes)
                    {
                        lblStatus.Text = $"⚡ Cycling USB port {portNumber}...";
                        Application.DoEvents();

                        bool success = _usbPortController.CyclePort(portNumber);

                        if (success)
                        {
                            lblStatus.Text = $"✅ USB port {portNumber} cycled successfully";
                            MessageBox.Show(
                                $"✅ Port {portNumber} cycled successfully!\n\n" +
                                "The device should reconnect automatically.\n" +
                                "Check the console for detailed output.",
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            lblStatus.Text = $"❌ Failed to cycle USB port {portNumber}";
                            MessageBox.Show(
                                $"❌ Failed to cycle port {portNumber}\n\n" +
                                "Possible reasons:\n" +
                                "• Port number doesn't exist\n" +
                                "• Hub doesn't support per-port control\n" +
                                "• Insufficient permissions\n" +
                                "• Wrong hub selected\n\n" +
                                "Check console for error details.\n\n" +
                                "Try using Arduino + Relay instead.",
                                "Failed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Invalid port number format.\n\n" +
                        "Please enter a valid number.",
                        "Invalid Input",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private void BtnDisconnectUsbHub_Click(object sender, EventArgs e)
        {
            if (_usbPortController == null)
            {
                MessageBox.Show("Not connected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _usbPortController.Dispose();
            _usbPortController = null;

            lblStatus.Text = "⏹️ Disconnected from USB hub";
            MessageBox.Show(
                "Disconnected from USB hub.",
                "Disconnected",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }


        private void ConnectUsbLight()
        {
            if (_usbLight.IsConnected)
            {
                MessageBox.Show("USB Light is already connected.",
                    "Already Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Try to connect
            bool connected = _usbLight.Connect();

            if (connected)
            {
                lblUsbLightStatus.Text = $"💡 USB Light: Connected ({_usbLight.PortName})";
                lblUsbLightStatus.BackColor = Color.FromArgb(220, 255, 220); // Light green

                // Test blink
                MessageBox.Show(
                    $"USB Light connected successfully on {_usbLight.PortName}!\n\n" +
                    "The light will blink 3 times to confirm.",
                    "Connection Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _usbLight.Blink(3);
            }
            else
            {
                lblUsbLightStatus.Text = "💡 USB Light: Not Connected";
                lblUsbLightStatus.BackColor = Color.FromArgb(255, 220, 220); // Light red

                MessageBox.Show(
                    "Failed to connect USB light.\n\n" +
                    "Troubleshooting:\n" +
                    "• Check if USB device is plugged in\n" +
                    "• Verify COM port is available\n" +
                    "• Check device drivers are installed\n" +
                    "• Try reconnecting the USB device",
                    "Connection Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void OpenCorrectionsManager()
        {
            if (string.IsNullOrEmpty(_basePath))
            {
                MessageBox.Show(
                    "Base path not configured.\n\n" +
                    "Please load a model or configure the base path from the Settings menu.",
                    "Configuration Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var correctionsPath = Path.Combine(_basePath, "corrections.csv");
            if (!File.Exists(correctionsPath))
            {
                var result = MessageBox.Show(
                    "No corrections file found.\n\n" +
                    "Would you like to create an empty corrections file?",
                    "No Corrections",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        File.WriteAllLines(correctionsPath, new[]
                        {
                    "Timestamp,ImagePath,OriginalLabel,Confidence,CorrectedLabel"
                });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to create corrections file:\n{ex.Message}",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            // Open the Corrections Manager form
            var managerForm = new CorrectionsManagerForm(_basePath);
            managerForm.FormClosed += (s, e) =>
            {
                // Refresh correction count after closing the manager
                UpdateCorrectionCount();
            };
            managerForm.ShowDialog(this);
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

        private void MenuVerifySetup_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_basePath))
            {
                MessageBox.Show("Base path not configured.", "Setup Check",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var checks = new StringBuilder();
            checks.AppendLine("🔍 TRUE INCREMENTAL LEARNING SETUP CHECK\n");

            // Check 1: Model exists
            bool hasModel = !string.IsNullOrEmpty(_modelPath) && File.Exists(_modelPath);
            checks.AppendLine(hasModel
                ? "✅ Model loaded: " + Path.GetFileName(_modelPath)
                : "❌ No model loaded");

            // Check 2: images.csv exists
            var csvPath = Path.Combine(_basePath, "images.csv");
            bool hasCsv = File.Exists(csvPath);
            checks.AppendLine(hasCsv
                ? "✅ Original training data found (images.csv)"
                : "❌ Missing images.csv - True incremental will not work!");

            // Check 3: corrections.csv exists
            var correctionsPath = Path.Combine(_basePath, "corrections.csv");
            bool hasCorrections = File.Exists(correctionsPath);
            int correctionCount = hasCorrections ? _correctionManager.GetCorrectionCount() : 0;
            checks.AppendLine(hasCorrections
                ? $"✅ Corrections file exists ({correctionCount} pending)"
                : "ℹ️ No corrections yet");

            // Overall status
            checks.AppendLine();
            if (hasModel && hasCsv)
            {
                checks.AppendLine("✅ READY FOR TRUE INCREMENTAL LEARNING!");
            }
            else if (hasModel && !hasCsv)
            {
                checks.AppendLine("⚠️ WARNING: Missing original training data");
                checks.AppendLine($"\nTo fix: Copy images.csv to:\n{_basePath}");
            }
            else
            {
                checks.AppendLine("❌ Setup incomplete - load a model first");
            }

            MessageBox.Show(checks.ToString(), "Setup Verification",
                MessageBoxButtons.OK,
                hasModel && hasCsv ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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
                _correctionManager = new CorrectionManager(_basePath);
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

                var cfgPath = Path.ChangeExtension(_modelPath, ".json");
                if (File.Exists(cfgPath))
                {
                    var json = File.ReadAllText(cfgPath);
                    var cfg = System.Text.Json.JsonSerializer.Deserialize<RoiConfig>(json);
                    _roiRect = new Rectangle(cfg.RoiX, cfg.RoiY, cfg.RoiW, cfg.RoiH);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Model load failed: {ex.Message}";
            }
        }

        private string PreprocessForPrediction(string imagePath)
        {
            // If ROI is not set, just use original
            if (_roiRect == Rectangle.Empty)
                return imagePath;

            using var bmp = new Bitmap(imagePath);

            int x = Math.Max(0, Math.Min(_roiRect.X, bmp.Width - _roiRect.Width));
            int y = Math.Max(0, Math.Min(_roiRect.Y, bmp.Height - _roiRect.Height));
            var safeRoi = new Rectangle(
                x,
                y,
                Math.Min(_roiRect.Width, bmp.Width),
                Math.Min(_roiRect.Height, bmp.Height));

            using var cropped = bmp.Clone(safeRoi, bmp.PixelFormat);

            // Here you could also resize to training size if needed

            string tmpPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
            cropped.Save(tmpPath, System.Drawing.Imaging.ImageFormat.Png);
            return tmpPath;
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

                // Use PredictWithConfidence to get both label and confidence
                var (pred, confidence) = _predictor.PredictWithConfidence(file);

                // Clean the prediction label - remove any quotes
                string cleanPred = pred?.Trim().Trim('"') ?? "Unknown";

                _results.Add(new ImageResult
                {
                    FileName = Path.GetFileName(file),
                    ImagePath = file,
                    Subfolder = subfolder,
                    PredictedLabel = cleanPred,
                    CorrectedLabel = cleanPred,
                    Confidence = confidence
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
            PopulateCorrectionComboBox(); // ← Add this line
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
            sw.WriteLine("FileName,ImagePath,SubFolder,Prediction,Confidence");

            foreach (var r in _results)
            {
                // Escape CSV values properly - only wrap in quotes if they contain commas
                string fileName = EscapeCsvValue(r.FileName);
                string imagePath = EscapeCsvValue(r.ImagePath);
                string subfolder = EscapeCsvValue(r.Subfolder);
                string prediction = r.PredictedLabel; // No quotes needed for OK/NG

                sw.WriteLine($"{fileName},{imagePath},{subfolder},{prediction},{r.Confidence:F4}");
            }
        }

        /// <summary>
        /// Escapes a CSV value by wrapping it in quotes only if it contains commas, quotes, or newlines
        /// </summary>
        private string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Remove any existing quotes from the value first
            value = value.Trim('"');

            // Only wrap in quotes if the value contains commas, quotes, or newlines
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                // Escape any internal quotes by doubling them
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            return value;
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
                    Confidence = $"{r.Confidence:P2}",
                    Corrected = r.CorrectedLabel
                }).ToList();
        }

        private void PopulateFilters()
        {
            cbFolderFilter.Items.Clear();
            cbPredFilter.Items.Clear();

            // Folder filter
            cbFolderFilter.Items.Add("All");
            var uniqueFolders = _results
                .Select(r => r.Subfolder)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s);

            foreach (var f in uniqueFolders)
                cbFolderFilter.Items.Add(f);

            cbFolderFilter.SelectedIndex = 0;

            // Prediction filter - FIXED: Remove duplicates
            cbPredFilter.Items.Add("All");
            var uniquePredictions = _results
                .Select(r => r.PredictedLabel)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()                    // ← This removes duplicates
                .OrderBy(s => s);              // ← Optional: sort alphabetically

            foreach (var p in uniquePredictions)
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

                    // Load image without locking the file
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        pictureBox.Image = Image.FromStream(fs);
                    }

                    var result = _results.FirstOrDefault(r => r.ImagePath == path);
                    if (result != null)
                    {
                        lblInfo.Text = $"{Path.GetFileName(path)} | Prediction: {result.PredictedLabel} ({result.Confidence:P2})";
                    }
                    else
                    {
                        lblInfo.Text = Path.GetFileName(path);
                    }
                }
            }
        }

        private void BtnCorrect_Click(object sender, EventArgs e)
        {
            if (_correctionManager == null)
            {
                MessageBox.Show("Correction manager not initialized. Please configure base path from Settings menu.",
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

            // Get the label and remove any quotes
            var newLabel = cbCorrection.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(newLabel))
            {
                newLabel = newLabel.Trim().Trim('"');
            }

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

            // Disable button while saving
            btnCorrect.Enabled = false;
            btnCorrect.Text = "Saving...";
            lblStatus.Text = "💾 Saving correction...";
            Application.DoEvents();

            try
            {
                bool success = _correctionManager.SaveCorrection(
                    result.ImagePath,
                    result.PredictedLabel,
                    result.Confidence,
                    newLabel,
                    out string errorMessage);

                if (success)
                {
                    // Update the result in memory
                    result.CorrectedLabel = newLabel;

                    // Show success message
                    lblStatus.Text = $"✅ Correction saved: {result.FileName} → {newLabel}";
                    MessageBox.Show($"✅ Correction saved successfully!\n\n" +
                                  $"File: {result.FileName}\n" +
                                  $"Original: {result.PredictedLabel} ({result.Confidence:P2})\n" +
                                  $"Corrected to: {newLabel}",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Update UI
                    UpdateCorrectionCount();

                    // Refresh grid to show updated actual classification
                    var currentFilter = new
                    {
                        Subfolder = cbFolderFilter.SelectedItem?.ToString(),
                        Prediction = cbPredFilter.SelectedItem?.ToString()
                    };

                    var filtered = _results.Where(r =>
                        (currentFilter.Subfolder == "All" || string.IsNullOrEmpty(currentFilter.Subfolder) || r.Subfolder == currentFilter.Subfolder) &&
                        (currentFilter.Prediction == "All" || string.IsNullOrEmpty(currentFilter.Prediction) || r.PredictedLabel == currentFilter.Prediction)
                    ).ToList();

                    PopulateGrid(filtered);

                    // Re-select the same row if possible
                    foreach (DataGridViewRow gridRow in grid.Rows)
                    {
                        if (gridRow.Cells["ImagePath"].Value?.ToString() == path)
                        {
                            gridRow.Selected = true;
                            grid.FirstDisplayedScrollingRowIndex = gridRow.Index;
                            break;
                        }
                    }
                }
                else
                {
                    lblStatus.Text = "❌ Failed to save correction";
                    MessageBox.Show($"Failed to save correction:\n\n{errorMessage}\n\n" +
                                  "Possible causes:\n" +
                                  "• The corrections.csv file is open in Excel or another program\n" +
                                  "• The file is being used by another process\n" +
                                  "• Insufficient disk space or permissions\n\n" +
                                  "Please close any programs using the file and try again.",
                        "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "❌ Unexpected error during correction save";
                MessageBox.Show($"Unexpected error while saving correction:\n\n{ex.Message}\n\n" +
                              "Please check the application logs for more details.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCorrect.Enabled = true;
                btnCorrect.Text = "Save Correction";
            }
        }
        // === BONUS: Add visual indicator for corrected items ===

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (grid.Columns[e.ColumnIndex].Name == "ActualClass" ||
                grid.Columns[e.ColumnIndex].Name == "ActualLabel")
            {
                var row = grid.Rows[e.RowIndex];
                var imagePath = row.Cells["ImagePath"].Value?.ToString();
                var result = _results.FirstOrDefault(r => r.ImagePath == imagePath);

                if (result != null && result.CorrectedLabel != result.PredictedLabel)
                {
                    // Highlight corrected rows
                    e.CellStyle.BackColor = Color.FromArgb(255, 248, 220); // Light yellow
                    e.CellStyle.ForeColor = Color.DarkOrange;
                    e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
                }
            }
        }

        /// <summary>
        /// Safely reads all lines from a file with retry logic
        /// </summary>
        private List<string> ReadFileWithRetry(string filePath, int maxRetries = 3, int delayMs = 500)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        var lines = new List<string>();
                        while (!sr.EndOfStream)
                        {
                            lines.Add(sr.ReadLine());
                        }
                        return lines;
                    }
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    Console.WriteLine($"⏳ File is locked, retrying in {delayMs}ms... (attempt {i + 1}/{maxRetries})");
                    Thread.Sleep(delayMs);
                }
            }
            throw new IOException($"Could not access file after {maxRetries} attempts: {filePath}");
        }

        /// <summary>
        /// Safely writes all lines to a file with retry logic
        /// </summary>
        private void WriteFileWithRetry(string filePath, IEnumerable<string> lines, int maxRetries = 3, int delayMs = 500)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs))
                    {
                        foreach (var line in lines)
                        {
                            sw.WriteLine(line);
                        }
                    }
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    Console.WriteLine($"⏳ File is locked, retrying in {delayMs}ms... (attempt {i + 1}/{maxRetries})");
                    Thread.Sleep(delayMs);
                }
            }
            throw new IOException($"Could not write to file after {maxRetries} attempts: {filePath}");
        }



        private void UpdateCorrectionCount()
        {
            if (_correctionManager == null)
            {
                lblCorrectionCount.Text = "Corrections pending: N/A";
                lblCorrectionCount.BackColor = Color.LightGray;
                return;
            }

            try
            {
                int count = _correctionManager.GetCorrectionCount();
                lblCorrectionCount.Text = $"Corrections pending: {count}";
                lblCorrectionCount.BackColor = count > 0
                    ? Color.FromArgb(255, 248, 220) // Light yellow
                    : Color.FromArgb(220, 255, 220); // Light green
            }
            catch (Exception ex)
            {
                lblCorrectionCount.Text = "Corrections pending: Error";
                lblCorrectionCount.BackColor = Color.FromArgb(255, 220, 220); // Light red
                Console.WriteLine($"Error updating correction count: {ex.Message}");
            }
        }

        /// <summary>
        /// Moves corrected images to their designated folders based on corrections.csv
        /// </summary>
        private bool MoveCorrectionsToFolders(out string statusMessage)
        {
            statusMessage = "";
            var correctionPath = Path.Combine(_basePath, "corrections.csv");

            if (!File.Exists(correctionPath))
            {
                statusMessage = "No corrections file found.";
                return false;
            }

            List<string> lines;
            try
            {
                lines = ReadFileWithRetry(correctionPath);
                if (lines.Count <= 1) // Only header or empty
                {
                    statusMessage = "No corrections to move.";
                    return true;
                }
                lines = lines.Skip(1).ToList(); // Skip header
            }
            catch (Exception ex)
            {
                statusMessage = $"Failed to read corrections file: {ex.Message}";
                return false;
            }

            int movedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;
            var errorMessages = new List<string>();
            var movedFiles = new List<(string oldPath, string newPath)>();

            Console.WriteLine($"\n📦 Moving {lines.Count} corrected images to designated folders...\n");

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var parts = line.Split(',');
                    if (parts.Length < 5)
                    {
                        Console.WriteLine($"⚠️  Invalid line format, skipping");
                        skippedCount++;
                        continue;
                    }

                    var imagePath = parts[1].Trim().Trim('"');
                    var correctedLabel = parts[4].Trim().Trim('"');

                    if (!File.Exists(imagePath))
                    {
                        Console.WriteLine($"⚠️  File not found: {Path.GetFileName(imagePath)}");
                        skippedCount++;
                        continue;
                    }

                    // Get current folder and target folder
                    var currentFolder = Path.GetDirectoryName(imagePath);
                    var parentFolder = Directory.GetParent(currentFolder)?.FullName;

                    if (string.IsNullOrEmpty(parentFolder))
                    {
                        Console.WriteLine($"⚠️  Cannot determine parent folder for: {imagePath}");
                        skippedCount++;
                        continue;
                    }

                    var currentFolderName = new DirectoryInfo(currentFolder).Name;
                    var targetFolder = Path.Combine(parentFolder, correctedLabel);

                    // Check if already in correct folder
                    if (currentFolderName.Equals(correctedLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"✓  Already in correct folder: {Path.GetFileName(imagePath)} ({correctedLabel})");
                        skippedCount++;
                        continue;
                    }

                    // Create target folder if it doesn't exist
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                        Console.WriteLine($"📁 Created folder: {correctedLabel}");
                    }

                    // Move the file
                    var fileName = Path.GetFileName(imagePath);
                    var targetPath = Path.Combine(targetFolder, fileName);

                    // Handle duplicate filenames
                    if (File.Exists(targetPath))
                    {
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        var extension = Path.GetExtension(fileName);
                        var counter = 1;

                        while (File.Exists(targetPath))
                        {
                            fileName = $"{nameWithoutExt}_corrected{counter}{extension}";
                            targetPath = Path.Combine(targetFolder, fileName);
                            counter++;
                        }
                        Console.WriteLine($"⚠️  Duplicate filename, renaming to: {fileName}");
                    }

                    // Ensure file is not locked before moving
                    try
                    {
                        using (var fs = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            // File is accessible
                        }
                    }
                    catch (IOException)
                    {
                        Console.WriteLine($"⚠️  File is locked, skipping: {Path.GetFileName(imagePath)}");
                        skippedCount++;
                        continue;
                    }

                    File.Move(imagePath, targetPath);
                    movedFiles.Add((imagePath, targetPath));

                    Console.WriteLine($"✅ Moved: {Path.GetFileName(imagePath)}");
                    Console.WriteLine($"   From: {currentFolderName} → To: {correctedLabel}");
                    movedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    var fileName = "Unknown";
                    try
                    {
                        var parts = line.Split(',');
                        if (parts.Length > 1)
                            fileName = Path.GetFileName(parts[1].Trim().Trim('"'));
                    }
                    catch { }

                    errorMessages.Add($"{fileName}: {ex.Message}");
                    Console.WriteLine($"❌ Error moving {fileName}: {ex.Message}");
                }
            }

            // Update corrections.csv with new paths
            if (movedFiles.Count > 0)
            {
                try
                {
                    UpdateCorrectionsWithNewPaths(movedFiles, correctionPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Warning: Could not update corrections.csv: {ex.Message}");
                    Console.WriteLine($"   This won't affect training, but archive may have old paths.");
                }
            }

            Console.WriteLine($"\n📊 Move Summary:");
            Console.WriteLine($"   ✅ Moved: {movedCount}");
            Console.WriteLine($"   ⚠️  Skipped: {skippedCount}");
            Console.WriteLine($"   ❌ Errors: {errorCount}");

            statusMessage = $"Moved: {movedCount}, Skipped: {skippedCount}, Errors: {errorCount}";

            if (errorMessages.Any())
            {
                statusMessage += $"\n\nErrors:\n{string.Join("\n", errorMessages.Take(5))}";
                if (errorMessages.Count > 5)
                    statusMessage += $"\n... and {errorMessages.Count - 5} more";
            }

            return errorCount == 0;
        }

        /// <summary>
        /// Updates corrections.csv with new file paths after moving
        /// </summary>
        private void UpdateCorrectionsWithNewPaths(List<(string oldPath, string newPath)> movedFiles, string correctionPath)
        {
            try
            {
                // Read current CSV with retry
                var lines = ReadFileWithRetry(correctionPath);

                if (lines.Count == 0)
                {
                    Console.WriteLine($"⚠️  Corrections file is empty, skipping update");
                    return;
                }

                // Create a dictionary of old path -> new path
                var pathMap = movedFiles.ToDictionary(
                    f => f.oldPath,
                    f => f.newPath,
                    StringComparer.OrdinalIgnoreCase);

                // Update paths in CSV (skip header at index 0)
                for (int i = 1; i < lines.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    var parts = lines[i].Split(',');
                    if (parts.Length >= 2)
                    {
                        var oldPath = parts[1].Trim().Trim('"');
                        if (pathMap.ContainsKey(oldPath))
                        {
                            // Update the path while preserving CSV format
                            parts[1] = pathMap[oldPath].Contains(',')
                                ? $"\"{pathMap[oldPath]}\""
                                : pathMap[oldPath];
                            lines[i] = string.Join(",", parts);
                        }
                    }
                }

                // Write updated CSV with retry
                WriteFileWithRetry(correctionPath, lines);
                Console.WriteLine($"✅ Updated corrections.csv with new paths ({movedFiles.Count} entries)");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update corrections.csv: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// Updates corrections.csv with new file paths after moving
        /// </summary>
        private void UpdateCorrectionsWithNewPaths(List<string> movedFiles)
        {
            try
            {
                var correctionPath = Path.Combine(_basePath, "corrections.csv");
                var lines = File.ReadAllLines(correctionPath).ToList();

                // Create a dictionary of old path -> new path
                var pathMap = movedFiles
                    .Select(f => f.Split('|'))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => parts[0], parts => parts[1]);

                // Update paths in CSV
                for (int i = 1; i < lines.Count; i++) // Skip header
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length >= 2)
                    {
                        var oldPath = parts[1].Trim();
                        if (pathMap.ContainsKey(oldPath))
                        {
                            parts[1] = pathMap[oldPath];
                            lines[i] = string.Join(",", parts);
                        }
                    }
                }

                // Write updated CSV
                File.WriteAllLines(correctionPath, lines);
                Console.WriteLine($"✅ Updated corrections.csv with new paths ({movedFiles.Count} entries)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Failed to update corrections.csv: {ex.Message}");
            }
        }

        // === UPDATED Quick Update Handler with Image Moving ===

        private void BtnQuickUpdate_Click(object sender, EventArgs e)
        {
            // Validation
            if (_predictor == null || string.IsNullOrEmpty(_basePath))
            {
                MessageBox.Show("Please load a model first.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_correctionManager == null)
            {
                MessageBox.Show("Correction manager not initialized.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int correctionCount = _correctionManager.GetCorrectionCount();
            if (correctionCount == 0)
            {
                MessageBox.Show("No corrections to apply.",
                    "No Corrections", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Check dataset size
            var originalCsvPath = Path.Combine(_basePath, "images.csv");
            bool hasOriginalData = File.Exists(originalCsvPath);
            int originalCount = hasOriginalData ? File.ReadLines(originalCsvPath).Count() : 0;

            // Build confirmation message
            string confirmMessage;
            string strategy;

            if (!hasOriginalData)
            {
                confirmMessage =
                    $"⚠️ WARNING: Original training data not found!\n\n" +
                    $"Training will use ONLY {correctionCount} corrections.\n" +
                    $"This will cause catastrophic forgetting.\n\n" +
                    $"Continue anyway (NOT RECOMMENDED)?";
                strategy = "Corrections Only";
            }
            else if (originalCount > 10000)
            {
                int estimatedSamples = Math.Min(3000, originalCount / 10) + correctionCount;
                confirmMessage =
                    $"🔄 IN-PLACE MODEL UPDATE\n\n" +
                    $"Dataset: {originalCount:N0} original samples\n" +
                    $"Corrections: {correctionCount}\n\n" +
                    $"⚡ Strategy: Smart Sampling (Fast)\n" +
                    $"• Training samples: ~{estimatedSamples:N0}\n" +
                    $"• Estimated time: 2-4 minutes\n" +
                    $"• Model file: {Path.GetFileName(_modelPath)}\n\n" +
                    $"✅ Original model will be updated (not replaced)\n" +
                    $"✅ Corrected images will be moved to proper folders\n" +
                    $"✅ Backup created automatically\n" +
                    $"✅ All corrections will be learned\n\n" +
                    $"Continue?";
                strategy = "Smart Sampling";
            }
            else if (originalCount > 1000)
            {
                int estimatedSamples = Math.Min(5000, originalCount / 2) + correctionCount;
                confirmMessage =
                    $"🔄 IN-PLACE MODEL UPDATE\n\n" +
                    $"Dataset: {originalCount:N0} original samples\n" +
                    $"Corrections: {correctionCount}\n\n" +
                    $"📊 Strategy: Balanced Sampling\n" +
                    $"• Training samples: ~{estimatedSamples:N0}\n" +
                    $"• Estimated time: 1-2 minutes\n" +
                    $"• Model file: {Path.GetFileName(_modelPath)}\n\n" +
                    $"✅ Original model will be updated\n" +
                    $"✅ Corrected images will be moved to proper folders\n" +
                    $"✅ Backup created automatically\n\n" +
                    $"Continue?";
                strategy = "Balanced";
            }
            else
            {
                confirmMessage =
                    $"🔄 IN-PLACE MODEL UPDATE\n\n" +
                    $"Dataset: {originalCount:N0} original samples\n" +
                    $"Corrections: {correctionCount}\n\n" +
                    $"📊 Strategy: Full Dataset\n" +
                    $"• Training samples: {originalCount + correctionCount:N0}\n" +
                    $"• Estimated time: 30-60 seconds\n" +
                    $"• Model file: {Path.GetFileName(_modelPath)}\n\n" +
                    $"✅ Original model will be updated\n" +
                    $"✅ Corrected images will be moved to proper folders\n" +
                    $"✅ Backup created automatically\n\n" +
                    $"Continue?";
                strategy = "Full Dataset";
            }

            var confirmResult = MessageBox.Show(
                confirmMessage,
                "Confirm In-Place Update",
                MessageBoxButtons.YesNo,
                hasOriginalData ? MessageBoxIcon.Question : MessageBoxIcon.Warning);

            if (confirmResult != DialogResult.Yes)
                return;

            // Disable UI during training
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            btnQuickUpdate.Enabled = false;
            btnRetrain.Enabled = false;
            btnCorrect.Enabled = false;
            btnSelectFolder.Enabled = false;

            try
            {
                // STEP 1: Move corrected images to proper folders
                lblStatus.Text = $"📦 Step 1/3: Moving corrected images to proper folders...";
                Application.DoEvents();

                if (MoveCorrectionsToFolders(out string moveStatus))
                {
                    lblStatus.Text = $"✅ Images moved successfully\n   {moveStatus}";
                    Console.WriteLine($"✅ {moveStatus}");
                }
                else
                {
                    lblStatus.Text = $"⚠️ Some images could not be moved\n   {moveStatus}";
                    Console.WriteLine($"⚠️ {moveStatus}");

                    // Ask if user wants to continue
                    var continueResult = MessageBox.Show(
                        $"Some images could not be moved:\n\n{moveStatus}\n\n" +
                        $"Continue with training anyway?",
                        "Image Move Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (continueResult != DialogResult.Yes)
                    {
                        lblStatus.Text = "❌ Update cancelled by user";
                        return;
                    }
                }

                System.Threading.Thread.Sleep(1000); // Brief pause to show status

                // STEP 2: Train model
                lblStatus.Text = $"🔄 Step 2/3: Training model ({strategy})...\n" +
                                $"   ⏳ This will take {EstimateTime(originalCount)}\n" +
                                $"   Please wait, do not close the application.";
                Application.DoEvents();

                // Synchronous training
                var trueTrainer = new TrueIncrementalTrainer(new Microsoft.ML.MLContext(), _basePath);
                trueTrainer.IncrementalUpdateInPlace(_predictor.ModelPath);

                // STEP 3: Reload model and cleanup
                lblStatus.Text = $"📥 Step 3/3: Reloading updated model...";
                Application.DoEvents();

                // Reload the updated model (same path)
                _predictor.Dispose(); // Release the old model
                _predictor = new Predictor(_modelPath);

                // Archive corrections
                var correctionPath = Path.Combine(_basePath, "corrections.csv");
                var archivePath = Path.Combine(_basePath, $"corrections_archive_{DateTime.Now:yyyyMMddHHmmss}.csv");

                if (File.Exists(correctionPath))
                {
                    File.Copy(correctionPath, archivePath, true);
                }

                // Clear corrections
                _correctionManager.ClearCorrections(createBackup: false, out _);

                // Update UI
                lblStatus.Text = $"✅ Model updated successfully!\n" +
                                $"   Strategy: {strategy}\n" +
                                $"   Corrections applied: {correctionCount}\n" +
                                $"   Images moved to correct folders\n" +
                                $"   Model: {Path.GetFileName(_modelPath)}";

                UpdateCorrectionCount();

                // Show detailed success message
                MessageBox.Show(
                    $"🎉 Model Updated Successfully!\n\n" +
                    $"✅ Step 1: Images moved to correct folders\n" +
                    $"   {moveStatus}\n\n" +
                    $"✅ Step 2: Model trained\n" +
                    $"   Strategy: {strategy}\n" +
                    $"   Corrections applied: {correctionCount}\n\n" +
                    $"✅ Step 3: Cleanup completed\n" +
                    $"   Corrections archived: {Path.GetFileName(archivePath)}\n" +
                    $"   Model file: {Path.GetFileName(_modelPath)}\n\n" +
                    $"The model is ready to use!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"❌ Update failed: {ex.Message}";

                MessageBox.Show(
                    $"Model update failed:\n\n{ex.Message}\n\n" +
                    $"Your original model has been restored from backup.\n\n" +
                    $"Possible causes:\n" +
                    $"• Not enough corrections (need both OK and NG)\n" +
                    $"• Corrupted image files\n" +
                    $"• Disk space issues\n" +
                    $"• Model file locked by another process\n" +
                    $"• Images locked by another program\n\n" +
                    $"Try:\n" +
                    $"1. Close any programs using the images or model\n" +
                    $"2. Verify corrections are valid\n" +
                    $"3. Check disk space\n" +
                    $"4. Restart the application if needed",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                // Restore UI
                progressBar.Visible = false;
                btnQuickUpdate.Enabled = true;
                btnRetrain.Enabled = true;
                btnCorrect.Enabled = true;
                btnSelectFolder.Enabled = true;
            }
        }

        // Helper to estimate time based on dataset size
        private string EstimateTime(int samples)
        {
            if (samples == 0) return "30 seconds";
            if (samples < 1000) return "30-60 seconds";
            if (samples < 5000) return "1-2 minutes";
            if (samples < 10000) return "2-3 minutes";
            return "2-4 minutes";
        }



        private class TextBoxWriter : TextWriter
        {
            private readonly RichTextBox _textBox;

            public TextBoxWriter(RichTextBox textBox)
            {
                _textBox = textBox;
            }

            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

            public override void Write(char value)
            {
                if (_textBox.InvokeRequired)
                {
                    _textBox.Invoke(new Action(() => Write(value)));
                    return;
                }
                _textBox.AppendText(value.ToString());
                _textBox.SelectionStart = _textBox.TextLength;
                _textBox.ScrollToCaret();
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

        private class MultiWriter : TextWriter
        {
            private readonly TextWriter[] _writers;

            public MultiWriter(params TextWriter[] writers)
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
            btnCorrect.Enabled = false;

            await Task.Run(() =>
            {
                try
                {
                    var trainer = new ModelTrainer(new Microsoft.ML.MLContext(), _basePath, _roiRect);
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
            btnCorrect.Enabled = true;

            LoadModel();
            MessageBox.Show("Full retrain completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private void PopulateCorrectionComboBox()
        {
            cbCorrection.Items.Clear();

            if (_results == null || !_results.Any())
                return;

            // Get unique prediction labels from results
            var uniqueLabels = _results
                .Select(r => r.PredictedLabel)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            foreach (var label in uniqueLabels)
            {
                cbCorrection.Items.Add(label);
            }

            // Set default selection to first item if available
            if (cbCorrection.Items.Count > 0)
            {
                cbCorrection.SelectedIndex = 0;
            }
        }

        private void BtnSelectSingleImage_Click(object sender, EventArgs e)
        {
            if (_predictor == null)
            {
                MessageBox.Show("Model not loaded. Please configure a model first.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var dialog = new OpenFileDialog
            {
                Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Image for Prediction"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                PredictSingleImage(dialog.FileName);
            }
        }

        // =============================================================================
        // ONLY ONE METHOD CHANGES IN Form1.cs — PredictSingleImage
        // Replace your existing PredictSingleImage with this complete version.
        // Everything else in Form1.cs stays exactly as it was in the last full file.
        // =============================================================================

        private void PredictSingleImage(string imagePath)
        {
            try
            {
                lblSingleImageResult.Visible = true;
                lblSingleImageResult.Text = "🔍 Analyzing image...";
                lblSingleImageResult.BackColor = Color.FromArgb(255, 248, 220);
                Application.DoEvents();

                var preprocessedPath = PreprocessForPrediction(imagePath);

                // ── DEBUG: Show raw OOD numbers in a message box ──────────────────
                // Remove this block once you confirm OOD is working correctly.
                var debug = _predictor.PredictDetailed(preprocessedPath);
                MessageBox.Show(
                    $"File:          {Path.GetFileName(imagePath)}\n\n" +
                    $"Confidence:    {debug.Confidence:P2}\n" +
                    $"Margin:        {debug.Margin:P2}\n" +
                    $"Entropy Ratio: {debug.EntropyRatio:P2}\n\n" +
                    $"OOD Rejected:  {debug.IsOutOfDistribution}\n" +
                    $"Reason:        {debug.RejectionReason ?? "none — image looks like a weld"}",
                    "OOD Diagnostics",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                // ── END DEBUG ─────────────────────────────────────────────────────

                // THIS IS THE KEY LINE — uses _predictor (with OOD), NOT _singlePredictor
                var (label, confidence) = _predictor.PredictWithConfidenceAndOodCheck(preprocessedPath);

                // Display the original image
                pictureBox.Image?.Dispose();
                using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                pictureBox.Image = Image.FromStream(fs);

                // ── Image rejected as not a weld ──────────────────────────────────
                if (label == Predictor.UNKNOWN_LABEL)
                {
                    lblSingleImageResult.BackColor = Color.FromArgb(224, 224, 224); // grey
                    lblSingleImageResult.ForeColor = Color.FromArgb(66, 66, 66);
                    lblSingleImageResult.Text =
                        $"⚠️ Not a valid weld image — prediction rejected\n" +
                        $"   Raw confidence: {confidence:P2} — failed OOD checks.\n" +
                        $"   Please select a weld photo.";

                    lblInfo.Text = $"{Path.GetFileName(imagePath)} | Rejected: not a valid weld image";
                    lblStatus.Text = $"⚠️ Image rejected — does not look like a weld ({confidence:P2} raw confidence)";
                    lblStatus.BackColor = Color.FromArgb(224, 224, 224);
                    lblStatus.ForeColor = Color.FromArgb(66, 66, 66);
                    return;
                }

                // ── Valid weld image ──────────────────────────────────────────────
                string confidenceText = $"{confidence:P2}";
                Color resultColor;
                string emoji;

                if (confidence >= 0.90f)
                {
                    resultColor = Color.FromArgb(232, 245, 233);
                    emoji = "✅";
                }
                else if (confidence >= 0.70f)
                {
                    resultColor = Color.FromArgb(255, 248, 220);
                    emoji = "⚠️";
                }
                else
                {
                    resultColor = Color.FromArgb(255, 235, 238);
                    emoji = "❓";
                }

                lblSingleImageResult.BackColor = resultColor;
                lblSingleImageResult.ForeColor = confidence >= 0.70f
                    ? Color.FromArgb(46, 125, 50)
                    : Color.FromArgb(211, 47, 47);
                lblSingleImageResult.Text = $"{emoji} Prediction: {label} ({confidenceText} confidence)";

                lblInfo.Text = $"{Path.GetFileName(imagePath)} | Prediction: {label} ({confidenceText})";
                lblStatus.Text = $"✅ Single image predicted: {label} ({confidenceText})";
                lblStatus.BackColor = resultColor;
                lblStatus.ForeColor = SystemColors.ControlText;
            }
            catch (Exception ex)
            {
                lblSingleImageResult.Visible = true;
                lblSingleImageResult.Text = $"❌ Prediction failed: {ex.Message}";
                lblSingleImageResult.BackColor = Color.FromArgb(255, 235, 238);
                lblSingleImageResult.ForeColor = Color.FromArgb(211, 47, 47);
                lblStatus.Text = $"❌ Prediction failed: {ex.Message}";
                MessageBox.Show($"Failed to predict image:\n\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Add after BtnSelectSingleImage_Click method:

        private void BtnMonitorFolder_Click(object sender, EventArgs e)
        {
            if (_folderWatcher == null)
            {
                // Start monitoring
                StartFolderMonitoring();
            }
            else
            {
                // Stop monitoring
                StopFolderMonitoring();
            }
        }

        //private void StartFolderMonitoring()
        //{
        //    if (_predictor == null)
        //    {
        //        MessageBox.Show("Model not loaded. Please configure a model first.",
        //            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        return;
        //    }

        //    using var dialog = new FolderBrowserDialog
        //    {
        //        Description = "Select root folder containing class subfolders (OK, NG, etc.)"
        //    };

        //    if (dialog.ShowDialog() != DialogResult.OK)
        //        return;

        //    _monitoredFolder = dialog.SelectedPath;

        //    // Verify it has subfolders
        //    var classFolders = Directory.GetDirectories(_monitoredFolder);
        //    if (classFolders.Length == 0)
        //    {
        //        MessageBox.Show("Selected folder has no subfolders.\n\n" +
        //                       "Please select a folder containing class subfolders like 'OK', 'NG', etc.",
        //            "No Class Folders", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //        return;
        //    }

        //    try
        //    {
        //        // Initialize timer for batch processing
        //        _processingTimer = new System.Windows.Forms.Timer();
        //        _processingTimer.Interval = 2000; // Process queue every 2 seconds
        //        _processingTimer.Tick += ProcessImageQueue;
        //        _processingTimer.Start();

        //        // Setup FileSystemWatcher
        //        _folderWatcher = new FileSystemWatcher(_monitoredFolder)
        //        {
        //            IncludeSubdirectories = true,
        //            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        //            Filter = "*.*"
        //        };

        //        _folderWatcher.Created += OnNewImageDetected;
        //        _folderWatcher.EnableRaisingEvents = true;

        //        btnMonitorFolder.Text = "👁️ Monitor Folder (ON)";
        //        btnMonitorFolder.BackColor = Color.FromArgb(76, 175, 80); // Green

        //        lblStatus.Text = $"✅ Monitoring: {_monitoredFolder}\n" +
        //                        $"   Class folders: {string.Join(", ", classFolders.Select(f => Path.GetFileName(f)))}";

        //        MessageBox.Show($"Folder monitoring started!\n\n" +
        //                       $"Monitoring: {_monitoredFolder}\n" +
        //                       $"Class folders: {classFolders.Length}\n\n" +
        //                       $"New images will be automatically predicted.",
        //            "Monitoring Active", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Failed to start monitoring:\n{ex.Message}",
        //            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        StopFolderMonitoring();
        //    }
        //}

        private void StartFolderMonitoring()
        {
            if (_predictor == null)
            {
                MessageBox.Show("Model not loaded. Please configure a model first.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select root folder containing class subfolders (OK, NG, etc.)"
            };

            if (dialog.ShowDialog() != DialogResult.OK) return;

            _monitoredFolder = dialog.SelectedPath;

            var classFolders = Directory.GetDirectories(_monitoredFolder);
            if (classFolders.Length == 0)
            {
                MessageBox.Show(
                    "Selected folder has no subfolders.\n\n" +
                    "Please select a folder containing class subfolders like 'OK', 'NG', etc.",
                    "No Class Folders", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _processingTimer = new System.Windows.Forms.Timer();
                _processingTimer.Interval = 2000;
                _processingTimer.Tick += ProcessImageQueue;
                _processingTimer.Start();

                _folderWatcher = new FileSystemWatcher(_monitoredFolder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter = "*.*"
                };

                _folderWatcher.Created += OnNewImageDetected;
                _folderWatcher.EnableRaisingEvents = true;

                btnMonitorFolder.Text = "👁️ Monitor Folder (ON)";
                btnMonitorFolder.BackColor = Color.FromArgb(76, 175, 80);

                // Reset all counters when starting a fresh monitoring session
                _criticalMissCount = 0;
                _falseAlarmCount = 0;
                _abnormalCount = 0;

                lblStatus.Text = $"✅ Monitoring: {_monitoredFolder}\n" +
                                      $"   Class folders: {string.Join(", ", classFolders.Select(f => Path.GetFileName(f)))}";
                lblStatus.BackColor = SystemColors.Control;
                lblStatus.ForeColor = SystemColors.ControlText;

                MessageBox.Show(
                    $"Folder monitoring started!\n\n" +
                    $"Monitoring: {_monitoredFolder}\n" +
                    $"Class folders: {classFolders.Length}\n\n" +
                    $"New images will be automatically predicted.\n" +
                    $"🔴 Critical misses (NG→OK) will trigger USB light + red alert.",
                    "Monitoring Active", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start monitoring:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopFolderMonitoring();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopFolderMonitoring();
            _usbPortController?.Dispose();
            _usbLight?.Dispose();
            base.OnFormClosing(e);
        }

        // Update StopFolderMonitoring
        private void StopFolderMonitoring()
        {
            if (_folderWatcher != null)
            {
                _folderWatcher.EnableRaisingEvents = false;
                _folderWatcher.Dispose();
                _folderWatcher = null;
            }

            if (_processingTimer != null)
            {
                _processingTimer.Stop();
                _processingTimer.Dispose();
                _processingTimer = null;
            }

            _newImageQueue.Clear();
            _monitoredFolder = null;
            _abnormalCount = 0;

            // Turn off USB light when stopping
            if (_usbLight.IsConnected)
            {
                _usbLight.TurnOff();
            }

            btnMonitorFolder.Text = "👁️ Monitor Folder (OFF)";
            btnMonitorFolder.BackColor = Color.FromArgb(156, 39, 176); // Purple

            lblStatus.Text = "⏹️ Folder monitoring stopped";
        }

        private void OnNewImageDetected(object sender, FileSystemEventArgs e)
        {
            var ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp")
                return;

            // Check if it's in a subfolder (class folder)
            var parentFolder = Path.GetDirectoryName(e.FullPath);
            if (parentFolder == _monitoredFolder)
                return; // Ignore files in root, only process files in subfolders

            // Wait a bit for file to be fully written
            Thread.Sleep(500);

            // Add to queue
            lock (_newImageQueue)
            {
                if (!_newImageQueue.Contains(e.FullPath))
                {
                    _newImageQueue.Enqueue(e.FullPath);
                    Console.WriteLine($"📥 New image detected: {Path.GetFileName(e.FullPath)}");
                }
            }
        }

        // Updated ProcessImageQueue with USB light control
        //private void ProcessImageQueue(object sender, EventArgs e)
        //{
        //    if (_isProcessingQueue || _predictor == null)
        //        return;

        //    string imagePath = null;
        //    lock (_newImageQueue)
        //    {
        //        if (_newImageQueue.Count == 0)
        //            return;
        //        imagePath = _newImageQueue.Dequeue();
        //    }

        //    _isProcessingQueue = true;

        //    try
        //    {
        //        // Verify file still exists and is accessible
        //        if (!File.Exists(imagePath))
        //        {
        //            Console.WriteLine($"⚠️ File no longer exists: {Path.GetFileName(imagePath)}");
        //            return;
        //        }

        //        // Wait for file to be fully written
        //        for (int i = 0; i < 3; i++)
        //        {
        //            try
        //            {
        //                using (var fs = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.None))
        //                {
        //                    break; // File is accessible
        //                }
        //            }
        //            catch (IOException)
        //            {
        //                if (i == 2) throw;
        //                Thread.Sleep(500);
        //            }
        //        }

        //        // Get class folder name
        //        var classFolder = new DirectoryInfo(Path.GetDirectoryName(imagePath)).Name;

        //        // Predict
        //        var (label, confidence) = _predictor.PredictWithConfidence(imagePath);
        //        string cleanLabel = label?.Trim().Trim('"') ?? "Unknown";

        //        // Check for mismatch (abnormal)
        //        bool isAbnormal = !classFolder.Equals(cleanLabel, StringComparison.OrdinalIgnoreCase);

        //        // Log to console
        //        Console.WriteLine($"🔍 Auto-predicted: {Path.GetFileName(imagePath)}");
        //        Console.WriteLine($"   Folder: {classFolder} | Prediction: {cleanLabel} ({confidence:P2})");

        //        if (isAbnormal)
        //        {
        //            _abnormalCount++;
        //            Console.WriteLine($"⚠️ ABNORMAL DETECTED! Count: {_abnormalCount}");

        //            // Turn ON USB light for abnormal detection
        //            if (_usbLight.IsConnected)
        //            {
        //                _usbLight.TurnOn();
        //            }
        //        }
        //        else
        //        {
        //            Console.WriteLine($"✅ Normal classification");

        //            // Turn OFF USB light if no more abnormals in queue
        //            if (_abnormalCount == 0 && _usbLight.IsConnected)
        //            {
        //                _usbLight.TurnOff();
        //            }
        //        }

        //        // Update status
        //        string statusEmoji = isAbnormal ? "⚠️" : "✅";
        //        Color statusColor = isAbnormal
        //            ? Color.FromArgb(255, 235, 238)  // Light red
        //            : Color.FromArgb(232, 245, 233); // Light green

        //        Invoke(new Action(() =>
        //        {
        //            lblStatus.Text = $"{statusEmoji} Auto-prediction:\n" +
        //                           $"   File: {Path.GetFileName(imagePath)}\n" +
        //                           $"   Folder: {classFolder} → Predicted: {cleanLabel} ({confidence:P2})\n" +
        //                           $"   Abnormal detections: {_abnormalCount}";
        //            lblStatus.BackColor = statusColor;
        //        }));

        //        // Add to results if mismatch detected
        //        if (isAbnormal)
        //        {
        //            Invoke(new Action(() =>
        //            {
        //                _results.Add(new ImageResult
        //                {
        //                    FileName = Path.GetFileName(imagePath),
        //                    ImagePath = imagePath,
        //                    Subfolder = classFolder,
        //                    PredictedLabel = cleanLabel,
        //                    CorrectedLabel = cleanLabel,
        //                    Confidence = confidence
        //                });

        //                // Refresh grid
        //                PopulateGrid(_results);
        //                PopulateFilters();

        //                // Show notification for high-confidence mismatches
        //                if (confidence > 0.7f)
        //                {
        //                    // Play alert sound
        //                    System.Media.SystemSounds.Exclamation.Play();

        //                    MessageBox.Show(
        //                        $"⚠️ ABNORMAL DETECTION!\n\n" +
        //                        $"File: {Path.GetFileName(imagePath)}\n" +
        //                        $"Current Folder: {classFolder}\n" +
        //                        $"Predicted: {cleanLabel} ({confidence:P2})\n\n" +
        //                        $"USB Light has been turned ON.\n" +
        //                        $"Total abnormals: {_abnormalCount}\n\n" +
        //                        $"Please review and correct.",
        //                        "Quality Alert",
        //                        MessageBoxButtons.OK,
        //                        MessageBoxIcon.Warning);
        //                }
        //            }));
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"❌ Failed to process {Path.GetFileName(imagePath)}: {ex.Message}");
        //    }
        //    finally
        //    {
        //        _isProcessingQueue = false;
        //    }
        //}

        private void ProcessImageQueue(object sender, EventArgs e)
        {
            if (_isProcessingQueue || _predictor == null) return;

            string imagePath = null;
            lock (_newImageQueue)
            {
                if (_newImageQueue.Count == 0) return;
                imagePath = _newImageQueue.Dequeue();
            }

            _isProcessingQueue = true;

            try
            {
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"⚠️ File no longer exists: {Path.GetFileName(imagePath)}");
                    return;
                }

                // Wait for file to finish writing
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        using var fs = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.None);
                        break;
                    }
                    catch (IOException)
                    {
                        if (i == 2) throw;
                        Thread.Sleep(500);
                    }
                }

                // Ground truth = the subfolder the image was placed in BEFORE prediction
                string groundTruth = new DirectoryInfo(Path.GetDirectoryName(imagePath)).Name;

                // Run prediction
                var (label, confidence) = _predictor.PredictWithConfidence(imagePath);
                string cleanLabel = label?.Trim().Trim('"') ?? "Unknown";

                // ── Classify the result ───────────────────────────────────────
                //
                //  CRITICAL MISS : NG folder → predicted OK
                //                  A defective product is about to pass as good.
                //                  This is the dangerous case — escalate immediately.
                //
                //  FALSE ALARM   : OK folder → predicted NG
                //                  A good product was incorrectly flagged.
                //                  Annoying, but NOT a safety risk.
                //
                //  CORRECT       : folder label matches prediction — all good.
                //
                bool isCriticalMiss = groundTruth.Equals("NG", StringComparison.OrdinalIgnoreCase)
                                   && cleanLabel.Equals("OK", StringComparison.OrdinalIgnoreCase);

                bool isFalseAlarm = groundTruth.Equals("OK", StringComparison.OrdinalIgnoreCase)
                                   && cleanLabel.Equals("NG", StringComparison.OrdinalIgnoreCase);

                bool isCorrect = !isCriticalMiss && !isFalseAlarm;

                Console.WriteLine($"🔍 Auto-predicted: {Path.GetFileName(imagePath)}");
                Console.WriteLine($"   Folder (truth): {groundTruth}  |  Predicted: {cleanLabel} ({confidence:P2})");

                // ── React based on result ─────────────────────────────────────
                if (isCriticalMiss)
                {
                    _criticalMissCount++;
                    _abnormalCount++;
                    Console.WriteLine($"🔴 CRITICAL MISS #{_criticalMissCount} — NG product predicted as OK!");

                    // USB light ON immediately — stays ON until user clears alerts
                    if (_usbLight.IsConnected)
                        _usbLight.TurnOn();

                    // Switch to UI thread for all visual updates
                    Invoke(new Action(() => FlashCriticalAlert(imagePath, Path.GetFileName(imagePath), confidence)));
                }
                else if (isFalseAlarm)
                {
                    _falseAlarmCount++;
                    Console.WriteLine($"🟡 False alarm #{_falseAlarmCount} — OK product flagged as NG");

                    // No USB light for false alarms — not a safety risk
                    Invoke(new Action(() =>
                    {
                        lblStatus.Text = $"🟡 False Alarm #{_falseAlarmCount}: {Path.GetFileName(imagePath)}\n" +
                                              $"   Folder: OK  →  Predicted: NG ({confidence:P2})\n" +
                                              $"   Good product incorrectly flagged — not a safety risk.";
                        lblStatus.BackColor = Color.FromArgb(255, 248, 220); // soft yellow
                        lblStatus.ForeColor = SystemColors.ControlText;
                    }));
                }
                else
                {
                    Console.WriteLine($"✅ Correct prediction");

                    // Only turn off USB light when no critical misses are still active
                    if (_criticalMissCount == 0 && _usbLight.IsConnected)
                        _usbLight.TurnOff();

                    Invoke(new Action(() =>
                    {
                        lblStatus.Text = $"✅ Correct: {groundTruth} → {cleanLabel} ({confidence:P2})\n" +
                                              $"   Critical misses: {_criticalMissCount}  |  False alarms: {_falseAlarmCount}";
                        lblStatus.BackColor = Color.FromArgb(232, 245, 233); // soft green
                        lblStatus.ForeColor = SystemColors.ControlText;
                    }));
                }

                // Add any mismatch to the results grid so the user can review/correct it
                if (!isCorrect)
                {
                    Invoke(new Action(() =>
                    {
                        _results.Add(new ImageResult
                        {
                            FileName = Path.GetFileName(imagePath),
                            ImagePath = imagePath,
                            Subfolder = groundTruth,
                            PredictedLabel = cleanLabel,
                            CorrectedLabel = cleanLabel,
                            Confidence = confidence
                        });

                        PopulateGrid(_results);
                        PopulateFilters();
                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to process {Path.GetFileName(imagePath)}: {ex.Message}");
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }

        private async void FlashCriticalAlert(string imagePath, string fileName, float confidence)
        {
            // Show the offending image immediately so the operator can see it
            if (File.Exists(imagePath))
            {
                pictureBox.Image?.Dispose();
                using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                pictureBox.Image = Image.FromStream(fs);
            }

            // Set status to urgent red with white text
            lblStatus.Text = $"🔴 CRITICAL MISS #{_criticalMissCount} — DEFECTIVE PRODUCT PASSED AS OK!\n" +
                                  $"   File: {fileName}  |  Confidence: {confidence:P2}\n" +
                                  $"   USB alert light is ON — inspect this product immediately.\n" +
                                  $"   Click 'Clear Alerts' once the product has been removed.";
            lblStatus.BackColor = Color.FromArgb(211, 47, 47);  // strong red
            lblStatus.ForeColor = Color.White;

            // Pulse 6 times (red ↔ darker red) to catch the operator's eye
            for (int i = 0; i < 6; i++)
            {
                lblStatus.BackColor = i % 2 == 0
                    ? Color.FromArgb(211, 47, 47)   // red
                    : Color.FromArgb(183, 28, 28);  // darker red
                await Task.Delay(250);
            }

            // Settle on steady red — stays until operator clicks Clear Alerts
            lblStatus.BackColor = Color.FromArgb(211, 47, 47);
            lblStatus.ForeColor = Color.White;
        }

        // Add button to clear abnormal count and turn off light
        private void BtnClearAbnormals_Click(object sender, EventArgs e)
        {
            if (_criticalMissCount == 0 && _falseAlarmCount == 0 && _abnormalCount == 0)
            {
                MessageBox.Show("No alerts to clear.", "Nothing to Clear",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Clear all alerts and turn off USB light?\n\n" +
                $"🔴 Critical misses (NG→OK):  {_criticalMissCount}\n" +
                $"🟡 False alarms   (OK→NG):   {_falseAlarmCount}\n\n" +
                $"Session counters will reset. Monitoring continues.",
                "Clear Alerts", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _criticalMissCount = 0;
                _falseAlarmCount = 0;
                _abnormalCount = 0;

                if (_usbLight.IsConnected)
                    _usbLight.TurnOff();

                lblStatus.Text = "✅ Alerts cleared — monitoring continues.";
                lblStatus.BackColor = Color.FromArgb(232, 245, 233); // soft green
                lblStatus.ForeColor = SystemColors.ControlText;       // restore default
            }
        }


        private Rectangle _roiRect = Rectangle.Empty;

        private class RoiConfig
        {
            public int RoiX { get; set; }
            public int RoiY { get; set; }
            public int RoiW { get; set; }
            public int RoiH { get; set; }
        }
        public class ImageResult
        {
            public string? FileName { get; set; }
            public string? ImagePath { get; set; }
            public string? Subfolder { get; set; }
            public string? PredictedLabel { get; set; }
            public string? CorrectedLabel { get; set; }
            public float Confidence { get; set; }
        }
    }
}