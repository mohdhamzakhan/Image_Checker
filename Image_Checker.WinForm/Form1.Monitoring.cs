using Image_Checker.Services;
using System.IO;

namespace Image_Checker.WinForm
{
    // Folder monitoring for Form1. The watching itself (watcher restarts, rescans, folder
    // disappearing/returning) lives in FolderImageMonitor so it can never silently stop.
    // This file only turns its queue into predictions and keeps the status line alive.
    public partial class Form1
    {
        private FolderImageMonitor? _monitor;
        private int _monitorScanIntervalMs = 5000;
        private readonly Dictionary<string, int> _fileRetryCount = new();
        private int _checkedCount = 0;
        private int _correctCount = 0;
        private string _lastMonitorLine = "";
        private Color _lastMonitorBack = SystemColors.Control;
        private static readonly object _monitorLogLock = new();

        // ─────────────────────────────────────────────────────────────────
        //  Start / stop
        // ─────────────────────────────────────────────────────────────────

        private void BtnMonitorFolder_Click(object sender, EventArgs e)
        {
            if (_monitor == null)
                StartFolderMonitoring();
            else
                StopFolderMonitoring();
        }

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

            string root = dialog.SelectedPath;
            string[] classFolders;
            try
            {
                classFolders = Directory.GetDirectories(root);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot read the selected folder:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                _monitoredFolder = root;

                // Fresh session
                _criticalMissCount = 0;
                _falseAlarmCount = 0;
                _abnormalCount = 0;
                _checkedCount = 0;
                _correctCount = 0;
                _fileRetryCount.Clear();
                _lastMonitorLine = "   Waiting for new images...";
                _lastMonitorBack = SystemColors.Control;

                _monitor = new FolderImageMonitor(root, _monitorScanIntervalMs);
                _monitor.Log += LogMonitor;
                _monitor.Start();

                _processingTimer = new System.Windows.Forms.Timer { Interval = 2000 };
                _processingTimer.Tick += ProcessImageQueue;
                _processingTimer.Start();

                btnMonitorFolder.Text = "👁️ Monitor Folder (ON)";
                btnMonitorFolder.BackColor = Color.FromArgb(76, 175, 80);
                ApplyMonitorStatus(force: true);

                string noFolders = classFolders.Length == 0
                    ? "\n\nNo class subfolders exist yet. That is fine - monitoring keeps running; " +
                      "put new images inside subfolders such as OK or NG."
                    : "";

                MessageBox.Show(
                    $"Folder monitoring started!\n\n" +
                    $"Monitoring: {root}\n" +
                    $"Class folders: {classFolders.Length}\n\n" +
                    $"New images placed inside a class subfolder (OK, NG...) are predicted.\n" +
                    $"Only mismatches are listed in the grid.\n" +
                    $"Monitoring keeps running by itself, even when no images arrive for a long time " +
                    $"or the folder is temporarily unreachable.\n" +
                    $"🔴 Critical misses (NG→OK) trigger the USB light + red alert." + noFolders,
                    "Monitoring Active", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start monitoring:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopFolderMonitoring();
            }
        }

        private void StopFolderMonitoring()
        {
            if (_monitor != null)
            {
                _monitor.Log -= LogMonitor;
                _monitor.Dispose();
                _monitor = null;
            }

            if (_processingTimer != null)
            {
                _processingTimer.Stop();
                _processingTimer.Dispose();
                _processingTimer = null;
            }

            _fileRetryCount.Clear();
            _monitoredFolder = null;
            _abnormalCount = 0;

            // Turn off USB light when stopping
            if (_usbLight != null && _usbLight.IsConnected)
                _usbLight.TurnOff();

            btnMonitorFolder.Text = "👁️ Monitor Folder (OFF)";
            btnMonitorFolder.BackColor = Color.FromArgb(156, 39, 176); // Purple
            lblStatus.Text = "⏹️ Folder monitoring stopped";
        }

        // ─────────────────────────────────────────────────────────────────
        //  Timer tick: one image per tick, never throws, never stops the loop
        // ─────────────────────────────────────────────────────────────────

        private void ProcessImageQueue(object? sender, EventArgs e)
        {
            if (_isProcessingQueue) return;

            var monitor = _monitor;
            if (monitor == null) return;

            _isProcessingQueue = true;
            string imagePath = "";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Say so instead of silently doing nothing
                if (_predictor == null)
                {
                    ShowMonitorProblem("⚠️ No model loaded - monitoring is paused until a model is loaded.");
                    return;
                }

                if (!monitor.TryDequeue(out imagePath))
                {
                    // Idle: nothing to predict, but keep the status alive so you can SEE it is running
                    ApplyMonitorStatus(force: false);
                    return;
                }

                if (!File.Exists(imagePath))
                {
                    _fileRetryCount.Remove(imagePath);
                    return;
                }

                // Still being written? Put it back and try on a later tick - never block the UI.
                if (!FolderImageMonitor.IsFileReady(imagePath))
                {
                    _fileRetryCount.TryGetValue(imagePath, out int tries);
                    if (tries < 15)   // about 30 s at a 2 s tick
                    {
                        _fileRetryCount[imagePath] = tries + 1;
                        monitor.Requeue(imagePath);
                        _lastMonitorLine = $"   ⏳ Waiting for {Path.GetFileName(imagePath)} to finish writing " +
                                           $"(attempt {tries + 1}/15)...";
                        ApplyMonitorStatus(force: false);
                        if (tries == 0) LogMonitor($"NOT READY (still being written): {imagePath}");
                    }
                    else
                    {
                        _fileRetryCount.Remove(imagePath);
                        LogMonitor($"GAVE UP (file stayed locked by a writer): {imagePath}");
                        _lastMonitorLine = $"   ❌ Gave up on {Path.GetFileName(imagePath)} - file stayed locked.";
                        ApplyMonitorStatus(force: false);
                    }
                    return;
                }
                _fileRetryCount.Remove(imagePath);

                // Ground truth = the subfolder the image was placed in BEFORE prediction
                string groundTruth = new DirectoryInfo(Path.GetDirectoryName(imagePath)!).Name;

                // Same ROI-cropped input that Select Folder uses
                string processedPath = PreprocessForPrediction(imagePath);
                bool isTempFile = processedPath != imagePath;

                (string Label, float Confidence) prediction;
                try
                {
                    prediction = PredictAny(processedPath);
                }
                finally
                {
                    if (isTempFile && File.Exists(processedPath))
                        File.Delete(processedPath);
                }
                var (label, confidence) = prediction;
                string cleanLabel = label?.Trim().Trim('"') ?? "Unknown";
                string fileName = Path.GetFileName(imagePath);
                string shownPath = imagePath;

                // CRITICAL MISS: NG folder predicted OK | FALSE ALARM: OK folder predicted NG
                bool isCriticalMiss = groundTruth.Equals("NG", StringComparison.OrdinalIgnoreCase)
                                   && cleanLabel.Equals("OK", StringComparison.OrdinalIgnoreCase);
                bool isFalseAlarm = groundTruth.Equals("OK", StringComparison.OrdinalIgnoreCase)
                                   && cleanLabel.Equals("NG", StringComparison.OrdinalIgnoreCase);
                bool isCorrect = !isCriticalMiss && !isFalseAlarm;

                string verdict = isCriticalMiss ? "🔴 Critical miss"
                               : isFalseAlarm ? "🟡 False alarm"
                               : "✅ Correct";

                _checkedCount++;
                if (isCorrect) _correctCount++;

                // Only MISMATCHES go into the grid
                if (!isCorrect)
                {
                    Invoke(new Action(() => AddMonitoredRow(new ImageResult
                    {
                        FileName = fileName,
                        ImagePath = shownPath,
                        Subfolder = groundTruth,
                        PredictedLabel = cleanLabel,
                        CorrectedLabel = cleanLabel,
                        Confidence = confidence,
                        Verdict = verdict
                    })));

                    // Move mismatched images when confidence is above 80%
                    if (confidence > 0.80f)
                    {
                        MoveHighConfidenceMismatch(
                            imagePath,
                            groundTruth,
                            cleanLabel,
                            confidence
                        );
                    }
                }

                if (isCriticalMiss)
                {
                    _criticalMissCount++;
                    _abnormalCount++;
                    if (_usbLight != null && _usbLight.IsConnected)
                        _usbLight.TurnOn();

                    Invoke(new Action(() => FlashCriticalAlert(shownPath, fileName, confidence)));
                }
                else if (isFalseAlarm)
                {
                    _falseAlarmCount++;
                    Invoke(new Action(() => ShowMonitorSummary(
                        $"   Last: {fileName} is in OK but predicted NG ({confidence:P1}) - see grid",
                        Color.FromArgb(255, 248, 220))));
                }
                else
                {
                    if (_criticalMissCount == 0 && _usbLight != null && _usbLight.IsConnected)
                        _usbLight.TurnOff();

                    Invoke(new Action(() => ShowMonitorSummary(
                        $"   Last: {fileName} → {cleanLabel} ({confidence:P1}) matches folder {groundTruth}",
                        Color.FromArgb(232, 245, 233))));
                }

                LogMonitor($"{fileName} folder={groundTruth} pred={cleanLabel} " +
                           $"{confidence:P1} {verdict} | {sw.ElapsedMilliseconds} ms | rows={_results.Count}");
            }
            catch (Exception ex)
            {
                // One bad image must never stop monitoring
                LogMonitor($"ERROR {Path.GetFileName(imagePath)}: {ex}");
                try { ShowMonitorProblem($"❌ Error on {Path.GetFileName(imagePath)}: {ex.Message}"); } catch { }
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }


        private void MoveHighConfidenceMismatch(
    string imagePath,
    string groundTruth,
    string predictedLabel,
    float confidence)
        {
            if (confidence <= 0.80f)
                return;

            string destinationRoot = Path.Combine(
                AppContext.BaseDirectory,
                "HighConfidenceMismatches",
                groundTruth
            );

            Directory.CreateDirectory(destinationRoot);

            string fileName = Path.GetFileName(imagePath);
            string destinationPath = Path.Combine(destinationRoot, fileName);

            // Avoid overwriting existing images
            if (File.Exists(destinationPath))
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);

                destinationPath = Path.Combine(
                    destinationRoot,
                    $"{name}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}"
                );
            }

            File.Copy(imagePath, destinationPath);

            LogMonitor(
                $"MOVED HIGH-CONFIDENCE MISMATCH: {imagePath} -> {destinationPath} | " +
                $"GroundTruth={groundTruth}, Prediction={predictedLabel}, Confidence={confidence:P1}"
            );
        }

        // ─────────────────────────────────────────────────────────────────
        //  Status line
        // ─────────────────────────────────────────────────────────────────

        private void ShowMonitorSummary(string lastLine, Color back)
        {
            _lastMonitorLine = lastLine;
            _lastMonitorBack = back;
            ApplyMonitorStatus(force: true);
        }

        private void ShowMonitorProblem(string text)
        {
            lblStatus.Text = text;
            lblStatus.BackColor = Color.FromArgb(255, 235, 238);
            lblStatus.ForeColor = SystemColors.ControlText;
        }

        /// <summary>
        /// Counts + last result + a live health line. Called on every idle tick, so the text changes
        /// (last scan time) prove monitoring is alive; if the time stops moving, something is stuck.
        /// An active critical-miss alert is not overwritten until the operator clears it.
        /// </summary>
        private void ApplyMonitorStatus(bool force)
        {
            var m = _monitor;
            if (m == null) return;
            if (!force && _criticalMissCount > 0) return;

            string health;
            Color back = _lastMonitorBack;
            if (!m.FolderReachable)
            {
                health = "   ⚠️ Folder not reachable - retrying automatically...";
                back = Color.FromArgb(255, 235, 238);
            }
            else
            {
                health = $"   ● Live - last scan {m.LastScanUtc.ToLocalTime():HH:mm:ss}" +
                         (m.WatcherActive ? "" : " (scan mode)") +
                         (m.WatcherRestarts > 0 ? $" | watcher restarted {m.WatcherRestarts}x" : "") +
                         (m.QueueCount > 0 ? $" | {m.QueueCount} waiting" : "");
            }

            string ignored = m.RootIgnoredCount > 0
                ? $"\n   ⚠️ {m.RootIgnoredCount} image(s) were put directly in the monitored folder and ignored - " +
                  "put them inside OK or NG."
                : "";

            lblStatus.Text =
                $"👁️ Monitoring: {_checkedCount} checked | ✅ {_correctCount} correct | " +
                $"🟡 {_falseAlarmCount} false alarms | 🔴 {_criticalMissCount} critical misses\n" +
                (string.IsNullOrEmpty(_lastMonitorLine) ? "" : _lastMonitorLine + "\n") +
                health + ignored;
            lblStatus.BackColor = back;
            lblStatus.ForeColor = SystemColors.ControlText;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Grid + log helpers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Adds one monitored image to the grid with ONE grid refresh.</summary>
        private void AddMonitoredRow(ImageResult row)
        {
            string? selectedPath = grid.SelectedRows.Count > 0
                ? grid.SelectedRows[0].Cells["ImagePath"].Value?.ToString()
                : null;

            _results.Add(row);

            // Rebuild the filter lists only when a new folder/label appears
            bool newFolder = !cbFolderFilter.Items.Contains(row.Subfolder ?? "");
            bool newLabel = !cbPredFilter.Items.Contains(row.PredictedLabel ?? "");
            if (newFolder || newLabel)
                PopulateFilters();
            else
                ApplyFilters(this, EventArgs.Empty);   // one refresh, honouring current filters

            PopulateCorrectionComboBox();

            if (selectedPath != null)
            {
                // keep the row the operator is working on
                foreach (DataGridViewRow r in grid.Rows)
                {
                    if (r.Cells["ImagePath"].Value?.ToString() == selectedPath)
                    {
                        r.Selected = true;
                        break;
                    }
                }
            }
            else if (grid.Rows.Count > 0)
            {
                grid.ClearSelection();   // binding auto-selects row 0; undo that
                try { grid.FirstDisplayedScrollingRowIndex = grid.Rows.Count - 1; }
                catch { /* grid too small to scroll - ignore */ }
            }
        }

        // Console output is invisible in a WinExe, so monitoring writes monitor.log next to the exe.
        // Called from background threads too, hence the lock. Rotates at 5 MB.
        private static void LogMonitor(string message)
        {
            try
            {
                lock (_monitorLogLock)
                {
                    string path = Path.Combine(AppContext.BaseDirectory, "monitor.log");
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 5_000_000)
                        File.Move(path, Path.Combine(AppContext.BaseDirectory, "monitor.old.log"), true);

                    File.AppendAllText(path,
                        $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}