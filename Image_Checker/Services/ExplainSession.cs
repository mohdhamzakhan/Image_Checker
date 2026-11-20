#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Image_Checker.Services
{
    // ─────────────────────────────────────────────────────────────────────
    //  Reads a sample of the training images to learn what each class looks like
    // ─────────────────────────────────────────────────────────────────────
    public static class TrainingStatsBuilder
    {
        private static readonly object CacheLock = new object();
        private static string? _cacheKey;
        private static TrainingStats? _cacheValue;

        /// <summary>Cached per images.csv + ROI, so it is only built once per session.</summary>
        public static TrainingStats GetCached(string basePath, Rectangle roi, int maxPerClass = 60)
        {
            string csv = Path.Combine(basePath, "images.csv");
            string key = csv + "|" + (File.Exists(csv) ? File.GetLastWriteTimeUtc(csv).Ticks : 0L) +
                         "|" + roi.X + "," + roi.Y + "," + roi.Width + "," + roi.Height;

            lock (CacheLock)
            {
                if (_cacheKey == key && _cacheValue != null) return _cacheValue;
            }

            TrainingStats built = Build(basePath, roi, maxPerClass);
            lock (CacheLock)
            {
                _cacheKey = key;
                _cacheValue = built;
            }
            return built;
        }

        /// <summary>Up to maxPerClass evenly spaced images per class, cropped exactly like inference does.</summary>
        public static TrainingStats Build(string basePath, Rectangle roi, int maxPerClass = 60)
        {
            var result = new TrainingStats();
            string csv = Path.Combine(basePath, "images.csv");
            if (!File.Exists(csv)) return result;

            var byLabel = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadLines(csv))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                int i = line.LastIndexOf(',');
                if (i <= 0 || i >= line.Length - 1) continue;

                string path = Unquote(line.Substring(0, i).Trim());
                string label = Unquote(line.Substring(i + 1).Trim());
                if (path.Length == 0 || label.Length == 0) continue;
                if (!Path.IsPathRooted(path)) path = Path.Combine(basePath, path);

                List<string>? files;
                if (!byLabel.TryGetValue(label, out files))
                {
                    files = new List<string>();
                    byLabel[label] = files;
                }
                files.Add(path);
            }

            foreach (KeyValuePair<string, List<string>> kv in byLabel)
            {
                List<string> files = kv.Value;
                double step = Math.Max(1.0, (double)files.Count / Math.Max(1, maxPerClass));
                int taken = 0;
                for (double pos = 0; pos < files.Count && taken < maxPerClass; pos += step)
                {
                    string file = files[(int)pos];
                    if (!File.Exists(file)) continue;
                    try
                    {
                        using (Bitmap bmp = ExplainImaging.LoadCopy(file))
                        using (Bitmap crop = ExplainImaging.CropToRoi(bmp, roi))
                        {
                            result.Add(kv.Key, ExplainImaging.ComputeStats(crop));
                            taken++;
                        }
                    }
                    catch
                    {
                        // unreadable sample: skip it
                    }
                }
            }
            return result;
        }

        private static string Unquote(string s)
        {
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2).Replace("\"\"", "\"");
            return s;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Everything the "Why?" window shows
    // ─────────────────────────────────────────────────────────────────────
    public sealed class ExplainResult : IDisposable
    {
        public Bitmap Original = null!;
        public Bitmap OriginalWithRoi = null!;
        public Bitmap Crop = null!;
        public Bitmap? Heatmap;
        public float[,]? Heat;
        public Rectangle RoiUsed;
        public ImageStats Stats = null!;
        public ScoreResult Baseline = null!;
        public ExplainInput Input = null!;
        public Explanation Explanation = null!;
        public List<HotSpot> Spots = new List<HotSpot>();
        public void Dispose()
        {
            if (Original != null) Original.Dispose();
            if (OriginalWithRoi != null) OriginalWithRoi.Dispose();
            if (Crop != null) Crop.Dispose();
            if (Heatmap != null) Heatmap.Dispose();
        }
    }

    public static class ExplainSession
    {
        /// <summary>Fast part (well under a second): crop, statistics, one prediction, the written explanation.</summary>
        public static ExplainResult Prepare(string imagePath, Rectangle roi, string? folderLabel,
                                            IImageScorer scorer, TrainingStats? training)
        {
            Bitmap original = ExplainImaging.LoadCopy(imagePath);
            Bitmap? withRoi = null;
            Bitmap? crop = null;
            try
            {
                Rectangle used = ExplainImaging.ClampRoi(original.Width, original.Height, roi);
                crop = ExplainImaging.CropToRoi(original, roi);
                withRoi = ExplainImaging.DrawRoi(original, used);
                ImageStats stats = ExplainImaging.ComputeStats(crop);
                ScoreResult baseline = ExplainImaging.ScoreBitmap(scorer, crop);

                var input = new ExplainInput
                {
                    FolderLabel = folderLabel,
                    Score = baseline,
                    Stats = stats,
                    Training = training,
                    OriginalWidth = original.Width,
                    OriginalHeight = original.Height,
                    RoiWidth = roi.Width,
                    RoiHeight = roi.Height
                };

                return new ExplainResult
                {
                    Original = original,
                    OriginalWithRoi = withRoi,
                    Crop = crop,
                    RoiUsed = used,
                    Stats = stats,
                    Baseline = baseline,
                    Input = input,
                    Explanation = MismatchExplainer.Explain(input)
                };
            }
            catch
            {
                original.Dispose();
                if (withRoi != null) withRoi.Dispose();
                if (crop != null) crop.Dispose();
                throw;
            }
        }

        /// <summary>Adds the training-image comparison once it is available (it can take a moment the first time).</summary>
        public static void Refine(ExplainResult result, TrainingStats training)
        {
            result.Input.Training = training;
            result.Explanation = MismatchExplainer.Explain(result.Input);
            //if (result.Heat != null) AddHotspot(result);
            if (result.Heat != null) AddSpotReasons(result);
        }

        /// <summary>
        /// Slow part: hide each patch in turn and re-score (cells x cells predictions).
        /// Returns false if cancelled or if the model gave no scores.
        /// </summary>
        public static bool AddHeatmap(ExplainResult result, IImageScorer scorer, int cells,
                                      Func<bool>? cancelled, Action<int, int>? progress)
        {
            ScoreResult baseline = result.Baseline;
            if (baseline.Scores.Length == 0) return false;

            int classIndex = result.Explanation.TopIndex;
            int w = result.Crop.Width, h = result.Crop.Height;
            Color fill = ExplainImaging.MeanColor(result.Stats);
            float cw = (float)w / cells, ch = (float)h / cells;

            float[,]? heat = OcclusionMap.Compute(cells, classIndex, baseline.Scores, (r, c) =>
            {
                // the patch is the cell grown by 50%, so neighbouring patches overlap and the map is smooth
                int x = (int)Math.Round(c * cw - cw * 0.25f);
                int y = (int)Math.Round(r * ch - ch * 0.25f);
                var patch = new Rectangle(x, y, (int)Math.Round(cw * 1.5f), (int)Math.Round(ch * 1.5f));
                using (Bitmap hidden = ExplainImaging.Hide(result.Crop, patch, fill))
                    return ExplainImaging.ScoreBitmap(scorer, hidden).Scores;
            }, cancelled, progress);

            if (heat == null) return false;

            //result.Heat = heat;
            //if (result.Heatmap != null) result.Heatmap.Dispose();
            //result.Heatmap = ExplainImaging.RenderHeatmap(result.Crop, heat);
            //AddHotspot(result);
            //return true;

            result.Heat = heat;
            result.Spots = OcclusionMap.FindSpots(heat, baseline.Scores[classIndex], w, h, 4);
            if (result.Heatmap != null) result.Heatmap.Dispose();
            result.Heatmap = ExplainImaging.RenderSpots(result.Crop, result.Spots);
            AddSpotReasons(result);
            return true;
        }
        private static void AddSpotReasons(ExplainResult result)
        {
            if (result.Heat == null) return;

            string cls = result.Explanation.TopLabel;
            int idx = result.Explanation.TopIndex;
            float baseline = idx < result.Baseline.Scores.Length ? result.Baseline.Scores[idx] : 0.5f;

            if (result.Spots.Count == 0)
            {
                result.Explanation.AddReason(OcclusionMap.DescribeHotspot(result.Heat, cls, baseline));
                return;
            }

            foreach (HotSpot s in result.Spots)
            {
                string full = "Spot #" + s.Rank + " (" + s.Where + "): holds " +
                    s.SharePercent.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                    "% of the evidence for " + cls + ". Hiding it lowers the " + cls + " score from " +
                    (baseline * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "% to " +
                    (s.ScoreWithout * 100f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%.";
                string shortText = "spot #" + s.Rank + " " + s.Where + " " +
                    s.SharePercent.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                result.Explanation.AddReason(full, shortText);
            }
        }
        private static void AddHotspot(ExplainResult result)
        {
            if (result.Heat == null) return;
            float baseline = result.Explanation.TopIndex < result.Baseline.Scores.Length
                ? result.Baseline.Scores[result.Explanation.TopIndex] : 0.5f;
            result.Explanation.AddReason(OcclusionMap.DescribeHotspot(result.Heat, result.Explanation.TopLabel, baseline));
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Adapter: the real model behind IImageScorer
    // ─────────────────────────────────────────────────────────────────────
    public sealed class PredictorScorer : IImageScorer
    {
        private readonly Predictor _predictor;
        private readonly bool _ownsPredictor;

        /// <param name="ownsPredictor">true = disposed together with this scorer (use for a dedicated instance).</param>
        public PredictorScorer(Predictor predictor, bool ownsPredictor)
        {
            _predictor = predictor;
            _ownsPredictor = ownsPredictor;
        }

        public ScoreResult Score(string imagePath)
        {
            return _predictor.PredictScores(imagePath);
        }

        public void Dispose()
        {
            if (_ownsPredictor) _predictor.Dispose();
        }
    }
}