using Image_Checker.DataModels;
using Image_Checker.Utils;
using Microsoft.ML;
using System.Drawing;
using System.IO;
using System.Threading;

namespace Image_Checker.Services
{
    public class ModelTrainer
    {
        private readonly MLContext _mlContext;
        private readonly string _basePath;

        public ModelTrainer(MLContext mlContext, string basePath)
        {
            _mlContext = mlContext;
            _basePath = basePath;
        }

        private bool IsGrayscaleDataset()
        {
            Console.WriteLine("🔍 Detecting image color format...");
            var sample = Directory.GetFiles(Path.Combine(_basePath, "OK"))
                .Concat(Directory.GetFiles(Path.Combine(_basePath, "NG")))
                .FirstOrDefault();

            if (sample == null)
            {
                Console.WriteLine("⚠️ No sample images found, assuming grayscale");
                return true;
            }

            try
            {
                using var bmp = Image.FromFile(sample);
                bool isGray = bmp.PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed;
                Console.WriteLine($"   Format detected: {(isGray ? "Grayscale (8bpp)" : "RGB (Color)")}");
                return isGray;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error detecting format: {ex.Message}, assuming grayscale");
                return true;
            }
        }

        private bool AreImagePathsAbsolute(string csvPath)
        {
            var line = File.ReadLines(csvPath).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(line)) return false;
            var path = line.Split(',')[0].Trim('"');
            return Path.IsPathRooted(path);
        }

        private void FixDoubleDrivePaths(string csvPath)
        {
            Console.WriteLine("🔧 Checking for path issues...");
            var lines = File.ReadAllLines(csvPath);
            var fixedLines = new List<string>();
            int fixedCount = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',', 2);
                if (parts.Length < 2) continue;

                var path = parts[0].Trim('"');
                var label = parts[1].Trim();

                var idx = path.IndexOf(@"D:\", 3, StringComparison.OrdinalIgnoreCase);
                if (idx > 2)
                {
                    path = path.Substring(idx);
                    fixedCount++;
                }

                fixedLines.Add($"{path},{label}");
            }

            File.WriteAllLines(csvPath, fixedLines);
            if (fixedCount > 0)
                Console.WriteLine($"   ✅ Fixed {fixedCount} double-drive paths");
            else
                Console.WriteLine($"   ✅ No path issues found");
        }

        private void ValidateImageFiles(string csvPath, CancellationToken cancellationToken)
        {
            Console.WriteLine("\n🔍 Validating image files...");
            int missing = 0, unreadable = 0, total = 0;

            foreach (var line in File.ReadLines(csvPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                total++;
                var parts = line.Split(',');
                if (parts.Length < 2) continue;
                var img = parts[0].Trim('"');

                if (!File.Exists(img))
                {
                    missing++;
                    Console.WriteLine($"   ❌ Missing: {Path.GetFileName(img)}");
                    continue;
                }

                try
                {
                    using var fs = File.OpenRead(img);
                    if (fs.Length == 0)
                    {
                        unreadable++;
                        Console.WriteLine($"   ⚠️ Empty file: {Path.GetFileName(img)}");
                    }
                }
                catch
                {
                    unreadable++;
                    Console.WriteLine($"   ⚠️ Corrupted: {Path.GetFileName(img)}");
                }
            }

            Console.WriteLine($"   Validation complete:");
            Console.WriteLine($"   • Total: {total} files");
            Console.WriteLine($"   • Valid: {total - missing - unreadable} files");
            if (missing > 0) Console.WriteLine($"   • Missing: {missing} files");
            if (unreadable > 0) Console.WriteLine($"   • Unreadable: {unreadable} files");
            Console.WriteLine();
        }

        public void TrainAndEvaluate(
            int cvFolds = 3,
            int trials = 5,
            bool useSDCA = true,
            bool useLBFGS = true,
            bool useFastTree = true,
            bool useLightGBM = true,
            bool useTransferLearning = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var csvPath = Path.Combine(_basePath, "images.csv");
            if (!File.Exists(csvPath))
            {
                Console.WriteLine("❌ CSV file not found.");
                return;
            }

            FixDoubleDrivePaths(csvPath);
            cancellationToken.ThrowIfCancellationRequested();

            ValidateImageFiles(csvPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            bool isGray = IsGrayscaleDataset();
            Console.WriteLine(isGray
                ? "📸 Using grayscale processing (no transfer learning)"
                : "🎨 Using RGB processing (transfer learning enabled)");

            bool absolutePaths = AreImagePathsAbsolute(csvPath);
            Console.WriteLine($"📂 Path type: {(absolutePaths ? "Absolute" : "Relative")}");

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine("\n📊 Loading dataset...");
            var data = _mlContext.Data.LoadFromTextFile<ImageData>(csvPath, separatorChar: ',', hasHeader: false);
            Console.WriteLine("✅ Dataset loaded");

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine("\n✂️ Splitting data: 80% train, 20% test...");
            var split = _mlContext.Data.TrainTestSplit(data, 0.2, seed: 42);
            Console.WriteLine("✅ Data split complete");

            cancellationToken.ThrowIfCancellationRequested();

            // === Preprocessing ===
            Console.WriteLine("\n🔧 Building preprocessing pipeline...");
            var preprocess = _mlContext.Transforms.LoadImages(
                    outputColumnName: "InputImage",
                    imageFolder: absolutePaths ? null : _basePath,
                    inputColumnName: nameof(ImageData.ImagePath))
                .Append(_mlContext.Transforms.ResizeImages(
                    outputColumnName: "ResizedImage",
                    imageWidth: 150,
                    imageHeight: 150,
                    inputColumnName: "InputImage",
                    resizing: Microsoft.ML.Transforms.Image.ImageResizingEstimator.ResizingKind.Fill))
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "Features",
                    inputColumnName: "ResizedImage",
                    interleavePixelColors: true,
                    offsetImage: 128f,
                    scaleImage: 1f / 128f))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ImageData.Label)));

            Console.WriteLine("   • Image loading");
            Console.WriteLine("   • Resize to 150x150");
            Console.WriteLine("   • Feature extraction (RGB, normalized)");
            Console.WriteLine("   • Label encoding");

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine("\n⚙️ Fitting preprocessing pipeline...");
            var preprocessModel = preprocess.Fit(data);
            Console.WriteLine("✅ Preprocessing complete");

            cancellationToken.ThrowIfCancellationRequested();

            // Prepare cached training data for tuning
            Console.WriteLine("\n💾 Preparing data for hyperparameter tuning...");
            var preprocessedTrain = preprocessModel.Transform(split.TrainSet);
            var cachedTrain = _mlContext.Data.Cache(preprocessedTrain);
            Console.WriteLine("✅ Training data cached in memory");

            cancellationToken.ThrowIfCancellationRequested();

            // === Model Training ===
            var trainers = new List<(string Name, IEstimator<ITransformer> Est, bool Full)>();

            // Add selected baseline models
            if (useSDCA)
            {
                cancellationToken.ThrowIfCancellationRequested();
                trainers.Add(("SDCA_MaxEnt", _mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "Label", featureColumnName: "Features"), false));
            }

            if (useLBFGS)
            {
                cancellationToken.ThrowIfCancellationRequested();
                trainers.Add(("LBFGS_MaxEnt", _mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                    labelColumnName: "Label", featureColumnName: "Features"), false));
            }

            Console.WriteLine($"\n🎯 Registered {trainers.Count} baseline model(s)");

            // === FastTree Tuning ===
            if (useFastTree)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine("\n🌳 Starting FastTree hyperparameter tuning...");
                try
                {
                    var ftResult = FastTreeTuner.Tune(
                        _mlContext,
                        cachedTrain,
                        "Label",
                        nTrials: trials,
                        cvFolds: cvFolds,
                        seed: 42,
                        logPath: Path.Combine(_basePath, "fasttree_tuning_log.csv"),
                        sampleFraction: 0.5,
                        cancellationToken: cancellationToken);

                    if (ftResult.BestEstimator != null && !double.IsNegativeInfinity(ftResult.Score))
                    {
                        trainers.Add(("FastTree_Tuned", ftResult.BestEstimator, false));
                        Console.WriteLine($"✅ FastTree tuning complete (Best MacroAcc={ftResult.Score:P2})");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ FastTree tuning returned invalid score");
                        trainers.Add(("FastTree_Default", _mlContext.MulticlassClassification.Trainers.OneVersusAll(
                            _mlContext.BinaryClassification.Trainers.FastTree("Label", "Features",
                                numberOfLeaves: 50, numberOfTrees: 100, learningRate: 0.1)), false));
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("⚠️ FastTree tuning cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ FastTree tuning failed: {ex.GetBaseException().Message}");
                    trainers.Add(("FastTree_Default", _mlContext.MulticlassClassification.Trainers.OneVersusAll(
                        _mlContext.BinaryClassification.Trainers.FastTree("Label", "Features",
                            numberOfLeaves: 50, numberOfTrees: 100, learningRate: 0.1)), false));
                }
            }

            // === LightGBM Tuning ===
            if (useLightGBM)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine("\n🔬 Starting LightGBM hyperparameter tuning...");
                try
                {
                    var lgbResult = LightGbmTuner.Tune(
                        _mlContext,
                        cachedTrain,
                        "Label",
                        nTrials: trials,
                        cvFolds: cvFolds,
                        seed: 42,
                        logPath: Path.Combine(_basePath, "lgb_tuning_log.csv"),
                        sampleFraction: 0.5,
                        cancellationToken: cancellationToken);

                    if (lgbResult.BestEstimator != null && !double.IsNegativeInfinity(lgbResult.Score))
                    {
                        trainers.Add(("LightGBM_Tuned", lgbResult.BestEstimator, false));
                        Console.WriteLine($"✅ LightGBM tuning complete (Best MacroAcc={lgbResult.Score:P2})");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ LightGBM tuning returned invalid score");
                        trainers.Add(("LightGBM_Default", _mlContext.MulticlassClassification.Trainers.LightGbm(
                            labelColumnName: "Label", featureColumnName: "Features",
                            numberOfLeaves: 31, learningRate: 0.02f, numberOfIterations: 300), false));
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("⚠️ LightGBM tuning cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ LightGBM tuning failed: {ex.GetBaseException().Message}");
                    trainers.Add(("LightGBM_Default", _mlContext.MulticlassClassification.Trainers.LightGbm(
                        labelColumnName: "Label", featureColumnName: "Features",
                        numberOfLeaves: 31, learningRate: 0.02f, numberOfIterations: 300), false));
                }
            }

            // === Transfer Learning (RGB only - NO TUNING) ===
            if (useTransferLearning && !isGray)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine("\n🧠 Adding transfer learning model (MobilenetV2)...");
                try
                {
                    var options = new Microsoft.ML.Vision.ImageClassificationTrainer.Options
                    {
                        FeatureColumnName = "InputImage",
                        LabelColumnName = "Label",
                        Arch = Microsoft.ML.Vision.ImageClassificationTrainer.Architecture.MobilenetV2,
                        Epoch = 10,
                        BatchSize = 8,
                        LearningRate = 0.01f
                    };

                    var transferPipeline = _mlContext.Transforms.LoadImages("InputImage", _basePath, nameof(ImageData.ImagePath))
                        .Append(_mlContext.Transforms.ResizeImages("InputImage", 224, 224))
                        .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label"))
                        .Append(_mlContext.MulticlassClassification.Trainers.ImageClassification(options))
                        .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                    trainers.Add(("ImageClassification_Transfer", transferPipeline, true));
                    Console.WriteLine("✅ Transfer learning model added");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Transfer learning unavailable: {ex.GetBaseException().Message}");
                }
            }
            else if (useTransferLearning && isGray)
            {
                Console.WriteLine("\n⚠️ Transfer learning skipped: Only works with RGB (color) images");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // === Train & Evaluate All Models ===
            Console.WriteLine($"\n🚀 Evaluating {trainers.Count} models on test set...");
            Console.WriteLine("═══════════════════════════════════════════════");

            var results = new List<(string Name, double Macro, double Micro, double LogLoss, ITransformer Model, bool Full)>();
            int modelNum = 1;

            foreach (var (name, est, full) in trainers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine($"\n[{modelNum}/{trainers.Count}] Evaluating: {name}");
                Console.WriteLine("─────────────────────────────────────────────");

                try
                {
                    ITransformer model;

                    if (full)
                    {
                        // Full pipeline (already includes preprocessing)
                        Console.WriteLine("   ⏳ Training full pipeline...");
                        var startTime = DateTime.Now;
                        model = est.Fit(split.TrainSet);
                        var duration = DateTime.Now - startTime;
                        Console.WriteLine($"   ✅ Training complete in {duration.TotalSeconds:F1}s");
                    }
                    else
                    {
                        // Need to add preprocessing
                        var pipeline = preprocess
                            .Append(est)
                            .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

                        Console.WriteLine("   ⏳ Training model...");
                        var startTime = DateTime.Now;
                        model = pipeline.Fit(split.TrainSet);
                        var duration = DateTime.Now - startTime;
                        Console.WriteLine($"   ✅ Training complete in {duration.TotalSeconds:F1}s");
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    Console.WriteLine("   📊 Evaluating on test set...");
                    var preds = model.Transform(split.TestSet);
                    var m = _mlContext.MulticlassClassification.Evaluate(preds, "Label");

                    Console.WriteLine($"   Results:");
                    Console.WriteLine($"   • Macro Accuracy: {m.MacroAccuracy:P2}");
                    Console.WriteLine($"   • Micro Accuracy: {m.MicroAccuracy:P2}");
                    Console.WriteLine($"   • Log Loss: {m.LogLoss:F4}");

                    results.Add((name, m.MacroAccuracy, m.MicroAccuracy, m.LogLoss, model, full));
                    Console.WriteLine($"   ✅ {name} completed successfully");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"   ⚠️ {name} training cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ {name} failed: {ex.GetBaseException().Message}");
                }

                modelNum++;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!results.Any())
            {
                Console.WriteLine("\n❌ No models trained successfully. Check dataset and paths.");
                return;
            }

            // === Final Results ===
            Console.WriteLine("\n\n");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("📊 FINAL MODEL COMPARISON");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine($"{"Model",-35} {"Macro Acc",12} {"Micro Acc",12} {"Log Loss",10}");
            Console.WriteLine("───────────────────────────────────────────────────────────────────");

            foreach (var r in results.OrderByDescending(r => r.Macro))
            {
                Console.WriteLine($"{r.Name,-35} {r.Macro,12:P2} {r.Micro,12:P2} {r.LogLoss,10:F4}");
            }

            var best = results.OrderByDescending(r => r.Macro).First();
            Console.WriteLine("\n🏆 BEST MODEL: " + best.Name);
            Console.WriteLine($"   • Macro Accuracy: {best.Macro:P2}");
            Console.WriteLine($"   • Micro Accuracy: {best.Micro:P2}");
            Console.WriteLine($"   • Log Loss: {best.LogLoss:F4}");

            cancellationToken.ThrowIfCancellationRequested();

            var modelName = PathUtils.SanitizeFileName(best.Name);
            var modelPath = Path.Combine(_basePath, $"bestModel-{modelName}-{DateTime.Now:yyyyMMddHHmmss}.zip");

            Console.WriteLine($"\n💾 Saving best model...");
            _mlContext.Model.Save(best.Model, split.TrainSet.Schema, modelPath);
            Console.WriteLine($"✅ Model saved: {Path.GetFileName(modelPath)}");
            Console.WriteLine($"   Location: {modelPath}");
        }
    }
}