using Image_Checker.DataModels;
using Microsoft.ML;
using System;
using System.IO;
using System.Linq;

namespace Image_Checker.Services
{
    /// <summary>
    /// Utility class for making predictions on single images
    /// </summary>
    public class SingleImagePredictor : IDisposable
    {
        private readonly MLContext _mlContext;
        private readonly ITransformer _model;
        private readonly PredictionEngine<ImageData, ImagePrediction> _predictionEngine;
        private readonly string _modelPath;

        public SingleImagePredictor(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model file not found: {modelPath}");

            _modelPath = modelPath;
            _mlContext = new MLContext(seed: 42);

            Console.WriteLine($"📦 Loading model from: {Path.GetFileName(modelPath)}");

            // Load the model
            _model = _mlContext.Model.Load(modelPath, out var schema);

            // Create prediction engine
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(_model);

            Console.WriteLine("✅ Model loaded successfully");
        }

        /// <summary>
        /// Predicts the label for a single image
        /// </summary>
        /// <param name="imagePath">Full path to the image file</param>
        /// <returns>Predicted label (e.g., "OK", "NG", "Cat", "Dog", etc.)</returns>
        public string Predict(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);

            return prediction.PredictedLabel;
        }

        /// <summary>
        /// Predicts the label and confidence for a single image
        /// </summary>
        /// <param name="imagePath">Full path to the image file</param>
        /// <returns>Tuple of (PredictedLabel, Confidence)</returns>
        public (string Label, float Confidence) PredictWithConfidence(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);

            // Get the confidence score for the predicted class
            var maxScore = prediction.Score?.Max() ?? 0f;

            return (prediction.PredictedLabel, maxScore);
        }

        /// <summary>
        /// Predicts the label with detailed scores for all classes
        /// </summary>
        /// <param name="imagePath">Full path to the image file</param>
        /// <returns>Prediction result with scores for all classes</returns>
        public ImagePredictionResult PredictWithAllScores(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image file not found: {imagePath}");

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);

            var result = new ImagePredictionResult
            {
                ImagePath = imagePath,
                PredictedLabel = prediction.PredictedLabel,
                Confidence = prediction.Score?.Max() ?? 0f
            };

            // Add scores for all classes if available
            if (prediction.Score != null)
            {
                result.AllScores = prediction.Score.ToArray();
            }

            return result;
        }

        /// <summary>
        /// Batch predict multiple images
        /// </summary>
        /// <param name="imagePaths">Collection of image paths</param>
        /// <returns>Collection of prediction results</returns>
        public List<ImagePredictionResult> PredictBatch(IEnumerable<string> imagePaths)
        {
            var results = new List<ImagePredictionResult>();

            foreach (var imagePath in imagePaths)
            {
                try
                {
                    var result = PredictWithAllScores(imagePath);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to predict {Path.GetFileName(imagePath)}: {ex.Message}");
                    results.Add(new ImagePredictionResult
                    {
                        ImagePath = imagePath,
                        PredictedLabel = "ERROR",
                        Confidence = 0f,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Gets model information including supported classes
        /// </summary>
        public ModelInfo GetModelInfo()
        {
            // This would require extracting schema information
            // For now, return basic info
            return new ModelInfo
            {
                ModelPath = _modelPath,
                ModelName = Path.GetFileName(_modelPath),
                LoadedDate = DateTime.Now
            };
        }

        public void Dispose()
        {
            _predictionEngine?.Dispose();
        }
    }

    /// <summary>
    /// Detailed prediction result
    /// </summary>
    public class ImagePredictionResult
    {
        public string ImagePath { get; set; }
        public string PredictedLabel { get; set; }
        public float Confidence { get; set; }
        public float[] AllScores { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Model information
    /// </summary>
    public class ModelInfo
    {
        public string ModelPath { get; set; }
        public string ModelName { get; set; }
        public DateTime LoadedDate { get; set; }
        public string[] SupportedClasses { get; set; }
    }

}