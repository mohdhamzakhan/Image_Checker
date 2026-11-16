using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Image_Checker.Services
{
    /// <summary>
    /// Watches a folder tree for NEW images and never stops on its own.
    ///
    /// Why this exists: a bare FileSystemWatcher silently stops delivering events when it
    /// is idle on a network share / USB drive, when its buffer overflows, or when the folder
    /// disappears for a moment. This class
    ///   - rescans the folder every few seconds on a background thread (safety net),
    ///   - recreates the watcher whenever it errors, dies or the folder comes back,
    ///   - keeps running while the folder is unreachable and resumes by itself,
    ///   - hands new images to the UI through a thread-safe queue (TryDequeue).
    /// Images that already exist when monitoring starts are NOT queued.
    /// </summary>
    public sealed class FolderImageMonitor : IDisposable
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };

        private readonly string _root;
        private readonly int _scanIntervalMs;
        private readonly object _lock = new();
        private readonly Queue<string> _queue = new();
        private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _rootIgnoredPaths = new(StringComparer.OrdinalIgnoreCase);

        private FileSystemWatcher? _watcher;
        private System.Threading.Timer? _scanTimer;
        private int _scanRunning;
        private int _watcherRestarts;
        private long _lastScanTicks;
        private volatile bool _watcherFaulted;
        private volatile bool _folderReachable = true;
        private volatile bool _disposed;
        private bool _started;

        /// <summary>Diagnostic messages (raised on background threads).</summary>
        public event Action<string>? Log;

        public FolderImageMonitor(string root, int scanIntervalMs = 5000)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("Folder is required.", nameof(root));

            _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            _scanIntervalMs = Math.Max(200, scanIntervalMs);
        }

        // ── Status for the UI ────────────────────────────────────────────
        public string Root => _root;
        public bool FolderReachable => _folderReachable;
        public bool WatcherActive { get { lock (_lock) return _watcher?.EnableRaisingEvents == true; } }
        public int WatcherRestarts => Volatile.Read(ref _watcherRestarts);
        public int QueueCount { get { lock (_lock) return _queue.Count; } }
        public int RootIgnoredCount { get { lock (_lock) return _rootIgnoredPaths.Count; } }
        public DateTime LastScanUtc => new DateTime(Interlocked.Read(ref _lastScanTicks), DateTimeKind.Utc);

        // ── Lifecycle ────────────────────────────────────────────────────
        public void Start()
        {
            ThrowIfDisposed();
            lock (_lock)
            {
                if (_started) return;
                _started = true;
                _seen.Clear();
                _queue.Clear();

                // Remember what is already there so only NEW images are predicted.
                // (Done before the watcher starts; anything arriving meanwhile is caught by the first scan.)
                try
                {
                    foreach (var f in Directory.EnumerateFiles(_root, "*.*", SearchOption.AllDirectories))
                        _seen.Add(f);
                }
                catch (Exception ex)
                {
                    _folderReachable = Directory.Exists(_root);
                    Emit($"SEED ERROR: {ex.Message}");
                }

                CreateWatcherNoLock();
            }

            Interlocked.Exchange(ref _lastScanTicks, DateTime.UtcNow.Ticks);
            _scanTimer = new System.Threading.Timer(OnScanTimer, null, _scanIntervalMs, Timeout.Infinite);
            Emit($"=== Monitoring started: {_root} (rescan every {_scanIntervalMs} ms) ===");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _scanTimer?.Dispose(); } catch { }
            lock (_lock)
            {
                DisposeWatcherNoLock();
                _queue.Clear();
            }
            Emit("=== Monitoring stopped ===");
        }

        // ── Queue access (UI thread) ─────────────────────────────────────
        public bool TryDequeue(out string path)
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    path = _queue.Dequeue();
                    return true;
                }
            }
            path = "";
            return false;
        }

        /// <summary>Put an image back (e.g. it is still being written) to try again later.</summary>
        public void Requeue(string path)
        {
            lock (_lock) _queue.Enqueue(path);
        }

        /// <summary>True when nobody is still WRITING the file (readers such as antivirus are fine).</summary>
        public static bool IsFileReady(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return fs.Length > 0;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        // ── Watcher ──────────────────────────────────────────────────────
        private void CreateWatcherNoLock()
        {
            DisposeWatcherNoLock();
            try
            {
                var w = new FileSystemWatcher(_root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter = "*.*"
                };
                try { w.InternalBufferSize = 65536; } catch { /* not supported on every platform */ }

                w.Created += OnCreated;
                w.Renamed += OnRenamed;
                w.Deleted += OnDeleted;
                w.Error += OnError;
                w.EnableRaisingEvents = true;

                _watcher = w;
                _watcherFaulted = false;
            }
            catch (Exception ex)
            {
                _watcher = null;
                _watcherFaulted = true;
                Emit($"WATCHER START FAILED: {ex.Message} (scan mode until it recovers)");
            }
        }

        private void DisposeWatcherNoLock()
        {
            var w = _watcher;
            _watcher = null;
            if (w == null) return;
            try
            {
                w.EnableRaisingEvents = false;
                w.Created -= OnCreated;
                w.Renamed -= OnRenamed;
                w.Deleted -= OnDeleted;
                w.Error -= OnError;
                w.Dispose();
            }
            catch { }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            Emit($"EVENT Created: {e.FullPath}");
            TryEnqueue(e.FullPath, "watcher");
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            Emit($"EVENT Renamed: {e.OldFullPath} -> {e.FullPath}");
            TryEnqueue(e.FullPath, "watcher-rename");
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            lock (_lock) { _seen.Remove(e.FullPath); _rootIgnoredPaths.Remove(e.FullPath); }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            _watcherFaulted = true;
            Emit($"WATCHER ERROR: {e.GetException().Message} - restarting it on the next scan");
            ThreadPool.QueueUserWorkItem(_ => RunScan());
        }

        // ── Safety-net scan (background thread) ──────────────────────────
        private void OnScanTimer(object? state)
        {
            try { RunScan(); }
            finally
            {
                if (!_disposed)
                {
                    try { _scanTimer?.Change(_scanIntervalMs, Timeout.Infinite); }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        private void RunScan()
        {
            if (_disposed) return;
            if (Interlocked.Exchange(ref _scanRunning, 1) == 1) return;   // a scan is already running

            try
            {
                // 1) Is the folder there? If not, keep waiting - never give up.
                if (!Directory.Exists(_root))
                {
                    if (_folderReachable)
                    {
                        _folderReachable = false;
                        Emit($"FOLDER NOT REACHABLE: {_root} - retrying every {_scanIntervalMs} ms");
                    }
                    lock (_lock) DisposeWatcherNoLock();   // a watcher on a vanished folder is dead anyway
                    _watcherFaulted = true;
                    return;
                }

                if (!_folderReachable)
                {
                    _folderReachable = true;
                    _watcherFaulted = true;                // force a fresh watcher
                    Emit($"FOLDER BACK: {_root}");
                }

                // 2) Is the watcher alive? If not, recreate it.
                lock (_lock)
                {
                    if (_disposed) return;
                    if (_watcher == null || _watcherFaulted || !_watcher.EnableRaisingEvents)
                    {
                        CreateWatcherNoLock();
                        if (_watcher != null)
                        {
                            Interlocked.Increment(ref _watcherRestarts);
                            Emit("WATCHER RESTARTED");
                        }
                    }
                }

                // 3) Scan for anything the watcher missed
                var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in Directory.EnumerateFiles(_root, "*.*", SearchOption.AllDirectories))
                {
                    if (_disposed) return;
                    present.Add(f);
                    TryEnqueue(f, "scan");
                }

                // 4) Forget files that are really gone (so a re-created file is predicted again).
                //    Check File.Exists so a file that arrived DURING this scan is not forgotten.
                List<string> candidates;
                lock (_lock) candidates = _seen.Where(p => !present.Contains(p)).ToList();
                var gone = candidates.Where(p => !File.Exists(p)).ToList();
                if (gone.Count > 0)
                {
                    lock (_lock)
                    {
                        foreach (var g in gone) { _seen.Remove(g); _rootIgnoredPaths.Remove(g); }
                    }
                }
            }
            catch (Exception ex)
            {
                Emit($"SCAN ERROR: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _lastScanTicks, DateTime.UtcNow.Ticks);
                Interlocked.Exchange(ref _scanRunning, 0);
            }
        }

        // ── Queueing ─────────────────────────────────────────────────────
        private bool TryEnqueue(string fullPath, string source)
        {
            if (_disposed) return false;

            var ext = Path.GetExtension(fullPath);
            if (!ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return false;

            var parent = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(parent) || PathsEqual(parent, _root))
            {
                bool first;
                lock (_lock) first = _rootIgnoredPaths.Add(fullPath);
                if (first) Emit($"IGNORED (file is in the monitored root, not in a class subfolder): {fullPath}");
                return false;
            }

            lock (_lock)
            {
                if (!_seen.Add(fullPath)) return false;   // already queued or processed
                _queue.Enqueue(fullPath);
            }

            Emit($"QUEUED ({source}): {fullPath}");
            return true;
        }

        private static bool PathsEqual(string a, string b) =>
            string.Equals(Path.TrimEndingDirectorySeparator(a),
                          Path.TrimEndingDirectorySeparator(b),
                          StringComparison.OrdinalIgnoreCase);

        private void Emit(string message)
        {
            try { Log?.Invoke(message); } catch { }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FolderImageMonitor));
        }
    }
}