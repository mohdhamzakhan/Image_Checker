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
        public Rectangle Bounds { get; set; }     // in crop pixels
        public double SharePercent { get; set; }  // share of all evidence for the class
        public float ScoreWithout { get; set; }   // class score if this spot is hidden
        public string Where { get; set; } = "";
    }
    public static class OcclusionMap
    {
        private const float Eps = 1e-4f;
        /// <summary>Log-odds change below which a patch is treated as irrelevant.</summary>
        public const float NoRegionThreshold = 0.10f;

        public static float Logit(float p)
        {
            p = Math.Max(Eps, Math.Min(1f - Eps, p));
            return (float)Math.Log(p / (1f - p));
        }

        public static float Sigmoid(float x)
        {
            return (float)(1.0 / (1.0 + Math.Exp(-x)));
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

        /// <summary>Strongest separate spots that pushed toward the class (non-overlapping, best first).</summary>
        public static List<HotSpot> FindSpots(float[,] heat, float baselineScore,
                                              int imageWidth, int imageHeight, int maxSpots = 4)
        {
            int rows = heat.GetLength(0), cols = heat.GetLength(1);
            var spots = new List<HotSpot>();

            double totalPositive = 0;
            foreach (float v in heat) if (v > 0) totalPositive += v;
            if (totalPositive <= 0) return spots;

            float cw = (float)imageWidth / cols, ch = (float)imageHeight / rows;
            var taken = new bool[rows, cols];
            var image = new Rectangle(0, 0, imageWidth, imageHeight);

            for (int n = 0; n < maxSpots; n++)
            {
                float best = NoRegionThreshold;
                int br = -1, bc = -1;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        if (!taken[r, c] && heat[r, c] > best) { best = heat[r, c]; br = r; bc = c; }
                if (br < 0) break;

                // evidence of the spot = positive push in its 3x3 neighbourhood
                double local = 0;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int rr = br + dr, cc = bc + dc;
                        if (rr >= 0 && rr < rows && cc >= 0 && cc < cols && heat[rr, cc] > 0)
                            local += heat[rr, cc];
                    }

                // keep the next spot away from this one
                for (int dr = -2; dr <= 2; dr++)
                    for (int dc = -2; dc <= 2; dc++)
                    {
                        int rr = br + dr, cc = bc + dc;
                        if (rr >= 0 && rr < rows && cc >= 0 && cc < cols) taken[rr, cc] = true;
                    }

                var bounds = Rectangle.Intersect(image, new Rectangle(
                    (int)Math.Round(bc * cw - cw * 0.25f), (int)Math.Round(br * ch - ch * 0.25f),
                    (int)Math.Round(cw * 1.5f), (int)Math.Round(ch * 1.5f)));

                spots.Add(new HotSpot
                {
                    Rank = spots.Count + 1,
                    Bounds = bounds,
                    SharePercent = Math.Min(100.0, local / totalPositive * 100.0),
                    ScoreWithout = Sigmoid(Logit(baselineScore) - best),
                    Where = Where(br, bc, rows, cols)
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