using Image_Checker.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

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
        private DateTime _monitoringDate = DateTime.MinValue;
        private string? _waitingForDateFolder;
        private int _dailyFalseAlarmCount = 0;
        private int _dailyCriticalMissCount = 0;
        private int _dailyCheckedCount = 0;
        private int _dailyCorrectCount = 0;
        private bool _dailySummaryDirty = false;

        //private DateTime? _lastSavedSummaryDate = null;

        private static readonly object _dailyCsvLock = new();

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

        private string GetMonitorBasePath()
        {
            string configPath = Path.Combine(
                AppContext.BaseDirectory,
                "app_config.txt"
            );

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException(
                    "app_config.txt not found.",
                    configPath
                );
            }

            string? configuredPath = File.ReadAllLines(configPath)
                .FirstOrDefault(x =>
                    x.TrimStart().StartsWith(
                        "MonitorBasePath=",
                        StringComparison.OrdinalIgnoreCase
                    )
                );

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw new InvalidOperationException(
                    "MonitorBasePath is missing in app_config.txt."
                );
            }

            string path = configuredPath.Substring(
                configuredPath.IndexOf('=') + 1
            ).Trim();

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    "MonitorBasePath cannot be empty."
                );
            }

            return Path.GetFullPath(path);
        }

        private void StartFolderMonitoring()
        {
            if (_predictor == null)
            {
                MessageBox.Show(
                    "Model not loaded. Please configure a model first.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            string root;
            string[] classFolders;

            try
            {
                // Read fixed base path from app_config.txt
                string basePath = GetMonitorBasePath();

                // Dynamic date folder: yy\MM\dd
                DateTime today = DateTime.Today;

                string dateFolder = Path.Combine(
                    today.ToString("yy"),
                    today.ToString("MM"),
                    today.ToString("dd")
                );

                // Complete production folder path
                root = Path.Combine(basePath, dateFolder);

                // Validate today's folder
                if (!Directory.Exists(root))
                {
                    MessageBox.Show(
                        $"Today's production folder does not exist:\n\n{root}",
                        "Folder Not Found",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                // Read existing class folders (OK, NG, etc.)
                classFolders = Directory.GetDirectories(root);

                if (classFolders.Length == 0)
                {
                    MessageBox.Show(
                        $"No class subfolders found in:\n\n{root}\n\n" +
                        "Expected folders such as OK and NG.",
                        "No Class Folders",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Cannot initialize monitoring folder:\n\n{ex.Message}",
                    "Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            try
            {
                _monitoredFolder = root;

                // Fresh monitoring session
                _criticalMissCount = 0;
                _falseAlarmCount = 0;
                _abnormalCount = 0;
                _checkedCount = 0;
                _correctCount = 0;
                // Restore today's accumulated inspection statistics
                LoadDailyInspectionSummary(DateTime.Today);

                _fileRetryCount.Clear();

                _lastMonitorLine = "   Waiting for new images...";
                _lastMonitorBack = SystemColors.Control;

                // Initialize folder monitor
                _monitor = new FolderImageMonitor(
                    root,
                    _monitorScanIntervalMs
                );

                _monitor.Log += OnMonitorEngineLog;
                _monitor.Start();

                // Load training-image comparison statistics before the first prediction.
                WarmTrainingStats();

                _monitoringDate = DateTime.Today;
                _waitingForDateFolder = null;


                // Initialize processing timer
                _processingTimer = new System.Windows.Forms.Timer
                {
                    Interval = 2000
                };

                _processingTimer.Tick += ProcessImageQueue;
                _processingTimer.Start();

                // Update UI
                btnMonitorFolder.Text = "👁️ Monitor Folder (ON)";
                btnMonitorFolder.BackColor = Color.FromArgb(76, 175, 80);

                ApplyMonitorStatus(force: true);

                // Show monitoring confirmation
                MessageBox.Show(
                    $"Folder monitoring started successfully!\n\n" +
                    $"Base Path: {GetMonitorBasePath()}\n" +
                    $"Date Folder: {DateTime.Now:yy\\MM\\dd}\n" +
                    $"Monitoring: {root}\n\n" +
                    $"Class folders: {classFolders.Length}\n\n" +
                    $"New images placed inside existing class subfolders " +
                    "(OK, NG, etc.) will be automatically predicted.\n\n" +
                    $"Only mismatches are listed in the grid.\n\n" +
                    $"Monitoring continues automatically, even when no images arrive.\n\n" +
                    $"🔴 Critical misses (NG → OK) trigger the USB light and red alert.",
                    "Monitoring Active",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to start monitoring:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                StopFolderMonitoring();
            }
        }

        private void StopFolderMonitoring()
        {
            // Save current day's inspection summary before stopping
            if (_monitoringDate != DateTime.MinValue)
            {
                bool saved = SaveDailyInspectionSummary(_monitoringDate.Date);

                if (!saved)
                {
                    MessageBox.Show(
                        "Unable to save daily inspection summary.\n" +
                        "Please check CSV file permissions.",
                        "CSV Save Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }

            // Dispose folder monitor
            if (_monitor != null)
            {
                _monitor.Log -= OnMonitorEngineLog;
                _monitor.Dispose();
                _monitor = null;
            }

            // Stop processing timer
            if (_processingTimer != null)
            {
                _processingTimer.Stop();
                _processingTimer.Dispose();
                _processingTimer = null;
            }

            _fileRetryCount.Clear();
            _monitoredFolder = null;
            _abnormalCount = 0;

            _monitoringDate = DateTime.MinValue;
            _waitingForDateFolder = null;

            // Turn off USB light
            if (_usbLight != null && _usbLight.IsConnected)
                _usbLight.TurnOff();

            btnMonitorFolder.Text = "👁️ Monitor Folder (OFF)";
            btnMonitorFolder.BackColor = Color.FromArgb(156, 39, 176);

            lblStatus.Text = "⏹️ Folder monitoring stopped";
        }

        private bool EnsureCurrentDateMonitor()
        {
            DateTime today = DateTime.Today;

            // Detect date change
            if (_monitoringDate.Date != today)
            {
                // Save previous day's inspection statistics
                if (_monitoringDate != DateTime.MinValue)
                {
                    bool saved = SaveDailyInspectionSummary(
                        _monitoringDate.Date
                    );

                    // Do not switch dates if CSV saving fails.
                    // Retry on the next timer tick.
                    if (!saved)
                        return false;

                    // Restore any records already saved for the new date (e.g. app restarted).
                    LoadDailyInspectionSummary(today);
                }

                LogMonitor(
                    $"DATE CHANGED: {_monitoringDate:yy\\MM\\dd} -> {today:yy\\MM\\dd}"
                );

                // Dispose previous day's monitor
                if (_monitor != null)
                {
                    _monitor.Log -= OnMonitorEngineLog;
                    _monitor.Dispose();
                    _monitor = null;
                }

                _fileRetryCount.Clear();

                _monitoringDate = today;
                _monitoredFolder = null;
                _waitingForDateFolder = null;

                _lastMonitorLine =
                    $"   Switching to production folder {today:yy\\MM\\dd}...";

                ApplyMonitorStatus(force: true);
            }

            // Already monitoring today's folder
            if (_monitor != null)
                return true;

            try
            {
                // Read configured base path
                string basePath = GetMonitorBasePath();

                // Build today's folder path
                string dateFolder = today.ToString(@"yy\MM\dd");
                string root = Path.Combine(basePath, dateFolder);

                // Wait for production system to create today's folder
                // Do not create any folders here
                if (!Directory.Exists(root))
                {
                    if (_waitingForDateFolder != root)
                    {
                        LogMonitor($"WAITING: Production folder not found: {root}");
                        _waitingForDateFolder = root;
                    }

                    _lastMonitorLine = $"   ⏳ Waiting for today's production folder: {dateFolder}";

                    lblStatus.Text = _lastMonitorLine;
                    lblStatus.BackColor = Color.FromArgb(255, 248, 220);
                    lblStatus.ForeColor = SystemColors.ControlText;

                    return false;
                }

                // Start new date's monitor
                var newMonitor = new FolderImageMonitor(root, _monitorScanIntervalMs);

                newMonitor.Log += OnMonitorEngineLog;
                newMonitor.Start();

                _monitor = newMonitor;
                _monitoredFolder = root;
                _waitingForDateFolder = null;

                _lastMonitorLine = $"   Monitoring today's production folder: {dateFolder}";
                _lastMonitorBack = SystemColors.Control;

                LogMonitor($"MONITORING STARTED: {root}");

                ApplyMonitorStatus(force: true);

                return true;
            }
            catch (Exception ex)
            {
                LogMonitor($"DATE SWITCH ERROR: {ex}");

                _lastMonitorLine = $"   ❌ Cannot initialize today's folder: {ex.Message}";
                ApplyMonitorStatus(force: false);

                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Timer tick: one image per tick, never throws, never stops the loop
        // ─────────────────────────────────────────────────────────────────

        private static bool IsCriticalMissPair(string folder, string label)
        {
            return folder.Equals("NG", StringComparison.OrdinalIgnoreCase)
                && label.Equals("OK", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFalseAlarmPair(string folder, string label)
        {
            return folder.Equals("OK", StringComparison.OrdinalIgnoreCase)
                && label.Equals("NG", StringComparison.OrdinalIgnoreCase);
        }

        private void ProcessImageQueue(object? sender, EventArgs e)
        {
            if (_isProcessingQueue) return;

            // Automatically detect date changes and switch production folders.
            if (!EnsureCurrentDateMonitor()) return;

            if (_dailySummaryDirty)
                SaveDailyInspectionSummary(DateTime.Today);

            var monitor = _monitor;
            if (monitor == null) return;

            _isProcessingQueue = true;
            string imagePath = "";
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (_predictor == null)
                {
                    ShowMonitorProblem("⚠️ No model loaded - monitoring is paused until a model is loaded.");
                    return;
                }

                if (!monitor.TryDequeue(out imagePath))
                {
                    ApplyMonitorStatus(force: false);
                    return;
                }

                if (!File.Exists(imagePath))
                {
                    _fileRetryCount.Remove(imagePath);
                    return;
                }

                // Wait for the production system to finish writing the image.
                if (!FolderImageMonitor.IsFileReady(imagePath))
                {
                    _fileRetryCount.TryGetValue(imagePath, out int tries);
                    if (tries < 15)
                    {
                        _fileRetryCount[imagePath] = tries + 1;
                        monitor.Requeue(imagePath);
                        _lastMonitorLine = $"   ⏳ Waiting for {Path.GetFileName(imagePath)} to finish writing " +
                                           $"(attempt {tries + 1}/15)...";
                        ApplyMonitorStatus(force: false);
                        if (tries == 0) LogDiag($"NOT READY (still being written): {imagePath}");
                    }
                    else
                    {
                        _fileRetryCount.Remove(imagePath);
                        LogDiag($"GAVE UP (file stayed locked by a writer): {imagePath}");
                        _lastMonitorLine = $"   ❌ Gave up on {Path.GetFileName(imagePath)} - file stayed locked.";
                        ApplyMonitorStatus(force: false);
                    }
                    return;
                }
                _fileRetryCount.Remove(imagePath);

                // Ground truth is the parent class-folder name (OK / NG).
                string groundTruth = new DirectoryInfo(Path.GetDirectoryName(imagePath)!).Name;
                string processedPath = PreprocessForPrediction(imagePath);
                bool isTempFile = !string.Equals(processedPath, imagePath, StringComparison.OrdinalIgnoreCase);

                (string Label, float Confidence) prediction;
                Explanation? why = null;
                try
                {
                    prediction = PredictAny(processedPath);
                    string predictedForExplain = (prediction.Label ?? "").Trim().Trim('"');

                    // Preserve the first implementation's explanation while the ROI image exists.
                    if (IsCriticalMissPair(groundTruth, predictedForExplain) ||
                        IsFalseAlarmPair(groundTruth, predictedForExplain))
                    {
                        try
                        {
                            why = QuickExplain(processedPath, imagePath, groundTruth);
                        }
                        catch (Exception ex)
                        {
                            LogDiag($"EXPLAIN ERROR {Path.GetFileName(imagePath)}: {ex.Message}");
                        }
                    }
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

                bool isCriticalMiss = IsCriticalMissPair(groundTruth, cleanLabel);
                bool isFalseAlarm = IsFalseAlarmPair(groundTruth, cleanLabel);
                bool isKnownClass = groundTruth.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                                    groundTruth.Equals("NG", StringComparison.OrdinalIgnoreCase);
                bool isKnownPrediction = cleanLabel.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                                         cleanLabel.Equals("NG", StringComparison.OrdinalIgnoreCase);
                // Unknown labels/classes must not silently be counted as correct.
                bool isCorrect = isKnownClass && isKnownPrediction && !isCriticalMiss && !isFalseAlarm &&
                    groundTruth.Equals(cleanLabel, StringComparison.OrdinalIgnoreCase);

                string verdict = isCriticalMiss ? "🔴 Critical miss"
                               : isFalseAlarm ? "🟡 False alarm"
                               : isCorrect ? "✅ Correct"
                               : "⚠️ Unknown / unsupported label";
                string whyShort = why?.OneLine ?? "";

                _checkedCount++;
                _dailyCheckedCount++;
                if (isCorrect)
                {
                    _correctCount++;
                    _dailyCorrectCount++;
                }
                else if (isCriticalMiss)
                {
                    _dailyCriticalMissCount++;
                }
                else if (isFalseAlarm)
                {
                    _dailyFalseAlarmCount++;
                }

                // Persist AFTER all daily counters have been updated.
                _dailySummaryDirty = true;
                SaveDailyInspectionSummary(DateTime.Today);

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
                        Verdict = verdict,
                        Why = whyShort
                    })));

                    if (isCriticalMiss || isFalseAlarm)
                        LogMismatch(isCriticalMiss, fileName, shownPath, groundTruth, cleanLabel, confidence, why);

                    // Keep the second implementation's high-confidence mismatch archive.
                    if ((isCriticalMiss || isFalseAlarm) && confidence > 0.80f)
                    {
                        try
                        {
                            MoveHighConfidenceMismatch(imagePath, groundTruth, cleanLabel, confidence);
                        }
                        catch (Exception ex)
                        {
                            LogDiag($"IMAGE COPY ERROR: {imagePath} | {ex.Message}");
                        }
                    }
                    else if (!isCriticalMiss && !isFalseAlarm)
                    {
                        LogDiag($"UNSUPPORTED LABEL: {fileName} folder={groundTruth} prediction={cleanLabel}");
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
                    string reason = whyShort.Length > 0 ? " Why: " + whyShort + "." : "";
                    Invoke(new Action(() => ShowMonitorSummary(
                        $"   Last: {fileName} is in OK but predicted NG ({MismatchExplainer.Pct(confidence)}).{reason} Double-click its row for details.",
                        Color.FromArgb(255, 248, 220))));
                }
                else if (isCorrect)
                {
                    if (_criticalMissCount == 0 && _usbLight != null && _usbLight.IsConnected)
                        _usbLight.TurnOff();
                    Invoke(new Action(() => ShowMonitorSummary(
                        $"   Last: {fileName} → {cleanLabel} ({MismatchExplainer.Pct(confidence)}) matches folder {groundTruth}",
                        Color.FromArgb(232, 245, 233))));
                }
                else
                {
                    Invoke(new Action(() => ShowMonitorSummary(
                        $"   Last: {fileName} has unsupported folder/prediction: {groundTruth} → {cleanLabel}",
                        Color.FromArgb(255, 235, 238))));
                }

                LogDiag($"PROCESSED {fileName} folder={groundTruth} pred={cleanLabel} " +
                        $"{confidence:P1} {verdict} | {sw.ElapsedMilliseconds} ms | rows={_results.Count}");
            }
            catch (Exception ex)
            {
                LogDiag($"ERROR {Path.GetFileName(imagePath)}: {ex}");
                try { ShowMonitorProblem($"❌ Error on {Path.GetFileName(imagePath)}: {ex.Message}"); } catch { }
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }

        private bool SaveDailyInspectionSummary(DateTime reportDate)
        {
            string csvPath = Path.Combine(
                AppContext.BaseDirectory,
                "DailyInspectionSummary.csv"
            );

            string tempPath = csvPath + ".tmp";

            try
            {
                lock (_dailyCsvLock)
                {
                    var lines = File.Exists(csvPath)
                        ? File.ReadAllLines(csvPath).ToList()
                        : new List<string>();

                    if (lines.Count == 0)
                        lines.Add("Date,FalseAlarms,CriticalMisses,Checked,Correct");

                    string dateKey = reportDate.ToString("yyyy-MM-dd");

                    int existingIndex = lines.FindIndex(line =>
                    {
                        string[] columns = line.Split(',');

                        return columns.Length >= 3 &&
                               columns[0].Trim() == dateKey;
                    });

                    string row =
     $"{dateKey}," +
     $"{_dailyFalseAlarmCount}," +
     $"{_dailyCriticalMissCount}," +
     $"{_dailyCheckedCount}," +
     $"{_dailyCorrectCount}";

                    if (existingIndex >= 0)
                        lines[existingIndex] = row;
                    else
                        lines.Add(row);

                    // Write temporary file and flush to disk
                    using (var fs = new FileStream(
                        tempPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        FileOptions.WriteThrough))
                    using (var writer = new StreamWriter(
                        fs,
                        new System.Text.UTF8Encoding(true)))
                    {
                        foreach (string line in lines)
                            writer.WriteLine(line);

                        writer.Flush();
                        fs.Flush(true);
                    }

                    // Replace existing CSV
                    File.Move(tempPath, csvPath, true);

                    _dailySummaryDirty = false;
                }

                LogMonitor(
                    $"DAILY SUMMARY PERSISTED: {reportDate:yyyy-MM-dd} | " +
                    $"False Alarms={_dailyFalseAlarmCount} | " +
                    $"Critical Misses={_dailyCriticalMissCount}"
                );

                return true;
            }
            catch (Exception ex)
            {
                _dailySummaryDirty = true;

                LogMonitor($"DAILY CSV SAVE ERROR: {ex}");

                return false;
            }
        }

        private void LoadDailyInspectionSummary(DateTime reportDate)
        {
            _dailyFalseAlarmCount = 0;
            _dailyCriticalMissCount = 0;
            _dailyCheckedCount = 0;
            _dailyCorrectCount = 0;

            string csvPath = Path.Combine(
                AppContext.BaseDirectory,
                "DailyInspectionSummary.csv"
            );

            if (!File.Exists(csvPath))
                return;

            try
            {
                lock (_dailyCsvLock)
                {
                    foreach (string line in File.ReadAllLines(csvPath))
                    {
                        string[] columns = line.Split(',');

                        if (columns.Length < 3)
                            continue;

                        if (columns[0].Trim() != reportDate.ToString("yyyy-MM-dd"))
                            continue;

                        int.TryParse(columns[1], out _dailyFalseAlarmCount);
                        int.TryParse(columns[2], out _dailyCriticalMissCount);

                        // New columns; older CSV rows may not contain them.
                        if (columns.Length >= 4)
                            int.TryParse(columns[3], out _dailyCheckedCount);

                        if (columns.Length >= 5)
                            int.TryParse(columns[4], out _dailyCorrectCount);

                        break;
                    }
                }

                LogMonitor(
                    $"DAILY COUNTS RESTORED: {reportDate:yyyy-MM-dd} | " +
                    $"False Alarms={_dailyFalseAlarmCount} | " +
                    $"Critical Misses={_dailyCriticalMissCount}"
                );
            }
            catch (Exception ex)
            {
                LogMonitor($"DAILY CSV LOAD ERROR: {ex}");
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
        //  Mismatch and diagnostic logs
        // ─────────────────────────────────────────────────────────────────

        // Ignore high-volume queue chatter; retain watcher and health events in the diagnostic log.
        private void OnMonitorEngineLog(string message)
        {
            if (message.StartsWith("EVENT", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("QUEUED", StringComparison.OrdinalIgnoreCase))
                return;
            LogDiag(message);
        }

        private void LogMismatch(bool criticalMiss, string fileName, string path, string folder,
                                 string predicted, float confidence, Explanation? why)
        {
            string kind = criticalMiss
                ? "CRITICAL MISS (folder NG, model OK)"
                : "FALSE ALARM (folder OK, model NG)";
            string detail = why != null
                ? why.LogText
                : "scores: " + predicted + " " + MismatchExplainer.Pct(confidence) + " | why: not available";

            WriteLog("monitor.log",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {kind} | {fileName} | " +
                $"folder={folder} model={predicted} {MismatchExplainer.Pct(confidence)} | {detail} | " +
                $"roi={_roiRect.X},{_roiRect.Y},{_roiRect.Width},{_roiRect.Height} | {path}");
        }

        private static void LogDiag(string message)
        {
            WriteLog("monitor.diag.log", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }

        private static void WriteLog(string fileName, string line)
        {
            try
            {
                lock (_monitorLogLock)
                {
                    string path = Path.Combine(AppContext.BaseDirectory, fileName);
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 5_000_000)
                    {
                        string old = Path.ChangeExtension(path, ".old.log");
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never interrupt image monitoring.
            }
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

        // Existing date/CSV/health messages are diagnostic events, not inspection mismatches.
        private static void LogMonitor(string message) => LogDiag(message);
    }
}