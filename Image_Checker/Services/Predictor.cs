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
        private readonly MLContext _mlContext;
        private readonly ITransformer _model;
        private readonly PredictionEngine<ImageData, ImagePrediction> _predictionEngine;
        private float _lastConfidence;

        public Predictor(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model not found: {modelPath}");

            _mlContext = new MLContext();
            _model = _mlContext.Model.Load(modelPath, out _);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(_model);
            _lastConfidence = 0f;
        }

        /// <summary>
        /// Predicts the label for an image and returns the predicted label
        /// </summary>
        public string Predict(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Image not found: {imagePath}");

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);

            // Store the confidence for the predicted label
            _lastConfidence = prediction.Score?.Max() ?? 0f;

            return prediction.PredictedLabel;
        }

        /// <summary>
        /// Predicts the label and returns both label and confidence
        /// </summary>
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

        /// <summary>
        /// Gets the confidence score from the last prediction
        /// </summary>
        public float GetLastConfidence()
        {
            return _lastConfidence;
        }

        /// <summary>
        /// Gets detailed prediction with all scores
        /// </summary>
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
    }

    /// <summary>
    /// Detailed prediction result with all confidence scores
    /// </summary>
    public class PredictionResult
    {
        public string PredictedLabel { get; set; }
        public float Confidence { get; set; }
        public float[] AllScores { get; set; }

        public override string ToString()
        {
            return $"{PredictedLabel} ({Confidence:P2})";
        }
    }

    /// <summary>
    /// Prediction output from ML.NET
    /// </summary>
    public class ImagePrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; }

        [ColumnName("Score")]
        public float[] Score { get; set; }
    }
}