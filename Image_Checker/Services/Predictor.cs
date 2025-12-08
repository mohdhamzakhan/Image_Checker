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

        public string ModelPath => _modelPath;

        public Predictor(string modelPath)
        {
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found: {modelPath}");
            }

            _modelPath = modelPath;
            _mlContext = new MLContext(seed: 42);

            LoadModel();
        }

        /// <summary>
        /// Loads or reloads the model from disk
        /// </summary>
        private void LoadModel()
        {
            // Dispose existing prediction engine if any
            _predictionEngine?.Dispose();

            // Load model
            _model = _mlContext.Model.Load(_modelPath, out var modelSchema);

            // Create prediction engine
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(_model);
        }

        /// <summary>
        /// Reloads the model from disk (use after in-place updates)
        /// </summary>
        public void ReloadModel()
        {
            Console.WriteLine($"🔄 Reloading model from: {Path.GetFileName(_modelPath)}");
            LoadModel();
            Console.WriteLine("✅ Model reloaded successfully");
        }

        /// <summary>
        /// Predict single image
        /// </summary>
        public string Predict(string imagePath)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Predictor));

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image not found: {imagePath}");
            }

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);

            return prediction.PredictedLabel;
        }

        /// <summary>
        /// Predict with confidence score
        /// </summary>
        public (string label, float confidence) PredictWithConfidence(string imagePath)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Predictor));

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image not found: {imagePath}");
            }

            var imageData = new ImageData { ImagePath = imagePath };
            var prediction = _predictionEngine.Predict(imageData);

            // Get confidence (max probability)
            float confidence = 0f;
            if (prediction.Score != null && prediction.Score.Length > 0)
            {
                confidence = prediction.Score[0];
                for (int i = 1; i < prediction.Score.Length; i++)
                {
                    if (prediction.Score[i] > confidence)
                        confidence = prediction.Score[i];
                }
            }

            return (prediction.PredictedLabel, confidence);
        }

        /// <summary>
        /// Get detailed prediction with all class probabilities
        /// </summary>
        public ImagePrediction PredictDetailed(string imagePath)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Predictor));

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image not found: {imagePath}");
            }

            var imageData = new ImageData { ImagePath = imagePath };
            return _predictionEngine.Predict(imageData);
        }

        /// <summary>
        /// Get model information
        /// </summary>
        public string GetModelInfo()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Predictor));

            var fileInfo = new FileInfo(_modelPath);
            return $"Model: {fileInfo.Name}\n" +
                   $"Size: {fileInfo.Length / 1024:N0} KB\n" +
                   $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
        }

        /// <summary>
        /// Disposes resources and releases model file lock
        /// </summary>
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
    }

    // Prediction output class
    public class ImagePrediction
    {
        public string PredictedLabel { get; set; }
        public float[] Score { get; set; }
    }
}