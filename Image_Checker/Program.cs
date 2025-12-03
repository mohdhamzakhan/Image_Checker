using Image_Checker.Services;
using Image_Checker.Utils;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using Microsoft.ML.Vision;
using System.IO;
using System.Linq;
internal class Program
{
    private static string defaultPath = @"D:\Laptop\Code\Image_Checker\Image_Checker\";

    static void Main(string[] args)
    {
        var basePath = PathUtils.ResolvePath(args, defaultPath);
        var csvPath = Path.Combine(basePath, "images.csv");

        if (!File.Exists(csvPath))
            DataValidator.CreateCsv(basePath);

        DataValidator.PrintLabelDistribution(csvPath);

        var mlContext = new MLContext(seed: 0);
        var trainer = new ModelTrainer(mlContext, basePath);

        trainer.TrainAndEvaluate();

        Console.WriteLine("\n✅ Training complete. Best model saved in project directory.");
    }
}