using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Image_Checker.Services
{
    public static class LightGbmTuner
    {
        public class TuningResult
        {
            public IEstimator<ITransformer> BestEstimator { get; set; }
            public IDictionary<string, object> Params { get; set; }
            public double Score { get; set; }
        }

        public static TuningResult Tune(
            MLContext mlContext,
            IDataView data,
            string labelCol = "Label",
            int nTrials = 2,
            int cvFolds = 3,
            int seed = 42,
            string logPath = null,
            double sampleFraction = 0.5,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var totalRows = data.GetColumn<float[]>(data.Schema["Features"]).Count();
            var sampleSize = (int)(totalRows * sampleFraction);

            Console.WriteLine($"   📊 Sampling {sampleFraction:P0} of data for hyperparameter tuning...");
            var sampledData = mlContext.Data.TakeRows(data, sampleSize);
            Console.WriteLine($"   ✅ Using {sampleSize} of {totalRows} samples for tuning");

            cancellationToken.ThrowIfCancellationRequested();

            var rnd = new Random(seed);
            var log = new List<string> { "Trial,Leaves,MinData,LR,Iter,Score,Duration_Sec" };

            // Reduced parameter ranges for faster exploration
            var leaves = new[] { 16, 31, 50 };
            var mins = new[] { 5, 20 };
            var lrs = new[] { 0.01f, 0.03f };
            var iters = new[] { 100, 300 };

            double bestScore = double.NegativeInfinity;
            IEstimator<ITransformer> bestEstimator = null;
            var bestParams = new Dictionary<string, object>();

            for (int i = 0; i < nTrials; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var leaf = leaves[rnd.Next(leaves.Length)];
                var min = mins[rnd.Next(mins.Length)];
                var lr = lrs[rnd.Next(lrs.Length)];
                var it = iters[rnd.Next(iters.Length)];

                Console.WriteLine($"   Trial {i + 1}/{nTrials}: Leaves={leaf}, MinData={min}, LR={lr}, Iter={it}");

                var est = mlContext.MulticlassClassification.Trainers.LightGbm(
                    labelColumnName: labelCol,
                    featureColumnName: "Features",
                    numberOfLeaves: leaf,
                    minimumExampleCountPerLeaf: min,
                    learningRate: lr,
                    numberOfIterations: it);

                try
                {
                    Console.WriteLine($"      ⏳ Running {cvFolds}-fold cross-validation...");
                    var startTime = DateTime.Now;

                    var results = mlContext.MulticlassClassification.CrossValidate(sampledData, est, cvFolds, seed: seed);

                    cancellationToken.ThrowIfCancellationRequested();

                    var duration = DateTime.Now - startTime;
                    var avg = results.Average(r => r.Metrics.MacroAccuracy);

                    Console.WriteLine($"      ✅ Complete in {duration.TotalSeconds:F1}s | Score: {avg:P2}");
                    log.Add($"{i + 1},{leaf},{min},{lr},{it},{avg},{duration.TotalSeconds:F1}");

                    if (avg > bestScore)
                    {
                        bestScore = avg;
                        bestEstimator = est;
                        bestParams = new Dictionary<string, object>
                        {
                            { "Leaves", leaf },
                            { "MinData", min },
                            { "LR", lr },
                            { "Iter", it }
                        };
                        Console.WriteLine($"      🏆 New best score!");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"      ⚠️ Trial {i + 1} cancelled");
                    log.Add($"{i + 1},{leaf},{min},{lr},{it},CANCELLED,0");
                    throw;
                }
                catch (Exception ex)
                {
                    var baseEx = ex.GetBaseException();
                    Console.WriteLine($"      ❌ Failed: {baseEx.Message}");
                    log.Add($"{i + 1},{leaf},{min},{lr},{it},NaN,0 # {baseEx.Message}");
                }
                Console.WriteLine();
            }

            if (logPath != null)
            {
                File.WriteAllLines(logPath, log);
                Console.WriteLine($"   📝 Tuning log saved: {Path.GetFileName(logPath)}");
            }

            Console.WriteLine("\n   Best LightGBM parameters:");
            foreach (var kv in bestParams)
                Console.WriteLine($"     {kv.Key}: {kv.Value}");
            Console.WriteLine($"   Best CV MacroAccuracy = {bestScore:P4}");

            return new TuningResult
            {
                BestEstimator = bestEstimator,
                Params = bestParams,
                Score = bestScore
            };
        }
    }
}