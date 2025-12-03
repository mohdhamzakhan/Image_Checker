using Image_Checker.DataModels;
using Microsoft.ML;

namespace Image_Checker.Services
{
    public class Predictor
    {
        private readonly MLContext _mlContext;
        private readonly ITransformer _model;
        private readonly PredictionEngine<ImageData, ImagePrediction> _engine;

        public static void TestSingleImage(string modelPath, string imagePath)
        {
            var ml = new MLContext();
            var model = ml.Model.Load(modelPath, out _);
            var engine = ml.Model.CreatePredictionEngine<ImageData, ImagePrediction>(model);

            var result = engine.Predict(new ImageData { ImagePath = imagePath });
            Console.WriteLine($"\nPrediction for {Path.GetFileName(imagePath)}");
            Console.WriteLine($"Predicted Label: {result.PredictedLabel}");
            Console.WriteLine($"Confidence: {result.Score.Max() * 100:F2}%");
        }

        public Predictor(string modelPath)
        {
            _mlContext = new MLContext();

            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model not found at: {modelPath}");

            _model = _mlContext.Model.Load(modelPath, out _);
            _engine = _mlContext.Model.CreatePredictionEngine<ImageData, ImagePrediction>(_model);
        }

        public string Predict(string imagePath)
        {
            var input = new ImageData { ImagePath = imagePath };
            var prediction = _engine.Predict(input);
            return prediction.PredictedLabel;
        }
    }
}
