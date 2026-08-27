using Image_Checker.DataModels;
using Image_Checker.Utils;
using Microsoft.ML;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using static SkiaSharp.SKImageFilter;

namespace Image_Checker.Services
{
    public class ModelTrainer
    {
        private readonly MLContext _mlContext;
        private readonly string _basePath;
        private readonly Rectangle _roiRect;

        public ModelTrainer(MLContext mlContext, string basePath, Rectangle roiRect)
        {
            _mlContext = mlContext;
            _basePath = basePath;
            _roiRect = roiRect;
        }

        /// <summary>
        /// Gets all class folders dynamically from the dataset
        /// </summary>
        private List<string> GetClassFolders()
        {
            return Directory.GetDirectories(_basePath)
                .Select(d => new DirectoryInfo(d).Name)
                .Where(name => !name.StartsWith("."))
                .OrderBy(name => name)
                .ToList();
        }

        /// <summary>
        /// Detects if images are grayscale or RGB by sampling
        /// </summary>
        private bool IsGrayscaleDataset()
        {
            Console.WriteLine("🔍 Detecting image color format...");

            var classFolders = GetClassFolders();

            if (!classFolders.Any())
            {
                Console.WriteLine("⚠️ No class folders found, assuming grayscale");
                return true;
            }

            var sampleImages = classFolders
                .SelectMany(folder =>
                {
                    var folderPath = Path.Combine(_basePath, folder);
                    return Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                   f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                   f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
                })
                .Take(5)
                .ToList();

            if (!sampleImages.Any())
            {
                Console.WriteLine("⚠️ No sample images found, assuming grayscale");
                return true;
            }

            try
            {
                using var bmp = Image.FromFile(sampleImages.First());
                bool isGray = bmp.PixelFormat == System.Drawing.Imaging.PixelFormat.Format8bppIndexed;
                Console.WriteLine($"   Format detected: {(isGray ? "Grayscale (8bpp)" : "RGB (Color)")}");
                Console.WriteLine($"   Sampled from: {Path.GetFileName(sampleImages.First())}");
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

            var tempPath = csvPath + ".tmp";
            int fixedCount = 0;

            using (var reader = new StreamReader(csvPath))
            using (var writer = new StreamWriter(tempPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',', 2);
                    if (parts.Length < 2) continue;

                    var path = parts[0].Trim().Trim('"').Trim();
                    var label = parts[1].Trim().Trim('"').Trim();

                    var idx = path.IndexOf(@":\", 3, StringComparison.OrdinalIgnoreCase);
                    if (idx > 2)
                    {
                        path = path.Substring(idx - 1);
                        fixedCount++;
                    }

                    writer.WriteLine($"{path},{label}");
                }
            }

            File.Delete(csvPath);
            File.Move(tempPath, csvPath);

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
                    if (missing <= 5)
                        Console.WriteLine($"   ❌ Missing: {Path.GetFileName(img)}");
                    continue;
                }

                try
                {
                    using var fs = File.OpenRead(img);
                    if (fs.Length == 0)
                    {
                        unreadable++;
                        if (unreadable <= 5)
                            Console.WriteLine($"   ⚠️ Empty file: {Path.GetFileName(img)}");
                    }
                }
                catch
                {
                    unreadable++;
                    if (unreadable <= 5)
                        Console.WriteLine($"   ⚠️ Corrupted: {Path.GetFileName(img)}");
                }
            }

            Console.WriteLine($"   Validation complete:");
            Console.WriteLine($"   • Total: {total} files");
            Console.WriteLine($"   • Valid: {total - missing - unreadable} files");
            if (missing > 0)
            {
                Console.WriteLine($"   • Missing: {missing} files");
                if (missing > 5) Console.WriteLine($"     (and {missing - 5} more...)");
            }
            if (unreadable > 0)
            {
                Console.WriteLine($"   • Unreadable: {unreadable} files");
                if (unreadable > 5) Console.WriteLine($"     (and {unreadable - 5} more...)");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Reads a tuner CSV log and returns a list of TrialRecords for the report.
        /// </summary>
        private static List<TrialRecord> ReadTrialsFromCsv(string csvPath, string tunerType)
        {
            var list = new List<TrialRecord>();
            if (!File.Exists(csvPath)) return list;

            bool first = true;
            foreach (var line in File.ReadLines(csvPath))
            {
                if (first) { first = false; continue; } // skip header
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Strip any trailing comment (e.g. "# error message")
                var cleanLine = line.Contains('#') ? line[..line.IndexOf('#')].TrimEnd() : line;
                var parts = cleanLine.Split(',');
                if (parts.Length < 6) continue;

                int trialNum = int.TryParse(parts[0], out var t) ? t : 0;
                string scoreRaw = parts[^2].Trim(); // second-to-last column = Score
                bool failed = scoreRaw.StartsWith("NaN", StringComparison.OrdinalIgnoreCase)
                              || scoreRaw.StartsWith("ALL_", StringComparison.OrdinalIgnoreCase);
                bool cancelled = scoreRaw.StartsWith("CANCEL", StringComparison.OrdinalIgnoreCase);
                double score = (failed || cancelled)
                    ? double.NaN
                    : double.TryParse(scoreRaw,
                          System.Globalization.NumberStyles.Any,
                          System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : double.NaN;

                var paramDict = new Dictionary<string, object>();
                if (tunerType == "fasttree" && parts.Length >= 5)
                {
                    paramDict["Trees"] = parts[1].Trim();
                    paramDict["Leaves"] = parts[2].Trim();
                    paramDict["LR"] = parts[3].Trim();
                    paramDict["MinDocs"] = parts[4].Trim();
                }
                else if (tunerType == "lightgbm" && parts.Length >= 5)
                {
                    paramDict["Leaves"] = parts[1].Trim();
                    paramDict["MinData"] = parts[2].Trim();
                    paramDict["LR"] = parts[3].Trim();
                    paramDict["Iter"] = parts[4].Trim();
                }

                list.Add(new TrialRecord
                {
                    TrialNumber = trialNum,
                    Params = paramDict,
                    Score = score,
                    Failed = failed,
                    Cancelled = cancelled,
                });
            }

            return list;
        }

        public void TrainAndEvaluate(
            int cvFolds = 3,
            int trials = 5,
            bool useSDCA = true,
            bool useLBFGS = true,
            bool useFastTree = true,
            bool useLightGBM = true,
            bool useTransferLearning = false,
            int imageWidth = 224,
            int imageHeight = 224,
            CancellationToken cancellationToken = default)
        {
            var sessionStart = DateTime.Now;

            cancellationToken.ThrowIfCancellationRequested();

            // ── Locate CSV ────────────────────────────────────────────────────
            var csvPath = Path.Combine(_basePath, "images.csv");
            if (!File.Exists(csvPath))
            {
                Console.WriteLine("❌ CSV file not found.");
                return;
            }

            var classLabels = DataValidator.GetClassLabels(csvPath);
            Console.WriteLine($"\n🏷️ Detected {classLabels.Count} classes: {string.Join(", ", classLabels)}");

            if (classLabels.Count < 2)
            {
                Console.WriteLine("❌ At least 2 classes required for training.");
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

            // ── Load & split data ─────────────────────────────────────────────
            Console.WriteLine("\n📊 Loading dataset...");
            var data = _mlContext.Data.LoadFromTextFile<ImageData>(csvPath, separatorChar: ',', hasHeader: false);
            Console.WriteLine("✅ Dataset loaded");

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine("\n✂️ Splitting data: 80% train, 20% test...");
            var split = _mlContext.Data.TrainTestSplit(data, 0.2, seed: 42);
            Console.WriteLine("✅ Data split complete");

            cancellationToken.ThrowIfCancellationRequested();

            // ── Build preprocessing pipeline ──────────────────────────────────
            Console.WriteLine("\n🔧 Building preprocessing pipeline...");
            var preprocess = _mlContext.Transforms.LoadImages(
                    outputColumnName: "InputImage",
                    imageFolder: null,
                    inputColumnName: nameof(ImageData.ImagePath))
                .Append(_mlContext.Transforms.ResizeImages(
                    outputColumnName: "ResizedImage",
                    imageWidth: imageWidth,
                    imageHeight: imageHeight,
                    inputColumnName: "InputImage",
                    resizing: Microsoft.ML.Transforms.Image.ImageResizingEstimator.ResizingKind.IsoCrop))
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "RawFeatures",
                    inputColumnName: "ResizedImage",
                    interleavePixelColors: !isGray,
                    offsetImage: isGray ? 0f : 128f,
                    scaleImage: isGray ? 1f / 255f : 1f / 128f))
                .Append(_mlContext.Transforms.ProjectToPrincipalComponents(
                    outputColumnName: "Features",
                    inputColumnName: "RawFeatures",
                    rank: 100))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ImageData.Label)));

            Console.WriteLine("   • Image loading");
            Console.WriteLine($"   • Resize to {imageWidth}x{imageHeight}");
            Console.WriteLine("   • Feature extraction");
            Console.WriteLine($"   • PCA rank 100");
            Console.WriteLine($"   • Label encoding ({classLabels.Count} classes)");

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine("\n⚙️ Fitting preprocessing pipeline...");
            var preprocessModel = preprocess.Fit(data);
            Console.WriteLine("✅ Preprocessing complete");

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine("\n💾 Caching training data...");
            var preprocessedTrain = preprocessModel.Transform(split.TrainSet);
            var cachedTrain = _mlContext.Data.Cache(preprocessedTrain);
            Console.WriteLine("✅ Training data cached in memory");

            cancellationToken.ThrowIfCancellationRequested();

            // ── Register trainers ─────────────────────────────────────────────
            var trainers = new List<(string Name, IEstimator<ITransformer> Est, bool Full)>();

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

            // ── FastTree tuning ───────────────────────────────────────────────
            TunerRunResult ftRun = null;

            if (useFastTree)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine("\n🌳 Starting FastTree hyperparameter tuning...");

                var ftLogPath = Path.Combine(_basePath, "fasttree_tuning_log.csv");

                try
                {
                    var ftResult = FastTreeTuner.Tune(
                        _mlContext, cachedTrain, "Label",
                        nTrials: trials, cvFolds: cvFolds, seed: 42,
                        logPath: ftLogPath, sampleFraction: 0.5,
                        cancellationToken: cancellationToken);

                    var ftTrials = ReadTrialsFromCsv(ftLogPath, "fasttree");
                    ftRun = new TunerRunResult
                    {
                        TunerName = "FastTree",
                        Trials = ftTrials,
                        BestParams = ftResult.Params,
                        BestScore = ftResult.Score,
                        CVFolds = cvFolds,
                        SampleFraction = 0.5,
                    };

                    if (ftResult.BestEstimator != null && !double.IsNegativeInfinity(ftResult.Score))
                    {
                        trainers.Add(("FastTree_Tuned", ftResult.BestEstimator, false));
                        Console.WriteLine($"✅ FastTree tuning complete (Best MacroAcc={ftResult.Score:P2})");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ FastTree tuning returned invalid score — using defaults");
                        trainers.Add(("FastTree_Default",
                            _mlContext.MulticlassClassification.Trainers.OneVersusAll(
                                _mlContext.BinaryClassification.Trainers.FastTree(
                                    "Label", "Features",
                                    numberOfLeaves: 50, numberOfTrees: 100, learningRate: 0.1)),
                            false));
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
                    trainers.Add(("FastTree_Default",
                        _mlContext.MulticlassClassification.Trainers.OneVersusAll(
                            _mlContext.BinaryClassification.Trainers.FastTree(
                                "Label", "Features",
                                numberOfLeaves: 50, numberOfTrees: 100, learningRate: 0.1)),
                        false));

                    // Still attempt to build a run record from whatever was logged
                    ftRun = new TunerRunResult
                    {
                        TunerName = "FastTree",
                        Trials = ReadTrialsFromCsv(ftLogPath, "fasttree"),
                        BestParams = new Dictionary<string, object>(),
                        BestScore = double.NegativeInfinity,
                        CVFolds = cvFolds,
                        SampleFraction = 0.5,
                    };
                }
            }

            // ── LightGBM tuning ───────────────────────────────────────────────
            TunerRunResult lgbRun = null;

            if (useLightGBM)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine("\n🔬 Starting LightGBM hyperparameter tuning...");

                var lgbLogPath = Path.Combine(_basePath, "lgb_tuning_log.csv");

                try
                {
                    var lgbResult = LightGbmTuner.Tune(
                        _mlContext, cachedTrain, "Label",
                        nTrials: trials, cvFolds: cvFolds, seed: 42,
                        logPath: lgbLogPath, sampleFraction: 0.5,
                        cancellationToken: cancellationToken);

                    var lgbTrials = ReadTrialsFromCsv(lgbLogPath, "lightgbm");
                    lgbRun = new TunerRunResult
                    {
                        TunerName = "LightGBM",
                        Trials = lgbTrials,
                        BestParams = lgbResult.Params,
                        BestScore = lgbResult.Score,
                        CVFolds = cvFolds,
                        SampleFraction = 0.5,
                    };

                    if (lgbResult.BestEstimator != null && !double.IsNegativeInfinity(lgbResult.Score))
                    {
                        trainers.Add(("LightGBM_Tuned", lgbResult.BestEstimator, false));
                        Console.WriteLine($"✅ LightGBM tuning complete (Best MacroAcc={lgbResult.Score:P2})");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ LightGBM tuning returned invalid score — using defaults");
                        trainers.Add(("LightGBM_Default",
                            _mlContext.MulticlassClassification.Trainers.LightGbm(
                                labelColumnName: "Label", featureColumnName: "Features",
                                numberOfLeaves: 31, learningRate: 0.02f, numberOfIterations: 300),
                            false));
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
                    trainers.Add(("LightGBM_Default",
                        _mlContext.MulticlassClassification.Trainers.LightGbm(
                            labelColumnName: "Label", featureColumnName: "Features",
                            numberOfLeaves: 31, learningRate: 0.02f, numberOfIterations: 300),
                        false));

                    lgbRun = new TunerRunResult
                    {
                        TunerName = "LightGBM",
                        Trials = ReadTrialsFromCsv(lgbLogPath, "lightgbm"),
                        BestParams = new Dictionary<string, object>(),
                        BestScore = double.NegativeInfinity,
                        CVFolds = cvFolds,
                        SampleFraction = 0.5,
                    };
                }
            }

            // ── Transfer learning ─────────────────────────────────────────────
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
                        .Append(_mlContext.Transforms.ResizeImages("InputImage", imageWidth, imageHeight))
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

            // ── Train & evaluate each model ───────────────────────────────────
            Console.WriteLine($"\n🚀 Evaluating {trainers.Count} models on test set...");
            Console.WriteLine("═══════════════════════════════════════════════");

            // Tuple now includes TrainTimeSec for the report
            var results = new List<(string Name, double Macro, double Micro, double LogLoss,
                                    ITransformer Model, bool Full, double TrainSec)>();
            int modelNum = 1;

            foreach (var (name, est, full) in trainers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Console.WriteLine($"\n[{modelNum}/{trainers.Count}] Evaluating: {name}");
                Console.WriteLine("─────────────────────────────────────────────");

                try
                {
                    ITransformer model;
                    double trainSec;

                    if (full)
                    {
                        Console.WriteLine("   ⏳ Training full pipeline...");
                        var t0 = DateTime.Now;
                        model = est.Fit(split.TrainSet);
                        trainSec = (DateTime.Now - t0).TotalSeconds;
                        Console.WriteLine($"   ✅ Training complete in {trainSec:F1}s");
                    }
                    else
                    {
                        var pipeline = preprocess
                            .Append(est)
                            .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

                        Console.WriteLine("   ⏳ Training model...");
                        var t0 = DateTime.Now;
                        model = pipeline.Fit(split.TrainSet);
                        trainSec = (DateTime.Now - t0).TotalSeconds;
                        Console.WriteLine($"   ✅ Training complete in {trainSec:F1}s");
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    Console.WriteLine("   📊 Evaluating on test set...");
                    var preds = model.Transform(split.TestSet);
                    var m = _mlContext.MulticlassClassification.Evaluate(preds, "Label");

                    Console.WriteLine($"   Results:");
                    Console.WriteLine($"   • Macro Accuracy: {m.MacroAccuracy:P2}");
                    Console.WriteLine($"   • Micro Accuracy: {m.MicroAccuracy:P2}");
                    Console.WriteLine($"   • Log Loss:       {m.LogLoss:F4}");

                    results.Add((name, m.MacroAccuracy, m.MicroAccuracy, m.LogLoss, model, full, trainSec));
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

            // ── Print comparison table ────────────────────────────────────────
            Console.WriteLine("\n\n");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("📊 FINAL MODEL COMPARISON");
            Console.WriteLine($"   Training Dataset: {classLabels.Count} classes ({string.Join(", ", classLabels)})");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine($"{"Model",-35} {"Macro Acc",12} {"Micro Acc",12} {"Log Loss",10}");
            Console.WriteLine("───────────────────────────────────────────────────────────────────");

            foreach (var r in results.OrderByDescending(r => r.Macro))
                Console.WriteLine($"{r.Name,-35} {r.Macro,12:P2} {r.Micro,12:P2} {r.LogLoss,10:F4}");

            var best = results.OrderByDescending(r => r.Macro).First();
            Console.WriteLine("\n🏆 BEST MODEL: " + best.Name);
            Console.WriteLine($"   • Macro Accuracy: {best.Macro:P2}");
            Console.WriteLine($"   • Micro Accuracy: {best.Micro:P2}");
            Console.WriteLine($"   • Log Loss: {best.LogLoss:F4}");
            Console.WriteLine($"   • Can classify: {string.Join(", ", classLabels)}");

            cancellationToken.ThrowIfCancellationRequested();

            // ── Save best model ───────────────────────────────────────────────
            var modelName = PathUtils.SanitizeFileName(best.Name);
            var modelPath = Path.Combine(_basePath, $"bestModel-{modelName}-{DateTime.Now:yyyyMMddHHmmss}.zip");

            Console.WriteLine($"\n💾 Saving best model...");
            _mlContext.Model.Save(best.Model, split.TrainSet.Schema, modelPath);
            Console.WriteLine($"✅ Model saved: {Path.GetFileName(modelPath)}");
            Console.WriteLine($"   Location: {modelPath}");
            Console.WriteLine($"   Classes: {string.Join(", ", classLabels)}");

            // ── ROI config JSON ───────────────────────────────────────────────
            var roiConfig = new
            {
                RoiX = _roiRect.X,
                RoiY = _roiRect.Y,
                RoiW = _roiRect.Width,
                RoiH = _roiRect.Height,
                ImageWidth = imageWidth,
                ImageHeight = imageHeight
            };

            var json = System.Text.Json.JsonSerializer.Serialize(
                roiConfig,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var cfgPath = Path.ChangeExtension(modelPath, ".json");
            File.WriteAllText(cfgPath, json);
            Console.WriteLine($"   ROI config saved: {Path.GetFileName(cfgPath)}");

            // ── Confusion matrix ──────────────────────────────────────────────
            Console.WriteLine("\n\n");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("🔬 CONFUSION MATRIX — BEST MODEL: " + best.Name);
            Console.WriteLine("═══════════════════════════════════════════════");

            // Pass null here — the HTML report below replaces the standalone CM file
            var cmResult = ConfusionMatrixReporter.Evaluate(
                mlContext: _mlContext,
                model: best.Model,
                testSet: split.TestSet,
                classLabels: classLabels,
                reportPath: null);

            Console.WriteLine($"\n✅ Matrix complete — {cmResult.TotalSamples} test samples evaluated");
            Console.WriteLine($"   Classes : {string.Join(", ", cmResult.Labels)}");

            foreach (var m in cmResult.PerClass)
                Console.WriteLine($"   {m.Label,-20} F1={m.F1:P1}  Prec={m.Precision:P1}  Rec={m.Recall:P1}");

            // ── Build per-class image counts ──────────────────────────────────
            var classCounts = new Dictionary<string, int>();
            foreach (var lbl in classLabels)
            {
                int cnt = File.ReadLines(csvPath)
                    .Count(line => line.EndsWith($",{lbl}")
                               || line.Contains($",{lbl},"));
                classCounts[lbl] = cnt;
            }

            var sessionEnd = DateTime.Now;

            // ── Collect artifact paths ────────────────────────────────────────
            var ftLogPath2 = Path.Combine(_basePath, "fasttree_tuning_log.csv");
            var lgbLogPath2 = Path.Combine(_basePath, "lgb_tuning_log.csv");
            var reportHtmlPath = Path.ChangeExtension(modelPath, ".report.html");

            var artifactPaths = new List<string> { modelPath, cfgPath, reportHtmlPath };
            if (useFastTree && File.Exists(ftLogPath2)) artifactPaths.Add(ftLogPath2);
            if (useLightGBM && File.Exists(lgbLogPath2)) artifactPaths.Add(lgbLogPath2);

            // ── Build tuner run list (only include tuners that actually ran) ───
            var tunerRuns = new List<TunerRunResult>();
            if (ftRun != null) tunerRuns.Add(ftRun);
            if (lgbRun != null) tunerRuns.Add(lgbRun);

            // ── Generate HTML training report ─────────────────────────────────
            Console.WriteLine("\n📄 Generating training report...");

            var reportInput = new ReportInput
            {
                SessionStart = sessionStart,
                SessionEnd = sessionEnd,
                SourcePath = _basePath,
                OutputPath = _basePath,
                CVFolds = cvFolds,
                TuningTrials = trials,

                Dataset = new DatasetStats
                {
                    TotalImages = (int)(data.GetRowCount() ?? 0),
                    ClassCounts = classCounts,
                    ImageFormat = isGray ? "Grayscale 8bpp" : "RGB 24bpp",
                    ImageWidth = imageWidth,
                    ImageHeight = imageHeight,
                    TrainCount = (int)(split.TrainSet.GetRowCount() ?? 0),
                    TestCount = (int)(split.TestSet.GetRowCount() ?? 0),
                },

                Pipeline = new PipelineConfig
                {
                    ImageWidth = imageWidth,
                    ImageHeight = imageHeight,
                    IsGrayscale = isGray,
                    PcaRank = 100,
                    AbsolutePaths = absolutePaths,
                    ClassCount = classLabels.Count,
                },

                TunerResults = tunerRuns,

                ModelResults = results
                    .OrderByDescending(r => r.Macro)
                    .Select(r => new ReportModelResult
                    {
                        Name = r.Name,
                        MacroAccuracy = r.Macro,
                        MicroAccuracy = r.Micro,
                        LogLoss = r.LogLoss,
                        TrainTimeSeconds = r.TrainSec,
                    }).ToList(),

                BestModelName = best.Name,
                ModelZipPath = modelPath,
                ConfusionMatrix = cmResult,
                ArtifactPaths = artifactPaths,
            };

            // Override the report path so it sits next to the .zip
            var writtenReport = ReportGenerator.Write(reportInput);
            Console.WriteLine($"✅ Report saved: {Path.GetFileName(writtenReport)}");
            Console.WriteLine($"   Location: {writtenReport}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HOG feature extractor (unchanged)
        // ─────────────────────────────────────────────────────────────────────

        public class HogFeatureExtractor
        {
            private readonly int _cellSize;
            private readonly int _numBins;

            public HogFeatureExtractor(int cellSize = 8, int numBins = 9)
            {
                _cellSize = cellSize;
                _numBins = numBins;
            }

            public float[] Extract(Bitmap bmp)
            {
                int width = bmp.Width;
                int height = bmp.Height;
                var gray = new float[width * height];

                var bmpData = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format24bppRgb);

                try
                {
                    int stride = bmpData.Stride;
                    int byteCount = Math.Abs(stride) * height;
                    var pixels = new byte[byteCount];
                    Marshal.Copy(bmpData.Scan0, pixels, 0, byteCount);

                    for (int y = 0; y < height; y++)
                    {
                        int rowOffset = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int offset = rowOffset + x * 3;
                            byte b = pixels[offset];
                            byte g = pixels[offset + 1];
                            byte r = pixels[offset + 2];
                            gray[y * width + x] = (0.299f * r + 0.587f * g + 0.114f * b) / 255f;
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                var magX = new float[width * height];
                var magY = new float[width * height];

                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        int idx = y * width + x;
                        magX[idx] = gray[idx + 1] - gray[idx - 1];
                        magY[idx] = gray[idx + width] - gray[idx - width];
                    }
                }

                int cellsX = width / _cellSize;
                int cellsY = height / _cellSize;
                var features = new float[cellsX * cellsY * _numBins];
                int featureIdx = 0;

                for (int cx = 0; cx < cellsX; cx++)
                {
                    for (int cy = 0; cy < cellsY; cy++)
                    {
                        var hist = new float[_numBins];

                        for (int px = 0; px < _cellSize; px++)
                        {
                            for (int py = 0; py < _cellSize; py++)
                            {
                                int x = cx * _cellSize + px;
                                int y = cy * _cellSize + py;
                                int idx = y * width + x;

                                float gx = magX[idx];
                                float gy = magY[idx];
                                float magnitude = (float)Math.Sqrt(gx * gx + gy * gy);
                                float angle = (float)(Math.Atan2(Math.Abs(gy), Math.Abs(gx)) * 180.0 / Math.PI);

                                int bin = (int)(angle / (180.0f / _numBins)) % _numBins;
                                hist[bin] += magnitude;
                            }
                        }

                        float normSq = 1e-6f;
                        for (int i = 0; i < _numBins; i++)
                            normSq += hist[i] * hist[i];
                        float norm = (float)Math.Sqrt(normSq);

                        for (int i = 0; i < _numBins; i++)
                            features[featureIdx++] = hist[i] / norm;
                    }
                }

                return features;
            }
        }

        private string BuildHogCsv(string originalCsvPath, int imageWidth, int imageHeight, CancellationToken ct)
        {
            Console.WriteLine("\n🔧 Extracting HOG features from images...");
            var hogCsvPath = Path.Combine(_basePath, "images_hog.csv");
            var hog = new HogFeatureExtractor(cellSize: 8, numBins: 9);

            int total = File.ReadLines(originalCsvPath).Count();
            int done = 0;

            using var sw = new StreamWriter(hogCsvPath);

            foreach (var line in File.ReadLines(originalCsvPath))
            {
                ct.ThrowIfCancellationRequested();

                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var imgPath = parts[0].Trim().Trim('"');
                var label = parts[1].Trim().Trim('"');

                if (!File.Exists(imgPath)) continue;

                try
                {
                    using var bmp = new Bitmap(imgPath);
                    using var resized = new Bitmap(bmp, new Size(imageWidth, imageHeight));
                    var features = hog.Extract(resized);

                    sw.WriteLine($"{label},{string.Join(",", features.Select(f => f.ToString("F6")))}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️ Skipped {Path.GetFileName(imgPath)}: {ex.Message}");
                }

                done++;
                if (done % 20 == 0)
                    Console.WriteLine($"   HOG: {done}/{total} images processed...");
            }

            Console.WriteLine($"✅ HOG CSV created: {Path.GetFileName(hogCsvPath)}");
            Console.WriteLine($"   Features per image: {(imageWidth / 8) * (imageHeight / 8) * 9}");
            return hogCsvPath;
        }
    }
}