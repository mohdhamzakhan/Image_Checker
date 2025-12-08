using Image_Checker.DataModels;
using Microsoft.ML;
using Microsoft.ML.Transforms.Image;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Image_Checker.Services
{
    /// <summary>
    /// OPTIMIZED True Incremental Learning - In-Place Model Updates
    /// Updates existing model file instead of creating new ones
    /// </summary>
    public class TrueIncrementalTrainer
    {
        private readonly MLContext _mlContext;
        private readonly string _basePath;
        private readonly string _correctionsPath;
        private readonly string _originalCsvPath;

        public TrueIncrementalTrainer(MLContext mlContext, string basePath)
        {
            _mlContext = mlContext;
            _basePath = basePath;
            _correctionsPath = Path.Combine(basePath, "corrections.csv");
            _originalCsvPath = Path.Combine(basePath, "images.csv");
        }

        /// <summary>
        /// SYNCHRONOUS In-Place Update: Updates existing model file directly
        /// </summary>
        public void IncrementalUpdateInPlace(string modelPath)
        {
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("🔄 IN-PLACE INCREMENTAL UPDATE");
            Console.WriteLine("   Strategy: Update existing model file");
            Console.WriteLine("═══════════════════════════════════════════════");

            // Validate inputs
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model not found: {modelPath}");
            }

            if (!File.Exists(_correctionsPath))
            {
                throw new FileNotFoundException("No corrections file found.");
            }

            Console.WriteLine($"📂 Model: {Path.GetFileName(modelPath)}");
            Console.WriteLine($"📂 Corrections: {Path.GetFileName(_correctionsPath)}");

            // Create backup before updating
            var backupPath = CreateBackup(modelPath);
            Console.WriteLine($"💾 Backup created: {Path.GetFileName(backupPath)}");

            // Step 1: Load corrections
            Console.WriteLine("\n📝 Step 1: Processing corrections...");
            var (correctionsList, correctionCount, labelCounts) = LoadCorrections();

            if (correctionCount == 0)
            {
                throw new InvalidOperationException("No valid corrections found.");
            }

            Console.WriteLine($"   ✅ Loaded {correctionCount} corrections");
            Console.WriteLine($"   📊 Label distribution:");
            foreach (var kv in labelCounts)
            {
                Console.WriteLine($"      • {kv.Key}: {kv.Value} samples");
            }

            // Step 2: Decide on strategy
            Console.WriteLine("\n🔄 Step 2: Selecting training strategy...");

            bool hasOriginalData = File.Exists(_originalCsvPath);
            int originalCount = 0;

            if (hasOriginalData)
            {
                originalCount = File.ReadLines(_originalCsvPath).Count();
                Console.WriteLine($"   • Original dataset: {originalCount:N0} samples");
            }

            string combinedCsvPath;
            int totalSamples;

            if (!hasOriginalData)
            {
                Console.WriteLine("   ⚠️ Original data not found - using corrections only");
                Console.WriteLine("   WARNING: This may cause catastrophic forgetting!");
                combinedCsvPath = CreateCorrectionsOnlyDataset(correctionsList, out totalSamples);
            }
            else if (originalCount > 10000)
            {
                Console.WriteLine($"   ✅ Large dataset detected ({originalCount:N0} samples)");
                Console.WriteLine("   📊 Strategy: Smart sampling for speed");
                combinedCsvPath = CreateSampledDataset(correctionsList, originalCount, out totalSamples);
            }
            else if (originalCount > 1000)
            {
                Console.WriteLine($"   ✅ Medium dataset detected ({originalCount:N0} samples)");
                Console.WriteLine("   📊 Strategy: Balanced sampling");
                combinedCsvPath = CreateBalancedDataset(correctionsList, originalCount, out totalSamples);
            }
            else
            {
                Console.WriteLine($"   ✅ Small dataset detected ({originalCount:N0} samples)");
                Console.WriteLine("   📊 Strategy: Full dataset");
                combinedCsvPath = CreateCombinedDataset(correctionsList, out totalSamples);
            }

            // Step 3: Load data
            Console.WriteLine("\n📥 Step 3: Loading training data...");
            var combinedData = _mlContext.Data.LoadFromTextFile<ImageData>(
                combinedCsvPath,
                separatorChar: ',',
                hasHeader: false);
            Console.WriteLine($"   ✅ Loaded {totalSamples:N0} samples for training");

            // Step 4: Build pipeline
            Console.WriteLine("\n⚙️ Step 4: Building training pipeline...");
            var pipeline = BuildTrainingPipeline(correctionCount, totalSamples);
            Console.WriteLine("   ✅ Pipeline ready");

            // Step 5: Train
            Console.WriteLine("\n🚀 Step 5: Training model...");
            Console.WriteLine($"   • Training samples: {totalSamples:N0}");
            Console.WriteLine($"   • Including corrections: {correctionCount}");
            Console.WriteLine($"   • Estimated time: {EstimateTrainingTime(totalSamples)}");

            var startTime = DateTime.Now;
            ITransformer updatedModel;

            try
            {
                updatedModel = pipeline.Fit(combinedData);
                var duration = DateTime.Now - startTime;
                Console.WriteLine($"   ✅ Training complete in {duration.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Training failed: {ex.Message}");
                Console.WriteLine($"   Restoring backup...");
                RestoreBackup(backupPath, modelPath);
                throw;
            }

            // Step 6: Evaluate
            Console.WriteLine("\n📊 Step 6: Evaluating updated model...");
            EvaluateModel(updatedModel, combinedData);

            // Step 7: Save in-place (replace original file)
            Console.WriteLine($"\n💾 Step 7: Updating model file in-place...");

            try
            {
                // Delete old model file
                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                }

                // Save updated model with same filename
                _mlContext.Model.Save(updatedModel, combinedData.Schema, modelPath);
                Console.WriteLine($"   ✅ Model updated: {Path.GetFileName(modelPath)}");

                // Delete backup after successful update
                Console.WriteLine($"   🗑️ Cleaning up backup...");
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Failed to save model: {ex.Message}");
                Console.WriteLine($"   Restoring backup...");
                RestoreBackup(backupPath, modelPath);
                throw;
            }

            // Cleanup temp files
            CleanupTempFiles(combinedCsvPath);

            // Summary
            Console.WriteLine("\n═══════════════════════════════════════════════");
            Console.WriteLine("✅ IN-PLACE UPDATE COMPLETED");
            Console.WriteLine($"   • Training samples: {totalSamples:N0}");
            Console.WriteLine($"   • Corrections applied: {correctionCount}");
            Console.WriteLine($"   • Training time: {(DateTime.Now - startTime).TotalSeconds:F1}s");
            Console.WriteLine($"   • Model: {Path.GetFileName(modelPath)} (updated)");
            Console.WriteLine("═══════════════════════════════════════════════");
        }

        /// <summary>
        /// Creates a timestamped backup of the model
        /// </summary>
        private string CreateBackup(string modelPath)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = Path.Combine(
                Path.GetDirectoryName(modelPath),
                $"backup_{Path.GetFileNameWithoutExtension(modelPath)}_{timestamp}.zip");

            File.Copy(modelPath, backupPath, true);
            return backupPath;
        }

        /// <summary>
        /// Restores model from backup
        /// </summary>
        private void RestoreBackup(string backupPath, string modelPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, modelPath, true);
                    Console.WriteLine($"   ✅ Backup restored successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Failed to restore backup: {ex.Message}");
            }
        }

        private string CreateSampledDataset(List<(string path, string label)> corrections,
            int originalCount, out int totalSamples)
        {
            int sampleSize = Math.Min(3000, Math.Max(500, originalCount / 10));

            Console.WriteLine($"   • Sampling strategy:");
            Console.WriteLine($"     - Original: {originalCount:N0} → Sampling {sampleSize:N0} ({(double)sampleSize / originalCount:P1})");
            Console.WriteLine($"     - Corrections: {corrections.Count} (100% included)");

            var originalLines = File.ReadAllLines(_originalCsvPath).ToList();

            var byLabel = originalLines.GroupBy(line =>
            {
                var parts = line.Split(',');
                return parts.Length >= 2 ? parts[1].Trim('"') : "";
            }).ToDictionary(g => g.Key, g => g.ToList());

            var sampledLines = new List<string>();
            var random = new Random(42);

            foreach (var labelGroup in byLabel)
            {
                int labelSample = (int)(sampleSize * ((double)labelGroup.Value.Count / originalCount));
                var sampled = labelGroup.Value.OrderBy(x => random.Next()).Take(labelSample);
                sampledLines.AddRange(sampled);
                Console.WriteLine($"     - {labelGroup.Key}: {labelSample:N0} samples");
            }

            var correctionDict = corrections.ToDictionary(
                c => c.path,
                c => c.label,
                StringComparer.OrdinalIgnoreCase);

            var finalLines = new List<string>();
            int replaced = 0;

            foreach (var line in sampledLines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var imagePath = parts[0].Trim('"');

                if (correctionDict.TryGetValue(imagePath, out var correctedLabel))
                {
                    finalLines.Add($"{imagePath},{correctedLabel}");
                    correctionDict.Remove(imagePath);
                    replaced++;
                }
                else
                {
                    finalLines.Add(line);
                }
            }

            foreach (var kvp in correctionDict)
            {
                finalLines.Add($"{kvp.Key},{kvp.Value}");
            }

            totalSamples = finalLines.Count;
            Console.WriteLine($"   • Final dataset: {totalSamples:N0} samples ({replaced} replacements)");

            var tempCsv = Path.Combine(_basePath, "temp_sampled_dataset.csv");
            File.WriteAllLines(tempCsv, finalLines);

            return tempCsv;
        }

        private string CreateBalancedDataset(List<(string path, string label)> corrections,
            int originalCount, out int totalSamples)
        {
            int sampleSize = Math.Min(5000, Math.Max(500, originalCount / 2));

            Console.WriteLine($"   • Balanced sampling: {sampleSize:N0} of {originalCount:N0} samples");

            var originalLines = File.ReadAllLines(_originalCsvPath).ToList();
            var random = new Random(42);

            var sampledLines = originalLines.OrderBy(x => random.Next()).Take(sampleSize).ToList();

            var correctionDict = corrections.ToDictionary(
                c => c.path,
                c => c.label,
                StringComparer.OrdinalIgnoreCase);

            var finalLines = new List<string>();
            int replaced = 0;

            foreach (var line in sampledLines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var imagePath = parts[0].Trim('"');

                if (correctionDict.TryGetValue(imagePath, out var correctedLabel))
                {
                    finalLines.Add($"{imagePath},{correctedLabel}");
                    correctionDict.Remove(imagePath);
                    replaced++;
                }
                else
                {
                    finalLines.Add(line);
                }
            }

            foreach (var kvp in correctionDict)
            {
                finalLines.Add($"{kvp.Key},{kvp.Value}");
            }

            totalSamples = finalLines.Count;
            Console.WriteLine($"   • Final dataset: {totalSamples:N0} samples");

            var tempCsv = Path.Combine(_basePath, "temp_balanced_dataset.csv");
            File.WriteAllLines(tempCsv, finalLines);

            return tempCsv;
        }

        private string CreateCombinedDataset(List<(string path, string label)> corrections,
            out int totalSamples)
        {
            var originalLines = File.ReadAllLines(_originalCsvPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var correctionDict = corrections.ToDictionary(
                c => c.path,
                c => c.label,
                StringComparer.OrdinalIgnoreCase);

            var combinedLines = new List<string>();
            int replaced = 0;

            foreach (var line in originalLines)
            {
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var imagePath = parts[0].Trim('"');

                if (correctionDict.TryGetValue(imagePath, out var correctedLabel))
                {
                    combinedLines.Add($"{imagePath},{correctedLabel}");
                    correctionDict.Remove(imagePath);
                    replaced++;
                }
                else
                {
                    combinedLines.Add(line);
                }
            }

            foreach (var kvp in correctionDict)
            {
                combinedLines.Add($"{kvp.Key},{kvp.Value}");
            }

            totalSamples = combinedLines.Count;
            Console.WriteLine($"   • Using all {totalSamples:N0} samples");

            var tempCsv = Path.Combine(_basePath, "temp_combined_dataset.csv");
            File.WriteAllLines(tempCsv, combinedLines);

            return tempCsv;
        }

        private string CreateCorrectionsOnlyDataset(List<(string path, string label)> corrections,
            out int totalSamples)
        {
            Console.WriteLine("\n⚠️ CORRECTIONS-ONLY MODE");
            Console.WriteLine($"   Training on {corrections.Count} corrections without original data");
            Console.WriteLine("   WARNING: Model will forget original patterns!");

            var tempCsv = Path.Combine(_basePath, "temp_corrections_only.csv");
            File.WriteAllLines(tempCsv, corrections.Select(c => $"{c.path},{c.label}"));

            totalSamples = corrections.Count;
            return tempCsv;
        }

        private (List<(string path, string label)> corrections, int count, Dictionary<string, int> labelCounts) LoadCorrections()
        {
            var corrections = new List<(string path, string label)>();
            var labelCounts = new Dictionary<string, int>();
            int skipped = 0;

            var lines = File.ReadAllLines(_correctionsPath).Skip(1);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 5)
                {
                    skipped++;
                    continue;
                }

                var imagePath = parts[1].Trim('"');
                var correctedLabel = parts[4].Trim('"');

                if (string.IsNullOrWhiteSpace(correctedLabel) || !File.Exists(imagePath))
                {
                    skipped++;
                    continue;
                }

                corrections.Add((imagePath, correctedLabel));

                if (!labelCounts.ContainsKey(correctedLabel))
                    labelCounts[correctedLabel] = 0;
                labelCounts[correctedLabel]++;
            }

            if (skipped > 0)
                Console.WriteLine($"   ⚠️ Skipped {skipped} invalid corrections");

            return (corrections, corrections.Count, labelCounts);
        }

        private IEstimator<ITransformer> BuildTrainingPipeline(int correctionCount, int totalSamples)
        {
            bool isGray = IsGrayscaleDataset();
            Console.WriteLine($"   • Image format: {(isGray ? "Grayscale" : "RGB")}");
            var preprocess = _mlContext.Transforms.LoadImages(
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
                    colorsToExtract: ImagePixelExtractingEstimator.ColorBits.All,
                    orderOfExtraction: ImagePixelExtractingEstimator.ColorsOrder.ARGB,
                    interleavePixelColors: !isGray,  // ← CHANGED
                    offsetImage: isGray ? 0f : 128f,  // ← CHANGED
                    scaleImage: isGray ? 1f / 255f : 1f / 128f))  // ← CHANGED
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ImageData.Label)));

            IEstimator<ITransformer> trainer;
            string trainerName;

            if (totalSamples < 500)
            {
                trainerName = "SDCA MaximumEntropy";
                Console.WriteLine($"   • Trainer: {trainerName} (fast, small dataset)");
                trainer = _mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    maximumNumberOfIterations: 100);
            }
            else if (totalSamples < 2000)
            {
                trainerName = "L-BFGS MaximumEntropy";
                Console.WriteLine($"   • Trainer: {trainerName} (balanced)");
                trainer = _mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    l1Regularization: 0.03f,
                    l2Regularization: 0.03f);
            }
            else
            {
                trainerName = "LightGBM";
                Console.WriteLine($"   • Trainer: {trainerName} (optimized for speed)");
                trainer = _mlContext.MulticlassClassification.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfLeaves: 31,
                    learningRate: 0.05f,
                    numberOfIterations: 100);
            }

            return preprocess
                .Append(trainer)
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));
        }

        private void EvaluateModel(ITransformer model, IDataView data)
        {
            try
            {
                var predictions = model.Transform(data);
                var metrics = _mlContext.MulticlassClassification.Evaluate(predictions, "Label");

                Console.WriteLine($"   • Micro Accuracy: {metrics.MicroAccuracy:P2}");
                Console.WriteLine($"   • Macro Accuracy: {metrics.MacroAccuracy:P2}");
                Console.WriteLine($"   • Log Loss: {metrics.LogLoss:F4}");

                if (metrics.MicroAccuracy >= 0.85)
                    Console.WriteLine($"   ✅ Excellent performance!");
                else if (metrics.MicroAccuracy >= 0.75)
                    Console.WriteLine($"   ✓ Good performance");
                else
                    Console.WriteLine($"   ⚠️ Consider full retrain for better accuracy");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Evaluation failed: {ex.Message}");
            }
        }

        private string EstimateTrainingTime(int samples)
        {
            if (samples < 500) return "10-20 seconds";
            if (samples < 1000) return "20-40 seconds";
            if (samples < 2000) return "40-90 seconds";
            if (samples < 5000) return "1.5-3 minutes";
            return "2-4 minutes";
        }

        /// <summary>
        /// Detects if images are grayscale or RGB by sampling
        /// </summary>
        private bool IsGrayscaleDataset()
        {
            Console.WriteLine("🔍 Detecting image color format...");

            try
            {
                // Sample from corrections file
                var samplePath = File.ReadLines(_correctionsPath)
                    .Skip(1)
                    .Take(5)
                    .Select(line => line.Split(',')[1].Trim('"'))
                    .FirstOrDefault(path => File.Exists(path));

                if (samplePath == null && File.Exists(_originalCsvPath))
                {
                    // Sample from original dataset
                    samplePath = File.ReadLines(_originalCsvPath)
                        .Take(5)
                        .Select(line => line.Split(',')[0].Trim('"'))
                        .FirstOrDefault(path => File.Exists(path));
                }

                if (samplePath != null)
                {
                    using var bmp = System.Drawing.Image.FromFile(samplePath);
                    bool isGray = bmp.PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed;
                    Console.WriteLine($"   Format: {(isGray ? "Grayscale" : "RGB")}");
                    return isGray;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Detection failed: {ex.Message}, assuming RGB");
            }

            return false; // Default to RGB
        }

        private void CleanupTempFiles(params string[] files)
        {
            foreach (var file in files)
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch { }
            }
        }
    }
}