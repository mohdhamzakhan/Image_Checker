#nullable enable
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing;

namespace Image_Checker.Services
{
    /// <summary>
    /// Occlusion sensitivity: hide one patch of the image at a time and see how much the model's
    /// score for a class drops. A big drop means that patch is what pushed the model to that class.
    /// Works with ANY model because it only needs scores, not model internals.
    ///
    /// The change is measured in LOG-ODDS, not raw probability: a model that is 99.9% sure barely moves
    /// in probability when one patch is hidden, yet the patch may still matter a lot. Log-odds keep the
    /// map informative however confident the model is.
    /// </summary>
    /// 

    public sealed class HotSpot
    {
        public int Rank { get; set; }
        public int ClassIndex { get; set; }
        public string ClassName { get; set; } = "";
        public Rectangle Bounds { get; set; }          // crop pixels
        public string Where { get; set; } = "";
        public float ScoreBefore { get; set; }         // class score now
        public float ScoreWithout { get; set; }        // class score with this area covered
        public int NewTopIndex { get; set; } = -1;     // >= 0 when covering it alone changes the model's answer

        public float Drop { get { return ScoreBefore - ScoreWithout; } }

        public string DropText
        {
            get
            {
                float pts = Drop * 100f;
                if (pts < 0.05f) return "no change";
                return "-" + pts.ToString(pts < 10f ? "0.0" : "0", CultureInfo.InvariantCulture) + " pts";
            }
        }
    }

    /// <summary>Every class's score for every hidden cell, from ONE scan.</summary>
    public sealed class OcclusionResult
    {
        public float[] Baseline = new float[0];
        public float[,][] Scores = new float[0, 0][];

        public float ClassScore(int r, int c, int k)
        {
            float[] s = Scores[r, c];
            return k < s.Length ? s[k] : Baseline[k];
        }

        /// <summary>Log-odds change of class k per cell (positive = the cell supports the class).</summary>
        public float[,] Heat(int k)
        {
            int rows = Scores.GetLength(0), cols = Scores.GetLength(1);
            var heat = new float[rows, cols];
            float b = OcclusionMap.Logit(Baseline[k]);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    heat[r, c] = b - OcclusionMap.Logit(ClassScore(r, c, k));
            return heat;
        }
    }
    public static class OcclusionMap
    {
        private const float Eps = 1e-4f;
        /// <summary>Log-odds change below which a patch is treated as irrelevant.</summary>
        public const float NoRegionThreshold = 0.10f;
        private const float MinVisibleDrop = 0.0005f;   // 0.05 points
        public static float Logit(float p)
        {
            p = Math.Max(Eps, Math.Min(1f - Eps, p));
            return (float)Math.Log(p / (1f - p));
        }

        public static float Sigmoid(float x)
        {
            return (float)(1.0 / (1.0 + Math.Exp(-x)));
        }

        public static Rectangle CellPatch(int r, int c, float cw, float ch)
        {
            return new Rectangle((int)Math.Round(c * cw - cw * 0.25f), (int)Math.Round(r * ch - ch * 0.25f),
                                 (int)Math.Round(cw * 1.5f), (int)Math.Round(ch * 1.5f));
        }

        private static int ArgMax(float[] v)
        {
            int best = 0;
            for (int i = 1; i < v.Length; i++) if (v[i] > v[best]) best = i;
            return best;
        }

        public static OcclusionResult? Run(int cells, float[] baseline, Func<int, int, float[]> scoreWithCellHidden,
                                   Func<bool>? isCancelled = null, Action<int, int>? progress = null)
        {
            if (cells < 2) throw new ArgumentOutOfRangeException(nameof(cells));
            var res = new OcclusionResult { Baseline = baseline, Scores = new float[cells, cells][] };
            int total = cells * cells, done = 0;
            for (int r = 0; r < cells; r++)
                for (int c = 0; c < cells; c++)
                {
                    if (isCancelled != null && isCancelled()) return null;
                    res.Scores[r, c] = scoreWithCellHidden(r, c);
                    done++;
                    if (progress != null) progress(done, total);
                }
            return res;
        }

        /// <summary>
        /// heat[row, col] = log-odds of the class minus its log-odds with that cell hidden.
        /// Positive = the cell supported the class, negative = the cell argued against it.
        /// Returns null if cancelled.
        /// </summary>
        public static float[,]? Compute(
            int cells,
            int classIndex,
            float[] baseline,
            Func<int, int, float[]> scoreWithCellHidden,
            Func<bool>? isCancelled = null,
            Action<int, int>? progress = null)
        {
            if (cells < 2) throw new ArgumentOutOfRangeException(nameof(cells));
            if (classIndex < 0 || classIndex >= baseline.Length) throw new ArgumentOutOfRangeException(nameof(classIndex));

            var heat = new float[cells, cells];
            int total = cells * cells, done = 0;
            for (int r = 0; r < cells; r++)
            {
                for (int c = 0; c < cells; c++)
                {
                    if (isCancelled != null && isCancelled()) return null;
                    float[] sc = scoreWithCellHidden(r, c);
                    float hidden = classIndex < sc.Length ? sc[classIndex] : baseline[classIndex];
                    heat[r, c] = Logit(baseline[classIndex]) - Logit(hidden);
                    done++;
                    if (progress != null) progress(done, total);
                }
            }
            return heat;
        }

        /// <summary>Strongest separate areas that support class k (best first, non-overlapping).</summary>
        public static List<HotSpot> FindSpots(OcclusionResult res, int k, string className,
                                              int imageWidth, int imageHeight, int maxSpots = 4,
                                              bool[,]? eligible = null)
        {
            float[,] heat = res.Heat(k);
            int rows = heat.GetLength(0), cols = heat.GetLength(1);
            float cw = (float)imageWidth / cols, ch = (float)imageHeight / rows;
            var taken = new bool[rows, cols];
            var image = new Rectangle(0, 0, imageWidth, imageHeight);
            int baseTop = ArgMax(res.Baseline);
            var spots = new List<HotSpot>();

            for (int n = 0; n < maxSpots; n++)
            {
                float best = NoRegionThreshold;
                int br = -1, bc = -1;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        if (taken[r, c] || heat[r, c] <= best) continue;
                        if (eligible != null && !eligible[r, c]) continue;                         // blank area
                        if (res.Baseline[k] - res.ClassScore(r, c, k) < MinVisibleDrop) continue;  // changes nothing visible
                        best = heat[r, c]; br = r; bc = c;
                    }
                if (br < 0) break;

                for (int dr = -2; dr <= 2; dr++)
                    for (int dc = -2; dc <= 2; dc++)
                    {
                        int rr = br + dr, cc = bc + dc;
                        if (rr >= 0 && rr < rows && cc >= 0 && cc < cols) taken[rr, cc] = true;
                    }

                float[] sc = res.Scores[br, bc];
                int topNow = ArgMax(sc);
                spots.Add(new HotSpot
                {
                    Rank = spots.Count + 1,
                    ClassIndex = k,
                    ClassName = className,
                    Bounds = Rectangle.Intersect(image, CellPatch(br, bc, cw, ch)),
                    Where = Where(br, bc, rows, cols),
                    ScoreBefore = res.Baseline[k],
                    ScoreWithout = res.ClassScore(br, bc, k),
                    NewTopIndex = (baseTop == k && topNow != k) ? topNow : -1
                });
            }
            return spots;
        }

        /// <summary>The cell that pushed hardest toward the class and how it is spread.</summary>
        public static string DescribeHotspot(float[,] heat, string className, float baselineScore)
        {
            int rows = heat.GetLength(0), cols = heat.GetLength(1);
            float max = float.MinValue; int mr = 0, mc = 0;
            double positive = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float v = heat[r, c];
                    if (v > max) { max = v; mr = r; mc = c; }
                    if (v > 0) positive += v;
                }
            }

            if (max < NoRegionThreshold)
                return "The heatmap shows no single region that the answer depends on: hiding any one patch barely changes the " +
                       className + " score, so the model is reacting to the image as a whole.";

            string where = Where(mr, mc, rows, cols);
            float hidden = Sigmoid(Logit(baselineScore) - max);
            string text = "The strongest push toward " + className + " comes from the " + where +
                          " of the cropped area (hiding it lowers the " + className + " score from " +
                          (baselineScore * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "% to " +
                          (hidden * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%).";

            // how concentrated is the evidence? share of the positive push held by the 3 strongest cells
            var top = new float[3];
            foreach (float v in heat)
            {
                if (v <= 0) continue;
                if (v > top[0]) { top[2] = top[1]; top[1] = top[0]; top[0] = v; }
                else if (v > top[1]) { top[2] = top[1]; top[1] = v; }
                else if (v > top[2]) { top[2] = v; }
            }
            if (positive > 0)
            {
                double share = (top[0] + top[1] + top[2]) / positive;
                text += share >= 0.5 ? " The evidence is concentrated in a small area."
                                     : " The evidence is spread over several areas.";
            }
            return text;
        }

        public static string Where(int row, int col, int rows, int cols)
        {
            string v = row < rows / 3.0 ? "upper" : row >= rows * 2.0 / 3.0 ? "lower" : "middle";
            string h = col < cols / 3.0 ? "left" : col >= cols * 2.0 / 3.0 ? "right" : "center";
            if (v == "middle" && h == "center") return "center";
            if (v == "middle") return h + " side";
            if (h == "center") return v + " part";
            return v + "-" + h;
        }
    }
}