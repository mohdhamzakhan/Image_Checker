namespace Image_Checker.Services
{
    /// <summary>
    /// One FolderImageMonitor + one model per profile.
    /// Call Poll() from a UI timer (e.g. every 500 ms).
    /// </summary>
    public sealed class MultiFolderMonitor : IDisposable
    {
        private const int MaxNotReadyRetries = 10;

        private sealed class Channel
        {
            public MonitorProfile Profile = null!;
            public FolderImageMonitor Monitor = null!;
            public IImageScorer Scorer = null!;
            public Dictionary<string, int> Retries = new(StringComparer.OrdinalIgnoreCase);
        }

        private readonly Func<MonitorProfile, IImageScorer> _scorerFactory;
        private readonly List<Channel> _channels = new();

        public event Action<MonitorProfile, string>? Log;
        public event Action<MonitorProfile, string, ScoreResult>? ImagePredicted;
        public event Action<MonitorProfile, string, Exception>? ImageFailed;

        /// <param name="scorerFactory">Creates the model for a profile,
        /// e.g. p => new PredictorScorer(new Predictor(p.ModelPath /* your ctor */), true)</param>
        public MultiFolderMonitor(Func<MonitorProfile, IImageScorer> scorerFactory)
        {
            _scorerFactory = scorerFactory;
        }

        public int ChannelCount => _channels.Count;

        public void Start(IEnumerable<MonitorProfile> profiles)
        {
            Stop();
            foreach (var p in profiles.Where(p => p.Enabled))
            {
                try
                {
                    var scorer = _scorerFactory(p);
                    var monitor = new FolderImageMonitor(p.FolderPath);
                    var profile = p;
                    monitor.Log += msg => Log?.Invoke(profile, msg);
                    monitor.Start();
                    _channels.Add(new Channel { Profile = p, Monitor = monitor, Scorer = scorer });
                }
                catch (Exception ex)
                {
                    Log?.Invoke(p, $"START FAILED for '{p.Name}': {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            foreach (var c in _channels)
            {
                try { c.Monitor.Dispose(); } catch { }
                try { c.Scorer.Dispose(); } catch { }
            }
            _channels.Clear();
        }

        /// <summary>Drains each folder's queue and predicts with that folder's own model.</summary>
        public int Poll(int maxPerChannel = 5)
        {
            int processed = 0;
            foreach (var c in _channels)
            {
                for (int i = 0; i < maxPerChannel && c.Monitor.TryDequeue(out var path); i++)
                {
                    if (!FolderImageMonitor.IsFileReady(path))
                    {
                        c.Retries.TryGetValue(path, out int n);
                        if (n < MaxNotReadyRetries)
                        {
                            c.Retries[path] = n + 1;
                            c.Monitor.Requeue(path);
                        }
                        else ImageFailed?.Invoke(c.Profile, path, new IOException("File never became readable."));
                        continue;
                    }

                    try
                    {
                        var result = c.Scorer.Score(path);
                        ImagePredicted?.Invoke(c.Profile, path, result);
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        ImageFailed?.Invoke(c.Profile, path, ex);
                    }
                }
            }
            return processed;
        }

        public void Dispose() => Stop();
    }
}