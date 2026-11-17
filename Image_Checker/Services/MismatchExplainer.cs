#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Image_Checker.Services
{
    /// <summary>
    /// Explains in plain language why the model's answer differs from the folder.
    ///
    /// The model (PCA + a classic classifier) cannot say "I looked at this pixel", so the
    /// explanation is built from three honest sources:
    ///   1. the class scores (how sure, how close a call),
    ///   2. the image's brightness / contrast / sharpness compared with the training images
    ///      of each class (an unusual-looking image is a common cause of wrong answers),
    ///   3. framing problems (photo smaller than the ROI, no ROI at all).
    /// The occlusion heatmap (OcclusionMap) adds a fourth: where in the image the model reacted.
    /// </summary>
    public static class MismatchExplainer
    {
        private const double VerySure = 0.90;
        private const double FairlySure = 0.70;
        private const double ClearGap = 0.30;
        private const double UnusualLow = 0.10;
        private const double UnusualHigh = 0.90;
        private const double TypicalLow = 0.20;
        private const double TypicalHigh = 0.80;
        private const int MaxStatHints = 3;

        public static Explanation Explain(ExplainInput input)
        {
            var s = input.Score;
            var ex = new Explanation();

            if (s.Scores == null || s.Scores.Length == 0)
            {
                ex.Kind = "Prediction";
                ex.Headline = "The model returned no scores for this image.";
                ex.OneLine = "no scores";
                return ex;
            }

            string[] names = ResolveClassNames(s, input.FolderLabel, input.KnownLabels);
            int top = ArgMax(s.Scores);
            int second = SecondIndex(s.Scores, top);
            float pTop = s.Scores[top];
            float pSecond = second >= 0 ? s.Scores[second] : 0f;
            float margin = pTop - pSecond;
            string predicted = names[top];
            string secondName = second >= 0 ? names[second] : "";
            string? folder = string.IsNullOrWhiteSpace(input.FolderLabel) ? null : input.FolderLabel!.Trim();
            bool folderKnown = folder != null && names.Any(n => Same(n, folder));

            ex.ClassNames = names;
            ex.Scores = s.Scores;
            ex.TopIndex = top;
            ex.TopLabel = predicted;
            ex.TopScore = pTop;
            ex.Margin = margin;
            ex.Bars = BuildBars(names, s.Scores);

            // ── What kind of result is it? ───────────────────────────────
            if (!folderKnown)
                ex.Kind = "Prediction";
            else if (Same(folder!, predicted))
                ex.Kind = "Match";
            else if (Same(folder!, "OK") && Same(predicted, "NG"))
                ex.Kind = "FalseAlarm";
            else if (Same(folder!, "NG") && Same(predicted, "OK"))
                ex.Kind = "CriticalMiss";
            else
                ex.Kind = "Mismatch";

            string band = pTop >= VerySure ? "very sure"
                        : (pTop >= FairlySure && margin >= ClearGap) ? "fairly sure"
                        : "unsure (close call)";

            switch (ex.Kind)
            {
                case "FalseAlarm":
                    ex.Headline = "🟡 False alarm: the folder says OK, the model says NG (" + Pct(pTop) + ")";
                    break;
                case "CriticalMiss":
                    ex.Headline = "🔴 Critical miss: the folder says NG, the model says OK (" + Pct(pTop) + ")";
                    break;
                case "Mismatch":
                    ex.Headline = "⚠️ Mismatch: the folder says " + folder + ", the model says " + predicted + " (" + Pct(pTop) + ")";
                    break;
                case "Match":
                    ex.Headline = "✅ The model agrees with the folder: " + predicted + " (" + Pct(pTop) + ")";
                    break;
                default:
                    ex.Headline = "The model predicts " + predicted + " (" + Pct(pTop) + ")";
                    break;
            }

            // ── Reasons ──────────────────────────────────────────────────
            string scoreList = string.Join(", ", Enumerable.Range(0, s.Scores.Length)
                .OrderByDescending(i => s.Scores[i]).Select(i => names[i] + " " + Pct(s.Scores[i])));

            if (!ex.IsMismatch)
            {
                ex.AddReason("Model scores: " + scoreList + ". The model is " + band + ".");
                ex.OneLine = predicted + " " + Pct(pTop) + " (" + band + ")";
                return ex;
            }

            ex.AddReason("Model scores: " + scoreList + ". It chose " + predicted + "; the folder says " + folder + ".");

            // How sure is it?
            string reasonConfidence;
            string shortConfidence;
            if (pTop >= VerySure)
            {
                shortConfidence = "very sure (" + Pct(pTop) + ") - check the folder label, or the model learned a misleading cue";
                reasonConfidence = "The model is very sure (" + Pct(pTop) + ") yet disagrees with the folder. " +
                    "Either the image is in the wrong folder, or the model learned a misleading cue for this kind of image. " +
                    "Look at the image yourself: if the folder is right, save a correction and retrain.";
            }
            else if (pTop >= FairlySure && margin >= ClearGap)
            {
                shortConfidence = "fairly sure (" + Pct(pTop) + ", " + Pct(margin) + " ahead of " + secondName + ")";
                reasonConfidence = "The model is fairly sure (" + Pct(pTop) + ", " + Pct(margin) + " ahead of " + secondName +
                    "). This is a real disagreement, not a coin flip.";
            }
            else
            {
                shortConfidence = "unsure, close call (only " + Pct(margin) + " between " + predicted + " and " + secondName + ")";
                reasonConfidence = "The model is unsure: its top choice has only " + Pct(pTop) + ", just " + Pct(margin) +
                    " ahead of " + secondName + ". Borderline images like this flip easily, so this looks like a hard case " +
                    "for the model rather than a clear error.";
            }
            ex.AddReason(reasonConfidence, shortConfidence);

            if (ex.Kind == "CriticalMiss")
                ex.AddReason("This is an NG to OK miss: check the product by eye before it is used.",
                             "NG to OK miss: inspect the product by eye");

            if (s.IsOutOfDistribution)
                ex.AddReason("The model also considers this image unlike its training images" +
                    (string.IsNullOrEmpty(s.OodReason) ? "." : " (" + s.OodReason + ")."),
                    "flagged as unlike the training images" + (string.IsNullOrEmpty(s.OodReason) ? "" : " (" + s.OodReason + ")"));

            // Does the image look unusual compared with the training images?
            var hints = StatHints(input, folder!, predicted);
            foreach (var h in hints.Take(MaxStatHints)) ex.AddReason(h.Text, h.Short);

            // Framing
            if (input.RoiWidth <= 0 || input.RoiHeight <= 0)
            {
                ex.AddReason("No ROI is configured, so the whole photo was given to the model. If the model was trained " +
                    "on ROI crops, predictions on whole photos can be wrong.",
                    "no ROI configured (whole photo used)");
            }
            else if (input.OriginalWidth > 0 && input.OriginalHeight > 0 &&
                     (input.OriginalWidth < input.RoiWidth || input.OriginalHeight < input.RoiHeight))
            {
                ex.AddReason("The photo is only " + input.OriginalWidth + "x" + input.OriginalHeight +
                    ", smaller than the ROI (" + input.RoiWidth + "x" + input.RoiHeight + "), so the crop had to be shifted " +
                    "or shrunk and the weld may be framed differently than in training.",
                    "photo " + input.OriginalWidth + "x" + input.OriginalHeight + " is smaller than the ROI");
            }

            // ── Compact version for the grid ─────────────────────────────
            var sb = new StringBuilder();
            sb.Append(predicted).Append(' ').Append(Pct(pTop)).Append(" vs ").Append(secondName).Append(' ').Append(Pct(pSecond));
            sb.Append(" - ").Append(band);
            if (hints.Count > 0) sb.Append("; ").Append(hints[0].Short);
            ex.OneLine = sb.ToString();
            return ex;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Image statistics vs the training images
        // ─────────────────────────────────────────────────────────────────

        private sealed class Hint
        {
            public string Text = "";
            public string Short = "";
            public bool Strong;
            public double Extreme;   // how far from the middle of the class's range (0 .. 0.5)
        }

        private static List<Hint> StatHints(ExplainInput input, string folder, string predicted)
        {
            var hints = new List<Hint>();
            var stats = input.Stats;
            var training = input.Training;
            if (stats == null || training == null) return hints;
            if (training.SampleCount(folder) < TrainingStats.MinSamples) return hints;

            foreach (StatMetric m in new[] { StatMetric.Sharpness, StatMetric.Brightness, StatMetric.Contrast })
            {
                double value = stats.Get(m);
                double pt = training.Percentile(folder, m, value);
                if (double.IsNaN(pt)) continue;

                bool unusualForFolder = pt <= UnusualLow || pt >= UnusualHigh;
                if (!unusualForFolder) continue;

                double pp = training.Percentile(predicted, m, value);
                bool typicalForPredicted = !double.IsNaN(pp) && pp >= TypicalLow && pp <= TypicalHigh;
                bool low = pt < 0.5;

                var h = new Hint { Strong = typicalForPredicted, Extreme = Math.Abs(pt - 0.5) };
                string adj = Adjective(m, low);
                if (typicalForPredicted)
                {
                    h.Text = "The crop is " + adj + " than usual for " + folder + " images (" + Ord(pt) + " percentile) " +
                             "but normal for " + predicted + " images (" + Ord(pp) + " percentile).";
                }
                else
                {
                    h.Text = "The crop is " + adj + " than usual for " + folder + " images (" + Ord(pt) + " percentile).";
                }
                h.Short = MetricName(m) + (low ? " low" : " high") + " for " + folder + " (" + Ord(pt) + " pct)";
                hints.Add(h);
            }

            // strongest evidence first
            return hints.OrderByDescending(h => h.Strong).ThenByDescending(h => h.Extreme).ToList();
        }

        private static string Adjective(StatMetric m, bool low)
        {
            switch (m)
            {
                case StatMetric.Brightness: return low ? "darker" : "brighter";
                case StatMetric.Contrast: return low ? "lower in contrast" : "higher in contrast";
                default: return low ? "blurrier" : "sharper";
            }
        }

        private static string MetricName(StatMetric m)
        {
            switch (m)
            {
                case StatMetric.Brightness: return "brightness";
                case StatMetric.Contrast: return "contrast";
                default: return "sharpness";
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Class name for every score slot. Falls back to what a mismatch itself reveals.</summary>
        public static string[] ResolveClassNames(ScoreResult s, string? folder, IEnumerable<string>? knownLabels = null)
        {
            int n = s.Scores.Length;
            if (s.ClassNames != null && s.ClassNames.Length == n && s.ClassNames.All(c => !string.IsNullOrWhiteSpace(c)))
                return s.ClassNames.Select(c => c.Trim().Trim('"')).ToArray();

            var names = new string[n];
            for (int i = 0; i < n; i++) names[i] = "class " + i;

            int top = ArgMax(s.Scores);
            if (!string.IsNullOrWhiteSpace(s.PredictedLabel) && s.PredictedLabel != Predictor_UnknownLabel)
                names[top] = s.PredictedLabel.Trim().Trim('"');

            // two classes and the folder is a known class that differs from the prediction ->
            // the other slot must be the folder's class (a folder like "Batch1" is NOT assumed to be a class)
            if (n == 2 && !string.IsNullOrWhiteSpace(folder) && !Same(folder!, names[top]) &&
                IsKnownClass(folder!, knownLabels))
                names[1 - top] = folder!.Trim();

            return names;
        }

        private const string Predictor_UnknownLabel = "Unknown";

        private static bool IsKnownClass(string folder, IEnumerable<string>? knownLabels)
        {
            IEnumerable<string> known = knownLabels ?? new[] { "OK", "NG" };
            return known.Any(k => Same(k, folder));
        }

        public static string BuildBars(string[] names, float[] scores)
        {
            const int width = 20;
            int nameWidth = names.Length == 0 ? 0 : names.Max(x => x.Length);
            var sb = new StringBuilder();
            foreach (int i in Enumerable.Range(0, scores.Length).OrderByDescending(i => scores[i]))
            {
                int filled = (int)Math.Round(Math.Max(0, Math.Min(1, scores[i])) * width);
                sb.Append(names[i].PadRight(nameWidth)).Append("  ")
                  .Append(new string('█', filled)).Append(new string('░', width - filled))
                  .Append("  ").AppendLine(Pct(scores[i]).PadLeft(6));
            }
            return sb.ToString().TrimEnd('\r', '\n');
        }

        public static string Pct(float v)
        {
            return (v * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        public static string Ord(double fraction)
        {
            int n = (int)Math.Round(fraction * 100);
            n = Math.Max(1, Math.Min(99, n));
            int mod100 = n % 100, mod10 = n % 10;
            string suffix = (mod100 >= 11 && mod100 <= 13) ? "th" : mod10 == 1 ? "st" : mod10 == 2 ? "nd" : mod10 == 3 ? "rd" : "th";
            return n.ToString(CultureInfo.InvariantCulture) + suffix;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a.Trim().Trim('"'), b.Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);
        }

        private static int ArgMax(float[] v)
        {
            int best = 0;
            for (int i = 1; i < v.Length; i++) if (v[i] > v[best]) best = i;
            return best;
        }

        private static int SecondIndex(float[] v, int top)
        {
            int best = -1;
            for (int i = 0; i < v.Length; i++)
            {
                if (i == top) continue;
                if (best < 0 || v[i] > v[best]) best = i;
            }
            return best;
        }
    }
}