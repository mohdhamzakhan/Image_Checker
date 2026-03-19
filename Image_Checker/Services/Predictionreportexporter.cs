// ══════════════════════════════════════════════════════════════════════════════
//  PredictionReportExporter.cs
//  Generates an HTML + CSV prediction report after training.
//  Loads the saved model, runs predictions on the full pre-processed dataset
//  and writes both a styled HTML report and a raw CSV for further analysis.
// ══════════════════════════════════════════════════════════════════════════════

using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Image_Checker.Services
{
    public static class PredictionReportExporter
    {
        /// <summary>
        /// Generates Actual vs Predicted CSV + HTML report.
        /// Returns (csvPath, htmlPath).
        /// </summary>
        public static (string CsvPath, string HtmlPath) Export(
            MLContext mlContext,
            string modelZipPath,
            string cleanedCsvPath,
            string labelColumn,
            TaskType task,
            string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var csvPath = Path.Combine(outputDirectory, $"prediction_report_{stamp}.csv");
            var htmlPath = Path.Combine(outputDirectory, $"prediction_report_{stamp}.html");

            // ── Load model ────────────────────────────────────────────────────
            var model = mlContext.Model.Load(modelZipPath, out var schema);

            // ── Load cleaned data ─────────────────────────────────────────────
            var loader = mlContext.Data.CreateTextLoader(new Microsoft.ML.Data.TextLoader.Options
            {
                HasHeader = true,
                Separators = new[] { ',' },
                AllowQuoting = true
            });
            var data = loader.Load(cleanedCsvPath);
            var preds = model.Transform(data);

            // ── Extract actual + predicted ────────────────────────────────────
            var rows = BuildReportRows(mlContext, preds, labelColumn, task);

            // ── Write CSV ─────────────────────────────────────────────────────
            WriteCsv(rows, csvPath);

            // ── Compute summary metrics ───────────────────────────────────────
            var metrics = ComputeSummaryMetrics(rows, task);

            // ── Write HTML ────────────────────────────────────────────────────
            WriteHtml(rows, metrics, modelZipPath, task, htmlPath);

            return (csvPath, htmlPath);
        }

        // ── Row extraction ────────────────────────────────────────────────────

        private static List<(string Actual, string Predicted, float Score)> BuildReportRows(
            MLContext mlContext, IDataView preds, string labelColumn, TaskType task)
        {
            var result = new List<(string, string, float)>();

            try
            {
                if (task is TaskType.BinaryClassification or TaskType.MulticlassClassification)
                {
                    var actuals = preds.GetColumn<uint>("Label").ToList();
                    var predicted = preds.GetColumn<uint>("PredictedLabel").ToList();
                    var scores = preds.GetColumn<float>("Score").ToList();
                    int n = Math.Min(actuals.Count, predicted.Count);
                    for (int i = 0; i < n; i++)
                        result.Add((actuals[i].ToString(), predicted[i].ToString(),
                                    i < scores.Count ? scores[i] : 0f));
                }
                else  // Regression
                {
                    var actuals = preds.GetColumn<float>("Label").ToList();
                    var predicted = preds.GetColumn<float>("Score").ToList();
                    int n = Math.Min(actuals.Count, predicted.Count);
                    for (int i = 0; i < n; i++)
                        result.Add((actuals[i].ToString("G6"), predicted[i].ToString("G6"),
                                    predicted[i]));
                }
            }
            catch (Exception ex)
            {
                result.Add(("Error", ex.Message, 0f));
            }

            return result;
        }

        // ── Metrics ───────────────────────────────────────────────────────────

        private static Dictionary<string, string> ComputeSummaryMetrics(
            List<(string Actual, string Predicted, float Score)> rows, TaskType task)
        {
            var m = new Dictionary<string, string>();
            m["Total Rows"] = rows.Count.ToString();

            if (task is TaskType.BinaryClassification or TaskType.MulticlassClassification)
            {
                int correct = rows.Count(r => r.Actual == r.Predicted);
                m["Correct"] = correct.ToString();
                m["Wrong"] = (rows.Count - correct).ToString();
                m["Accuracy"] = $"{(double)correct / rows.Count:P2}";
            }
            else if (task == TaskType.Regression)
            {
                var pairs = rows
                    .Select(r => (
                        A: double.TryParse(r.Actual, out var a) ? a : 0.0,
                        P: double.TryParse(r.Predicted, out var p) ? p : 0.0))
                    .ToList();
                double mae = pairs.Average(x => Math.Abs(x.A - x.P));
                double rmse = Math.Sqrt(pairs.Average(x => Math.Pow(x.A - x.P, 2)));
                double meanA = pairs.Average(x => x.A);
                double ssTot = pairs.Sum(x => Math.Pow(x.A - meanA, 2));
                double ssRes = pairs.Sum(x => Math.Pow(x.A - x.P, 2));
                double r2 = ssTot < 1e-10 ? 1 : 1 - ssRes / ssTot;
                m["MAE"] = mae.ToString("F4");
                m["RMSE"] = rmse.ToString("F4");
                m["R²"] = r2.ToString("F4");
            }
            return m;
        }

        // ── CSV writer ────────────────────────────────────────────────────────

        private static void WriteCsv(
            List<(string Actual, string Predicted, float Score)> rows, string path)
        {
            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine("Row,Actual,Predicted,Score");
            for (int i = 0; i < rows.Count; i++)
                sw.WriteLine($"{i + 1},{rows[i].Actual},{rows[i].Predicted},{rows[i].Score:G6}");
        }

        // ── HTML writer ───────────────────────────────────────────────────────

        private static void WriteHtml(
            List<(string Actual, string Predicted, float Score)> rows,
            Dictionary<string, string> metrics,
            string modelPath,
            TaskType task,
            string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='UTF-8'>");
            sb.AppendLine("<title>Prediction Report</title><style>");
            sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:32px;color:#222;}");
            sb.AppendLine("h1{color:#2c5fa8;}h2{color:#444;border-bottom:1px solid #ddd;padding-bottom:6px;}");
            sb.AppendLine(".metrics{display:flex;gap:20px;flex-wrap:wrap;margin-bottom:24px;}");
            sb.AppendLine(".card{background:#f4f8ff;border:1px solid #c8daff;border-radius:8px;padding:16px 24px;min-width:140px;}");
            sb.AppendLine(".card .val{font-size:24px;font-weight:700;color:#2c5fa8;}");
            sb.AppendLine(".card .lbl{font-size:12px;color:#666;margin-top:4px;}");
            sb.AppendLine("table{border-collapse:collapse;width:100%;font-size:13px;}");
            sb.AppendLine("th{background:#2c5fa8;color:#fff;padding:8px 12px;text-align:left;}");
            sb.AppendLine("tr:nth-child(even){background:#f4f8ff;}");
            sb.AppendLine("td{padding:6px 12px;border-bottom:1px solid #e0e0e0;}");
            sb.AppendLine(".correct{color:#1a7a2e;font-weight:600;}.wrong{color:#c0392b;font-weight:600;}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h1>📊 Prediction Report</h1>");
            sb.AppendLine($"<p>Model: <code>{Path.GetFileName(modelPath)}</code> &nbsp;|&nbsp; Task: <strong>{task}</strong> &nbsp;|&nbsp; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

            // Metric cards
            sb.AppendLine("<h2>Summary Metrics</h2><div class='metrics'>");
            foreach (var (key, val) in metrics)
            {
                sb.AppendLine($"<div class='card'><div class='val'>{val}</div><div class='lbl'>{key}</div></div>");
            }
            sb.AppendLine("</div>");

            // Table
            sb.AppendLine("<h2>Predictions (first 500 rows)</h2>");
            sb.AppendLine("<table><tr><th>#</th><th>Actual</th><th>Predicted</th><th>Score</th><th>Result</th></tr>");
            foreach (var (row, i) in rows.Take(500).Select((r, i) => (r, i)))
            {
                bool correct = row.Actual == row.Predicted;
                string cls = task == TaskType.Regression ? "" : (correct ? "correct" : "wrong");
                string result = task == TaskType.Regression ? "" : (correct ? "✓ Correct" : "✗ Wrong");
                sb.AppendLine($"<tr><td>{i + 1}</td><td>{row.Actual}</td><td>{row.Predicted}</td>" +
                              $"<td>{row.Score:G4}</td><td class='{cls}'>{result}</td></tr>");
            }
            sb.AppendLine("</table>");
            if (rows.Count > 500)
                sb.AppendLine($"<p><em>Showing first 500 of {rows.Count} rows. See CSV for full results.</em></p>");
            sb.AppendLine("</body></html>");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}