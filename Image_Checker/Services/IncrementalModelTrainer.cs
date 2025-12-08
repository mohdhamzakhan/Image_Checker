using Image_Checker.DataModels;
using Image_Checker.Utils;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Image_Checker.Services
{
    public class IncrementalModelTrainer
    {
        private readonly MLContext _mlContext;
        private readonly string _basePath;
        private readonly string _correctionsPath;

        public IncrementalModelTrainer(MLContext mlContext, string basePath)
        {
            _mlContext = mlContext;
            _basePath = basePath;
            _correctionsPath = Path.Combine(basePath, "corrections.csv");
        }

        /// <summary>
        /// Performs incremental training using only corrected images
        /// </summary>
        public string IncrementalTrain(string existingModelPath)
        {
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("⚡ INCREMENTAL TRAINING");
            Console.WriteLine("═══════════════════════════════════════════════");

            if (!File.Exists(existingModelPath))
            {
                Console.WriteLine("❌ Base model not found.");
                throw new FileNotFoundException("Base model not found.");
            }

            if (!File.Exists(_correctionsPath))
            {
                Console.WriteLine("❌ No corrections file found.");
                throw new FileNotFoundException("No corrections file found.");
            }

            Console.WriteLine($"📂 Base model: {Path.GetFileName(existingModelPath)}");
            Console.WriteLine($"📝 Corrections file: {Path.GetFileName(_correctionsPath)}");

            // Load existing model
            Console.WriteLine("\n📥 Loading existing model...");
            var existingModel = _mlContext.Model.Load(existingModelPath, out var modelSchema);
            Console.WriteLine("✅ Base model loaded");

            // Create mini-dataset from corrections
            Console.WriteLine("\n🔧 Processing corrections...");
            var (correctionsCsv, classCount, labelCounts, totalSamples) = CreateCorrectionsDataset();

            // Validate we have enough data for training
            if (classCount < 2)
            {
                Console.WriteLine($"\n❌ ERROR: Found only {classCount} class(es) in corrections.");
                Console.WriteLine("   Incremental training requires corrections for at least 2 classes (OK and NG).");
                Console.WriteLine("\n   Current label distribution:");
                foreach (var kv in labelCounts)
                {
                    Console.WriteLine($"      {kv.Key}: {kv.Value} samples");
                }
                Console.WriteLine("\n   💡 Solution: Add corrections for both OK and NG labels before training.");
                throw new InvalidOperationException(
                    $"Insufficient classes in corrections. Found {classCount} class(es), need at least 2. " +
                    "Please add corrections for both OK and NG labels before training.");
            }

            Console.WriteLine($"\n✅ Validation passed:");
            Console.WriteLine($"   • Total corrections: {totalSamples}");
            Console.WriteLine($"   • Number of classes: {classCount}");
            Console.WriteLine($"   • Label distribution:");
            foreach (var kv in labelCounts)
            {
                Console.WriteLine($"      - {kv.Key}: {kv.Value} samples");
            }

            var correctionData = _mlContext.Data.LoadFromTextFile<ImageData>(
                correctionsCsv,
                separatorChar: ',',
                hasHeader: false);
            Console.WriteLine("\n✅ Corrections dataset loaded");

            // Build preprocessing pipeline (must match original)
            Console.WriteLine("\n⚙️ Building preprocessing pipeline...");
            var preprocess = _mlContext.Transforms.LoadImages(
                    outputColumnName: "InputImage",
                    imageFolder: null, // Use absolute paths
                    inputColumnName: nameof(ImageData.ImagePath))
                .Append(_mlContext.Transforms.ResizeImages(
                    outputColumnName: "ResizedImage",
                    imageWidth: 150,
                    imageHeight: 150,
                    inputColumnName: "InputImage",
                    resizing: Microsoft.ML.Transforms.Image.ImageResizingEstimator.ResizingKind.IsoCrop))
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "Features",
                    inputColumnName: "ResizedImage",
                    interleavePixelColors: true,
                    offsetImage: 128f,
                    scaleImage: 1f / 128f))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ImageData.Label)));
            Console.WriteLine("✅ Pipeline ready");

            // Choose appropriate trainer based on sample count and class distribution
            Console.WriteLine("\n🧠 Selecting trainer based on data characteristics...");
            var minClassSamples = labelCounts.Values.Min();

            IEstimator<ITransformer> trainer;
            string trainerName;

            // Use simpler trainers for small datasets or imbalanced data
            if (totalSamples < 20 || minClassSamples < 3)
            {
                trainerName = "SDCA MaximumEntropy";
                Console.WriteLine($"   Selected: {trainerName}");
                Console.WriteLine($"   Reason: Small dataset (total={totalSamples}, min class={minClassSamples})");
                Console.WriteLine("   Configuration:");
                Console.WriteLine("      • Max iterations: 100");
                Console.WriteLine("      • Suitable for small, sparse corrections");

                trainer = _mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    maximumNumberOfIterations: 100);
            }
            else if (totalSamples < 50)
            {
                trainerName = "L-BFGS MaximumEntropy";
                Console.WriteLine($"   Selected: {trainerName}");
                Console.WriteLine($"   Reason: Medium dataset (total={totalSamples})");
                Console.WriteLine("   Configuration:");
                Console.WriteLine("      • L1 Regularization: 0.1");
                Console.WriteLine("      • L2 Regularization: 0.1");
                Console.WriteLine("      • Balanced for incremental updates");

                trainer = _mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    l1Regularization: 0.1f,
                    l2Regularization: 0.1f);
            }
            else
            {
                trainerName = "LightGBM";
                Console.WriteLine($"   Selected: {trainerName}");
                Console.WriteLine($"   Reason: Larger dataset (total={totalSamples})");
                Console.WriteLine("   Configuration:");
                Console.WriteLine("      • Leaves: 20 (conservative)");
                Console.WriteLine("      • Min examples per leaf: 3");
                Console.WriteLine("      • Learning rate: 0.01 (fine-tuning)");
                Console.WriteLine("      • Iterations: 100");

                trainer = _mlContext.MulticlassClassification.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfLeaves: 20,
                    minimumExampleCountPerLeaf: 3,
                    learningRate: 0.01f,
                    numberOfIterations: 100);
            }

            var pipeline = preprocess
                .Append(trainer)
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

            Console.WriteLine($"\n🚀 Training with {trainerName} on {totalSamples} corrected samples...");
            var startTime = DateTime.Now;

            ITransformer updatedModel;
            try
            {
                updatedModel = pipeline.Fit(correctionData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Training failed: {ex.Message}");
                Console.WriteLine("\n💡 Troubleshooting:");
                Console.WriteLine("   1. Ensure you have corrections for BOTH OK and NG labels");
                Console.WriteLine("   2. Verify all image files exist and are accessible");
                Console.WriteLine("   3. Check that images are not corrupted");
                Console.WriteLine("   4. Try accumulating more corrections before training");
                Console.WriteLine($"\n   Current state: {classCount} classes, {totalSamples} samples");
                throw;
            }

            var duration = DateTime.Now - startTime;
            Console.WriteLine($"✅ Training complete in {duration.TotalSeconds:F1}s");

            // Evaluate on corrections (just for feedback)
            Console.WriteLine("\n📊 Evaluating on correction samples...");
            try
            {
                var predictions = updatedModel.Transform(correctionData);
                var metrics = _mlContext.MulticlassClassification.Evaluate(predictions, "Label");
                Console.WriteLine($"   • Accuracy on corrections: {metrics.MicroAccuracy:P2}");
                Console.WriteLine($"   • Macro Accuracy: {metrics.MacroAccuracy:P2}");
                Console.WriteLine($"   • Log Loss: {metrics.LogLoss:F4}");

                if (metrics.MicroAccuracy < 0.7)
                {
                    Console.WriteLine("\n   ⚠️ Warning: Low accuracy on corrections!");
                    Console.WriteLine("   The model may benefit from:");
                    Console.WriteLine("      • More diverse corrections");
                    Console.WriteLine("      • A full retrain on the entire dataset");
                }
                else
                {
                    Console.WriteLine("\n   ✅ Good accuracy on corrections!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Could not evaluate: {ex.Message}");
            }

            // Save incremental model
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var incrementalModelPath = Path.Combine(_basePath, $"incrementalModel-{timestamp}.zip");

            Console.WriteLine($"\n💾 Saving incremental model...");
            _mlContext.Model.Save(updatedModel, correctionData.Schema, incrementalModelPath);
            Console.WriteLine($"✅ Model saved: {Path.GetFileName(incrementalModelPath)}");
            Console.WriteLine($"   Location: {incrementalModelPath}");

            // Clean up temp file
            try
            {
                if (File.Exists(correctionsCsv))
                    File.Delete(correctionsCsv);
            }
            catch { /* Ignore cleanup errors */ }

            Console.WriteLine("\n═══════════════════════════════════════════════");
            Console.WriteLine("✅ INCREMENTAL TRAINING COMPLETED");
            Console.WriteLine($"   • Trainer used: {trainerName}");
            Console.WriteLine($"   • Samples processed: {totalSamples}");
            Console.WriteLine($"   • Classes: {string.Join(", ", labelCounts.Keys)}");
            Console.WriteLine("═══════════════════════════════════════════════");

            return incrementalModelPath;
        }

        /// <summary>
        /// Creates a CSV dataset from corrections.csv with proper formatting
        /// Returns: (csvPath, classCount, labelCounts, totalSamples)
        /// </summary>
        private (string csvPath, int classCount, Dictionary<string, int> labelCounts, int totalSamples) CreateCorrectionsDataset()
        {
            var lines = File.ReadAllLines(_correctionsPath).Skip(1); // Skip header
            var formatted = new List<string>();
            var labelCounts = new Dictionary<string, int>();
            int skipped = 0;

            Console.WriteLine("   📋 Reading corrections...");
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 5)
                {
                    Console.WriteLine($"   ⚠️ Skipping malformed line (expected 5 columns, got {parts.Length})");
                    skipped++;
                    continue;
                }

                var imagePath = parts[1].Trim('"');
                var correctedLabel = parts[4].Trim('"');

                if (string.IsNullOrWhiteSpace(correctedLabel))
                {
                    Console.WriteLine($"   ⚠️ Skipping entry with empty label: {Path.GetFileName(imagePath)}");
                    skipped++;
                    continue;
                }

                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"   ⚠️ Skipping missing file: {Path.GetFileName(imagePath)}");
                    skipped++;
                    continue;
                }

                formatted.Add($"{imagePath},{correctedLabel}");

                // Count labels
                if (!labelCounts.ContainsKey(correctedLabel))
                    labelCounts[correctedLabel] = 0;
                labelCounts[correctedLabel]++;
            }

            if (formatted.Count == 0)
            {
                throw new InvalidOperationException(
                    "No valid corrections found. Please ensure:\n" +
                    "   1. Corrections file has valid entries\n" +
                    "   2. Image files exist at specified paths\n" +
                    "   3. Corrected labels are not empty");
            }

            var tempCsv = Path.Combine(_basePath, "temp_corrections_dataset.csv");
            File.WriteAllLines(tempCsv, formatted);

            Console.WriteLine($"   ✅ Created corrections dataset:");
            Console.WriteLine($"      • Valid samples: {formatted.Count}");
            if (skipped > 0)
                Console.WriteLine($"      • Skipped (missing/invalid): {skipped}");

            return (tempCsv, labelCounts.Count, labelCounts, formatted.Count);
        }

        /// <summary>
        /// Online learning: Update model with single correction
        /// </summary>
        public void UpdateModelWithSingleCorrection(string modelPath, string imagePath, string correctLabel)
        {
            Console.WriteLine($"🔧 Single correction update:");
            Console.WriteLine($"   • Image: {Path.GetFileName(imagePath)}");
            Console.WriteLine($"   • Corrected label: {correctLabel}");

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image not found: {imagePath}");
            }

            // Load existing model
            Console.WriteLine("   📥 Loading model...");
            var model = _mlContext.Model.Load(modelPath, out _);

            // Create single-sample dataset
            var singleData = _mlContext.Data.LoadFromEnumerable(new[]
            {
                new ImageData { ImagePath = imagePath, Label = correctLabel }
            });

            // Retrain on this single sample - use SDCA for stability
            Console.WriteLine("   ⚙️ Building pipeline...");
            var preprocess = BuildPreprocessingPipeline();

            // Use SDCA for single corrections (most stable for online learning)
            Console.WriteLine("   ℹ️ Using SDCA MaxEnt for single correction (most stable)");
            var trainer = _mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                labelColumnName: "Label",
                featureColumnName: "Features",
                maximumNumberOfIterations: 50);

            var pipeline = preprocess.Append(trainer)
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            Console.WriteLine("   🚀 Training...");
            try
            {
                var updatedModel = pipeline.Fit(singleData);

                // Overwrite model
                Console.WriteLine("   💾 Saving...");
                _mlContext.Model.Save(updatedModel, singleData.Schema, modelPath);
                Console.WriteLine("   ✅ Model updated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Update failed: {ex.Message}");
                Console.WriteLine("   💡 Tip: Single corrections work best when accumulated and applied in batches.");
                throw;
            }
        }

        private IEstimator<ITransformer> BuildPreprocessingPipeline()
        {
            return _mlContext.Transforms.LoadImages(
                    outputColumnName: "InputImage",
                    imageFolder: null,
                    inputColumnName: nameof(ImageData.ImagePath))
                .Append(_mlContext.Transforms.ResizeImages(
                    outputColumnName: "ResizedImage",
                    imageWidth: 150,
                    imageHeight: 150,
                    inputColumnName: "InputImage",
                    resizing: Microsoft.ML.Transforms.Image.ImageResizingEstimator.ResizingKind.IsoCrop))
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "Features",
                    inputColumnName: "ResizedImage",
                    interleavePixelColors: true,
                    offsetImage: 128f,
                    scaleImage: 1f / 128f))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ImageData.Label)));
        }
    }
}