#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

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
        public Rectangle RoiUsed;
        public ImageStats Stats = null!;
        public ScoreResult Baseline = null!;
        public ExplainInput Input = null!;
        public Explanation Explanation = null!;

        public OcclusionResult? Occlusion;
        public Dictionary<int, List<HotSpot>> SpotsByClass = new Dictionary<int, List<HotSpot>>();
        /// <summary>All scores after covering ALL spots of class k at once.</summary>
        public Dictionary<int, float[]> AllHiddenScores = new Dictionary<int, float[]>();

        public void Dispose()
        {
            if (Original != null) Original.Dispose();
            if (OriginalWithRoi != null) OriginalWithRoi.Dispose();
            if (Crop != null) Crop.Dispose();
        }
    }

    public static class ExplainSession
    {
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

        public static void Refine(ExplainResult result, TrainingStats training)
        {
            result.Input.Training = training;
            result.Explanation = MismatchExplainer.Explain(result.Input);
            if (result.Occlusion != null) AddTopClassReasons(result);
        }

        /// <summary>One scan (cells x cells predictions) gives the spots for EVERY class.</summary>
        public static bool AddOcclusion(ExplainResult result, IImageScorer scorer, int cells,
                                        Func<bool>? cancelled, Action<int, int>? progress)
        {
            ScoreResult baseline = result.Baseline;
            if (baseline.Scores.Length == 0) return false;

            int w = result.Crop.Width, h = result.Crop.Height;
            Color fill = ExplainImaging.MeanColor(result.Stats);
            float cw = (float)w / cells, ch = (float)h / cells;

            OcclusionResult? occ = OcclusionMap.Run(cells, baseline.Scores, (r, c) =>
            {
                using (Bitmap hidden = ExplainImaging.Hide(result.Crop, OcclusionMap.CellPatch(r, c, cw, ch), fill))
                    return ExplainImaging.ScoreBitmap(scorer, hidden).Scores;
            }, cancelled, progress);
            if (occ == null) return false;

            result.Occlusion = occ;
            string[] names = result.Explanation.ClassNames;
            bool[,] content = ExplainImaging.ContentMap(result.Crop, cells);
            for (int k = 0; k < baseline.Scores.Length && k < names.Length; k++)
            {
                List<HotSpot> spots = OcclusionMap.FindSpots(occ, k, names[k], w, h, 4, content);
                result.SpotsByClass[k] = spots;
                if (spots.Count == 0) continue;
                using (Bitmap hidden = ExplainImaging.HideMany(result.Crop, spots.Select(s => s.Bounds), fill))
                    result.AllHiddenScores[k] = ExplainImaging.ScoreBitmap(scorer, hidden).Scores;
            }

            AddTopClassReasons(result);
            return true;
        }

        private static void AddTopClassReasons(ExplainResult r)
        {
            int k = r.Explanation.TopIndex;
            List<HotSpot>? spots;
            string? shortText = null;
            if (r.SpotsByClass.TryGetValue(k, out spots) && spots.Count > 0)
                shortText = "relies on the " + spots[0].Where + " (" + spots[0].DropText + ")";
            r.Explanation.AddReason("Where the model looked: " + Describe(r, k), shortText);
        }

        private static int ArgMax(float[] v)
        {
            int best = 0;
            for (int i = 1; i < v.Length; i++) if (v[i] > v[best]) best = i;
            return best;
        }

        /// <summary>Plain-language reading of the spots of class k.</summary>
        public static string Describe(ExplainResult r, int k)
        {
            string[] names = r.Explanation.ClassNames;
            string cls = names[k];
            float p = r.Baseline.Scores[k];
            int top = ArgMax(r.Baseline.Scores);
            bool wasTop = top == k;

            List<HotSpot>? spots;
            if (!r.SpotsByClass.TryGetValue(k, out spots) || spots.Count == 0)
                return "No single area stands out for " + cls + ". Covering any one patch barely changes its score, " +
                       "so the model is judging the crop as a whole (overall brightness, texture, layout) rather than one spot.";

            float[]? hidden;
            r.AllHiddenScores.TryGetValue(k, out hidden);

            var sb = new StringBuilder();
            sb.Append(spots.Count == 1 ? "One area" : spots.Count + " areas").Append(" support").Append(spots.Count == 1 ? "s " : " ")
              .Append(cls).Append(". ");

            if (hidden == null || k >= hidden.Length) return sb.ToString().Trim();

            float after = hidden[k];
            float drop = p - after;
            int newTop = ArgMax(hidden);
            bool flips = wasTop && newTop != k;

            sb.Append("Covering ").Append(spots.Count == 1 ? "it" : "all of them together").Append(" lowers ").Append(cls)
              .Append(" from ").Append(MismatchExplainer.Pct(p)).Append(" to ").Append(MismatchExplainer.Pct(after));
            if (flips) sb.Append(", and the model would then answer ").Append(names[newTop]);
            sb.Append(". ");

            if (!wasTop)
            {
                sb.Append("These are the parts of the crop that look most like ").Append(cls)
                  .Append(" to the model, even though it chose ").Append(names[top])
                  .Append(". If they sit on a real ").Append(cls).Append("-looking feature, that explains a close call.");
            }
            else if (flips || drop >= 0.30f)
            {
                sb.Append("The answer depends strongly on these areas. Look at them in the picture: if they show a real defect the model is right; " +
                          "if they show glare, a marking, a shadow or background, it is reacting to the wrong thing.");
            }
            else if (drop >= 0.10f)
            {
                sb.Append("Moderate dependence: these areas contribute, but the model also uses the rest of the crop.");
            }
            else
            {
                sb.Append("Weak dependence: the answer comes from the crop as a whole, not from one spot.");
                if (p >= 0.97f)
                    sb.Append(" (A model this sure stays sure when one area is covered, so a small change does not mean the area is unimportant.)");
            }
            return sb.ToString();
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