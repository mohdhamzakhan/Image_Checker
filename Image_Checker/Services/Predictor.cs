using Image_Checker.DataModels;
using Microsoft.ML;

namespace Image_Checker.Services
{
    public class Predictor : IDisposable
    {
        private readonly MLContext _mlContext;
        private ITransformer _model;
        private PredictionEngine<ImageData, ImagePrediction> _predictionEngine;
        private readonly string _modelPath;
        private bool _disposed = false;

        // ── OOD thresholds (tunable) ───────────────────────────────────────────
        // These three checks together catch images that look nothing like a weld.
        //
        // HOW TO TUNE:
        //   1. Run 20 known-good weld images through PredictDetailed() and note
        //      their Confidence, Margin, and EntropyRatio values.
        //   2. Run 20 random non-weld images and note the same values.
        //   3. Set thresholds in the gap between those two groups.
        //
        // CURRENT DEFAULTS work well when weld images score >90% confidence
        // and random images score 50-75% with a small margin between classes.
        private const float CONFIDENCE_THRESHOLD = 0.75f;  // max class score must exceed this
        private const float MARGIN_THRESHOLD = 0.15f;  // gap between top-1 and top-2 scores
        private const float MAX_ENTROPY_RATIO = 0.60f;  // 0=certain, 1=maximally uncertain

        public const string UNKNOWN_LABEL = "Unknown";

        public string ModelPath => _modelPath;

        public Predictor(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model file not found: {modelPath}");

            _modelPath = modelPath;
            _mlContext = new MLContext(seed: 42);
            LoadModel();
        }

        // ── Model loading ──────────────────────────────────────────────────────

        private void LoadModel()
        {
            _predictionEngine?.Dispose();
            _model = _mlContext.Model.Load(_modelPath, out _);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(_model);
        }

        public void ReloadModel()
        {
            Console.WriteLine($"🔄 Reloading model from: {Path.GetFileName(_modelPath)}");
            LoadModel();
            Console.WriteLine("✅ Model reloaded successfully");
        }

        // ── Prediction API ─────────────────────────────────────────────────────

        /// <summary>
        /// Basic prediction — returns label only. No OOD check.
        /// </summary>
        public string Predict(string imagePath)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Predictor));
            EnsureFileExists(imagePath);
            var prediction = _predictionEngine.Predict(new ImageData { ImagePath = imagePath });
            return prediction.PredictedLabel?.Trim().Trim('"') ?? UNKNOWN_LABEL;
        }

        /// <summary>
        /// Returns (label, confidence) with NO OOD check.
        /// Used by monitor mode — production images are always real welds,
        /// so we only care about miss detection (NG→OK), not OOD rejection.
        /// </summary>
        public (string label, float confidence) PredictWithConfidence(string imagePath)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Predictor));
            EnsureFileExists(imagePath);

            var prediction = _predictionEngine.Predict(new ImageData { ImagePath = imagePath });
            float confidence = MaxScore(prediction.Score);
            string cleanLabel = prediction.PredictedLabel?.Trim().Trim('"') ?? UNKNOWN_LABEL;

            return (cleanLabel, confidence);
        }

        /// <summary>
        /// Returns (label, confidence) WITH OOD check.
        /// Use this for single image prediction where any file could be selected.
        /// Returns label="Unknown" if the image does not look like a weld.
        /// </summary>
        public (string label, float confidence) PredictWithConfidenceAndOodCheck(string imagePath)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Predictor));
            EnsureFileExists(imagePath);

            var prediction = _predictionEngine.Predict(new ImageData { ImagePath = imagePath });
            float confidence = MaxScore(prediction.Score);

            if (IsOutOfDistribution(prediction.Score, out string reason))
            {
                Console.WriteLine($"⚠️  OOD rejected [{reason}] conf={confidence:P1}  file={Path.GetFileName(imagePath)}");
                return (UNKNOWN_LABEL, confidence);
            }

            string cleanLabel = prediction.PredictedLabel?.Trim().Trim('"') ?? UNKNOWN_LABEL;
            return (cleanLabel, confidence);
        }

        /// <summary>
        /// Full detailed result including OOD diagnostics.
        /// Useful for debugging/tuning threshold values.
        /// </summary>
        public DetailedPrediction PredictDetailed(string imagePath)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Predictor));
            EnsureFileExists(imagePath);

            var prediction = _predictionEngine.Predict(new ImageData { ImagePath = imagePath });
            float confidence = MaxScore(prediction.Score);
            bool ood = IsOutOfDistribution(prediction.Score, out string reason);

            return new DetailedPrediction
            {
                PredictedLabel = ood
                                      ? UNKNOWN_LABEL
                                      : prediction.PredictedLabel?.Trim().Trim('"') ?? UNKNOWN_LABEL,
                Confidence = confidence,
                AllScores = prediction.Score,
                IsOutOfDistribution = ood,
                RejectionReason = ood ? reason : null,
                EntropyRatio = CalculateEntropyRatio(prediction.Score),
                Margin = CalculateMargin(prediction.Score)
            };
        }

        public string GetModelInfo()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Predictor));
            var fi = new FileInfo(_modelPath);
            return $"Model: {fi.Name}\nSize: {fi.Length / 1024:N0} KB\nModified: {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _predictionEngine?.Dispose();
                _predictionEngine = null;
                _model = null;
                _disposed = true;
                Console.WriteLine("✅ Predictor disposed");
            }
        }

        // ── OOD detection ──────────────────────────────────────────────────────

        /// <summary>
        /// Three independent checks — any one failing marks the image as OOD.
        ///
        /// WHY THREE CHECKS:
        ///   Softmax always sums scores to 100%, so even a PowerPoint screenshot
        ///   gets a high score in one class. Margin and entropy expose the
        ///   internal confusion that a single confidence number hides.
        /// </summary>
        private static bool IsOutOfDistribution(float[] scores, out string reason)
        {
            reason = string.Empty;

            if (scores == null || scores.Length == 0)
            {
                reason = "no scores returned";
                return true;
            }

            // Check 1 — raw confidence floor
            float maxScore = MaxScore(scores);
            if (maxScore < CONFIDENCE_THRESHOLD)
            {
                reason = $"low confidence ({maxScore:P1} < {CONFIDENCE_THRESHOLD:P1})";
                return true;
            }

            // Check 2 — margin between top-1 and top-2 scores
            // Real weld → large gap (e.g. 95% vs 5% = 0.90 margin)
            // Random image → small gap (e.g. 60% vs 40% = 0.20 margin)
            float margin = CalculateMargin(scores);
            if (margin < MARGIN_THRESHOLD)
            {
                reason = $"low margin between classes ({margin:P1} < {MARGIN_THRESHOLD:P1})";
                return true;
            }

            // Check 3 — normalised Shannon entropy
            // High entropy = score mass spread across classes = confused model
            float entropyRatio = CalculateEntropyRatio(scores);
            if (entropyRatio > MAX_ENTROPY_RATIO)
            {
                reason = $"high entropy ({entropyRatio:P1} > {MAX_ENTROPY_RATIO:P1})";
                return true;
            }

            return false;
        }

        // ── Maths helpers ──────────────────────────────────────────────────────

        private static float MaxScore(float[] scores)
        {
            if (scores == null || scores.Length == 0) return 0f;
            float max = scores[0];
            for (int i = 1; i < scores.Length; i++)
                if (scores[i] > max) max = scores[i];
            return max;
        }

        private static float CalculateMargin(float[] scores)
        {
            if (scores == null || scores.Length < 2) return 1f;
            float first = float.MinValue, second = float.MinValue;
            foreach (float s in scores)
            {
                if (s > first) { second = first; first = s; }
                else if (s > second) { second = s; }
            }
            return first - second;
        }

        private static float CalculateEntropy(float[] scores)
        {
            if (scores == null || scores.Length == 0) return 0f;
            float entropy = 0f;
            foreach (float s in scores)
                if (s > 1e-9f) entropy -= s * MathF.Log(s);
            return entropy;
        }

        private static float CalculateEntropyRatio(float[] scores)
        {
            if (scores == null || scores.Length <= 1) return 0f;
            float maxEntropy = MathF.Log(scores.Length);
            return maxEntropy > 0f ? CalculateEntropy(scores) / maxEntropy : 0f;
        }

        private static void EnsureFileExists(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Image not found: {path}");
        }
    }

    // ── Output types ───────────────────────────────────────────────────────────

    public class ImagePrediction
    {
        public string PredictedLabel { get; set; }
        public float[] Score { get; set; }
    }

    public class DetailedPrediction
    {
        public string PredictedLabel { get; set; }
        public float Confidence { get; set; }
        public float[] AllScores { get; set; }
        public bool IsOutOfDistribution { get; set; }
        public string RejectionReason { get; set; }
        public float EntropyRatio { get; set; }
        public float Margin { get; set; }
    }
}