using System.Drawing;
using System.Text;
using Image_Checker.Services;
using Microsoft.ML;

namespace Image_Checker.Inspection.Services;

public sealed record TrainOptions(bool Sdca, bool Lbfgs, bool FastTree, bool LightGbm, int Trials, int CvFolds);

/// <summary>Trains the model of ONE step from Step{n}\Dataset\OK and \NG using the existing ModelTrainer.</summary>
public static class StepTrainer
{
    public static string Train(string datasetDir, Rectangle roi, TrainOptions options,
                               Action<string> log, CancellationToken ct)
    {
        if (!Directory.Exists(datasetDir))
            throw new DirectoryNotFoundException("Dataset folder not found: " + datasetDir);

        foreach (var label in new[] { "OK", "NG" })
        {
            string dir = Path.Combine(datasetDir, label);
            int count = Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
            if (count < 10)
                throw new InvalidOperationException(
                    $"Class {label} has only {count} images. Capture at least 10 (30 or more is recommended).");
        }

        var started = DateTime.Now;
        // color images have 3 values per pixel: keep the longest side at 224 px or less so training stays fast
        double scale = Math.Min(1.0, 224.0 / Math.Max(1, Math.Max(roi.Width, roi.Height)));
        var previousOut = Console.Out;
        Console.SetOut(new LineWriter(log));   // ModelTrainer reports through Console
        try
        {
            DataValidator.CreateCsv(datasetDir);   // images.csv from the OK / NG folders

            new ModelTrainer(new MLContext(seed: 42), datasetDir, roi).TrainAndEvaluate(
                cvFolds: options.CvFolds,
                trials: options.Trials,
                useSDCA: options.Sdca,
                useLBFGS: options.Lbfgs,
                useFastTree: options.FastTree,
                useLightGBM: options.LightGbm,
                useTransferLearning: false,
                imageWidth: Math.Max(32, (int)Math.Round(roi.Width * scale)),
                imageHeight: Math.Max(32, (int)Math.Round(roi.Height * scale)),
                cancellationToken: ct);
        }
        finally
        {
            Console.SetOut(previousOut);
        }

        var model = Directory.GetFiles(datasetDir, "bestModel-*.zip")
            .Where(f => File.GetLastWriteTime(f) >= started.AddSeconds(-2))
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();

        return model ?? throw new InvalidOperationException(
            "Training finished but no model file was produced. Read the log above for the reason.");
    }

    /// <summary>Turns Console output into log lines.</summary>
    private sealed class LineWriter : TextWriter
    {
        private readonly Action<string> _sink;
        private readonly StringBuilder _line = new();
        private readonly object _gate = new();

        public LineWriter(Action<string> sink) => _sink = sink;
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_gate)
            {
                if (value == '\n') { _sink(_line.ToString()); _line.Clear(); }
                else if (value != '\r') _line.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (value == null) return;
            foreach (var c in value) Write(c);
        }
    }
}