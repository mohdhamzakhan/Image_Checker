using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using static Google.Protobuf.Compiler.CodeGeneratorResponse.Types;

namespace Image_Checker.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    //  NOTE: The following types have been moved to dedicated files.
    //  Do NOT re-declare them here – doing so causes CS0101 duplicate errors.
    //    TaskType             → TaskType.cs
    //    AlgorithmOptions     → DataTrainerTypes.cs
    //    DataTrainerConfig    → DataTrainerTypes.cs
    //    TimeSeriesOptions    → DataTrainerTypes.cs
    //    ModelResult          → DataTrainerTypes.cs
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    //  MAIN TRAINER CLASS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic tabular + time-series model trainer.
    /// Supports binary classification, multiclass classification,
    /// regression and SSA-based time-series forecasting.
    /// </summary>
    public class DataModelTrainer
    {
        private readonly MLContext _ml;
        private readonly DataTrainerConfig _cfg;

        // Discovered schema info
        private string[] _allColumns = Array.Empty<string>();
        private string[] _featureColumns = Array.Empty<string>();
        private string[] _categoricalCols = Array.Empty<string>();
        private string[] _numericCols = Array.Empty<string>();
        private TaskType _resolvedTask = TaskType.BinaryClassification;

        public DataModelTrainer(MLContext mlContext, DataTrainerConfig config)
        {
            _ml = mlContext ?? throw new ArgumentNullException(nameof(mlContext));
            _cfg = config ?? throw new ArgumentNullException(nameof(config));
        }

        // ─── Public entry-point ───────────────────────────────────────────────

        /// <summary>
        /// Runs the full train → evaluate → save pipeline.
        /// Returns the path to the saved model zip, or null on failure.
        /// </summary>
        public string? TrainAndEvaluate(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PrintHeader();

            // ── 1. Validate config ────────────────────────────────────────────
            if (!ValidateConfig()) return null;

            // ── 2. Load raw data and infer schema ─────────────────────────────
            Console.WriteLine("\n📂 Loading data file...");
            var rawData = LoadRawData();
            if (rawData == null) return null;

            InferSchema(rawData);
            cancellationToken.ThrowIfCancellationRequested();

            // ── 3. Resolve task type ──────────────────────────────────────────
            _resolvedTask = ResolveTaskType(rawData);
            Console.WriteLine($"\n🎯 Task type resolved: {_resolvedTask}");

            // ── 4. TimeSeries branch ─────────────────────────────────────────
            // When task = TimeSeries we run BOTH pipelines:
            //   (a) SSA  – univariate forecasting on the label column alone
            //   (b) Regression – all selected algorithms on all feature columns
            //
            // This is important because SSA ignores every feature column except
            // the label, while regression can leverage PARTY_NAME, ITEM_ID etc.
            // The best tabular model path is returned (SSA model is also saved
            // as a side-effect).  If the user explicitly chose only TimeSeries
            // and has no numeric feature columns, only SSA runs.
            if (_resolvedTask == TaskType.TimeSeries)
            {
                // Always run SSA
                var ssaPath = RunTimeSeriesPipeline(rawData, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Also run tabular regression if there are numeric feature columns
                if (_numericCols.Length > 0 || _categoricalCols.Length > 0)
                {
                    Console.WriteLine(
                        "
🔄 Also running Regression pipeline on all feature columns...");
                    Console.WriteLine(
                        "   (SSA uses only the label series; regression uses all features)");

                    // Switch resolved task to Regression for the tabular run
                    _resolvedTask = TaskType.Regression;
                    var tabularPath = RunTabularPipeline(rawData, cancellationToken);

                    // Restore resolved task for accurate reporting
                    _resolvedTask = TaskType.TimeSeries;

                    // Return the tabular model path as the primary result
                    // (SSA model was already saved above)
                    return tabularPath ?? ssaPath;
                }

                return ssaPath;
            }

            // ── 5. Tabular branch ─────────────────────────────────────────────
            return RunTabularPipeline(rawData, cancellationToken);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TIME-SERIES PIPELINE
        // ─────────────────────────────────────────────────────────────────────

        private string? RunTimeSeriesPipeline(IDataView rawData, CancellationToken ct)
        {
            Console.WriteLine("\n📈 Running SSA Time-Series pipeline...");
            var ts = _cfg.TimeSeries;

            // ── Step 1: Build a view that contains ONLY the float label column ──
            // ForecastBySsa.inputColumnName must resolve to a Single (float) typed
            // column in the schema.  We project the full data down to just that one
            // column so there is no ambiguity regardless of what other columns exist.
            IEstimator<ITransformer> projectStep;
            if (_cfg.LabelColumnName == "Value")
            {
                // Column is already named "Value" – just copy to confirm it is float
                projectStep = _ml.Transforms.CopyColumns("Value", _cfg.LabelColumnName);
            }
            else
            {
                // Rename user's column to "Value" so SSA always finds it
                projectStep = _ml.Transforms.CopyColumns("Value", _cfg.LabelColumnName);
            }

            Console.WriteLine($"   • Projecting label '{_cfg.LabelColumnName}' → 'Value' (float)");
            var projector = projectStep.Fit(rawData);
            var projected = projector.Transform(rawData);   // schema now has "Value" : Single

            // ── Step 2: Row count (materialise via GetColumn, not DynamicRow) ───
            long totalRows = projected.GetColumn<float>("Value").LongCount();
            Console.WriteLine($"   • Total rows in series: {totalRows:N0}");

            // ── Step 3: Compute parameters ────────────────────────────────────
            int trainSize = ts.TrainSize > 0
                ? ts.TrainSize
                : (int)(totalRows * (1.0 - _cfg.TestFraction));

            if (trainSize < 4)
            {
                Console.WriteLine($"   ❌ Not enough rows for SSA training (need ≥ 4, got {trainSize}).");
                return null;
            }

            int windowSize = ts.WindowSize;
            int horizon = ts.HorizonSteps;
            int seriesLen = ts.SeriesLength > 0 ? ts.SeriesLength : trainSize;

            // SSA constraint: windowSize must be > horizon and ≤ seriesLength/2
            if (windowSize <= horizon)
            {
                windowSize = horizon + 1;
                Console.WriteLine($"   ⚠️  WindowSize auto-adjusted to {windowSize} (must be > horizon {horizon}).");
            }
            if (windowSize > seriesLen / 2)
            {
                windowSize = Math.Max(horizon + 1, seriesLen / 2);
                Console.WriteLine($"   ⚠️  WindowSize capped to {windowSize} (must be ≤ SeriesLength/2).");
            }
            if (seriesLen > trainSize)
            {
                seriesLen = trainSize;
                Console.WriteLine($"   ⚠️  SeriesLength capped to {seriesLen} (cannot exceed trainSize).");
            }

            Console.WriteLine($"   • Label column  : {_cfg.LabelColumnName} → 'Value'");
            Console.WriteLine($"   • Total rows    : {totalRows:N0}");
            Console.WriteLine($"   • Train size    : {trainSize}");
            Console.WriteLine($"   • Horizon       : {horizon}");
            Console.WriteLine($"   • Window size   : {windowSize}");
            Console.WriteLine($"   • Series length : {seriesLen}");
            Console.WriteLine($"   • Confidence    : {ts.ConfidenceLevel:P0}");

            if (!string.IsNullOrWhiteSpace(ts.DateColumn))
                Console.WriteLine($"   ⚠️  Date column '{ts.DateColumn}' used for reference only. " +
                                  $"Ensure data is pre-sorted by date for accurate SSA results.");

            ct.ThrowIfCancellationRequested();

            try
            {
                // ── Step 4: Build and fit the SSA forecaster on "Value" ─────────
                var ssaPipeline = _ml.Forecasting.ForecastBySsa(
                    outputColumnName: "Forecast",
                    inputColumnName: "Value",      // always "Value" after projection
                    windowSize: windowSize,
                    seriesLength: seriesLen,
                    trainSize: trainSize,
                    horizon: horizon,
                    confidenceLevel: ts.ConfidenceLevel,
                    confidenceLowerBoundColumn: "LowerBound",
                    confidenceUpperBoundColumn: "UpperBound");

                Console.WriteLine("\n   ⏳ Fitting SSA model...");
                var ssaModel = ssaPipeline.Fit(projected);
                Console.WriteLine("   ✅ SSA model fitted");

                ct.ThrowIfCancellationRequested();

                // ── Step 5: In-sample evaluation ─────────────────────────────────
                var transformed = ssaModel.Transform(projected);
                var forecastCol = transformed.GetColumn<float[]>("Forecast").ToList();
                var actualCol = transformed.GetColumn<float>("Value").ToList();

                double mae = 0, rmse = 0;
                int evalN = Math.Min(forecastCol.Count, actualCol.Count);
                for (int i = 0; i < evalN; i++)
                {
                    if (forecastCol[i] == null || forecastCol[i].Length == 0) continue;
                    double err = actualCol[i] - forecastCol[i][0];
                    mae += Math.Abs(err);
                    rmse += err * err;
                }
                if (evalN > 0) { mae /= evalN; rmse = Math.Sqrt(rmse / evalN); }

                Console.WriteLine($"\n📊 SSA In-sample metrics:");
                Console.WriteLine($"   • MAE  : {mae:F4}");
                Console.WriteLine($"   • RMSE : {rmse:F4}");

                // ── Step 6: Print next-horizon forecast ───────────────────────────
                Console.WriteLine($"\n🔮 Next {horizon}-step forecast:");
                var eng = ssaModel.CreateTimeSeriesEngine<TsValueRow, TsForecastRow>(_ml);
                var forecast = eng.Predict();
                for (int i = 0; i < forecast.Forecast.Length; i++)
                {
                    string lb = forecast.LowerBound.Length > i ? forecast.LowerBound[i].ToString("F4") : "—";
                    string ub = forecast.UpperBound.Length > i ? forecast.UpperBound[i].ToString("F4") : "—";
                    Console.WriteLine($"   Step {i + 1,3}: {forecast.Forecast[i]:F4}  [{lb} – {ub}]");
                }

                ct.ThrowIfCancellationRequested();

                // ── Step 7: Save ONLY the ssaModel ───────────────────────────────
                // We deliberately do NOT save the combined (CopyColumns → SSA) pipeline.
                //
                // Reason: if we save combinedPipeline.Fit(rawData), the saved model's
                // input schema requires the ORIGINAL column name (e.g. "QUANTITY_INVOICED").
                // At prediction time ModelPredictionForm builds a dummy IDataView with only
                // a "Value" column — that would cause:
                //   "Could not find input column 'QUANTITY_INVOICED'"
                //
                // By saving ssaModel directly its input schema is just { "Value": Single },
                // which matches the dummy view perfectly.
                return SaveTimeSeriesModel(ssaModel, projected.Schema, mae, rmse);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Time-series training failed: {ex.GetBaseException().Message}");
                Console.WriteLine($"   Details: {ex}");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TABULAR PIPELINE
        // ─────────────────────────────────────────────────────────────────────

        private string? RunTabularPipeline(IDataView rawData, CancellationToken ct)
        {
            // ── Split ─────────────────────────────────────────────────────────
            Console.WriteLine($"\n✂️  Splitting data: {(1 - _cfg.TestFraction):P0} train / {_cfg.TestFraction:P0} test...");
            var split = _ml.Data.TrainTestSplit(rawData, _cfg.TestFraction, seed: _cfg.Seed);
            Console.WriteLine("   ✅ Split complete");

            ct.ThrowIfCancellationRequested();

            // ── Build preprocessing pipeline ──────────────────────────────────
            Console.WriteLine("\n🔧 Building preprocessing pipeline...");
            var preprocess = BuildPreprocessingPipeline();
            PrintPreprocessingSummary();

            ct.ThrowIfCancellationRequested();

            // ── Fit preprocessor + cache ──────────────────────────────────────
            Console.WriteLine("\n⚙️  Fitting preprocessing pipeline...");
            var preprocessModel = preprocess.Fit(rawData);   // fit on ALL data so encoders see all categories
            var trainTransformed = preprocessModel.Transform(split.TrainSet);
            var cachedTrain = _ml.Data.Cache(trainTransformed);
            Console.WriteLine("   ✅ Preprocessing fitted, training data cached");

            ct.ThrowIfCancellationRequested();

            // ── Build trainer list ────────────────────────────────────────────
            var trainers = BuildTrainerList(ct);

            if (trainers.Count == 0)
            {
                Console.WriteLine("\n❌ No algorithms selected. Enable at least one in AlgorithmOptions.");
                return null;
            }

            // ── Train & evaluate ──────────────────────────────────────────────
            Console.WriteLine($"\n🚀 Evaluating {trainers.Count} model(s) on test set...");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var results = new List<ModelResult>();
            int modelNum = 1;

            foreach (var (name, trainer) in trainers)
            {
                ct.ThrowIfCancellationRequested();
                Console.WriteLine($"\n[{modelNum}/{trainers.Count}] ▶  {name}");
                Console.WriteLine("───────────────────────────────────────────────────────────");

                try
                {
                    IEstimator<ITransformer> fullPipeline = preprocess
                        .Append(trainer)
                        .Append(_ml.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));

                    Console.WriteLine("   ⏳ Training...");
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var model = fullPipeline.Fit(split.TrainSet);
                    sw.Stop();
                    Console.WriteLine($"   ✅ Trained in {sw.Elapsed.TotalSeconds:F1}s");

                    ct.ThrowIfCancellationRequested();

                    Console.WriteLine("   📊 Evaluating on test set...");
                    var preds = model.Transform(split.TestSet);
                    var result = EvaluateModel(name, preds, model);

                    PrintModelMetrics(result);
                    results.Add(result);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Failed: {ex.GetBaseException().Message}");
                }

                modelNum++;
            }

            ct.ThrowIfCancellationRequested();

            if (!results.Any())
            {
                Console.WriteLine("\n❌ No models completed successfully.");
                return null;
            }

            // ── Print comparison table ────────────────────────────────────────
            PrintComparisonTable(results);

            // ── Save best model ───────────────────────────────────────────────
            var best = results.OrderByDescending(r => r.PrimaryMetric).First();
            return SaveTabularModel(best, rawData.Schema);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PREPROCESSING PIPELINE BUILDER
        // ─────────────────────────────────────────────────────────────────────

        private IEstimator<ITransformer> BuildPreprocessingPipeline()
        {
            // Build a list of steps, then chain them all through EstimatorChain
            // using IEstimator<ITransformer> at every level to avoid the
            // EstimatorChain<TSpecific> → EstimatorChain<ITransformer> implicit
            // conversion error that occurs when using 'var'.
            var steps = new List<IEstimator<ITransformer>>();

            // Step 1 – Replace missing values in numeric columns
            foreach (var col in _numericCols)
            {
                steps.Add(_ml.Transforms.ReplaceMissingValues(col, col,
                    Microsoft.ML.Transforms.MissingValueReplacingEstimator.ReplacementMode.Mean));
            }

            // Step 2 – One-hot encode categorical columns
            foreach (var col in _categoricalCols)
            {
                steps.Add(_ml.Transforms.Categorical.OneHotEncoding(col + "_OHE", col));
            }

            // Step 3 – Map label to key (classification) or cast to float (regression)
            if (_resolvedTask is TaskType.BinaryClassification or TaskType.MulticlassClassification)
            {
                steps.Add(_ml.Transforms.Conversion.MapValueToKey("Label", _cfg.LabelColumnName));
            }
            else
            {
                steps.Add(_ml.Transforms.Conversion.ConvertType(
                    "Label", _cfg.LabelColumnName, DataKind.Single));
            }

            // Step 4 – Concatenate all feature columns into "Features" vector
            var featureCols = _numericCols
                .Concat(_categoricalCols.Select(c => c + "_OHE"))
                .ToArray();
            steps.Add(_ml.Transforms.Concatenate("Features", featureCols));

            // Step 5 – Normalize into "Features_Norm" for linear models
            // (tree models use the raw "Features" column instead)
            steps.Add(_ml.Transforms.NormalizeMeanVariance("Features_Norm", "Features"));

            // Chain all steps: seed with the first, then fold the rest in
            IEstimator<ITransformer> pipeline = steps[0];
            for (int i = 1; i < steps.Count; i++)
                pipeline = pipeline.Append(steps[i]);

            return pipeline;
        }

        private void PrintPreprocessingSummary()
        {
            Console.WriteLine($"   • Missing-value imputation   : Mean (numeric cols)");
            Console.WriteLine($"   • One-hot encoding           : {_categoricalCols.Length} column(s) → {string.Join(", ", _categoricalCols)}");
            Console.WriteLine($"   • Numeric features           : {_numericCols.Length} column(s)");
            Console.WriteLine($"   • Feature concatenation      : {_numericCols.Length + _categoricalCols.Length} total feature source(s)");
            Console.WriteLine($"   • Normalization              : Z-score (stddev) → 'Features_Norm'");
            Console.WriteLine($"   • Label column               : '{_cfg.LabelColumnName}' → 'Label'");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TRAINER LIST BUILDER
        // ─────────────────────────────────────────────────────────────────────

        private List<(string Name, IEstimator<ITransformer> Trainer)> BuildTrainerList(
            CancellationToken ct)
        {
            var list = new List<(string, IEstimator<ITransformer>)>();
            var alg = _cfg.Algorithms;

            // Choose the correct feature column name:
            // linear models work best with normalised features,
            // tree models prefer raw features.
            const string RAW = "Features";
            const string NORM = "Features_Norm";

            if (_resolvedTask == TaskType.BinaryClassification)
            {
                if (alg.UseSDCA)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("SDCA_Binary",
                        _ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                            labelColumnName: "Label", featureColumnName: NORM)));
                }
                if (alg.UseLBFGS)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("LBFGS_Binary",
                        _ml.BinaryClassification.Trainers.LbfgsLogisticRegression(
                            labelColumnName: "Label", featureColumnName: NORM)));
                }
                if (alg.UseAveragedPerceptron)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("AveragedPerceptron",
                        _ml.BinaryClassification.Trainers.AveragedPerceptron(
                            labelColumnName: "Label", featureColumnName: NORM)));
                }
                if (alg.UseFastTree)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("FastTree_Binary",
                        _ml.BinaryClassification.Trainers.FastTree(
                            labelColumnName: "Label", featureColumnName: RAW,
                            numberOfLeaves: 20, numberOfTrees: 100, learningRate: 0.2)));
                }
                if (alg.UseFastForest)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("FastForest_Binary",
                        _ml.BinaryClassification.Trainers.FastForest(
                            labelColumnName: "Label", featureColumnName: RAW,
                            numberOfLeaves: 20, numberOfTrees: 100)));
                }
                if (alg.UseLightGBM)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("LightGBM_Binary",
                        _ml.BinaryClassification.Trainers.LightGbm(
                            labelColumnName: "Label", featureColumnName: RAW,
                            numberOfLeaves: 31, learningRate: 0.05, numberOfIterations: 300)));
                }
            }
            else if (_resolvedTask == TaskType.MulticlassClassification)
            {
                if (alg.UseSDCA)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("SDCA_MaxEnt",
                        _ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                            labelColumnName: "Label", featureColumnName: NORM)));
                }
                if (alg.UseLBFGS)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("LBFGS_MaxEnt",
                        _ml.MulticlassClassification.Trainers.LbfgsMaximumEntropy(
                            labelColumnName: "Label", featureColumnName: NORM)));
                }
                if (alg.UseFastTree)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("FastTree_OVA",
                        _ml.MulticlassClassification.Trainers.OneVersusAll(
                            _ml.BinaryClassification.Trainers.FastTree("Label", RAW,
                                numberOfLeaves: 20, numberOfTrees: 100, learningRate: 0.2))));
                }
                if (alg.UseFastForest)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("FastForest_OVA",
                        _ml.MulticlassClassification.Trainers.OneVersusAll(
                            _ml.BinaryClassification.Trainers.FastForest("Label", RAW,
                                numberOfLeaves: 20, numberOfTrees: 100))));
                }
                if (alg.UseLightGBM)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("LightGBM_Multi",
                        _ml.MulticlassClassification.Trainers.LightGbm(
                            labelColumnName: "Label", featureColumnName: RAW,
                            numberOfLeaves: 31, learningRate: 0.05f, numberOfIterations: 300)));
                }
            }
            else // Regression
            {
                if (alg.UseSdcaRegression)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("SDCA_Regression",
                        _ml.Regression.Trainers.Sdca(
                            labelColumnName: "Label", featureColumnName: NORM)));
                }
                if (alg.UseOlsRegression)
                {
                    ct.ThrowIfCancellationRequested();
                    // OnlineGradientDescent is the always-available linear regression baseline.
                    // OLS requires Microsoft.ML.Mkl.Components – swap back if that package is present.
                    list.Add(("LinearSGD_Regression",
                        _ml.Regression.Trainers.OnlineGradientDescent(
                            labelColumnName: "Label", featureColumnName: NORM)));
                }
                if (alg.UseFastTreeRegression)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("FastTree_Regression",
                        _ml.Regression.Trainers.FastTree(
                            labelColumnName: "Label", featureColumnName: RAW,
                            numberOfLeaves: 20, numberOfTrees: 100, learningRate: 0.2)));
                }
                if (alg.UseFastForestRegression)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("FastForest_Regression",
                        _ml.Regression.Trainers.FastForest(
                            labelColumnName: "Label", featureColumnName: RAW,
                            numberOfLeaves: 20, numberOfTrees: 100)));
                }
                if (alg.UseLightGbmRegression)
                {
                    ct.ThrowIfCancellationRequested();
                    list.Add(("LightGBM_Regression",
                        _ml.Regression.Trainers.LightGbm(
                            labelColumnName: "Label", featureColumnName: RAW,
                            numberOfLeaves: 31, learningRate: 0.05f, numberOfIterations: 300)));
                }
            }

            Console.WriteLine($"\n🎯 Registered {list.Count} trainer(s):");
            foreach (var (n, _) in list)
                Console.WriteLine($"   • {n}");

            return list;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EVALUATION
        // ─────────────────────────────────────────────────────────────────────

        private ModelResult EvaluateModel(string name, IDataView preds, ITransformer model)
        {
            var result = new ModelResult { Name = name, Task = _resolvedTask, Model = model };

            switch (_resolvedTask)
            {
                case TaskType.BinaryClassification:
                    {
                        var m = _ml.BinaryClassification.Evaluate(preds, "Label", "Score");
                        result.PrimaryMetric = m.Accuracy;
                        result.SecondaryMetric = m.AreaUnderRocCurve;
                        result.TertiaryMetric = m.F1Score;
                        break;
                    }
                case TaskType.MulticlassClassification:
                    {
                        var m = _ml.MulticlassClassification.Evaluate(preds, "Label");
                        result.PrimaryMetric = m.MacroAccuracy;
                        result.SecondaryMetric = m.MicroAccuracy;
                        result.TertiaryMetric = m.LogLoss;
                        break;
                    }
                case TaskType.Regression:
                    {
                        var m = _ml.Regression.Evaluate(preds, "Label", "Score");
                        result.PrimaryMetric = m.RSquared;
                        result.SecondaryMetric = m.MeanAbsoluteError;
                        result.TertiaryMetric = m.RootMeanSquaredError;
                        break;
                    }
            }

            return result;
        }

        private void PrintModelMetrics(ModelResult r)
        {
            switch (r.Task)
            {
                case TaskType.BinaryClassification:
                    Console.WriteLine($"   • Accuracy : {r.PrimaryMetric:P2}");
                    Console.WriteLine($"   • AUC-ROC  : {r.SecondaryMetric:P2}");
                    Console.WriteLine($"   • F1 Score : {r.TertiaryMetric:P2}");
                    break;
                case TaskType.MulticlassClassification:
                    Console.WriteLine($"   • Macro Acc: {r.PrimaryMetric:P2}");
                    Console.WriteLine($"   • Micro Acc: {r.SecondaryMetric:P2}");
                    Console.WriteLine($"   • Log Loss : {r.TertiaryMetric:F4}");
                    break;
                case TaskType.Regression:
                    Console.WriteLine($"   • R²       : {r.PrimaryMetric:F4}");
                    Console.WriteLine($"   • MAE      : {r.SecondaryMetric:F4}");
                    Console.WriteLine($"   • RMSE     : {r.TertiaryMetric:F4}");
                    break;
            }
        }

        private void PrintComparisonTable(List<ModelResult> results)
        {
            Console.WriteLine("\n\n═══════════════════════════════════════════════════════════════════");
            Console.WriteLine("📊  FINAL MODEL COMPARISON");
            Console.WriteLine($"   Task: {_resolvedTask}  |  Label: '{_cfg.LabelColumnName}'");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════");

            switch (_resolvedTask)
            {
                case TaskType.BinaryClassification:
                    Console.WriteLine($"{"Model",-35} {"Accuracy",10} {"AUC-ROC",10} {"F1",10}");
                    Console.WriteLine(new string('─', 68));
                    foreach (var r in results.OrderByDescending(r => r.PrimaryMetric))
                        Console.WriteLine($"{r.Name,-35} {r.PrimaryMetric,10:P2} {r.SecondaryMetric,10:P2} {r.TertiaryMetric,10:P2}");
                    break;

                case TaskType.MulticlassClassification:
                    Console.WriteLine($"{"Model",-35} {"MacroAcc",10} {"MicroAcc",10} {"LogLoss",10}");
                    Console.WriteLine(new string('─', 68));
                    foreach (var r in results.OrderByDescending(r => r.PrimaryMetric))
                        Console.WriteLine($"{r.Name,-35} {r.PrimaryMetric,10:P2} {r.SecondaryMetric,10:P2} {r.TertiaryMetric,10:F4}");
                    break;

                case TaskType.Regression:
                    Console.WriteLine($"{"Model",-35} {"R²",10} {"MAE",10} {"RMSE",10}");
                    Console.WriteLine(new string('─', 68));
                    foreach (var r in results.OrderByDescending(r => r.PrimaryMetric))
                        Console.WriteLine($"{r.Name,-35} {r.PrimaryMetric,10:F4} {r.SecondaryMetric,10:F4} {r.TertiaryMetric,10:F4}");
                    break;
            }

            var best = results.OrderByDescending(r => r.PrimaryMetric).First();
            Console.WriteLine($"\n🏆  BEST MODEL : {best.Name}");
            PrintModelMetrics(best);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SAVE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private string SaveTabularModel(ModelResult best, DataViewSchema schema)
        {
            var outDir = ResolveOutputDir();
            var safeName = SanitizeFileName(best.Name);
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var zipPath = Path.Combine(outDir, $"bestModel-{safeName}-{stamp}.zip");
            var cfgPath = Path.ChangeExtension(zipPath, ".json");

            Console.WriteLine($"\n💾 Saving best model → {Path.GetFileName(zipPath)}");
            _ml.Model.Save(best.Model!, schema, zipPath);

            var config = new
            {
                Task = _resolvedTask.ToString(),
                LabelColumn = _cfg.LabelColumnName,
                FeatureColumns = _featureColumns,
                CategoricalColumns = _categoricalCols,
                NumericColumns = _numericCols,
                TrainedAt = DateTime.Now.ToString("o"),
                BestModel = best.Name,
                PrimaryMetric = best.PrimaryMetric,
                SecondaryMetric = best.SecondaryMetric,
                TertiaryMetric = best.TertiaryMetric
            };

            File.WriteAllText(cfgPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"   ✅ Saved: {zipPath}");
            Console.WriteLine($"   📄 Config: {cfgPath}");
            return zipPath;
        }

        private string SaveTimeSeriesModel(ITransformer model, DataViewSchema schema,
                                           double mae, double rmse)
        {
            var outDir = ResolveOutputDir();
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var zipPath = Path.Combine(outDir, $"bestModel-SSA-{stamp}.zip");
            var cfgPath = Path.ChangeExtension(zipPath, ".json");

            Console.WriteLine($"\n💾 Saving SSA model → {Path.GetFileName(zipPath)}");
            _ml.Model.Save(model, schema, zipPath);

            var config = new
            {
                Task = "TimeSeries",
                LabelColumn = _cfg.LabelColumnName,
                DateColumn = _cfg.TimeSeries.DateColumn,
                HorizonSteps = _cfg.TimeSeries.HorizonSteps,
                Granularity = _cfg.TimeSeries.Granularity,   // Day / Month / Year
                WindowSize = _cfg.TimeSeries.WindowSize,
                SeriesLength = _cfg.TimeSeries.SeriesLength,
                TrainedAt = DateTime.Now.ToString("o"),
                MAE = mae,
                RMSE = rmse
            };

            File.WriteAllText(cfgPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"   ✅ Saved: {zipPath}");
            Console.WriteLine($"   📄 Config: {cfgPath}");
            return zipPath;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SCHEMA INFERENCE
        // ─────────────────────────────────────────────────────────────────────

        private void InferSchema(IDataView data)
        {
            Console.WriteLine("\n🔍 Inferring schema...");

            _allColumns = data.Schema
                .Select(c => c.Name)
                .Where(n => n != _cfg.LabelColumnName)
                .ToArray();

            // Apply user's FeatureColumns filter
            var selected = _cfg.FeatureColumns.Count > 0
                ? _allColumns.Intersect(_cfg.FeatureColumns).ToArray()
                : _allColumns;

            // Remove ignored columns
            selected = selected.Except(_cfg.IgnoreColumns).ToArray();

            // Separate categorical vs numeric
            var catUserDefined = _cfg.CategoricalColumns.Count > 0;

            if (catUserDefined)
            {
                _categoricalCols = selected.Intersect(_cfg.CategoricalColumns).ToArray();
                _numericCols = selected.Except(_categoricalCols).ToArray();
            }
            else
            {
                // Auto-detect: columns with String type → categorical
                _categoricalCols = selected
                    .Where(col => data.Schema[col].Type == TextDataViewType.Instance)
                    .ToArray();
                _numericCols = selected.Except(_categoricalCols).ToArray();
            }

            _featureColumns = selected;

            Console.WriteLine($"   • Total columns   : {data.Schema.Count}");
            Console.WriteLine($"   • Feature columns : {_featureColumns.Length}");
            Console.WriteLine($"     → Numeric   ({_numericCols.Length}): {Truncate(string.Join(", ", _numericCols))}");
            Console.WriteLine($"     → Categoric ({_categoricalCols.Length}): {Truncate(string.Join(", ", _categoricalCols))}");
            Console.WriteLine($"   • Label column    : {_cfg.LabelColumnName}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TASK-TYPE RESOLUTION
        // ─────────────────────────────────────────────────────────────────────

        private TaskType ResolveTaskType(IDataView data)
        {
            if (_cfg.Task != TaskType.Auto) return _cfg.Task;

            // TimeSeries heuristic: user set TimeSeriesOptions.HorizonSteps > 0 with a numeric label
            if (_cfg.TimeSeries.HorizonSteps > 0 &&
                data.Schema[_cfg.LabelColumnName].Type != TextDataViewType.Instance)
                return TaskType.TimeSeries;

            // Is label column text / bool → classification
            var labelType = data.Schema[_cfg.LabelColumnName].Type;
            if (labelType == TextDataViewType.Instance)
                return TaskType.MulticlassClassification; // will map to key; binary resolved below

            // Sample distinct values to decide binary vs multi vs regression
            var distinctVals = data.GetColumn<float>(_cfg.LabelColumnName)
                                   .Distinct()
                                   .Take(20)
                                   .ToList();

            // If all values are 0 or 1 → binary
            if (distinctVals.All(v => v is 0f or 1f))
                return TaskType.BinaryClassification;

            // If a small integer set (≤ 20 distinct ints) → multiclass
            if (distinctVals.Count <= 20 &&
                distinctVals.All(v => v == MathF.Floor(v)))
                return TaskType.MulticlassClassification;

            // Otherwise → regression
            return TaskType.Regression;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DATA LOADING
        // ─────────────────────────────────────────────────────────────────────

        private IDataView? LoadRawData()
        {
            try
            {
                // Use TextLoader.Options with column inference
                var cols = InferTextLoaderColumns();

                var loader = _ml.Data.CreateTextLoader(new TextLoader.Options
                {
                    Separators = new[] { _cfg.Separator },
                    HasHeader = _cfg.HasHeader,
                    AllowQuoting = true,
                    TrimWhitespace = true,
                    Columns = cols
                });

                var data = loader.Load(_cfg.DataFilePath);

                // Count rows (materialises the view)
                long rowCount = data.GetRowCount() ?? -1;
                Console.WriteLine(rowCount >= 0
                    ? $"   ✅ Loaded {rowCount:N0} rows, {data.Schema.Count} columns"
                    : $"   ✅ Data loaded (row count deferred), {data.Schema.Count} columns");

                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Failed to load data: {ex.GetBaseException().Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads the header + first data row to infer column types for TextLoader.
        /// </summary>
        private TextLoader.Column[] InferTextLoaderColumns()
        {
            var lines = File.ReadLines(_cfg.DataFilePath).Take(_cfg.HasHeader ? 2 : 1).ToList();
            string[] headers;
            string[] firstRow;

            if (_cfg.HasHeader && lines.Count >= 2)
            {
                headers = lines[0].Split(_cfg.Separator);
                firstRow = lines[1].Split(_cfg.Separator);
            }
            else if (!_cfg.HasHeader && lines.Count >= 1)
            {
                firstRow = lines[0].Split(_cfg.Separator);
                headers = Enumerable.Range(0, firstRow.Length)
                                     .Select(i => $"Col{i}")
                                     .ToArray();
            }
            else
            {
                throw new InvalidOperationException("Data file is empty or unreadable.");
            }

            Console.WriteLine($"   • Detected {headers.Length} columns");

            var cols = new List<TextLoader.Column>();
            for (int i = 0; i < headers.Length; i++)
            {
                string name = headers[i].Trim().Trim('"');
                string rawVal = i < firstRow.Length ? firstRow[i].Trim().Trim('"') : "";

                // Always force the label column to Single (float).
                // This is critical for Time Series – ForecastBySsa requires a float
                // input column and will throw "Could not find input column" if the
                // column is typed as String even when the values are numeric.
                DataKind kind;
                if (name.Equals(_cfg.LabelColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    kind = DataKind.Single;
                }
                else
                {
                    bool isNum = float.TryParse(rawVal,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out _);
                    kind = isNum ? DataKind.Single : DataKind.String;
                }

                cols.Add(new TextLoader.Column(name, kind, i));
            }

            return cols.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  VALIDATION
        // ─────────────────────────────────────────────────────────────────────

        private bool ValidateConfig()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_cfg.DataFilePath))
                errors.Add("DataFilePath is required.");
            else if (!File.Exists(_cfg.DataFilePath))
                errors.Add($"DataFilePath not found: {_cfg.DataFilePath}");

            if (string.IsNullOrWhiteSpace(_cfg.LabelColumnName))
                errors.Add("LabelColumnName is required.");

            if (_cfg.TestFraction is <= 0 or >= 1)
                errors.Add("TestFraction must be between 0 and 1 (exclusive).");

            if (errors.Any())
            {
                Console.WriteLine("\n❌ Configuration errors:");
                errors.ForEach(e => Console.WriteLine($"   • {e}"));
                return false;
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  UTILITIES
        // ─────────────────────────────────────────────────────────────────────

        private string ResolveOutputDir()
        {
            var dir = string.IsNullOrWhiteSpace(_cfg.OutputDirectory)
                ? Path.GetDirectoryName(_cfg.DataFilePath)!
                : _cfg.OutputDirectory;

            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        }

        private static string Truncate(string s, int max = 80)
            => s.Length <= max ? s : s[..max] + "…";

        private static void PrintHeader()
        {
            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            DataModelTrainer  –  Tabular & Time-Series           ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  INNER TYPES (for SSA engine helper rows)
        // ─────────────────────────────────────────────────────────────────────

        // Input row for SSA engine.
        // [ColumnName("Value")] is required — NOT [LoadColumn(0)].
        // LoadFromEnumerable maps properties by name (or [ColumnName] attribute),
        // not by ordinal. Without this attribute the schema produced by
        // LoadFromEnumerable would have a column called "Value" (the property name)
        // but the attribute guarantees it even if the property name ever changes.
        // [LoadColumn(0)] is for TextLoader/CSV ordinal mapping — wrong here.
        private class TsValueRow
        {
            [ColumnName("Value")]
            public float Value { get; set; }
        }

        // Output row produced by ForecastBySsa
        private class TsForecastRow
        {
            public float[] Forecast { get; set; } = Array.Empty<float>();
            public float[] LowerBound { get; set; } = Array.Empty<float>();
            public float[] UpperBound { get; set; } = Array.Empty<float>();
        }
    }
}