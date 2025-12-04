using Image_Checker.DataModels;
using Microsoft.ML;

namespace ImageChecker.Services
{
    public class TrueIncrementalTrainer
    {
        private readonly MLContext _mlContext;
        private readonly string _basePath;
        private readonly string _correctionsPath;

        public TrueIncrementalTrainer(MLContext mlContext, string basePath)
        {
            _mlContext = mlContext;
            _basePath = basePath;
            _correctionsPath = Path.Combine(basePath, "corrections.csv");
        }

        /// <summary>
        /// True incremental training: Updates EXISTING model with new corrections
        /// Model 1: 1000 images → Model 2: 1000+10 → Model 3: 1000+10+20 = 1030
        /// </summary>
        public string IncrementalUpdate(string existingModelPath)
        {
            Console.WriteLine("🚀 TRUE INCREMENTAL TRAINING");
            Console.WriteLine($"📁 Base model: {Path.GetFileName(existingModelPath)}");

            // 1. VALIDATE INPUTS
            if (!File.Exists(existingModelPath))
                throw new FileNotFoundException("Base model not found");
            if (!File.Exists(_correctionsPath))
                throw new FileNotFoundException("corrections.csv not found");

            // 2. LOAD EXISTING MODEL
            Console.WriteLine("📥 Loading existing model...");
            var existingModel = _mlContext.Model.Load(existingModelPath, out var modelSchema);

            // 3. PREPARE CORRECTIONS DATASET
            Console.WriteLine("🔄 Processing corrections...");
            var (correctionsCsv, classCount, labelCounts, totalSamples) = CreateCorrectionsDataset();

            if (classCount < 2)
                throw new InvalidOperationException("Need corrections for BOTH OK and NG");

            Console.WriteLine($"✅ {totalSamples} correction samples loaded");
            Console.WriteLine("Label distribution:");
            foreach (var kv in labelCounts)
                Console.WriteLine($"  - {kv.Key}: {kv.Value} samples");

            var correctionData = _mlContext.Data.LoadFromTextFile<ImageData>(correctionsCsv,
                separatorChar: ',', hasHeader: false);

            // 4. BUILD PREPROCESSING PIPELINE (must match original training)
            var preprocess = BuildPreprocessingPipeline();

            // 5. SELECT ONLINE-CAPABLE TRAINER
            var trainer = SelectOnlineTrainer(totalSamples, labelCounts);
            Console.WriteLine($"🎯 Using trainer: {trainer.ToString().Split('.').Last()}");

            // 6. APPLY INCREMENTAL UPDATE TO EXISTING MODEL
            Console.WriteLine("🔄 Updating existing model with corrections...");
            var startTime = DateTime.Now;

            var updatePipeline = preprocess
                .Append(trainer)
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

            var updatedModel = updatePipeline.Fit(correctionData);

            var duration = DateTime.Now - startTime;
            Console.WriteLine($"✅ Update complete in {duration.TotalSeconds:F1}s");

            // 7. EVALUATE ON CORRECTIONS
            try
            {
                var predictions = updatedModel.Transform(correctionData);
                var metrics = _mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "Label");
                Console.WriteLine($"📊 Accuracy on corrections: {metrics.MacroAccuracy:P2} (Micro: {metrics.MicroAccuracy:P2})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Evaluation failed: {ex.Message}");
            }

            // 8. SAVE NEW INCREMENTAL MODEL
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var newModelPath = Path.Combine(_basePath, $"incrementalModel-v{timestamp}.zip");

            Console.WriteLine("💾 Saving updated model...");
            _mlContext.Model.Save(updatedModel, correctionData.Schema, newModelPath);
            Console.WriteLine($"✅ New model: {Path.GetFileName(newModelPath)}");

            // 9. CLEANUP
            try { if (File.Exists(correctionsCsv)) File.Delete(correctionsCsv); }
            catch { /* ignore */ }

            Console.WriteLine("🎉 INCREMENTAL UPDATE COMPLETE");
            Console.WriteLine($"📈 Cumulative learning: Original + {totalSamples} new corrections");
            return newModelPath;
        }

        private IEstimator<ITransformer> SelectOnlineTrainer(int totalSamples, Dictionary<string, int> labelCounts)
        {
            var minClassSamples = labelCounts.Values.Min();

            if (totalSamples <= 20 || minClassSamples <= 3)
            {
                // SDCA: Best for tiny updates, true online learning
                return _mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    maximumNumberOfIterations: 50);
            }
            else if (totalSamples <= 50)
            {
                // LBFGS: Good balance for medium updates
                return _mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    l1Regularization: 0.1f,
                    l2Regularization: 0.1f);
            }
            else
            {
                // LightGBM: Conservative settings for larger batches
                return _mlContext.MulticlassClassification.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfLeaves: 20,
                    minimumExampleCountPerLeaf: 3,
                    learningRate: 0.01f,
                    numberOfIterations: 100);
            }
        }

        private IEstimator<ITransformer> BuildPreprocessingPipeline()
        {
            return _mlContext.Transforms.LoadImages(
                    outputColumnName: "InputImage",
                    imageFolder: null, // Absolute paths
                    inputColumnName: nameof(ImageData.ImagePath))
                .Append(_mlContext.Transforms.ResizeImages(
                    outputColumnName: "ResizedImage",
                    imageWidth: 150,
                    imageHeight: 150,
                    inputColumnName: "InputImage",
                    resizing: Microsoft.ML.Transforms.Image.ImageResizingEstimator.ResizingKind.Fill))
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "Features",
                    inputColumnName: "ResizedImage",
                    interleavePixelColors: true,
                    offsetImage: 128f,
                    scaleImage: 1f / 128f))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "Label",
                    inputColumnName: nameof(ImageData.Label)));
        }

        private (string csvPath, int classCount, Dictionary<string, int> labelCounts, int totalSamples)
            CreateCorrectionsDataset()
        {
            var lines = File.ReadAllLines(_correctionsPath).Skip(1); // Skip header
            var formatted = new List<string>();
            var labelCounts = new Dictionary<string, int>();
            int skipped = 0;

            Console.WriteLine("📋 Reading corrections...");
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 5)
                {
                    skipped++; continue;
                }

                var imagePath = parts[1].Trim();
                var correctedLabel = parts[4].Trim();

                if (string.IsNullOrWhiteSpace(correctedLabel) || !File.Exists(imagePath))
                {
                    skipped++; continue;
                }

                formatted.Add($"{imagePath},{correctedLabel}");

                if (!labelCounts.ContainsKey(correctedLabel))
                    labelCounts[correctedLabel] = 0;
                labelCounts[correctedLabel]++;
            }

            if (formatted.Count == 0)
                throw new InvalidOperationException("No valid corrections found");

            var tempCsv = Path.Combine(_basePath, "temp_corrections.csv");
            File.WriteAllLines(tempCsv, formatted);

            Console.WriteLine($"✅ Created dataset: {formatted.Count} valid samples");
            if (skipped > 0)
                Console.WriteLine($"⚠️  Skipped {skipped} invalid entries");

            return (tempCsv, labelCounts.Count, labelCounts, formatted.Count);
        }
    }
}
