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
            var correctionsCsv = CreateCorrectionsDataset();

            var correctionData = _mlContext.Data.LoadFromTextFile<ImageData>(
                correctionsCsv,
                separatorChar: ',',
                hasHeader: false);
            Console.WriteLine("✅ Corrections dataset loaded");

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
                    resizing: Microsoft.ML.Transforms.Image.ImageResizingEstimator.ResizingKind.Fill))
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "Features",
                    inputColumnName: "ResizedImage",
                    interleavePixelColors: true,
                    offsetImage: 128f,
                    scaleImage: 1f / 128f))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ImageData.Label)));
            Console.WriteLine("✅ Pipeline ready");

            // Fine-tune with LightGBM on corrections
            Console.WriteLine("\n🧠 Configuring LightGBM trainer...");
            var trainer = _mlContext.MulticlassClassification.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 31,
                minimumExampleCountPerLeaf: 5,
                learningRate: 0.01f,
                numberOfIterations: 100);
            Console.WriteLine("   • Leaves: 31");
            Console.WriteLine("   • Min examples: 5");
            Console.WriteLine("   • Learning rate: 0.01");
            Console.WriteLine("   • Iterations: 100");

            var pipeline = preprocess
                .Append(trainer)
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

            Console.WriteLine("\n🚀 Training on corrected samples...");
            var startTime = DateTime.Now;
            var updatedModel = pipeline.Fit(correctionData);
            var duration = DateTime.Now - startTime;
            Console.WriteLine($"✅ Training complete in {duration.TotalSeconds:F1}s");

            // Save incremental model
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var incrementalModelPath = Path.Combine(_basePath, $"incrementalModel-{timestamp}.zip");

            Console.WriteLine($"\n💾 Saving incremental model...");
            _mlContext.Model.Save(updatedModel, correctionData.Schema, incrementalModelPath);
            Console.WriteLine($"✅ Model saved: {Path.GetFileName(incrementalModelPath)}");
            Console.WriteLine($"   Location: {incrementalModelPath}");

            Console.WriteLine("\n═══════════════════════════════════════════════");
            Console.WriteLine("✅ INCREMENTAL TRAINING COMPLETED");
            Console.WriteLine("═══════════════════════════════════════════════");

            return incrementalModelPath;
        }

        /// <summary>
        /// Creates a CSV dataset from corrections.csv with proper formatting
        /// </summary>
        private string CreateCorrectionsDataset()
        {
            var lines = File.ReadAllLines(_correctionsPath).Skip(1); // Skip header
            var formatted = new List<string>();
            int skipped = 0;

            Console.WriteLine("   📋 Reading corrections...");
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                var imagePath = parts[1].Trim('"');
                var correctedLabel = parts[4].Trim('"');

                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"   ⚠️ Skipping missing file: {Path.GetFileName(imagePath)}");
                    skipped++;
                    continue;
                }

                formatted.Add($"{imagePath},{correctedLabel}");
            }

            var tempCsv = Path.Combine(_basePath, "temp_corrections_dataset.csv");
            File.WriteAllLines(tempCsv, formatted);

            Console.WriteLine($"   ✅ Created corrections dataset:");
            Console.WriteLine($"      • Valid samples: {formatted.Count}");
            if (skipped > 0)
                Console.WriteLine($"      • Skipped (missing): {skipped}");

            return tempCsv;
        }

        /// <summary>
        /// Online learning: Update model with single correction
        /// </summary>
        public void UpdateModelWithSingleCorrection(string modelPath, string imagePath, string correctLabel)
        {
            Console.WriteLine($"🔧 Single correction update:");
            Console.WriteLine($"   • Image: {Path.GetFileName(imagePath)}");
            Console.WriteLine($"   • Corrected label: {correctLabel}");

            // Load existing model
            Console.WriteLine("   📥 Loading model...");
            var model = _mlContext.Model.Load(modelPath, out _);

            // Create single-sample dataset
            var singleData = _mlContext.Data.LoadFromEnumerable(new[]
            {
                new ImageData { ImagePath = imagePath, Label = correctLabel }
            });

            // Retrain on this single sample
            Console.WriteLine("   ⚙️ Building pipeline...");
            var preprocess = BuildPreprocessingPipeline();
            var trainer = _mlContext.MulticlassClassification.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfIterations: 50,
                learningRate: 0.005f);

            var pipeline = preprocess.Append(trainer)
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            Console.WriteLine("   🚀 Training...");
            var updatedModel = pipeline.Fit(singleData);

            // Overwrite model
            Console.WriteLine("   💾 Saving...");
            _mlContext.Model.Save(updatedModel, singleData.Schema, modelPath);
            Console.WriteLine("   ✅ Model updated successfully");
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
                    resizing: Microsoft.ML.Transforms.Image.ImageResizingEstimator.ResizingKind.Fill))
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