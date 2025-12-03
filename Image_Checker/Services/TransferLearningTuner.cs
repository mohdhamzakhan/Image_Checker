using Image_Checker.DataModels;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Vision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Microsoft.ML.Vision.ImageClassificationTrainer;

namespace Image_Checker.Services
{
    public static class TransferLearningTuner
    {
        public class TuningResult
        {
            public IEstimator<ITransformer> BestEstimator { get; set; }
            public IDictionary<string, object> Params { get; set; }
            public double Score { get; set; }
        }

        public static TuningResult Tune(
            MLContext mlContext,
            IDataView trainData,
            IDataView testData,
            string basePath,
            int nTrials = 3,
            int seed = 42,
            string logPath = null)
        {
            var rnd = new Random(seed);
            var log = new List<string> { "Trial,Architecture,Epochs,BatchSize,LR,Score,Duration_Sec" };

            // Transfer learning hyperparameters
            var architectures = new[]
            {
                ImageClassificationTrainer.Architecture.ResnetV2101,
                ImageClassificationTrainer.Architecture.MobilenetV2,
                ImageClassificationTrainer.Architecture.InceptionV3
            };
            var epochs = new[] { 5, 10, 15 };
            var batchSizes = new[] { 8, 16 };
            var lrs = new[] { 0.001f, 0.01f, 0.02f };

            double bestScore = double.NegativeInfinity;
            IEstimator<ITransformer> bestEstimator = null;
            var bestParams = new Dictionary<string, object>();

            for (int i = 0; i < nTrials; i++)
            {
                var arch = architectures[rnd.Next(architectures.Length)];
                var epoch = epochs[rnd.Next(epochs.Length)];
                var batch = batchSizes[rnd.Next(batchSizes.Length)];
                var lr = lrs[rnd.Next(lrs.Length)];

                Console.WriteLine($"   Trial {i + 1}/{nTrials}: Arch={arch}, Epochs={epoch}, Batch={batch}, LR={lr}");

                var options = new ImageClassificationTrainer.Options
                {
                    FeatureColumnName = "InputImage",
                    LabelColumnName = "Label",
                    Arch = arch,
                    Epoch = epoch,
                    BatchSize = batch,
                    LearningRate = lr,
                    EarlyStoppingCriteria = new EarlyStopping(minDelta: 0.001f, patience: 3),
                    ValidationSet = testData
                };

                var pipeline = mlContext.Transforms.LoadImages("InputImage", basePath, nameof(ImageData.ImagePath))
                    .Append(mlContext.Transforms.ResizeImages("InputImage", 224, 224))
                    .Append(mlContext.Transforms.Conversion.MapValueToKey("Label"))
                    .Append(mlContext.MulticlassClassification.Trainers.ImageClassification(options))
                    .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                try
                {
                    Console.WriteLine($"      ⏳ Training transfer learning model...");
                    var startTime = DateTime.Now;

                    var model = pipeline.Fit(trainData);

                    var duration = DateTime.Now - startTime;

                    // Evaluate on test set
                    var predictions = model.Transform(testData);
                    var metrics = mlContext.MulticlassClassification.Evaluate(predictions, "Label");

                    Console.WriteLine($"      ✅ Complete in {duration.TotalSeconds:F1}s | Score: {metrics.MacroAccuracy:P2}");
                    log.Add($"{i + 1},{arch},{epoch},{batch},{lr},{metrics.MacroAccuracy},{duration.TotalSeconds:F1}");

                    if (metrics.MacroAccuracy > bestScore)
                    {
                        bestScore = metrics.MacroAccuracy;
                        bestEstimator = pipeline;
                        bestParams = new Dictionary<string, object>
                        {
                            { "Architecture", arch.ToString() },
                            { "Epochs", epoch },
                            { "BatchSize", batch },
                            { "LR", lr }
                        };
                        Console.WriteLine($"      🏆 New best score!");
                    }
                }
                catch (Exception ex)
                {
                    var baseEx = ex.GetBaseException();
                    Console.WriteLine($"      ❌ Failed: {baseEx.Message}");
                    log.Add($"{i + 1},{arch},{epoch},{batch},{lr},NaN,0 # {baseEx.Message}");
                }
                Console.WriteLine();
            }

            if (logPath != null)
            {
                File.WriteAllLines(logPath, log);
                Console.WriteLine($"   📝 Tuning log saved: {Path.GetFileName(logPath)}");
            }

            Console.WriteLine("\n   Best Transfer Learning parameters:");
            foreach (var kv in bestParams)
                Console.WriteLine($"     {kv.Key}: {kv.Value}");
            Console.WriteLine($"   Best Test MacroAccuracy = {bestScore:P4}");

            return new TuningResult
            {
                BestEstimator = bestEstimator,
                Params = bestParams,
                Score = bestScore
            };
        }
    }
}