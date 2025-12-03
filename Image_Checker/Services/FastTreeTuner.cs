using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Image_Checker.Services
{
    public static class FastTreeTuner
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
            int nTrials = 5,
            int cvFolds = 2,
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
            var log = new List<string> { "Trial,Trees,Leaves,LR,MinDocs,Score,Duration_Sec" };

            // FastTree hyperparameters
            var trees = new[] { 50, 100, 200 };
            var leaves = new[] { 20, 50, 80 };
            var lrs = new[] { 0.05, 0.1, 0.2 };
            var minDocs = new[] { 5, 10, 20 };

            double bestScore = double.NegativeInfinity;
            IEstimator<ITransformer> bestEstimator = null;
            var bestParams = new Dictionary<string, object>();

            for (int i = 0; i < nTrials; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tree = trees[rnd.Next(trees.Length)];
                var leaf = leaves[rnd.Next(leaves.Length)];
                var lr = lrs[rnd.Next(lrs.Length)];
                var minDoc = minDocs[rnd.Next(minDocs.Length)];

                Console.WriteLine($"   Trial {i + 1}/{nTrials}: Trees={tree}, Leaves={leaf}, LR={lr}, MinDocs={minDoc}");

                var est = mlContext.MulticlassClassification.Trainers.OneVersusAll(
                    mlContext.BinaryClassification.Trainers.FastTree(
                        labelColumnName: labelCol,
                        featureColumnName: "Features",
                        numberOfTrees: tree,
                        numberOfLeaves: leaf,
                        learningRate: lr,
                        minimumExampleCountPerLeaf: minDoc));

                try
                {
                    Console.WriteLine($"      ⏳ Running {cvFolds}-fold cross-validation...");
                    var startTime = DateTime.Now;

                    var results = mlContext.MulticlassClassification.CrossValidate(sampledData, est, cvFolds, seed: seed);

                    cancellationToken.ThrowIfCancellationRequested();

                    var duration = DateTime.Now - startTime;
                    var avg = results.Average(r => r.Metrics.MacroAccuracy);

                    Console.WriteLine($"      ✅ Complete in {duration.TotalSeconds:F1}s | Score: {avg:P2}");
                    log.Add($"{i + 1},{tree},{leaf},{lr},{minDoc},{avg},{duration.TotalSeconds:F1}");

                    if (avg > bestScore)
                    {
                        bestScore = avg;
                        bestEstimator = est;
                        bestParams = new Dictionary<string, object>
                        {
                            { "Trees", tree },
                            { "Leaves", leaf },
                            { "LR", lr },
                            { "MinDocs", minDoc }
                        };
                        Console.WriteLine($"      🏆 New best score!");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"      ⚠️ Trial {i + 1} cancelled");
                    log.Add($"{i + 1},{tree},{leaf},{lr},{minDoc},CANCELLED,0");
                    throw;
                }
                catch (Exception ex)
                {
                    var baseEx = ex.GetBaseException();
                    Console.WriteLine($"      ❌ Failed: {baseEx.Message}");
                    log.Add($"{i + 1},{tree},{leaf},{lr},{minDoc},NaN,0 # {baseEx.Message}");
                }
                Console.WriteLine();
            }

            if (logPath != null)
            {
                File.WriteAllLines(logPath, log);
                Console.WriteLine($"   📝 Tuning log saved: {Path.GetFileName(logPath)}");
            }

            Console.WriteLine("\n   Best FastTree parameters:");
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