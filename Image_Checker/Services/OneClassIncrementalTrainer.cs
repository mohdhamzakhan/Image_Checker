using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace Image_Checker.Services
{
    /// <summary>
    /// Incremental training for the one-class (autoencoder) anomaly detector.
    ///
    /// Unlike CnnIncrementalTrainer (which retrains the small supervised CNN
    /// from scratch — cheap and safe for that model), this does a genuine
    /// warm-start: it loads the EXISTING autoencoder weights and fine-tunes
    /// for a handful of epochs on the updated OK folder, instead of
    /// re-learning "what normal looks like" from nothing.
    ///
    /// It also closes the gap the original trainer had: the threshold was
    /// only ever calibrated against OK validation error. After fine-tuning,
    /// this class re-evaluates the new model against ALL known NG images
    /// (including any just-corrected ones) and will widen/tighten the
    /// threshold if that improves real NG recall — not just guesswork.
    /// </summary>
    public class OneClassIncrementalTrainer
    {
        public class Result
        {
            public string ModelPath { get; init; } = "";
            public float Threshold { get; init; }
            public OneClassChecker.EvaluationResult? Evaluation { get; init; }
            public bool ThresholdAdjusted { get; init; }
        }

        private readonly string _okFolderPath;
        private readonly string _ngFolderPath;
        private readonly string _outputPath;
        private readonly OneClassTrainer.Config _config;
        private readonly Rectangle _roiRect;
        private readonly Action<string>? _log;

        public OneClassIncrementalTrainer(
            string okFolderPath,
            string ngFolderPath,
            string outputPath,
            OneClassTrainer.Config config,
            Rectangle roiRect,
            Action<string>? log = null)
        {
            _okFolderPath = okFolderPath;
            _ngFolderPath = ngFolderPath;
            _outputPath = outputPath;
            _config = config;
            _roiRect = roiRect;
            _log = log;
        }

        // ══════════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies corrections (moves images to OK/NG folders), warm-starts
        /// from the existing model, fine-tunes briefly, then validates the
        /// new threshold against real NG images before accepting it.
        /// </summary>
        public Result IncrementalUpdate(
            string correctionsPath,
            string existingModelPath,
            int fineTuneEpochs = 5,
            float learningRateMultiplier = 0.1f,
            CancellationToken ct = default)
        {
            Log("═══════════════════════════════════════════════");
            Log("⚡ ONE-CLASS INCREMENTAL UPDATE");
            Log("═══════════════════════════════════════════════");

            if (!File.Exists(correctionsPath))
                throw new FileNotFoundException("No corrections file found.", correctionsPath);

            // Step 1: Parse + move corrections into the right folders.
            // OK corrections feed the autoencoder's training set.
            // NG corrections are kept only for evaluation — the autoencoder
            // never trains on NG images by design.
            var corrections = LoadCorrections(correctionsPath);
            Log($"📝 Loaded {corrections.Count} corrections");

            if (corrections.Count == 0)
                throw new InvalidOperationException("No valid corrections found.");

            ct.ThrowIfCancellationRequested();

            var moveResult = MoveCorrections(corrections, ct);
            Log($"📦 Moved: {moveResult.Moved}  Skipped: {moveResult.Skipped}  Errors: {moveResult.Errors}");

            ct.ThrowIfCancellationRequested();

            // Step 2: Warm-start fine-tune on the updated OK folder
            Log("\n🚀 Fine-tuning autoencoder from existing weights...");
            var trainer = new OneClassTrainer(_okFolderPath, _outputPath, _config, _roiRect, _log);
            var trainResult = trainer.IncrementalTrain(
                existingModelPath, fineTuneEpochs, learningRateMultiplier, ct);

            ct.ThrowIfCancellationRequested();

            // Step 3: Validate the new model against REAL OK + NG images —
            // this is the check the original training pipeline never did.
            Log("\n🔍 Validating against real OK/NG images...");
            using var checker = new OneClassChecker(trainResult.ModelPath);
            var evaluation = checker.Evaluate(_okFolderPath, _ngFolderPath);
            Log($"   {evaluation}");

            bool adjusted = false;
            float finalThreshold = trainResult.Threshold;

            // Step 4: If NG recall is weak, sweep thresholds and pick a
            // better one instead of shipping a model that misses defects.
            if (evaluation.TotalNg > 0 && evaluation.NgRecall < 0.9f)
            {
                Log("\n⚠️ NG recall below 90% — sweeping thresholds for a better cut point...");

                float baseThreshold = trainResult.Threshold;
                var candidates = Enumerable.Range(-10, 21)
                    .Select(i => baseThreshold * (1f + i * 0.05f))
                    .Where(t => t > 0f)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();

                var swept = checker.SweepThreshold(_okFolderPath, _ngFolderPath, candidates);

                // Prefer the lowest threshold that reaches >=95% NG recall,
                // breaking ties by keeping the fewest OK false alarms.
                var best = swept
                    .Where(s => s.Result.NgRecall >= 0.95f)
                    .OrderBy(s => s.Result.FalsePositiveOk)
                    .ThenBy(s => s.Threshold)
                    .FirstOrDefault();

                // Fall back to whichever threshold maximises NG recall if
                // none reach 95%.
                if (best.Result == null)
                    best = swept.OrderByDescending(s => s.Result.NgRecall)
                                .ThenBy(s => s.Result.FalsePositiveOk)
                                .First();

                if (best.Threshold != baseThreshold)
                {
                    Log($"   New threshold: {best.Threshold:F6}  →  {best.Result}");
                    checker.AdjustThreshold(best.Threshold, trainResult.ModelPath);
                    evaluation = best.Result;
                    adjusted = true;
                    finalThreshold = best.Threshold;
                }
            }

            Log("\n✅ ONE-CLASS INCREMENTAL UPDATE COMPLETE");
            Log($"   Model     : {Path.GetFileName(trainResult.ModelPath)}");
            Log($"   Threshold : {finalThreshold:F6}{(adjusted ? " (adjusted after NG validation)" : "")}");
            Log($"   Final     : {evaluation}");

            return new Result
            {
                ModelPath = trainResult.ModelPath,
                Threshold = finalThreshold,
                Evaluation = evaluation,
                ThresholdAdjusted = adjusted
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  MOVE CORRECTIONS TO OK / NG FOLDERS
        // ══════════════════════════════════════════════════════════════

        private record MoveResult(int Moved, int Skipped, int Errors);

        private MoveResult MoveCorrections(
            List<(string ImagePath, string CorrectedLabel)> corrections,
            CancellationToken ct)
        {
            int moved = 0, skipped = 0, errors = 0;

            foreach (var (imagePath, label) in corrections)
            {
                ct.ThrowIfCancellationRequested();

                if (!File.Exists(imagePath))
                {
                    Log($"   ⚠️ File not found, skipping: {Path.GetFileName(imagePath)}");
                    skipped++;
                    continue;
                }

                bool isOk = label.Equals("OK", StringComparison.OrdinalIgnoreCase);
                var targetDir = isOk ? _okFolderPath : _ngFolderPath;

                var currentDir = Path.GetDirectoryName(imagePath) ?? "";
                if (string.Equals(
                        Path.GetFullPath(currentDir).TrimEnd(Path.DirectorySeparatorChar),
                        Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                Directory.CreateDirectory(targetDir);
                var targetPath = GetUniqueTargetPath(targetDir, Path.GetFileName(imagePath));

                try
                {
                    File.Move(imagePath, targetPath);
                    Log($"   ✅ {Path.GetFileName(imagePath)} → {(isOk ? "OK" : "NG")}/");
                    moved++;
                }
                catch (Exception ex)
                {
                    Log($"   ❌ Could not move {Path.GetFileName(imagePath)}: {ex.Message}");
                    errors++;
                }
            }

            return new MoveResult(moved, skipped, errors);
        }

        // ══════════════════════════════════════════════════════════════
        //  PARSE CORRECTIONS CSV
        //  Timestamp, ImagePath, OriginalLabel, Confidence, CorrectedLabel
        // ══════════════════════════════════════════════════════════════

        private static List<(string ImagePath, string CorrectedLabel)>
            LoadCorrections(string csvPath)
        {
            var result = new List<(string, string)>();

            foreach (var line in File.ReadLines(csvPath).Skip(1)) // skip header
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                var imagePath = parts[1].Trim().Trim('"');
                var correctedLabel = parts[4].Trim().Trim('"');

                if (string.IsNullOrEmpty(imagePath) ||
                    string.IsNullOrEmpty(correctedLabel)) continue;

                result.Add((imagePath, correctedLabel));
            }

            return result;
        }

        private static string GetUniqueTargetPath(string dir, string fileName)
        {
            var target = Path.Combine(dir, fileName);
            if (!File.Exists(target)) return target;

            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            int i = 1;

            while (File.Exists(target))
                target = Path.Combine(dir, $"{name}_c{i++}{ext}");

            return target;
        }

        private void Log(string msg) => _log?.Invoke(msg);
    }
}