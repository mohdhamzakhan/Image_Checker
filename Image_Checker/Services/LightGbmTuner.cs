using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Image_Checker.Services
{
    public static class LightGbmTuner
    {
        public static TuningResult Tune(
            MLContext mlContext,
            IDataView data,
            string labelCol = "Label",
            int nTrials = 5,
            int cvFolds = 3,
            int seed = 42,
            string logPath = null,
            double sampleFraction = 0.5,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // FIX #6: Guard against missing Features column before doing any work
            if (!data.Schema.Any(c => c.Name == "Features"))
                throw new ArgumentException(
                    "Data must have a 'Features' column. Apply preprocessing before calling LightGbmTuner.");

            // FIX #1: Use GetRowCount() first (O(1) for cached views) and only
            //         fall back to full enumeration if the view is lazy/unknown
            var totalRows = (long)(data.GetRowCount()
                ?? data.GetColumn<float[]>(data.Schema["Features"]).LongCount());

            var sampleSize = (long)(totalRows * sampleFraction);

            Console.WriteLine($"   📊 Sampling {sampleFraction:P0} of data for hyperparameter tuning...");

            // FIX #2: Shuffle before TakeRows so the sample is class-balanced,
            //         not just the first N rows (which may all be one class)
            var shuffled = mlContext.Data.ShuffleRows(data, seed: seed);
            var sampledData = mlContext.Data.TakeRows(shuffled, sampleSize);

            Console.WriteLine($"   ✅ Using {sampleSize} of {totalRows} samples for tuning");

            cancellationToken.ThrowIfCancellationRequested();

            var rnd = new Random(seed);

            // FIX #5: Expanded parameter grid to reduce duplicate random draws
            var leaves = new[] { 16, 31, 50, 80, 127 };
            var mins = new[] { 5, 10, 20, 30 };
            var lrs = new[] { 0.005f, 0.01f, 0.03f, 0.05f, 0.1f };
            var iters = new[] { 100, 200, 300, 500 };

            double bestScore = double.NegativeInfinity;
            IEstimator<ITransformer> bestEstimator = null;
            var bestParams = new Dictionary<string, object>();

            // FIX #3: Stream log to disk immediately so partial results survive a cancel
            StreamWriter logWriter = null;
            try
            {
                if (logPath != null)
                {
                    logWriter = new StreamWriter(logPath, append: false);
                    logWriter.WriteLine("Trial,Leaves,MinData,LR,Iter,Score,Duration_Sec");
                    logWriter.Flush();
                }

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

                        var results = mlContext.MulticlassClassification.CrossValidate(
                            sampledData, est, cvFolds, seed: seed);

                        cancellationToken.ThrowIfCancellationRequested();

                        var duration = DateTime.Now - startTime;

                        // FIX #7: Guard against empty results (all folds failed)
                        if (!results.Any())
                        {
                            Console.WriteLine("      ⚠️ All CV folds failed, skipping trial");
                            logWriter?.WriteLine($"{i + 1},{leaf},{min},{lr},{it},ALL_FOLDS_FAILED,0");
                            logWriter?.Flush();
                            Console.WriteLine();
                            continue;
                        }

                        var avg = results.Average(r => r.Metrics.MacroAccuracy);

                        Console.WriteLine($"      ✅ Complete in {duration.TotalSeconds:F1}s | Score: {avg:P2}");

                        // FIX #3: Write and flush immediately per trial
                        logWriter?.WriteLine($"{i + 1},{leaf},{min},{lr},{it},{avg:F6},{duration.TotalSeconds:F1}");
                        logWriter?.Flush();

                        if (avg > bestScore)
                        {
                            bestScore = avg;
                            bestEstimator = est;
                            bestParams = new Dictionary<string, object>
                            {
                                { "Leaves",  leaf },
                                { "MinData", min  },
                                { "LR",      lr   },
                                { "Iter",    it   }
                            };
                            Console.WriteLine($"      🏆 New best score!");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"      ⚠️ Trial {i + 1} cancelled");
                        logWriter?.WriteLine($"{i + 1},{leaf},{min},{lr},{it},CANCELLED,0");
                        logWriter?.Flush();
                        throw;
                    }
                    catch (Exception ex)
                    {
                        var baseEx = ex.GetBaseException();
                        Console.WriteLine($"      ❌ Failed: {baseEx.Message}");
                        logWriter?.WriteLine($"{i + 1},{leaf},{min},{lr},{it},NaN,0 # {baseEx.Message}");
                        logWriter?.Flush();
                    }

                    Console.WriteLine();
                }
            }
            finally
            {
                // FIX #3: Always close the log writer, even on exception or cancel
                logWriter?.Dispose();
            }

            if (logPath != null)
                Console.WriteLine($"   📝 Tuning log saved: {Path.GetFileName(logPath)}");

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