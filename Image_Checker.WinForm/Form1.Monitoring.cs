using Image_Checker.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static Image_Checker.Services.IncrementalModelTrainer;

namespace Image_Checker.WinForm
{
    // Multi-source folder monitoring for Form1.
    // Each source = one base folder (containing yy\MM\dd) + its own model + its own daily counters.
    public partial class Form1
    {
        // ─────────────────────────────────────────────────────────────────
        //  Per-source runtime state
        // ─────────────────────────────────────────────────────────────────
        private sealed class SourceRuntime
        {
            public MonitorProfile Profile = null!;
            public FolderImageMonitor? Monitor;
           
            public Rectangle Roi;                 // this model's ROI
            public string? Folder;                // today's folder being watched
            public string? WaitingFolder;
            public DateTime Date = DateTime.MinValue;
            public readonly Dictionary<string, int> Retries = new();
            

            public int FalseAlarms, CriticalMisses, Checked, Correct;
            public bool Dirty;

            public string Name => string.IsNullOrWhiteSpace(Profile.Name) ? Profile.FolderPath : Profile.Name;

            public ModelType Type = ModelType.MlNet;
            public Predictor? Predictor;
            public TorchImageChecker? Torch;
            public OnnxImageChecker? Onnx;
            public OneClassChecker? OneClass;
            public Size TrainingSize = new Size(224, 224);
            public string Signature = "";
            public CancellationTokenSource? Cts;
            public Task? Worker;
            public TrainingStats? Training;
            public string BasePath => Path.GetDirectoryName(Profile.ModelPath) ?? "";
            public List<ScrewPosition> Positions = new();

            public void DisposeModel()
            {
                Predictor?.Dispose(); Predictor = null;
                Torch?.Dispose(); Torch = null;
                Onnx?.Dispose(); Onnx = null;
                OneClass?.Dispose(); OneClass = null;
            }
        }

        private readonly List<SourceRuntime> _sources = new();
        private List<MonitorProfile> _profiles = new();
        private int _rrIndex;

        private int _monitorScanIntervalMs = 5000;
        private int _checkedCount = 0;
        private int _correctCount = 0;
        private string _lastMonitorLine = "";
        private Color _lastMonitorBack = SystemColors.Control;
        private static readonly object _monitorLogLock = new();
        private static readonly object _dailyCsvLock = new();

        private bool _hasMainSnapshot;
        private ModelType _mainType;
        private Predictor? _mainPredictor;
        private TorchImageChecker? _mainTorch;
        private OnnxImageChecker? _mainOnnx;
        private OneClassChecker? _mainOneClass;
        private Rectangle _mainRoi;
        private Size _mainSize;
        private string? _mainFolder;
        private static readonly object _predictionCsvLock = new();

        // ─────────────────────────────────────────────────────────────────
        //  Model hooks  (the only part that needs YOUR existing code)
        // ─────────────────────────────────────────────────────────────────


        private void ActivateSource(SourceRuntime s)
        {
            _activeModelType = s.Type;
            _predictor = s.Predictor;
            _torchChecker = s.Torch;
            _onnxChecker = s.Onnx;
            _oneClassChecker = s.OneClass;
            _roiRect = s.Roi;
            _trainingImageSize = s.TrainingSize;
            _monitoredFolder = s.Folder;
        }

        /// <summary>
        /// Load the model in s.Profile.ModelPath into s.Predictor and its ROI into s.Roi.
        /// Return false if it cannot be loaded.
        /// TODO: call your EXISTING model-loading code here, but store the results in
        ///       s.Predictor / s.Roi instead of _predictor / _roiRect.
        /// </summary>
        private bool LoadModelForSource(SourceRuntime s)
        {
            string path = s.Profile.ModelPath;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string jsonPath = Path.ChangeExtension(path, ".json");

            // ROI + training size from <model>.json (same file your form already reads)
            if (File.Exists(jsonPath))
            {
                var cfg = System.Text.Json.JsonSerializer.Deserialize<RoiConfig>(
                    File.ReadAllText(jsonPath),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (cfg != null)
                {
                    s.Roi = new Rectangle(cfg.RoiX, cfg.RoiY, cfg.RoiW, cfg.RoiH);
                    s.TrainingSize = new Size(
                        cfg.ImageWidth > 0 ? cfg.ImageWidth : 224,
                        cfg.ImageHeight > 0 ? cfg.ImageHeight : 224);
                    s.Positions = cfg.Positions ?? new();
                    if (s.Positions.Count > 0) s.Roi = Rectangle.Empty;   // positions replace the single ROI
                }
            }

            switch (ext)
            {
                case ".zip":
                    s.Type = ModelType.MlNet;
                    s.Predictor = new Predictor(path);
                    return true;

                case ".bin":
                    bool oneClass = false;
                    if (File.Exists(jsonPath))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
                        oneClass = doc.RootElement.TryGetProperty("ModelType", out var mt) &&
                                   mt.GetString() == "OneClassAutoencoder";
                    }
                    if (oneClass) { s.Type = ModelType.OneClass; s.OneClass = new OneClassChecker(path); }
                    else { s.Type = ModelType.TorchSharp; s.Torch = new TorchImageChecker(path); }
                    return true;

                case ".onnx":
                    s.Type = ModelType.Onnx;
                    s.Onnx = new OnnxImageChecker(path);
                    return true;

                default:
                    throw new NotSupportedException($"Unsupported model format: {ext}");
            }
        }

        private Button? btnMonitorSources;

        private void EnsureMonitorSourcesButton()
        {
            if (btnMonitorSources != null) return;

            btnMonitorSources = new Button
            {
                Name = "btnMonitorSources",
                Text = "📁 Monitor Folders…",
                Size = new Size(Math.Max(150, btnMonitorFolder.Width), btnMonitorFolder.Height),
                Margin = btnMonitorFolder.Margin,
                Font = btnMonitorFolder.Font
            };
            btnMonitorSources.Click += BtnMonitorSources_Click;

            var parent = btnMonitorFolder.Parent!;
            if (parent is FlowLayoutPanel)
            {
                parent.Controls.Add(btnMonitorSources);
                parent.Controls.SetChildIndex(btnMonitorSources,
                    parent.Controls.GetChildIndex(btnMonitorFolder));   // directly after the Monitor button
            }
            else
            {
                // absolute layout: place it to the right of the Monitor button
                btnMonitorSources.Location = new Point(btnMonitorFolder.Right + 8, btnMonitorFolder.Top);
                parent.Controls.Add(btnMonitorSources);
                btnMonitorSources.BringToFront();
            }

            // Fallback that always works: right-click the Monitor button
            var menu = new ContextMenuStrip();
            menu.Items.Add("Monitor Folders…", null, BtnMonitorSources_Click);
            btnMonitorFolder.ContextMenuStrip = menu;
        }
        private sealed class ModelJson
        {
            public int RoiX { get; set; }
            public int RoiY { get; set; }
            public int RoiW { get; set; }
            public int RoiH { get; set; }
            public int ImageWidth { get; set; }
            public int ImageHeight { get; set; }
            public float NgThreshold { get; set; } = 0.5f;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Profiles
        // ─────────────────────────────────────────────────────────────────

        private void LoadProfiles()
        {
            _profiles = MonitorProfileStore.Load(AppContext.BaseDirectory);
            if (_profiles.Count > 0) return;

            // One-time import of the old single MonitorBasePath
            try
            {
                string legacy = GetMonitorBasePath();
                _profiles.Add(new MonitorProfile
                {
                    Name = new DirectoryInfo(legacy).Name,
                    FolderPath = legacy
                });
            }
            catch { /* no config yet: the user adds sources in the dialog */ }
        }

        // Wire this to a new button "btnMonitorSources" (text: "Monitor Folders…")
        private void BtnMonitorSources_Click(object? sender, EventArgs e)
        {
            if (_sources.Count > 0)
            {
                MessageBox.Show("Stop monitoring before editing the folders.", "Monitoring is running",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LoadProfiles();
            using var dlg = new MonitorProfilesForm(_profiles);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _profiles = dlg.Profiles.ToList();
            MonitorProfileStore.Save(AppContext.BaseDirectory, _profiles);
        }

        // Kept for the one-time import of the old setting
        private string GetMonitorBasePath()
        {
            string configPath = Path.Combine(AppContext.BaseDirectory, "app_config.txt");
            if (!File.Exists(configPath))
                throw new FileNotFoundException("app_config.txt not found.", configPath);

            string? configured = File.ReadAllLines(configPath).FirstOrDefault(x =>
                x.TrimStart().StartsWith("MonitorBasePath=", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException("MonitorBasePath is missing in app_config.txt.");

            string path = configured.Substring(configured.IndexOf('=') + 1).Trim();
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("MonitorBasePath cannot be empty.");

            return Path.GetFullPath(path);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Start / stop
        // ─────────────────────────────────────────────────────────────────

        //private void BtnMonitorFolder_Click(object sender, EventArgs e)
        //{
        //    if (_sources.Count == 0) StartFolderMonitoring();
        //    else StopFolderMonitoring();
        //}

        //  private void StartFolderMonitoring()
        //  {
        //      LoadProfiles();
        //      if (_profiles.Count == 0)
        //      {
        //          BtnMonitorSources_Click(this, EventArgs.Empty);
        //          if (_profiles.Count == 0) return;
        //      }

        //      var failed = new List<string>();
        //      _criticalMissCount = _falseAlarmCount = _abnormalCount = _checkedCount = _correctCount = 0;
        //      _lastMonitorLine = "   Waiting for new images...";
        //      _lastMonitorBack = SystemColors.Control;

        //      _mainType = _activeModelType; _mainPredictor = _predictor; _mainTorch = _torchChecker;
        //      _mainOnnx = _onnxChecker; _mainOneClass = _oneClassChecker;
        //      _mainRoi = _roiRect; _mainSize = _trainingImageSize; _mainFolder = _monitoredFolder;
        //      _hasMainSnapshot = true;

        //      foreach (var p in _profiles.Where(p => p.Enabled))
        //      {
        //          // Base folder or model missing -> the user is asked to select it
        //          if (!MonitorPathResolver.EnsureAvailable(this, p))
        //          {
        //              failed.Add($"{p.Name}: cancelled");
        //              continue;
        //          }

        //          var s = new SourceRuntime { Profile = p };
        //          try
        //          {
        //              if (!LoadModelForSource(s)) { failed.Add($"{p.Name}: model failed to load"); continue; }
        //              _sources.Add(s);
        //          }
        //          catch (Exception ex)
        //          {
        //              failed.Add($"{p.Name}: {ex.Message}");
        //          }
        //      }

        //      MonitorProfileStore.Save(AppContext.BaseDirectory, _profiles);   // keep corrected paths

        //      if (_sources.Count == 0)
        //      {
        //          MessageBox.Show("No folder could be started.\n\n" + string.Join("\n", failed),
        //              "Monitoring", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //          return;
        //      }

        //      try
        //      {
        //          _processingTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        //          _processingTimer.Tick += ProcessImageQueue;
        //          _processingTimer.Start();

        //          WarmTrainingStats();

        //          btnMonitorFolder.Text = "👁️ Monitor Folder (ON)";
        //          btnMonitorFolder.BackColor = Color.FromArgb(76, 175, 80);
        //          ApplyMonitorStatus(force: true);

        //          MessageBox.Show(
        //$"Monitoring {_sources.Count} source(s):\n\n" +
        //string.Join("\n", _sources.Select(s =>
        //    $"• {s.Name}: {s.Profile.ResolveRoot(DateTime.Today)}" +
        //    (s.Profile.UseDateFolders ? $"  (format {s.Profile.DateFormat})" : "  (scanned directly)"))) +
        //(failed.Count > 0 ? "\n\nSkipped:\n" + string.Join("\n", failed) : "") +
        //"\n\nDate-folder sources switch automatically at 00:00, even if the new folder appears later.\n" +
        //"Only mismatches are listed in the grid.\n\n" +
        //"🔴 Critical misses (NG → OK) trigger the USB light and red alert.",
        //"Monitoring Active", MessageBoxButtons.OK, MessageBoxIcon.Information);
        //      }
        //      catch (Exception ex)
        //      {
        //          MessageBox.Show($"Failed to start monitoring:\n\n{ex.Message}", "Error",
        //              MessageBoxButtons.OK, MessageBoxIcon.Error);
        //          StopFolderMonitoring();
        //      }
        //  }

        //  private void StopFolderMonitoring()
        //  {
        //      foreach (var s in _sources)
        //      {
        //          if (s.Date != DateTime.MinValue && !SaveDailyInspectionSummary(s, s.Date.Date))
        //              MessageBox.Show($"Unable to save the daily summary for '{s.Name}'.\nPlease check CSV file permissions.",
        //                  "CSV Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        //          if (s.Monitor != null)
        //          {
        //              s.Monitor.Log -= OnMonitorEngineLog;
        //              s.Monitor.Dispose();
        //              s.Monitor = null;
        //          }
        //          s.DisposeModel();
        //          // If your Predictor is IDisposable: s.Predictor?.Dispose();
        //      }
        //      _sources.Clear();
        //      if (_hasMainSnapshot)
        //      {
        //          _activeModelType = _mainType; _predictor = _mainPredictor; _torchChecker = _mainTorch;
        //          _onnxChecker = _mainOnnx; _oneClassChecker = _mainOneClass;
        //          _roiRect = _mainRoi; _trainingImageSize = _mainSize;
        //          _hasMainSnapshot = false;
        //      }
        //      if (_processingTimer != null)
        //      {
        //          _processingTimer.Stop();
        //          _processingTimer.Dispose();
        //          _processingTimer = null;
        //      }

        //      _monitoredFolder = null;
        //      _abnormalCount = 0;

        //      if (_usbLight != null && _usbLight.IsConnected) _usbLight.TurnOff();

        //      btnMonitorFolder.Text = "👁️ Monitor Folder (OFF)";
        //      btnMonitorFolder.BackColor = Color.FromArgb(156, 39, 176);
        //      lblStatus.Text = "⏹️ Folder monitoring stopped";
        //  }

        // ─────────────────────────────────────────────────────────────────
        //  Date rollover per source
        // ─────────────────────────────────────────────────────────────────


        private static string Sig(MonitorProfile p) =>
    $"{p.FolderPath}|{p.ModelPath}|{p.UseDateFolders}|{p.DateFormat}";

        private void BtnMonitorFolder_Click(object sender, EventArgs e)
        {
            if (_processingTimer == null) StartFolderMonitoring();
            else StopFolderMonitoring();
        }

        //private void BtnMonitorSources_Click(object? sender, EventArgs e)
        //{
        //    bool running = _processingTimer != null;
        //    if (!running) LoadProfiles();          // while running, keep the in-memory list

        //    using var dlg = new MonitorProfilesForm(_profiles.Select(p => p.Clone()));
        //    if (dlg.ShowDialog(this) != DialogResult.OK) return;

        //    _profiles = dlg.Profiles.ToList();
        //    MonitorProfileStore.Save(AppContext.BaseDirectory, _profiles);
        //    if (running) ApplyProfilesLive();
        //}

        private void ApplyProfilesLive()
        {
            var failed = new List<string>();

            // stop sources that were removed, disabled or changed
            foreach (var s in _sources.ToList())
            {
                var np = _profiles.FirstOrDefault(x => x.Id == s.Profile.Id);
                if (np == null || !np.Enabled || Sig(np) != s.Signature) StopSource(s);
                else s.Profile = np;               // picks up a renamed source
            }

            // start new / changed ones
            foreach (var p in _profiles.Where(p => p.Enabled && !_sources.Any(s => s.Profile.Id == p.Id)))
                TryStartSource(p, failed);

            MonitorProfileStore.Save(AppContext.BaseDirectory, _profiles);   // keeps corrected paths
            ApplyMonitorStatus(force: true);

            if (failed.Count > 0)
                MessageBox.Show("Could not start:\n\n" + string.Join("\n", failed),
                    "Monitor Folders", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private bool TryStartSource(MonitorProfile p, List<string> failed)
        {
            if (!MonitorPathResolver.EnsureAvailable(this, p)) { failed.Add($"{p.Name}: cancelled"); return false; }

            var s = new SourceRuntime { Profile = p, Signature = Sig(p) };
            try
            {
                if (!LoadModelForSource(s)) { s.DisposeModel(); failed.Add($"{p.Name}: model failed to load"); return false; }
            }
            catch (Exception ex) { s.DisposeModel(); failed.Add($"{p.Name}: {ex.Message}"); return false; }

            _sources.Add(s);
            WarmTrainingStats(s);
            EnsureCurrentDateMonitor(s);           // start watching right away
            s.Cts = new CancellationTokenSource();
            s.Worker = Task.Factory.StartNew(() => RunWorker(s, s.Cts.Token),
                s.Cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            return true;
        }

        private void StopSource(SourceRuntime s)
        {
            s.Cts?.Cancel();

            // wait for the worker; DoEvents lets its UI calls finish so we cannot deadlock
            var w = s.Worker;
            var until = DateTime.UtcNow.AddSeconds(5);
            while (w != null && !w.IsCompleted && DateTime.UtcNow < until)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }

            if (s.Date != DateTime.MinValue) SaveDailyInspectionSummary(s, s.Date.Date);

            if (s.Monitor != null)
            {
                s.Monitor.Log -= OnMonitorEngineLog;
                s.Monitor.Dispose();
                s.Monitor = null;
            }
            if (w == null || w.IsCompleted) s.DisposeModel();   // never dispose a model that is mid-prediction
            s.Cts?.Dispose();
            _sources.Remove(s);
        }

        private void StartFolderMonitoring()
        {
            LoadProfiles();
            if (_profiles.Count == 0)
            {
                BtnMonitorSources_Click(this, EventArgs.Empty);
                if (_profiles.Count == 0) return;
            }

            var failed = new List<string>();
            _criticalMissCount = _falseAlarmCount = _abnormalCount = _checkedCount = _correctCount = 0;
            _lastMonitorLine = "   Waiting for new images...";
            _lastMonitorBack = SystemColors.Control;

            foreach (var p in _profiles.Where(p => p.Enabled))
                TryStartSource(p, failed);

            MonitorProfileStore.Save(AppContext.BaseDirectory, _profiles);

            if (_sources.Count == 0)
            {
                MessageBox.Show("No folder could be started.\n\n" + string.Join("\n", failed),
                    "Monitoring", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _processingTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _processingTimer.Tick += ProcessImageQueue;     // now only housekeeping, see section 5
            _processingTimer.Start();
            //WarmTrainingStats();

            btnMonitorFolder.Text = "👁️ Monitor Folder (ON)";
            btnMonitorFolder.BackColor = Color.FromArgb(76, 175, 80);
            ApplyMonitorStatus(force: true);

            MessageBox.Show(
                $"Monitoring {_sources.Count} source(s):\n\n" +
                string.Join("\n", _sources.Select(s =>
                    $"• {s.Name}: {s.Profile.ResolveRoot(DateTime.Today)}" +
                    (s.Profile.UseDateFolders ? $"  (format {s.Profile.DateFormat})" : "  (scanned directly)"))) +
                (failed.Count > 0 ? "\n\nSkipped:\n" + string.Join("\n", failed) : "") +
                "\n\nYou can add or remove folders at any time with 'Monitor Folders…'.\n" +
                "Only mismatches are listed in the grid.\n\n" +
                "🔴 Critical misses (NG → OK) trigger the USB light and red alert.",
                "Monitoring Active", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void StopFolderMonitoring()
        {
            if (_processingTimer != null)
            {
                _processingTimer.Stop();
                _processingTimer.Dispose();
                _processingTimer = null;
            }

            foreach (var s in _sources.ToList()) StopSource(s);

            _monitoredFolder = null;
            _abnormalCount = 0;
            if (_usbLight != null && _usbLight.IsConnected) _usbLight.TurnOff();

            btnMonitorFolder.Text = "👁️ Monitor Folder (OFF)";
            btnMonitorFolder.BackColor = Color.FromArgb(156, 39, 176);
            lblStatus.Text = "⏹️ Folder monitoring stopped";
        }

        private bool EnsureCurrentDateMonitor(SourceRuntime s)
        {
            DateTime today = DateTime.Today;
            bool dated = s.Profile.UseDateFolders;

            if (s.Date.Date != today)
            {
                // Date-folder mode: finish yesterday's queued images before switching
                if (dated && s.Monitor != null && s.Monitor.QueueCount > 0)
                    return true;

                // The daily counters always roll over at midnight, in both modes
                if (s.Date != DateTime.MinValue && !SaveDailyInspectionSummary(s, s.Date.Date))
                    return false;   // retry next tick

                LogMonitor($"[{s.Name}] DATE CHANGED: {s.Date:yyyy-MM-dd} -> {today:yyyy-MM-dd}");
                LoadDailyInspectionSummary(s, today);
                s.Date = today;

                // Only date-folder sources move to a new folder; a scanned folder stays as it is
                if (dated && s.Monitor != null)
                {
                    s.Monitor.Log -= OnMonitorEngineLog;
                    s.Monitor.Dispose();
                    s.Monitor = null;
                    s.Retries.Clear();
                    s.Folder = null;
                    s.WaitingFolder = null;
                }
            }

            if (s.Monitor != null) return true;

            try
            {
                string root = s.Profile.ResolveRoot(today);

                // Never create the folder here; wait for the production system to create it
                if (!Directory.Exists(root))
                {
                    if (s.WaitingFolder != root)
                    {
                        LogMonitor($"[{s.Name}] WAITING: folder not found: {root}");
                        s.WaitingFolder = root;
                    }
                    return false;
                }

                var m = new FolderImageMonitor(root, _monitorScanIntervalMs);
                m.Log += OnMonitorEngineLog;
                m.Start();

                s.Monitor = m;
                s.Folder = root;
                s.WaitingFolder = null;
                LogMonitor($"[{s.Name}] MONITORING STARTED: {root}");
                return true;
            }
            catch (Exception ex)
            {
                LogMonitor($"[{s.Name}] DATE SWITCH ERROR: {ex}");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Timer tick: one image per tick, round-robin across sources
        // ─────────────────────────────────────────────────────────────────

        private static bool IsCriticalMissPair(string folder, string label) =>
            folder.Equals("NG", StringComparison.OrdinalIgnoreCase) &&
            label.Equals("OK", StringComparison.OrdinalIgnoreCase);

        private static bool IsFalseAlarmPair(string folder, string label) =>
            folder.Equals("OK", StringComparison.OrdinalIgnoreCase) &&
            label.Equals("NG", StringComparison.OrdinalIgnoreCase);

        //private void ProcessImageQueue(object? sender, EventArgs e)
        //{
        //    if (_isProcessingQueue) return;
        //    if (_sources.Count == 0) return;

        //    foreach (var c in _sources)
        //        EnsureCurrentDateMonitor(c);

        //    // Pick the next source that has a queued image
        //    SourceRuntime? s = null;
        //    for (int i = 0; i < _sources.Count; i++)
        //    {
        //        var c = _sources[(_rrIndex + i) % _sources.Count];
        //        if (c.Dirty) SaveDailyInspectionSummary(c, c.Date);
        //        if (c.Monitor != null && c.Monitor.QueueCount > 0)
        //        {
        //            s = c;
        //            _rrIndex = (_rrIndex + i + 1) % _sources.Count;
        //            break;
        //        }
        //    }

        //    if (s == null) { ApplyMonitorStatus(force: false); return; }

        //    ActivateSource(s);
        //    var monitor = s.Monitor!;

        //    _isProcessingQueue = true;
        //    string imagePath = "";
        //    var sw = System.Diagnostics.Stopwatch.StartNew();

        //    try
        //    {
        //        bool hasModel = s.Type switch
        //        {
        //            ModelType.MlNet => s.Predictor != null,
        //            ModelType.TorchSharp => s.Torch != null,
        //            ModelType.OneClass => s.OneClass != null,
        //            _ => s.Onnx != null
        //        };
        //        if (!hasModel)
        //        {
        //            ShowMonitorProblem($"⚠️ [{s.Name}] No model loaded.");
        //            return;
        //        }

        //        if (!monitor.TryDequeue(out imagePath)) return;

        //        if (!File.Exists(imagePath))
        //        {
        //            s.Retries.Remove(imagePath);
        //            return;
        //        }

        //        // Wait for the production system to finish writing the image
        //        if (!FolderImageMonitor.IsFileReady(imagePath))
        //        {
        //            s.Retries.TryGetValue(imagePath, out int tries);
        //            if (tries < 15)
        //            {
        //                s.Retries[imagePath] = tries + 1;
        //                monitor.Requeue(imagePath);
        //                _lastMonitorLine = $"   ⏳ [{s.Name}] Waiting for {Path.GetFileName(imagePath)} to finish writing (attempt {tries + 1}/15)...";
        //                ApplyMonitorStatus(force: false);
        //                if (tries == 0) LogDiag($"[{s.Name}] NOT READY (still being written): {imagePath}");
        //            }
        //            else
        //            {
        //                s.Retries.Remove(imagePath);
        //                LogDiag($"[{s.Name}] GAVE UP (file stayed locked by a writer): {imagePath}");
        //                _lastMonitorLine = $"   ❌ [{s.Name}] Gave up on {Path.GetFileName(imagePath)} - file stayed locked.";
        //                ApplyMonitorStatus(force: false);
        //            }
        //            return;
        //        }
        //        s.Retries.Remove(imagePath);

        //        // Ground truth is the parent class-folder name (OK / NG)
        //        string groundTruth = new DirectoryInfo(Path.GetDirectoryName(imagePath)!).Name;
        //        string processedPath = PreprocessForPrediction(imagePath);
        //        bool isTempFile = !string.Equals(processedPath, imagePath, StringComparison.OrdinalIgnoreCase);

        //        (string Label, float Confidence) prediction;
        //        Explanation? why = null;
        //        try
        //        {
        //            prediction = PredictAny(processedPath);
        //            string predictedForExplain = (prediction.Label ?? "").Trim().Trim('"');

        //            if (IsCriticalMissPair(groundTruth, predictedForExplain) ||
        //                IsFalseAlarmPair(groundTruth, predictedForExplain))
        //            {
        //                try { why = QuickExplain(processedPath, imagePath, groundTruth); }
        //                catch (Exception ex) { LogDiag($"[{s.Name}] EXPLAIN ERROR {Path.GetFileName(imagePath)}: {ex.Message}"); }
        //            }
        //        }
        //        finally
        //        {
        //            if (isTempFile && File.Exists(processedPath))
        //                File.Delete(processedPath);
        //        }

        //        var (label, confidence) = prediction;
        //        string cleanLabel = label?.Trim().Trim('"') ?? "Unknown";
        //        string fileName = Path.GetFileName(imagePath);
        //        string shownPath = imagePath;

        //        bool isCriticalMiss = IsCriticalMissPair(groundTruth, cleanLabel);
        //        bool isFalseAlarm = IsFalseAlarmPair(groundTruth, cleanLabel);
        //        bool isKnownClass = groundTruth.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
        //                            groundTruth.Equals("NG", StringComparison.OrdinalIgnoreCase);
        //        bool isKnownPrediction = cleanLabel.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
        //                                 cleanLabel.Equals("NG", StringComparison.OrdinalIgnoreCase);
        //        bool isCorrect = isKnownClass && isKnownPrediction && !isCriticalMiss && !isFalseAlarm &&
        //                         groundTruth.Equals(cleanLabel, StringComparison.OrdinalIgnoreCase);

        //        string verdict = isCriticalMiss ? "🔴 Critical miss"
        //                       : isFalseAlarm ? "🟡 False alarm"
        //                       : isCorrect ? "✅ Correct"
        //                       : "⚠️ Unknown / unsupported label";
        //        string whyShort = why?.OneLine ?? "";

        //        _checkedCount++;
        //        s.Checked++;
        //        if (isCorrect) { _correctCount++; s.Correct++; }
        //        else if (isCriticalMiss) s.CriticalMisses++;
        //        else if (isFalseAlarm) s.FalseAlarms++;

        //        s.Dirty = true;
        //        SaveDailyInspectionSummary(s, DateTime.Today);

        //        if (!isCorrect)
        //        {
        //            string sourceName = s.Name;
        //            Invoke(new Action(() => AddMonitoredRow(new ImageResult
        //            {
        //                FileName = fileName,
        //                ImagePath = shownPath,
        //                Subfolder = groundTruth,
        //                PredictedLabel = cleanLabel,
        //                CorrectedLabel = cleanLabel,
        //                Confidence = confidence,
        //                Verdict = verdict,
        //                Why = $"[{sourceName}] {whyShort}"
        //            })));

        //            if (isCriticalMiss || isFalseAlarm)
        //                LogMismatch(s, isCriticalMiss, fileName, shownPath, groundTruth, cleanLabel, confidence, why);

        //            if ((isCriticalMiss || isFalseAlarm) && confidence > 0.80f)
        //            {
        //                try { MoveHighConfidenceMismatch(s, imagePath, groundTruth, cleanLabel, confidence); }
        //                catch (Exception ex) { LogDiag($"[{s.Name}] IMAGE COPY ERROR: {imagePath} | {ex.Message}"); }
        //            }
        //            else if (!isCriticalMiss && !isFalseAlarm)
        //            {
        //                LogDiag($"[{s.Name}] UNSUPPORTED LABEL: {fileName} folder={groundTruth} prediction={cleanLabel}");
        //            }
        //        }

        //        string tag = $"[{s.Name}] ";
        //        if (isCriticalMiss)
        //        {
        //            _criticalMissCount++;
        //            _abnormalCount++;
        //            if (_usbLight != null && _usbLight.IsConnected) _usbLight.TurnOn();
        //            Invoke(new Action(() => FlashCriticalAlert(shownPath, fileName, confidence)));
        //        }
        //        else if (isFalseAlarm)
        //        {
        //            _falseAlarmCount++;
        //            string reason = whyShort.Length > 0 ? " Why: " + whyShort + "." : "";
        //            Invoke(new Action(() => ShowMonitorSummary(
        //                $"   Last: {tag}{fileName} is in OK but predicted NG ({MismatchExplainer.Pct(confidence)}).{reason} Double-click its row for details.",
        //                Color.FromArgb(255, 248, 220))));
        //        }
        //        else if (isCorrect)
        //        {
        //            if (_criticalMissCount == 0 && _usbLight != null && _usbLight.IsConnected)
        //                _usbLight.TurnOff();
        //            Invoke(new Action(() => ShowMonitorSummary(
        //                $"   Last: {tag}{fileName} → {cleanLabel} ({MismatchExplainer.Pct(confidence)}) matches folder {groundTruth}",
        //                Color.FromArgb(232, 245, 233))));
        //        }
        //        else
        //        {
        //            Invoke(new Action(() => ShowMonitorSummary(
        //                $"   Last: {tag}{fileName} has unsupported folder/prediction: {groundTruth} → {cleanLabel}",
        //                Color.FromArgb(255, 235, 238))));
        //        }

        //        LogDiag($"[{s.Name}] PROCESSED {fileName} folder={groundTruth} pred={cleanLabel} " +
        //                $"{confidence:P1} {verdict} | {sw.ElapsedMilliseconds} ms | rows={_results.Count}");
        //    }
        //    catch (Exception ex)
        //    {
        //        LogDiag($"[{s.Name}] ERROR {Path.GetFileName(imagePath)}: {ex}");
        //        try { ShowMonitorProblem($"❌ [{s.Name}] Error on {Path.GetFileName(imagePath)}: {ex.Message}"); } catch { }
        //    }
        //    finally
        //    {
        //        _isProcessingQueue = false;
        //    }
        //}

        private DateTime _lastSummarySave = DateTime.MinValue;

        private void ProcessImageQueue(object? sender, EventArgs e)
        {
            foreach (var c in _sources.ToList()) EnsureCurrentDateMonitor(c);

            if ((DateTime.Now - _lastSummarySave).TotalSeconds >= 5)
            {
                _lastSummarySave = DateTime.Now;
                foreach (var c in _sources)
                    if (c.Dirty) SaveDailyInspectionSummary(c, c.Date);
            }

            ApplyMonitorStatus(force: false);
        }

        private void Post(Action a)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); } catch { }
        }

        // ───────── one worker per source ─────────
        private void RunWorker(SourceRuntime s, CancellationToken ct)
        {
            var tries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            while (!ct.IsCancellationRequested)
            {
                var m = s.Monitor;                       // may be replaced at midnight
                if (m == null || !m.TryDequeue(out string path))
                {
                    ct.WaitHandle.WaitOne(100);
                    continue;
                }

                try { ProcessOne(s, m, path, tries); }
                catch (Exception ex)
                {
                    LogDiag($"[{s.Name}] ERROR {Path.GetFileName(path)}: {ex}");
                    Post(() => ShowMonitorProblem($"❌ [{s.Name}] Error on {Path.GetFileName(path)}: {ex.Message}"));
                }
            }
        }

        private void ProcessOne(SourceRuntime s, FolderImageMonitor m, string imagePath, Dictionary<string, int> tries)
        {
            var sw = Stopwatch.StartNew();

            if (!File.Exists(imagePath)) { tries.Remove(imagePath); return; }

            // still being written: put it back and move on
            if (!FolderImageMonitor.IsFileReady(imagePath))
            {
                tries.TryGetValue(imagePath, out int n);
                if (n < 30)
                {
                    tries[imagePath] = n + 1;
                    m.Requeue(imagePath);
                    if (n == 0) LogDiag($"[{s.Name}] NOT READY (still being written): {imagePath}");
                    Thread.Sleep(200);
                }
                else
                {
                    tries.Remove(imagePath);
                    LogDiag($"[{s.Name}] GAVE UP (file stayed locked by a writer): {imagePath}");
                }
                return;
            }
            tries.Remove(imagePath);

            string groundTruth = new DirectoryInfo(Path.GetDirectoryName(imagePath)!).Name;
            string processed = PreprocessFor(s, imagePath);
            bool isTemp = !string.Equals(processed, imagePath, StringComparison.OrdinalIgnoreCase);
            string posDetail = "";
            (string Label, float Confidence) pred;
            Explanation? why = null;
            try
            {
                pred = PredictFor(s, processed, out posDetail);
                string lab = (pred.Label ?? "").Trim().Trim('"');

                if (IsCriticalMissPair(groundTruth, lab) || IsFalseAlarmPair(groundTruth, lab))
                {
                    // QuickExplain uses the form's fields: run it on the UI thread with this source swapped in
                    try { why = QuickExplainFor(s, processed, imagePath, groundTruth); }
                    catch (Exception ex) { LogDiag($"[{s.Name}] EXPLAIN ERROR {Path.GetFileName(imagePath)}: {ex.Message}"); }
                }
            }
            finally
            {
                if (isTemp && File.Exists(processed)) File.Delete(processed);
            }

            float confidence = pred.Confidence;
            string cleanLabel = pred.Label?.Trim().Trim('"') ?? "Unknown";
            string fileName = Path.GetFileName(imagePath);

            bool isCriticalMiss = IsCriticalMissPair(groundTruth, cleanLabel);
            bool isFalseAlarm = IsFalseAlarmPair(groundTruth, cleanLabel);
            bool isKnownClass = groundTruth.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                                groundTruth.Equals("NG", StringComparison.OrdinalIgnoreCase);
            bool isKnownPred = cleanLabel.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                               cleanLabel.Equals("NG", StringComparison.OrdinalIgnoreCase);
            bool isCorrect = isKnownClass && isKnownPred && !isCriticalMiss && !isFalseAlarm &&
                             groundTruth.Equals(cleanLabel, StringComparison.OrdinalIgnoreCase);

            string verdict = isCriticalMiss ? "🔴 Critical miss"
                           : isFalseAlarm ? "🟡 False alarm"
                           : isCorrect ? "✅ Correct"
                           : "⚠️ Unknown / unsupported label";

            // slow disk work stays on the worker
            if (isCriticalMiss || isFalseAlarm)
            {
                LogMismatch(s, isCriticalMiss, fileName, imagePath, groundTruth, cleanLabel, confidence, why);
                if (confidence > 0.80f)
                {
                    try { MoveHighConfidenceMismatch(s, imagePath, groundTruth, cleanLabel, confidence); }
                    catch (Exception ex) { LogDiag($"[{s.Name}] IMAGE COPY ERROR: {imagePath} | {ex.Message}"); }
                }
            }
            else if (!isCorrect)
                LogDiag($"[{s.Name}] UNSUPPORTED LABEL: {fileName} folder={groundTruth} prediction={cleanLabel}");

            string whyShort = why?.OneLine ?? posDetail;
            // Save every ML prediction, including correct predictions.
            SavePredictionToCsv(
                s,
                fileName,
                imagePath,
                groundTruth,
                cleanLabel,
                confidence,
                verdict,
                whyShort,
                sw.ElapsedMilliseconds);

            // Update UI
            Post(() => ApplyResult(
                s,
                fileName,
                imagePath,
                groundTruth,
                cleanLabel,
                confidence,
                verdict,
                whyShort,
                isCorrect,
                isCriticalMiss,
                isFalseAlarm));

            LogDiag($"[{s.Name}] PROCESSED {fileName} folder={groundTruth} pred={cleanLabel} " +
                    $"{confidence:P1} {verdict} | {sw.ElapsedMilliseconds} ms | queue={m.QueueCount}");
        }

        // ───────── UI part (counters, grid, alerts) ─────────
        private void ApplyResult(SourceRuntime s, string fileName, string imagePath, string groundTruth,
            string cleanLabel, float confidence, string verdict, string whyShort,
            bool isCorrect, bool isCriticalMiss, bool isFalseAlarm)
        {
            if (!_sources.Contains(s)) return;       // source was removed in the meantime

            _checkedCount++; s.Checked++;
            if (isCorrect) { _correctCount++; s.Correct++; }
            else if (isCriticalMiss) s.CriticalMisses++;
            else if (isFalseAlarm) s.FalseAlarms++;
            s.Dirty = true;                          // saved by the 5-second timer

            if (!isCorrect)
            {
                AddMonitoredRow(new ImageResult
                {
                    FileName = fileName,
                    ImagePath = imagePath,
                    Subfolder = groundTruth,
                    PredictedLabel = cleanLabel,
                    CorrectedLabel = cleanLabel,
                    Confidence = confidence,
                    Verdict = verdict,
                    Why = $"[{s.Name}] {whyShort}",
                    SourceId = s.Profile.Id
                });
            }

            string tag = $"[{s.Name}] ";
            if (isCriticalMiss)
            {
                _criticalMissCount++;
                _abnormalCount++;
                if (_usbLight != null && _usbLight.IsConnected) _usbLight.TurnOn();
                FlashCriticalAlert(imagePath, fileName, confidence);
            }
            else if (isFalseAlarm)
            {
                _falseAlarmCount++;
                string reason = whyShort.Length > 0 ? " Why: " + whyShort + "." : "";
                ShowMonitorSummary(
                    $"   Last: {tag}{fileName} is in OK but predicted NG ({MismatchExplainer.Pct(confidence)}).{reason} Double-click its row for details.",
                    Color.FromArgb(255, 248, 220));
            }
            else if (isCorrect)
            {
                if (_criticalMissCount == 0 && _usbLight != null && _usbLight.IsConnected) _usbLight.TurnOff();
                ShowMonitorSummary(
                    $"   Last: {tag}{fileName} → {cleanLabel} ({MismatchExplainer.Pct(confidence)}) matches folder {groundTruth}",
                    Color.FromArgb(232, 245, 233));
            }
            else
            {
                ShowMonitorSummary(
                    $"   Last: {tag}{fileName} has unsupported folder/prediction: {groundTruth} → {cleanLabel}",
                    Color.FromArgb(255, 235, 238));
            }
        }

        // ───────── per-source helpers (they never touch the form's model fields) ─────────
        private static string PreprocessFor(SourceRuntime s, string imagePath)
        {
            if (s.Positions.Count > 0 || s.Roi == Rectangle.Empty) return imagePath;
            if (s.Roi == Rectangle.Empty) return imagePath;

            using var bmp = new Bitmap(imagePath);
            int x = Math.Max(0, Math.Min(s.Roi.X, bmp.Width - s.Roi.Width));
            int y = Math.Max(0, Math.Min(s.Roi.Y, bmp.Height - s.Roi.Height));
            var safe = new Rectangle(x, y, Math.Min(s.Roi.Width, bmp.Width), Math.Min(s.Roi.Height, bmp.Height));

            using var cropped = bmp.Clone(safe, bmp.PixelFormat);
            string tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
            cropped.Save(tmp, ImageFormat.Png);
            return tmp;
        }

        private static (string Label, float Confidence) PredictOne(SourceRuntime s, string path)
        {
            switch (s.Type)
            {
                case ModelType.TorchSharp:
                    { var r = s.Torch!.Predict(path); return (r.Label, r.Confidence); }
                case ModelType.Onnx:
                    { var r = s.Onnx!.Predict(path); return (r.Label, r.Confidence); }
                case ModelType.OneClass:
                    { var r = s.OneClass!.Predict(path); return (r.Label, r.Confidence); }
                default:
                    return s.Predictor!.PredictWithConfidence(path);
            }
        }

        private static (string Label, float Confidence) PredictFor(SourceRuntime s, string path, out string detail)
        {
            detail = "";
            if (s.Positions.Count == 0) return PredictOne(s, path);

            var results = new List<(string, string, float)>();
            foreach (var (pos, tmp) in PositionCropper.CropToTemp(path, s.Positions))
            {
                try
                {
                    var r = PredictOne(s, tmp);
                    results.Add((pos.Name, (r.Label ?? "").Trim().Trim('"'), r.Confidence));
                }
                finally { try { File.Delete(tmp); } catch { } }
            }
            var a = PositionCropper.Aggregate(results);
            detail = a.Detail;
            return (a.Label, a.Confidence);
        }

        // Swaps the source in only for the duration of the call, then puts the main model back
        //private Explanation? ExplainFor(SourceRuntime s, string processed, string imagePath, string groundTruth)
        //{
        //    var oType = _activeModelType; var oP = _predictor; var oT = _torchChecker;
        //    var oO = _onnxChecker; var oC = _oneClassChecker;
        //    var oRoi = _roiRect; var oSize = _trainingImageSize; var oFolder = _monitoredFolder;
        //    try
        //    {
        //        ActivateSource(s);
        //        return QuickExplain(processed, imagePath, groundTruth);
        //    }
        //    finally
        //    {
        //        _activeModelType = oType; _predictor = oP; _torchChecker = oT;
        //        _onnxChecker = oO; _oneClassChecker = oC;
        //        _roiRect = oRoi; _trainingImageSize = oSize; _monitoredFolder = oFolder;
        //    }
        //}

        // ─────────────────────────────────────────────────────────────────
        //  Daily summary CSV (one row per source per day)
        //  Header: Date,FalseAlarms,CriticalMisses,Checked,Correct,Source
        //  Old rows without a Source column are treated as source "".
        // ─────────────────────────────────────────────────────────────────

        private static string SrcKey(SourceRuntime s) => s.Name.Replace(',', '_').Trim();

        private bool SaveDailyInspectionSummary(SourceRuntime s, DateTime reportDate)
        {
            string csvPath = Path.Combine(AppContext.BaseDirectory, "DailyInspectionSummary.csv");
            string tempPath = csvPath + ".tmp";

            try
            {
                lock (_dailyCsvLock)
                {
                    var lines = File.Exists(csvPath) ? File.ReadAllLines(csvPath).ToList() : new List<string>();
                    const string header = "Date,FalseAlarms,CriticalMisses,Checked,Correct,Source";
                    if (lines.Count == 0) lines.Add(header); else lines[0] = header;

                    string dateKey = reportDate.ToString("yyyy-MM-dd");
                    string src = SrcKey(s);

                    int idx = lines.FindIndex(line =>
                    {
                        var c = line.Split(',');
                        return c.Length >= 3 && c[0].Trim() == dateKey &&
                               (c.Length >= 6 ? c[5].Trim() : "") == src;
                    });

                    string row = $"{dateKey},{s.FalseAlarms},{s.CriticalMisses},{s.Checked},{s.Correct},{src}";
                    if (idx >= 0) lines[idx] = row; else lines.Add(row);

                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write,
                                                   FileShare.None, 4096, FileOptions.WriteThrough))
                    using (var writer = new StreamWriter(fs, new System.Text.UTF8Encoding(true)))
                    {
                        foreach (var line in lines) writer.WriteLine(line);
                        writer.Flush();
                        fs.Flush(true);
                    }
                    File.Move(tempPath, csvPath, true);
                    s.Dirty = false;
                }

                LogMonitor($"[{s.Name}] DAILY SUMMARY PERSISTED: {reportDate:yyyy-MM-dd} | FA={s.FalseAlarms} CM={s.CriticalMisses}");
                return true;
            }
            catch (Exception ex)
            {
                s.Dirty = true;
                LogMonitor($"DAILY CSV SAVE ERROR: {ex}");
                return false;
            }
        }

        private void LoadDailyInspectionSummary(SourceRuntime s, DateTime reportDate)
        {
            s.FalseAlarms = s.CriticalMisses = s.Checked = s.Correct = 0;

            string csvPath = Path.Combine(AppContext.BaseDirectory, "DailyInspectionSummary.csv");
            if (!File.Exists(csvPath)) return;

            try
            {
                lock (_dailyCsvLock)
                {
                    string dateKey = reportDate.ToString("yyyy-MM-dd"), src = SrcKey(s);
                    foreach (var line in File.ReadAllLines(csvPath))
                    {
                        var c = line.Split(',');
                        if (c.Length < 3 || c[0].Trim() != dateKey) continue;
                        if ((c.Length >= 6 ? c[5].Trim() : "") != src) continue;

                        int.TryParse(c[1], out s.FalseAlarms);
                        int.TryParse(c[2], out s.CriticalMisses);
                        if (c.Length >= 4) int.TryParse(c[3], out s.Checked);
                        if (c.Length >= 5) int.TryParse(c[4], out s.Correct);
                        break;
                    }
                }
                LogMonitor($"[{s.Name}] DAILY COUNTS RESTORED: {reportDate:yyyy-MM-dd} | FA={s.FalseAlarms} CM={s.CriticalMisses}");
            }
            catch (Exception ex)
            {
                LogMonitor($"DAILY CSV LOAD ERROR: {ex}");
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  High-confidence mismatch archive (separated by source)
        // ─────────────────────────────────────────────────────────────────

        private void MoveHighConfidenceMismatch(SourceRuntime s, string imagePath, string groundTruth,
                                                string predictedLabel, float confidence)
        {
            if (confidence <= 0.80f) return;

            string safeSource = string.Concat(s.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            string destinationRoot = Path.Combine(AppContext.BaseDirectory, "HighConfidenceMismatches", safeSource, groundTruth);
            Directory.CreateDirectory(destinationRoot);

            string fileName = Path.GetFileName(imagePath);
            string destinationPath = Path.Combine(destinationRoot, fileName);

            if (File.Exists(destinationPath))
            {
                string name = Path.GetFileNameWithoutExtension(fileName);
                string extension = Path.GetExtension(fileName);
                destinationPath = Path.Combine(destinationRoot, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");
            }

            File.Copy(imagePath, destinationPath);

            LogMonitor($"[{s.Name}] MOVED HIGH-CONFIDENCE MISMATCH: {imagePath} -> {destinationPath} | " +
                       $"GroundTruth={groundTruth}, Prediction={predictedLabel}, Confidence={confidence:P1}");
        }

        // ─────────────────────────────────────────────────────────────────
        //  Logs
        // ─────────────────────────────────────────────────────────────────

        private void OnMonitorEngineLog(string message)
        {
            if (message.StartsWith("EVENT", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("QUEUED", StringComparison.OrdinalIgnoreCase))
                return;
            LogDiag(message);
        }

        private void LogMismatch(SourceRuntime s, bool criticalMiss, string fileName, string path, string folder,
                                 string predicted, float confidence, Explanation? why)
        {
            string kind = criticalMiss
                ? "CRITICAL MISS (folder NG, model OK)"
                : "FALSE ALARM (folder OK, model NG)";
            string detail = why != null
                ? why.LogText
                : "scores: " + predicted + " " + MismatchExplainer.Pct(confidence) + " | why: not available";

            WriteLog("monitor.log",
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | [{s.Name}] {kind} | {fileName} | " +
                $"folder={folder} model={predicted} {MismatchExplainer.Pct(confidence)} | {detail} | " +
                $"roi={s.Roi.X},{s.Roi.Y},{s.Roi.Width},{s.Roi.Height} | {path}");
        }

        private static void LogDiag(string message) =>
            WriteLog("monitor.diag.log", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");

        private static void LogMonitor(string message) => LogDiag(message);

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

        private void ApplyMonitorStatus(bool force)
        {
            if (_sources.Count == 0) return;
            if (!force && _criticalMissCount > 0) return;

            Color back = _lastMonitorBack;
            var health = new System.Text.StringBuilder();

            foreach (var s in _sources)
            {
                var m = s.Monitor;
                if (m == null)
                {
                    health.AppendLine(s.WaitingFolder != null
                            ? $"   ⏳ [{s.Name}] waiting for folder {s.WaitingFolder}"
                            : $"   ⏳ [{s.Name}] starting...");
                }
                else if (!m.FolderReachable)
                {
                    health.AppendLine($"   ⚠️ [{s.Name}] folder not reachable - retrying...");
                    back = Color.FromArgb(255, 235, 238);
                }
                else
                {
                    health.AppendLine($"   ● [{s.Name}] live - last scan {m.LastScanUtc.ToLocalTime():HH:mm:ss}" +
                        (m.WatcherActive ? "" : " (scan mode)") +
                        (m.WatcherRestarts > 0 ? $" | watcher restarted {m.WatcherRestarts}x" : "") +
                        (m.QueueCount > 0 ? $" | {m.QueueCount} waiting" : "") +
                        (m.RootIgnoredCount > 0 ? $" | ⚠️ {m.RootIgnoredCount} image(s) outside OK/NG ignored" : ""));
                }
            }

            lblStatus.Text =
                $"👁️ Monitoring {_sources.Count} source(s): {_checkedCount} checked | ✅ {_correctCount} correct | " +
                $"🟡 {_falseAlarmCount} false alarms | 🔴 {_criticalMissCount} critical misses\n" +
                (string.IsNullOrEmpty(_lastMonitorLine) ? "" : _lastMonitorLine + "\n") +
                health.ToString().TrimEnd();
            lblStatus.BackColor = back;
            lblStatus.ForeColor = SystemColors.ControlText;
        }

        // 2026-11-18 -> "26\11\18" (Path.Combine adds the separators)
        private static string DateFolderName(DateTime d) => Path.Combine(
            d.ToString("yy", System.Globalization.CultureInfo.InvariantCulture),
            d.ToString("MM", System.Globalization.CultureInfo.InvariantCulture),
            d.ToString("dd", System.Globalization.CultureInfo.InvariantCulture));

        // ─────────────────────────────────────────────────────────────────
        //  Grid helper (unchanged)
        // ─────────────────────────────────────────────────────────────────

        private void AddMonitoredRow(ImageResult row)
        {
            string? selectedPath = grid.SelectedRows.Count > 0
                ? grid.SelectedRows[0].Cells["ImagePath"].Value?.ToString()
                : null;

            _results.Add(row);

            bool newFolder = !cbFolderFilter.Items.Contains(row.Subfolder ?? "");
            bool newLabel = !cbPredFilter.Items.Contains(row.PredictedLabel ?? "");
            if (newFolder || newLabel)
                PopulateFilters();
            else
                ApplyFilters(this, EventArgs.Empty);

            PopulateCorrectionComboBox();

            if (selectedPath != null)
            {
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
                grid.ClearSelection();
                try { grid.FirstDisplayedScrollingRowIndex = grid.Rows.Count - 1; }
                catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Save Prediction
        // ─────────────────────────────────────────────────────────────────
        private void SavePredictionToCsv(
    SourceRuntime s,
    string fileName,
    string imagePath,
    string actualLabel,
    string predictedLabel,
    float confidence,
    string verdict,
    string explanation,
    long processingTimeMs)
        {
            try
            {
                string safeSource = string.Concat(
                    s.Name.Select(ch =>
                        Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");

                string directory = Path.Combine(
                    AppContext.BaseDirectory,
                    "PredictionHistory",
                    safeSource);

                Directory.CreateDirectory(directory);

                string csvPath = Path.Combine(directory, dateFolder + ".csv");

                const string header =
                    "Timestamp,Source,FileName,ImagePath,ActualLabel," +
                    "PredictedLabel,Confidence,Verdict,Explanation,ProcessingTimeMs";

                string Escape(string? value)
                {
                    value ??= "";
                    return "\"" + value.Replace("\"", "\"\"") + "\"";
                }

                string row = string.Join(",",
                    Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")),
                    Escape(s.Name),
                    Escape(fileName),
                    Escape(imagePath),
                    Escape(actualLabel),
                    Escape(predictedLabel),
                    confidence.ToString("F4",
                        System.Globalization.CultureInfo.InvariantCulture),
                    Escape(verdict),
                    Escape(explanation),
                    processingTimeMs.ToString(
                        System.Globalization.CultureInfo.InvariantCulture));

                lock (_predictionCsvLock)
                {
                    bool writeHeader = !File.Exists(csvPath) ||
                                       new FileInfo(csvPath).Length == 0;

                    using var writer = new StreamWriter(
                        csvPath,
                        append: true,
                        encoding: new System.Text.UTF8Encoding(true));

                    if (writeHeader)
                        writer.WriteLine(header);

                    writer.WriteLine(row);
                }
            }
            catch (Exception ex)
            {
                LogDiag($"[{s.Name}] PREDICTION CSV ERROR: {ex}");
            }
        }
    }
}