#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Image_Checker.Services
{
    // ─────────────────────────────────────────────────────────────────────
    //  Scoring abstraction (the real implementation wraps Predictor)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>One model prediction with the score of every class.</summary>
    public sealed class ScoreResult
    {
        public string PredictedLabel { get; set; } = "";
        public float[] Scores { get; set; } = new float[0];
        /// <summary>Class name for each score slot, or null when the model does not expose them.</summary>
        public string[]? ClassNames { get; set; }
        public bool IsOutOfDistribution { get; set; }
        public string? OodReason { get; set; }
    }

    public interface IImageScorer : IDisposable
    {
        ScoreResult Score(string imagePath);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Image statistics (brightness / contrast / sharpness of the model input)
    // ─────────────────────────────────────────────────────────────────────

    public enum StatMetric { Brightness, Contrast, Sharpness }

    public sealed class ImageStats
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public double Brightness { get; set; }   // mean gray level 0-255
        public double Contrast { get; set; }     // standard deviation of gray level
        public double Sharpness { get; set; }    // variance of the Laplacian (higher = sharper)
        public double MeanR { get; set; }
        public double MeanG { get; set; }
        public double MeanB { get; set; }

        public double Get(StatMetric m)
        {
            switch (m)
            {
                case StatMetric.Brightness: return Brightness;
                case StatMetric.Contrast: return Contrast;
                default: return Sharpness;
            }
        }
    }

    public static class ImageStatsMath
    {
        /// <summary>Stats from a gray image (values 0-255), row-major.</summary>
        public static ImageStats FromGray(float[] gray, int width, int height)
        {
            var s = new ImageStats { Width = width, Height = height };
            int n = Math.Min(gray.Length, width * height);
            if (n == 0) return s;

            double sum = 0;
            for (int i = 0; i < n; i++) sum += gray[i];
            double mean = sum / n;

            double sq = 0;
            for (int i = 0; i < n; i++) { double d = gray[i] - mean; sq += d * d; }

            s.Brightness = mean;
            s.Contrast = Math.Sqrt(sq / n);

            double lsum = 0, lsq = 0;
            int ln = 0;
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int i = y * width + x;
                    if (i + width >= n) continue;
                    double l = 4.0 * gray[i] - gray[i - width] - gray[i + width] - gray[i - 1] - gray[i + 1];
                    lsum += l; lsq += l * l; ln++;
                }
            }
            if (ln > 0)
            {
                double lm = lsum / ln;
                s.Sharpness = Math.Max(0, lsq / ln - lm * lm);
            }
            return s;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  What the training images look like, per class
    // ─────────────────────────────────────────────────────────────────────

    public sealed class TrainingStats
    {
        public const int MinSamples = 15;

        private readonly Dictionary<string, List<ImageStats>> _byClass =
            new Dictionary<string, List<ImageStats>>(StringComparer.OrdinalIgnoreCase);

        public void Add(string label, ImageStats stats)
        {
            List<ImageStats>? list;
            if (!_byClass.TryGetValue(label, out list))
            {
                list = new List<ImageStats>();
                _byClass[label] = list;
            }
            list.Add(stats);
        }

        public IEnumerable<string> Labels { get { return _byClass.Keys; } }

        public int SampleCount(string label)
        {
            List<ImageStats>? list;
            return _byClass.TryGetValue(label, out list) ? list.Count : 0;
        }

        /// <summary>
        /// Fraction (0-1) of this class's training images whose metric is at or below value.
        /// NaN when there are too few samples to say anything.
        /// </summary>
        public double Percentile(string label, StatMetric metric, double value)
        {
            List<ImageStats>? list;
            if (!_byClass.TryGetValue(label, out list) || list.Count < MinSamples) return double.NaN;
            int atOrBelow = 0;
            foreach (var s in list) if (s.Get(metric) <= value) atOrBelow++;
            return (double)atOrBelow / list.Count;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Explanation input / output
    // ─────────────────────────────────────────────────────────────────────

    public sealed class ExplainInput
    {
        /// <summary>The folder the image was in ("truth"); null/unknown = just explain the prediction.</summary>
        public string? FolderLabel { get; set; }
        /// <summary>
        /// The classes the model knows (e.g. from the training data). Only used when the model does not
        /// expose its class names: it stops an arbitrary folder name ("Batch1") being mistaken for a class.
        /// null = assume the app's usual OK / NG.
        /// </summary>
        public IEnumerable<string>? KnownLabels { get; set; }
        public ScoreResult Score { get; set; } = new ScoreResult();
        public ImageStats? Stats { get; set; }
        public TrainingStats? Training { get; set; }
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public int RoiWidth { get; set; }
        public int RoiHeight { get; set; }
    }

    public sealed class Explanation
    {
        public string Kind { get; set; } = "Prediction";   // FalseAlarm | CriticalMiss | Mismatch | Match | Prediction
        public string Headline { get; set; } = "";
        public string OneLine { get; set; } = "";
        public string Bars { get; set; } = "";
        public string[] ClassNames { get; set; } = new string[0];
        public float[] Scores { get; set; } = new float[0];
        public int TopIndex { get; set; }
        public string TopLabel { get; set; } = "";
        public float TopScore { get; set; }
        public float Margin { get; set; }
        public bool IsMismatch { get { return Kind == "FalseAlarm" || Kind == "CriticalMiss" || Kind == "Mismatch"; } }
        public List<string> Reasons { get; } = new List<string>();
        /// <summary>Compact versions of the reasons, for the one-line log.</summary>
        public List<string> ShortReasons { get; } = new List<string>();

        public void AddReason(string reason, string? shortReason = null)
        {
            if (string.IsNullOrWhiteSpace(reason)) return;
            Reasons.Add(reason);
            if (!string.IsNullOrWhiteSpace(shortReason)) ShortReasons.Add(shortReason!);
        }

        /// <summary>Scores, reasons and the footnote (no headline: the dialog shows that separately).</summary>
        public string BodyText
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine(Bars);
                sb.AppendLine();
                if (Reasons.Count > 0)
                {
                    sb.AppendLine(IsMismatch ? "Why it differs from the folder:" : "Notes:");
                    for (int i = 0; i < Reasons.Count; i++)
                        sb.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + Reasons[i]);
                    sb.AppendLine();
                }
                sb.AppendLine("These are hints built from the model's scores and image statistics, not the model's");
                sb.AppendLine("internal reasoning. The heatmap is the closest view of what the model reacted to.");
                return sb.ToString();
            }
        }

        /// <summary>Headline + body, formatted for a report.</summary>
        public string FullText
        {
            get { return Headline + Environment.NewLine + Environment.NewLine + BodyText; }
        }

        /// <summary>One compact line for monitor.log: scores, margin and the reasons.</summary>
        public string LogText
        {
            get
            {
                var scores = string.Join(", ", Enumerable.Range(0, Scores.Length)
                    .OrderByDescending(i => Scores[i])
                    .Select(i => ClassNames[i] + " " + MismatchExplainer.Pct(Scores[i])));
                string reasons = ShortReasons.Count > 0
                    ? string.Join("; ", ShortReasons.Select(r => r.Replace("\r", " ").Replace("\n", " ")))
                    : "no further detail";
                return "scores: " + scores + " | margin " + MismatchExplainer.Pct(Margin) + " | why: " + reasons;
            }
        }
    }
}