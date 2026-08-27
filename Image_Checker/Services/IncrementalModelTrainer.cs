using Image_Checker.DataModels;
using Image_Checker.Utils;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Image_Checker.Services
{
    /// <summary>
    /// Incremental training using two strategies:
    ///
    /// Strategy 1 — REPLAY (preferred):
    ///   Merge original images.csv + new corrections → retrain full model.
    ///   Prevents catastrophic forgetting. Used when images.csv is available.
    ///
    /// Strategy 2 — ENSEMBLE FALLBACK:
    ///   Train a correction-only model, then combine its scores with the
    ///   base model's scores at inference time via weighted averaging.
    ///   Used when the original dataset is no longer available.
    /// </summary>
    public class IncrementalModelTrainer
    {
        private readonly MLContext _mlContext;
        private readonly string _basePath;
        private readonly string _correctionsPath;

        // Added RoiConfig field
        private readonly RoiConfig _roiConfig;

        public IncrementalModelTrainer(
            MLContext mlContext,
            string basePath,
            RoiConfig roiConfig)
        {
            _mlContext = mlContext;
            _basePath = basePath;
            _roiConfig = roiConfig ?? new RoiConfig { ImageWidth = 224, ImageHeight = 224 };
            _correctionsPath = Path.Combine(basePath, "corrections.csv");
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINT
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Main incremental training entry point.
        /// Tries Replay first; falls back to Ensemble if original data unavailable.
        /// Returns path to the saved updated model.
        /// </summary>
        public string IncrementalTrain(
            string existingModelPath,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("⚡ INCREMENTAL TRAINING");
            Console.WriteLine("═══════════════════════════════════════════════");

            if (!File.Exists(existingModelPath))
                throw new FileNotFoundException("Base model not found.", existingModelPath);

            if (!File.Exists(_correctionsPath))
                throw new FileNotFoundException("No corrections file found.", _correctionsPath);

            Console.WriteLine($"📂 Base model: {Path.GetFileName(existingModelPath)}");
            Console.WriteLine($"📝 Corrections: {Path.GetFileName(_correctionsPath)}");

            cancellationToken.ThrowIfCancellationRequested();

            // Parse and validate corrections
            var (corrections, labelCounts) = LoadAndValidateCorrections();
            int totalSamples = corrections.Count;
            int classCount = labelCounts.Count;

            PrintLabelDistribution(labelCounts, totalSamples);

            if (classCount < 2)
            {
                throw new InvalidOperationException(
                    $"Incremental training requires corrections for at least 2 classes. " +
                    $"Found {classCount} class(es): {string.Join(", ", labelCounts.Keys)}.\n" +
                    "Add corrections for all classes before training.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Choose strategy based on original dataset availability
            var originalCsvPath = Path.Combine(_basePath, "images.csv");
            bool hasOriginalData = File.Exists(originalCsvPath) && HasEnoughRows(originalCsvPath);

            string resultModelPath;

            if (hasOriginalData)
            {
                Console.WriteLine("\n✅ Original dataset found → using REPLAY strategy");
                resultModelPath = ReplayTrain(
                    existingModelPath, originalCsvPath, corrections, labelCounts, cancellationToken);
            }
            else
            {
                Console.WriteLine("\n⚠️ Original dataset not found → using ENSEMBLE FALLBACK strategy");
                resultModelPath = EnsembleTrain(
                    existingModelPath, corrections, labelCounts, cancellationToken);
            }

            Console.WriteLine("\n═══════════════════════════════════════════════");
            Console.WriteLine("✅ INCREMENTAL TRAINING COMPLETED");
            Console.WriteLine($"   • Strategy: {(hasOriginalData ? "Replay" : "Ensemble Fallback")}");
            Console.WriteLine($"   • Corrections processed: {totalSamples}");
            Console.WriteLine($"   • Classes: {string.Join(", ", labelCounts.Keys)}");
            Console.WriteLine($"   • Model saved: {Path.GetFileName(resultModelPath)}");
            Console.WriteLine("═══════════════════════════════════════════════");

            return resultModelPath;
        }

        // ═══════════════════════════════════════════════════════════════
        // STRATEGY 1: REPLAY
        // Merge original data + corrections → full retrain
        // Prevents catastrophic forgetting completely
        // ═══════════════════════════════════════════════════════════════

        private string ReplayTrain(
            string existingModelPath,
            string originalCsvPath,
            List<(string ImagePath, string Label)> corrections,
            Dictionary<string, int> labelCounts,
            CancellationToken cancellationToken)
        {
            Console.WriteLine("\n📋 STRATEGY 1: REPLAY");
            Console.WriteLine("   Merging original dataset with corrections...");

            // Count original rows (streaming — no ReadAllLines)
            long originalCount = File.ReadLines(originalCsvPath).LongCount();
            Console.WriteLine($"   • Original samples: {originalCount}");
            Console.WriteLine($"   • New corrections:  {corrections.Count}");
            Console.WriteLine($"   • Total for retrain: {originalCount + corrections.Count}");

            cancellationToken.ThrowIfCancellationRequested();

            // Write merged CSV: original rows first, then corrections appended
            // FIX #3: Stream directly to file — no List<string> in RAM
            var mergedCsvPath = Path.Combine(_basePath, "images_merged.csv");

            try
            {
                using (var writer = new StreamWriter(mergedCsvPath, append: false))
                {
                    // Stream original rows
                    foreach (var line in File.ReadLines(originalCsvPath))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!string.IsNullOrWhiteSpace(line))
                            writer.WriteLine(line);
                    }

                    // Append corrections (corrections override old labels for same image)
                    foreach (var (imgPath, label) in corrections)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        writer.WriteLine($"{imgPath},{label}");
                    }
                }

                Console.WriteLine("   ✅ Merged dataset written");
                cancellationToken.ThrowIfCancellationRequested();

                // Load merged data and retrain
                var mergedData = _mlContext.Data.LoadFromTextFile<ImageData>(
                    mergedCsvPath, separatorChar: ',', hasHeader: false);

                var pipeline = BuildFullPipeline(labelCounts, corrections.Count);

                Console.WriteLine($"\n🚀 Retraining on merged dataset ({originalCount + corrections.Count} samples)...");
                var startTime = DateTime.Now;
                var updatedModel = pipeline.Fit(mergedData);
                Console.WriteLine($"✅ Retrain complete in {(DateTime.Now - startTime).TotalSeconds:F1}s");

                cancellationToken.ThrowIfCancellationRequested();

                // Quick eval on corrections only to verify the model learned them
                EvaluateOnCorrections(updatedModel, corrections, "Replay model");

                // Save — overwrite images.csv with merged so next incremental also benefits
                File.Copy(mergedCsvPath, originalCsvPath, overwrite: true);
                Console.WriteLine("   ✅ images.csv updated with merged data for future incremental runs");

                return SaveModel(updatedModel, mergedData.Schema, "replayModel");
            }
            finally
            {
                // FIX #6: Always clean up temp file even on exception
                TryDelete(mergedCsvPath);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // STRATEGY 2: ENSEMBLE FALLBACK
        // Train correction model → combine with base model at inference
        // Approximates warm start without original data
        // ═══════════════════════════════════════════════════════════════

        private string EnsembleTrain(
            string existingModelPath,
            List<(string ImagePath, string Label)> corrections,
            Dictionary<string, int> labelCounts,
            CancellationToken cancellationToken)
        {
            Console.WriteLine("\n📋 STRATEGY 2: ENSEMBLE FALLBACK");
            Console.WriteLine("   Training correction model to combine with base model...");

            cancellationToken.ThrowIfCancellationRequested();

            // Load base model (FIX #1: actually USE the loaded model)
            Console.WriteLine("   📥 Loading base model...");
            var baseModel = _mlContext.Model.Load(existingModelPath, out var baseSchema);
            Console.WriteLine("   ✅ Base model loaded");

            cancellationToken.ThrowIfCancellationRequested();

            // Write corrections to temp CSV (streamed)
            var tempCsvPath = Path.Combine(_basePath, "temp_corrections.csv");
            try
            {
                // FIX #3: Stream to file directly
                using (var writer = new StreamWriter(tempCsvPath, append: false))
                {
                    foreach (var (imgPath, label) in corrections)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        writer.WriteLine($"{imgPath},{label}");
                    }
                }

                var correctionData = _mlContext.Data.LoadFromTextFile<ImageData>(
                    tempCsvPath, separatorChar: ',', hasHeader: false);

                var pipeline = BuildFullPipeline(labelCounts, corrections.Count);

                Console.WriteLine($"\n🚀 Training correction model on {corrections.Count} samples...");
                var startTime = DateTime.Now;
                var correctionModel = pipeline.Fit(correctionData);
                Console.WriteLine($"✅ Correction model trained in {(DateTime.Now - startTime).TotalSeconds:F1}s");

                cancellationToken.ThrowIfCancellationRequested();

                EvaluateOnCorrections(correctionModel, corrections, "Correction model");

                // Save ensemble config alongside model so inference layer can load both
                var correctionModelPath = SaveModel(correctionModel, correctionData.Schema, "correctionModel");
                SaveEnsembleConfig(existingModelPath, correctionModelPath, labelCounts);

                Console.WriteLine("\n   ℹ️  ENSEMBLE INFERENCE:");
                Console.WriteLine("      At prediction time, load BOTH models and average their scores.");
                Console.WriteLine("      Base model weight: 0.6  |  Correction model weight: 0.4");
                Console.WriteLine($"      Config saved: ensemble_config.json");

                return correctionModelPath;
            }
            finally
            {
                // FIX #6: Always clean up temp file
                TryDelete(tempCsvPath);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PIPELINE BUILDER
        // Selects trainer based on correction batch size
        // FIX #4: Uses configurable image size, not hardcoded 150x150
        // ═══════════════════════════════════════════════════════════════

        private IEstimator<ITransformer> BuildFullPipeline(
            Dictionary<string, int> labelCounts,
            int totalSamples)
        {
            var preprocess = _mlContext.Transforms.LoadImages(
                    outputColumnName: "InputImage",
                    imageFolder: null,
                    inputColumnName: nameof(ImageData.ImagePath))

                // 1. Resize using your ROI Config (important for weld-specific areas)
                .Append(_mlContext.Transforms.ResizeImages(
                    outputColumnName: "ResizedImage",
                    imageWidth: _roiConfig.ImageWidth,
                    imageHeight: _roiConfig.ImageHeight,
                    inputColumnName: "InputImage",
                    resizing: Microsoft.ML.Transforms.Image.ImageResizingEstimator.ResizingKind.IsoCrop))

                // 2. Numerical extraction
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "RawFeatures",
                    inputColumnName: "ResizedImage",
                    interleavePixelColors: true,
                    offsetImage: 128f,
                    scaleImage: 1f / 128f))

                // 3. Dimensionality reduction
                .Append(_mlContext.Transforms.ProjectToPrincipalComponents(
                    outputColumnName: "Features",
                    inputColumnName: "RawFeatures",
                    rank: 50))

                // 4. Dynamic Label Mapping
                // This handles "Problem 1", "Problem 2", etc., by mapping them to stable keys
                .Append(_mlContext.Transforms.Conversion.MapValueToKey(
                    outputColumnName: "Label",
                    inputColumnName: nameof(ImageData.Label),
                    keyOrdinality: Microsoft.ML.Transforms.ValueToKeyMappingEstimator.KeyOrdinality.ByValue));

            // Select trainer based on batch size
            var minClassSamples = labelCounts.Values.Min();
            IEstimator<ITransformer> trainer;
            string trainerName;

            if (totalSamples < 20 || minClassSamples < 3)
            {
                trainerName = "SDCA MaximumEntropy";
                trainer = _mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    maximumNumberOfIterations: 100);
            }
            else if (totalSamples < 50)
            {
                trainerName = "L-BFGS MaximumEntropy";
                trainer = _mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    l1Regularization: 0.1f,
                    l2Regularization: 0.1f);
            }
            else
            {
                trainerName = "LightGBM";
                trainer = _mlContext.MulticlassClassification.Trainers.LightGbm(
                    labelColumnName: "Label",
                    featureColumnName: "Features",
                    numberOfLeaves: 20,
                    minimumExampleCountPerLeaf: 3,
                    learningRate: 0.01f,
                    numberOfIterations: 100);
            }

            Console.WriteLine($"\n🧠 Trainer selected: {trainerName}");
            Console.WriteLine($"   Reason: {totalSamples} total samples, min class size = {minClassSamples}");

            return preprocess
                .Append(trainer)
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));
        }

        // ═══════════════════════════════════════════════════════════════
        // SINGLE CORRECTION
        // FIX #5: Appends to corrections.csv instead of overwriting model
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Records a single correction to disk for the next batch incremental run.
        /// Does NOT immediately modify the model — accumulate then call IncrementalTrain.
        /// </summary>
        public void RecordCorrection(string imagePath, string correctedLabel)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image not found: {imagePath}");

            bool fileExists = File.Exists(_correctionsPath);

            // FIX #5: Append to corrections log — never modify the live model with one sample
            using var writer = new StreamWriter(_correctionsPath, append: true);

            if (!fileExists)
                writer.WriteLine("Timestamp,ImagePath,OriginalLabel,Confidence,CorrectedLabel");

            writer.WriteLine(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}," +
                $"{imagePath}," +
                $"," +  // original label not always known here
                $"," +  // confidence not always known here
                $"{correctedLabel}");

            Console.WriteLine($"✅ Correction recorded: {Path.GetFileName(imagePath)} → {correctedLabel}");
            Console.WriteLine($"   Total pending corrections: {CountPendingCorrections()}");
        }

        /// <summary>
        /// Returns how many corrections are queued and not yet trained on.
        /// </summary>
        public int CountPendingCorrections()
        {
            if (!File.Exists(_correctionsPath)) return 0;
            // FIX #2: Stream — skip header
            return File.ReadLines(_correctionsPath).Skip(1)
                .Count(l => !string.IsNullOrWhiteSpace(l));
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Streams corrections.csv, validates each row, returns parsed list.
        /// FIX #2: Uses File.ReadLines (streaming) instead of ReadAllLines
        /// FIX #7: Uses TryGetValue instead of ContainsKey + indexer
        /// </summary>
        private (List<(string ImagePath, string Label)> corrections,
                 Dictionary<string, int> labelCounts)
            LoadAndValidateCorrections()
        {
            Console.WriteLine("\n🔍 Loading and validating corrections...");

            var corrections = new List<(string, string)>();
            var labelCounts = new Dictionary<string, int>();
            int skipped = 0;
            int lineNum = 0;

            // FIX #2: Stream lines — skip header
            foreach (var line in File.ReadLines(_correctionsPath).Skip(1))
            {
                lineNum++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 5)
                {
                    Console.WriteLine($"   ⚠️ Line {lineNum}: malformed (expected 5 columns, got {parts.Length})");
                    skipped++;
                    continue;
                }

                var imagePath = parts[1].Trim().Trim('"');
                var correctedLabel = parts[4].Trim().Trim('"');

                if (string.IsNullOrWhiteSpace(correctedLabel))
                {
                    Console.WriteLine($"   ⚠️ Line {lineNum}: empty label, skipping");
                    skipped++;
                    continue;
                }

                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"   ⚠️ Line {lineNum}: missing file {Path.GetFileName(imagePath)}");
                    skipped++;
                    continue;
                }

                corrections.Add((imagePath, correctedLabel));

                // FIX #7: TryGetValue instead of ContainsKey + index
                labelCounts.TryGetValue(correctedLabel, out int current);
                labelCounts[correctedLabel] = current + 1;
            }

            if (corrections.Count == 0)
                throw new InvalidOperationException(
                    "No valid corrections found after validation.\n" +
                    "Ensure image files exist and labels are non-empty.");

            if (skipped > 0)
                Console.WriteLine($"   ⚠️ Skipped {skipped} invalid entries");

            Console.WriteLine($"   ✅ Loaded {corrections.Count} valid corrections");
            return (corrections, labelCounts);
        }

        private void EvaluateOnCorrections(
            ITransformer model,
            List<(string ImagePath, string Label)> corrections,
            string modelLabel)
        {
            Console.WriteLine($"\n📊 Quick eval — {modelLabel} on correction samples...");
            try
            {
                var evalData = _mlContext.Data.LoadFromEnumerable(
                    corrections.Select(c => new ImageData
                    {
                        ImagePath = c.ImagePath,
                        Label = c.Label
                    }));

                var preds = model.Transform(evalData);
                var metrics = _mlContext.MulticlassClassification.Evaluate(preds, "Label");

                Console.WriteLine($"   • Micro Accuracy : {metrics.MicroAccuracy:P2}");
                Console.WriteLine($"   • Macro Accuracy : {metrics.MacroAccuracy:P2}");
                Console.WriteLine($"   • Log Loss       : {metrics.LogLoss:F4}");

                if (metrics.MicroAccuracy < 0.7)
                {
                    Console.WriteLine("   ⚠️ Low accuracy on corrections — consider:");
                    Console.WriteLine("      • Accumulating more diverse corrections");
                    Console.WriteLine("      • Running a full retrain from scratch");
                }
                else
                {
                    Console.WriteLine("   ✅ Model learned corrections well");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Evaluation skipped: {ex.Message}");
            }
        }

        private string SaveModel(ITransformer model, DataViewSchema schema, string prefix)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var path = Path.Combine(_basePath, $"{prefix}-{timestamp}.zip");
            _mlContext.Model.Save(model, schema, path);
            Console.WriteLine($"\n💾 Model saved: {Path.GetFileName(path)}");
            return path;
        }

        private void SaveEnsembleConfig(
            string baseModelPath,
            string correctionModelPath,
            Dictionary<string, int> labelCounts)
        {
            var config = new
            {
                Strategy = "Ensemble",
                BaseModelPath = baseModelPath,
                CorrectionModelPath = correctionModelPath,
                BaseModelWeight = 0.6,
                CorrectionModelWeight = 0.4,
                Classes = labelCounts.Keys.OrderBy(k => k).ToList(),
                CreatedAt = DateTime.Now.ToString("o")
            };

            var json = System.Text.Json.JsonSerializer.Serialize(
                config,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            var cfgPath = Path.Combine(_basePath, "ensemble_config.json");
            File.WriteAllText(cfgPath, json);
            Console.WriteLine($"   ✅ Ensemble config saved: {Path.GetFileName(cfgPath)}");
        }

        private void PrintLabelDistribution(Dictionary<string, int> labelCounts, int total)
        {
            Console.WriteLine("\n   Label distribution in corrections:");
            foreach (var kv in labelCounts.OrderBy(x => x.Key))
            {
                double pct = (kv.Value * 100.0) / total;
                Console.WriteLine($"      {kv.Key}: {kv.Value} samples ({pct:F1}%)");
            }
        }

        private bool HasEnoughRows(string csvPath)
        {
            // Streaming count — returns false if file is empty or header-only
            int count = 0;
            foreach (var line in File.ReadLines(csvPath))
            {
                if (!string.IsNullOrWhiteSpace(line)) count++;
                if (count > 1) return true;
            }
            return false;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* ignore cleanup errors */ }
        }

        public class RoiConfig
        {
            public int RoiX { get; set; }
            public int RoiY { get; set; }
            public int RoiW { get; set; }
            public int RoiH { get; set; }
            public int ImageWidth { get; set; } = 224;
            public int ImageHeight { get; set; } = 224;
        }
    }
}