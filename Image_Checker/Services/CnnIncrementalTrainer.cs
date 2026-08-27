using Image_Checker.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace Image_Checker.Services
{
    /// <summary>
    /// Incremental training for TorchSharp CNN models.
    /// 
    /// Strategy: Move corrected images to their proper class folders,
    /// then retrain CnnTrainer from scratch on the full dataset.
    /// 
    /// This is the correct approach because:
    ///   - ONNX is inference-only (no training)
    ///   - TorchSharp .bin has no optimizer state for warm-start
    ///   - Full retrain on corrected dataset is fast (CNN is small)
    ///   - Prevents catastrophic forgetting completely
    /// </summary>
    public class CnnIncrementalTrainer
    {
        private readonly string _datasetPath;
        private readonly CnnTrainer.CnnConfig _config;
        private readonly Rectangle _roiRect;
        private readonly Action<string>? _log;

        public CnnIncrementalTrainer(
            string datasetPath,
            CnnTrainer.CnnConfig config,
            Rectangle roiRect,
            Action<string>? log = null)
        {
            _datasetPath = datasetPath;
            _config = config;
            _roiRect = roiRect;
            _log = log;
        }

        // ══════════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies corrections then retrains. Returns the new .bin path.
        /// </summary>
        public CnnTrainer.CnnResult IncrementalUpdate(
            string correctionsPath,
            CancellationToken ct = default)
        {
            Log("═══════════════════════════════════════════════");
            Log("⚡ CNN INCREMENTAL UPDATE");
            Log("═══════════════════════════════════════════════");

            if (!File.Exists(correctionsPath))
                throw new FileNotFoundException("No corrections file found.", correctionsPath);

            // Step 1: Parse corrections
            var corrections = LoadCorrections(correctionsPath);
            Log($"📝 Loaded {corrections.Count} corrections");

            if (corrections.Count == 0)
                throw new InvalidOperationException("No valid corrections found.");

            // Validate at least 2 classes exist in dataset after move
            var classDirs = Directory.GetDirectories(_datasetPath)
                .Select(d => new System.IO.DirectoryInfo(d))
                .Where(d => !d.Name.StartsWith("."))
                .ToList();

            if (classDirs.Count < 2)
                throw new InvalidOperationException(
                    "Dataset must contain at least 2 class folders.");

            ct.ThrowIfCancellationRequested();

            // Step 2: Move corrected images to correct class folders
            var moveResult = MoveCorrections(corrections, ct);
            Log($"📦 Moved: {moveResult.Moved}  Skipped: {moveResult.Skipped}  Errors: {moveResult.Errors}");

            ct.ThrowIfCancellationRequested();

            // Step 3: Retrain on full dataset (now includes moved corrections)
            Log("\n🚀 Retraining CNN on updated dataset...");
            var trainer = new CnnTrainer(_datasetPath, _config, _roiRect, _log);
            var result = trainer.Train(ct);

            Log("\n✅ CNN INCREMENTAL UPDATE COMPLETE");
            Log($"   Val Acc : {result.ValAccuracy:P2}  Loss: {result.ValLoss:F4}");
            Log($"   Model   : {Path.GetFileName(result.ModelPath)}");

            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //  MOVE CORRECTIONS TO CLASS FOLDERS
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

                var currentFolder = new DirectoryInfo(
                    Path.GetDirectoryName(imagePath)!).Name;

                // Already in the right folder
                if (currentFolder.Equals(label, StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                // Target = dataset root / label /
                var targetDir = Path.Combine(_datasetPath, label);
                Directory.CreateDirectory(targetDir);

                var targetPath = GetUniqueTargetPath(targetDir, Path.GetFileName(imagePath));

                try
                {
                    File.Move(imagePath, targetPath);
                    Log($"   ✅ {Path.GetFileName(imagePath)} → {label}/");
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
        //  Same format as CorrectionManager writes:
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