using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Image_Checker.Utils
{
    /// <summary>
    /// Generates and displays a confusion matrix for any number of classes.
    /// Works with any ML.NET multiclass classification model.
    /// Handles both key-typed and string-typed Label / PredictedLabel columns.
    /// </summary>
    public static class ConfusionMatrixReporter
    {
        /// <summary>
        /// Builds and prints the confusion matrix from a trained model + test split.
        /// </summary>
        public static ConfusionMatrixResult Evaluate(
            MLContext mlContext,
            ITransformer model,
            IDataView testSet,
            IList<string> classLabels,
            string reportPath = null)
        {
            if (classLabels == null || classLabels.Count == 0)
                throw new ArgumentException("classLabels must contain at least one entry.");

            // ── 1. Run predictions ────────────────────────────────────────────────
            var predictions = model.Transform(testSet);

            // ── 2. Extract actual vs. predicted ──────────────────────────────────
            int n = classLabels.Count;
            var matrix = new int[n, n];

            // Build a label → index lookup (case-insensitive, trim-safe)
            var labelIndex = classLabels
                .Select((lbl, idx) => (lbl, idx))
                .ToDictionary(
                    x => x.lbl.Trim(),
                    x => x.idx,
                    StringComparer.OrdinalIgnoreCase);

            // Detect column types at runtime so we handle both:
            //   • KeyDataViewType  (uint, 1-based, before MapKeyToValue)
            //   • String           (after MapKeyToValue)
            var schema = predictions.Schema;
            bool actualIsKey = schema["Label"].Type is KeyDataViewType;
            bool predictedIsKey = schema["PredictedLabel"].Type is KeyDataViewType;

            int[] actualIndices, predictedIndices;

            if (actualIsKey)
            {
                actualIndices = predictions.GetColumn<uint>("Label")
                    .Select(k => (int)k - 1)   // 1-based → 0-based
                    .ToArray();
            }
            else
            {
                actualIndices = predictions.GetColumn<string>("Label")
                    .Select(s => labelIndex.TryGetValue(s?.Trim() ?? "", out var idx) ? idx : -1)
                    .ToArray();
            }

            if (predictedIsKey)
            {
                predictedIndices = predictions.GetColumn<uint>("PredictedLabel")
                    .Select(k => (int)k - 1)
                    .ToArray();
            }
            else
            {
                predictedIndices = predictions.GetColumn<string>("PredictedLabel")
                    .Select(s => labelIndex.TryGetValue(s?.Trim() ?? "", out var idx) ? idx : -1)
                    .ToArray();
            }

            for (int i = 0; i < actualIndices.Length; i++)
            {
                int actual = actualIndices[i];
                int predicted = predictedIndices[i];

                if (actual >= 0 && actual < n &&
                    predicted >= 0 && predicted < n)
                {
                    matrix[actual, predicted]++;
                }
            }

            // ── 3. Compute per-class metrics ──────────────────────────────────────
            var metrics = ComputeMetrics(matrix, classLabels, n);

            // ── 4. Print to console ───────────────────────────────────────────────
            PrintConsole(matrix, classLabels, metrics, n);

            // ── 5. Optionally write HTML report ──────────────────────────────────
            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                var html = BuildHtmlReport(matrix, classLabels, metrics, n);
                File.WriteAllText(reportPath, html, Encoding.UTF8);
                Console.WriteLine($"\n📄 HTML report saved: {reportPath}");
            }

            return new ConfusionMatrixResult
            {
                Matrix = matrix,
                Labels = classLabels.ToList(),
                PerClass = metrics,
                TotalSamples = actualIndices.Length
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Internal helpers
        // ─────────────────────────────────────────────────────────────────────────

        private static List<ClassMetrics> ComputeMetrics(int[,] matrix, IList<string> labels, int n)
        {
            var result = new List<ClassMetrics>();

            for (int c = 0; c < n; c++)
            {
                int tp = matrix[c, c];

                int rowSum = 0;
                for (int j = 0; j < n; j++) rowSum += matrix[c, j];

                int colSum = 0;
                for (int i = 0; i < n; i++) colSum += matrix[i, c];

                int fn = rowSum - tp;
                int fp = colSum - tp;

                double precision = (tp + fp) == 0 ? 0.0 : (double)tp / (tp + fp);
                double recall = (tp + fn) == 0 ? 0.0 : (double)tp / (tp + fn);
                double f1 = (precision + recall) == 0
                    ? 0.0
                    : 2.0 * precision * recall / (precision + recall);

                result.Add(new ClassMetrics
                {
                    Label = labels[c],
                    TP = tp,
                    FP = fp,
                    FN = fn,
                    Support = rowSum,
                    Precision = precision,
                    Recall = recall,
                    F1 = f1
                });
            }

            return result;
        }

        private static void PrintConsole(int[,] matrix, IList<string> labels, List<ClassMetrics> metrics, int n)
        {
            int labelW = Math.Max(labels.Max(l => l.Length), 12);
            int cellW = Math.Max(labels.Max(l => l.Length), 6);

            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║              CONFUSION MATRIX                        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  Rows = Actual class   |   Columns = Predicted class");
            Console.WriteLine();

            var header = new StringBuilder();
            header.Append("  " + "Actual \\ Predicted".PadRight(labelW + 2));
            foreach (var lbl in labels)
                header.Append(lbl.PadLeft(cellW + 1));
            header.Append("   Support");
            Console.WriteLine(header.ToString());
            Console.WriteLine("  " + new string('─', labelW + 2 + (cellW + 1) * n + 10));

            for (int r = 0; r < n; r++)
            {
                var row = new StringBuilder();
                row.Append("  " + labels[r].PadRight(labelW + 2));
                for (int c = 0; c < n; c++)
                {
                    string val = matrix[r, c].ToString();
                    string cell = r == c ? $"[{val}]" : val;
                    row.Append(cell.PadLeft(cellW + 1));
                }
                row.Append($"   {metrics[r].Support}");
                Console.WriteLine(row.ToString());
            }

            Console.WriteLine();
            Console.WriteLine("  [ ] = Correct predictions (diagonal)");
            Console.WriteLine();

            Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │ Per-Class Report                                                 │");
            Console.WriteLine("  ├──────────────────┬──────────┬──────────┬──────────┬─────────────┤");
            Console.WriteLine("  │ Class            │ Precis.  │ Recall   │ F1 Score │ Support     │");
            Console.WriteLine("  ├──────────────────┼──────────┼──────────┼──────────┼─────────────┤");

            foreach (var m in metrics)
                Console.WriteLine(
                    $"  │ {m.Label.PadRight(16)} │ {m.Precision,8:P1} │ {m.Recall,8:P1} │ {m.F1,8:P1} │ {m.Support,11} │");

            Console.WriteLine("  ├──────────────────┼──────────┼──────────┼──────────┼─────────────┤");

            double macroP = metrics.Average(m => m.Precision);
            double macroR = metrics.Average(m => m.Recall);
            double macroF1 = metrics.Average(m => m.F1);
            int total = metrics.Sum(m => m.Support);

            Console.WriteLine(
                $"  │ {"Macro Avg".PadRight(16)} │ {macroP,8:P1} │ {macroR,8:P1} │ {macroF1,8:P1} │ {total,11} │");

            double weightedP = metrics.Sum(m => m.Precision * m.Support) / total;
            double weightedR = metrics.Sum(m => m.Recall * m.Support) / total;
            double weightedF1 = metrics.Sum(m => m.F1 * m.Support) / total;

            Console.WriteLine(
                $"  │ {"Weighted Avg".PadRight(16)} │ {weightedP,8:P1} │ {weightedR,8:P1} │ {weightedF1,8:P1} │ {total,11} │");

            Console.WriteLine("  └──────────────────┴──────────┴──────────┴──────────┴─────────────┘");
            Console.WriteLine();
        }

        private static string BuildHtmlReport(int[,] matrix, IList<string> labels, List<ClassMetrics> metrics, int n)
        {
            double macroP = metrics.Average(m => m.Precision);
            double macroR = metrics.Average(m => m.Recall);
            double macroF1 = metrics.Average(m => m.F1);
            int total = metrics.Sum(m => m.Support);

            int maxVal = 0;
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    if (matrix[r, c] > maxVal) maxVal = matrix[r, c];

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>");
            sb.AppendLine("<title>Confusion Matrix Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:Consolas,monospace;background:#0f0f0f;color:#e0e0e0;padding:2rem;}");
            sb.AppendLine("h1{color:#7eb8f7;border-bottom:1px solid #333;padding-bottom:.5rem;}");
            sb.AppendLine("table{border-collapse:collapse;margin:1rem 0;}");
            sb.AppendLine("th,td{padding:.4rem .8rem;border:1px solid #333;text-align:center;}");
            sb.AppendLine("th{background:#1a1a2e;color:#7eb8f7;}");
            sb.AppendLine(".diag{font-weight:bold;border:2px solid #7eb8f7;}");
            sb.AppendLine(".metrics th{background:#1a2e1a;color:#7ef79e;}");
            sb.AppendLine(".avg-row{background:#1a1a2e;}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine($"<h1>🔬 Confusion Matrix Report</h1>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} &nbsp;|&nbsp; Classes: {n} &nbsp;|&nbsp; Total samples: {total}</p>");

            sb.AppendLine("<h2>Confusion Matrix</h2>");
            sb.AppendLine("<p><em>Rows = Actual class &nbsp; | &nbsp; Columns = Predicted class</em></p>");
            sb.AppendLine("<table>");
            sb.Append("<tr><th>Actual \\ Predicted</th>");
            foreach (var lbl in labels) sb.Append($"<th>{lbl}</th>");
            sb.AppendLine("<th>Support</th></tr>");

            for (int r = 0; r < n; r++)
            {
                sb.Append($"<tr><th>{labels[r]}</th>");
                for (int c = 0; c < n; c++)
                {
                    int val = matrix[r, c];
                    double heat = maxVal > 0 ? (double)val / maxVal : 0;
                    string bg = r == c
                        ? $"rgba(126,184,247,{heat:F2})"
                        : $"rgba(255,80,80,{heat * 0.6:F2})";
                    string cls = r == c ? " class='diag'" : "";
                    sb.Append($"<td{cls} style='background:{bg}'>{val}</td>");
                }
                sb.AppendLine($"<td>{metrics[r].Support}</td></tr>");
            }
            sb.AppendLine("</table>");

            sb.AppendLine("<h2>Per-Class Metrics</h2>");
            sb.AppendLine("<table class='metrics'>");
            sb.AppendLine("<tr><th>Class</th><th>Precision</th><th>Recall</th><th>F1 Score</th><th>TP</th><th>FP</th><th>FN</th><th>Support</th></tr>");

            foreach (var m in metrics)
                sb.AppendLine($"<tr><td>{m.Label}</td><td>{m.Precision:P1}</td><td>{m.Recall:P1}</td><td>{m.F1:P1}</td>" +
                              $"<td>{m.TP}</td><td>{m.FP}</td><td>{m.FN}</td><td>{m.Support}</td></tr>");

            double wP = metrics.Sum(m => m.Precision * m.Support) / total;
            double wR = metrics.Sum(m => m.Recall * m.Support) / total;
            double wF1 = metrics.Sum(m => m.F1 * m.Support) / total;

            sb.AppendLine($"<tr class='avg-row'><td><strong>Macro Avg</strong></td><td>{macroP:P1}</td><td>{macroR:P1}</td><td>{macroF1:P1}</td><td colspan='4'></td></tr>");
            sb.AppendLine($"<tr class='avg-row'><td><strong>Weighted Avg</strong></td><td>{wP:P1}</td><td>{wR:P1}</td><td>{wF1:P1}</td><td colspan='3'></td><td>{total}</td></tr>");
            sb.AppendLine("</table>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Return types
    // ─────────────────────────────────────────────────────────────────────────────

    public class ConfusionMatrixResult
    {
        public int[,] Matrix { get; set; }
        public List<string> Labels { get; set; }
        public List<ClassMetrics> PerClass { get; set; }
        public int TotalSamples { get; set; }
    }

    public class ClassMetrics
    {
        public string Label { get; set; }
        public int TP { get; set; }
        public int FP { get; set; }
        public int FN { get; set; }
        public int Support { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1 { get; set; }
    }
}
