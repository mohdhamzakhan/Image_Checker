using Image_Checker.DataModels;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.IO;
using System.Linq;

namespace Image_Checker.Services
{
    public class Predictor
    {
        private MLContext _mlContext;
        private ITransformer _model;
        private PredictionEngine<ImageData, ImagePrediction> _predictionEngine;
        private float _lastConfidence;
        public string ModelPath { get; private set; }  // Expose current model path
        public string BasePath { get; private set; }   // Folder to search for models

        /// <summary>
        /// Creates predictor with SPECIFIC model path
        /// </summary>
        public Predictor(string modelPath)
        {
            BasePath = Path.GetDirectoryName(modelPath);
            ModelPath = modelPath;
            Initialize(modelPath);
        }

        /// <summary>
        /// Creates predictor that AUTO-LOADS LATEST model from basePath
        /// </summary>
        public Predictor(string basePath, bool autoLoadLatest = true)
        {
            BasePath = basePath;
            if (autoLoadLatest)
            {
                ModelPath = GetLatestModelPath(basePath);
            }
            else
            {
                ModelPath = basePath; // Will fail if not .zip
            }
            Initialize(ModelPath);
        }

        private void Initialize(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model not found: {modelPath}");

            Console.WriteLine($"✅ Predictor loaded: {Path.GetFileName(modelPath)}");

            _mlContext = new MLContext(seed: 42);  // Consistent seed
            _model = _mlContext.Model.Load(modelPath, out _);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(_model);
            _lastConfidence = 0f;
        }

        /// <summary>
        /// RELOADS the LATEST model from basePath (after incremental training)
        /// </summary>
        public void ReloadLatestModel()
        {
            var latestPath = GetLatestModelPath(BasePath);
            if (latestPath != ModelPath)
            {
                Console.WriteLine($"🔄 Reloading: {Path.GetFileName(latestPath)}");
                Initialize(latestPath);
            }
            else
            {
                Console.WriteLine("ℹ️  Already using latest model");
            }
        }

        /// <summary>
        /// Finds the MOST RECENT model file (bestModel-* or incrementalModel-*)
        /// </summary>
        private static string GetLatestModelPath(string basePath)
        {
            var modelFiles = Directory.GetFiles(basePath, "*model*.zip", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetCreationTime)
                .ToArray();

            if (modelFiles.Length == 0)
                throw new FileNotFoundException($"No model files found in: {basePath}");

            return modelFiles[0];  // Newest by creation time
        }

        // ========== YOUR EXISTING PREDICTION METHODS (UNCHANGED) ==========
        public string Predict(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image not found: {imagePath}");

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);
            _lastConfidence = prediction.Score?.Max() ?? 0f;
            return prediction.PredictedLabel;
        }

        public (string Label, float Confidence) PredictWithConfidence(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image not found: {imagePath}");

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);

            float confidence = prediction.Score?.Max() ?? 0f;
            _lastConfidence = confidence;
            return (prediction.PredictedLabel, confidence);
        }

        public float GetLastConfidence() => _lastConfidence;

        public PredictionResult PredictDetailed(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image not found: {imagePath}");

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);

            var result = new PredictionResult
            {
                PredictedLabel = prediction.PredictedLabel,
                Confidence = prediction.Score?.Max() ?? 0f,
                AllScores = prediction.Score
            };

            _lastConfidence = result.Confidence;
            return result;
        }

        /// <summary>
        /// Get info about current model
        /// </summary>
        public string GetModelInfo()
        {
            return $"Model: {Path.GetFileName(ModelPath)}\nPath: {ModelPath}\nAvailable models: {Directory.GetFiles(BasePath, "*model*.zip").Length}";
        }
    }

    // Your existing classes (unchanged)
    public class PredictionResult
    {
        public string PredictedLabel { get; set; }
        public float Confidence { get; set; }
        public float[] AllScores { get; set; }
        public override string ToString() => $"{PredictedLabel} ({Confidence:P2})";
    }

    public class ImagePrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; }

        [ColumnName("Score")]
        public float[] Score { get; set; }
    }
}
