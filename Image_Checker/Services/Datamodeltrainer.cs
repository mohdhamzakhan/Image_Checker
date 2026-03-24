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
                // ── (a) Always run SSA first ──────────────────────────────────────
                var ssaPath = RunTimeSeriesPipeline(rawData, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // ── (b) Also run tabular Regression for feature-based prediction ──
                // SSA forecasts overall quantity trend over time.
                // Regression predicts quantity for a specific customer+item combination.
                // Both are saved; the SSA path is returned as primary so the UI shows it.
                if (_numericCols.Length > 0 || _categoricalCols.Length > 0)
                {
                    Console.WriteLine(
                        "\n\U0001f504 Also running Regression pipeline on all feature columns...");
                    Console.WriteLine(
                        "   (SSA = time trend forecast; Regression = per-customer/item prediction)");

                    _resolvedTask = TaskType.Regression;
                    var tabularPath = RunTabularPipeline(rawData, cancellationToken);
                    _resolvedTask = TaskType.TimeSeries;

                    // Log both paths clearly so user knows which file does what
                    if (ssaPath != null)
                        Console.WriteLine($"\n\U0001f4c5 SSA forecast model  : {Path.GetFileName(ssaPath)}");
                    if (tabularPath != null)
                        Console.WriteLine($"\U0001f4ca Regression model     : {Path.GetFileName(tabularPath)}");
                    Console.WriteLine("   Load the SSA model for time-series forecasting.");
                    Console.WriteLine("   Load the Regression model for per-row quantity prediction.");

                    // Return SSA path as primary — that is what the user trained TS for.
                    // Regression is a bonus model saved alongside it.
                    return ssaPath ?? tabularPath;
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

            // ── Step 1: Aggregate by date period ─────────────────────────────────
            //
            // REASON: Transactional data has MANY rows per date
            // (e.g. 348,336 invoice rows → thousands of rows per month).
            // SSA requires exactly ONE value per time step, otherwise it treats
            // each transaction row as a separate step and produces meaningless
            // tiny forecasts (e.g. 257 per step instead of monthly total 50,000).
            //
            // When a date column is configured we:
            //   1. Read label (float) + date (string) columns into memory
            //   2. Normalise each date to the granularity period key
            //      Day  → "yyyy-MM-dd"
            //      Month→ "yyyy-MM"      ← typical for invoice data
            //      Year → "yyyy"
            //   3. GROUP BY period key, SUM the label values
            //   4. SORT ascending (SSA needs chronological order)
            //   5. Reload as a new IDataView { "Value": Single }
            //
            // Example: 348,336 rows → 24 monthly sums → SSA trains on 24 real points.

            IDataView seriesView;

            if (!string.IsNullOrWhiteSpace(ts.DateColumn))
            {
                Console.WriteLine($"   • Date column '{ts.DateColumn}' detected — aggregating by {ts.Granularity}...");

                // Read label values (already typed as float by InferTextLoaderColumns)
                var labelValues = rawData.GetColumn<float>(_cfg.LabelColumnName).ToList();

                // Read date column. It may be typed as String or Single depending on content.
                List<string> dateStrings;
                try
                {
                    dateStrings = rawData.GetColumn<string>(ts.DateColumn).ToList();
                }
                catch
                {
                    // Column was typed as Single (e.g. bare year numbers like 2024)
                    dateStrings = rawData.GetColumn<float>(ts.DateColumn)
                                         .Select(f => ((int)f).ToString())
                                         .ToList();
                }

                if (labelValues.Count != dateStrings.Count)
                    throw new InvalidOperationException(
                        $"Date column '{ts.DateColumn}' has {dateStrings.Count} rows " +
                        $"but label column has {labelValues.Count} rows.");

                // Convert one raw date string to a normalised period key
                string ToPeriodKey(string raw)
                {
                    raw = raw?.Trim() ?? string.Empty;
                    if (DateTime.TryParse(raw, out var dt))
                    {
                        return ts.Granularity switch
                        {
                            "Year" => dt.ToString("yyyy"),
                            "Day" => dt.ToString("yyyy-MM-dd"),
                            _ => dt.ToString("yyyy-MM")   // Month default
                        };
                    }
                    // Bare 4-digit year (e.g. "2024")
                    if (raw.Length == 4 && int.TryParse(raw, out _)) return raw;
                    // Unknown format — truncate to first 10 chars and hope for the best
                    return raw.Length > 10 ? raw[..10] : raw;
                }

                // Group → SUM → sort chronologically
                var grouped = labelValues
                    .Zip(dateStrings, (val, date) => (Key: ToPeriodKey(date), Val: val))
                    .GroupBy(x => x.Key)
                    .OrderBy(g => g.Key)
                    .Select(g => new TsValueRow { Value = g.Sum(x => x.Val) })
                    .ToList();

                Console.WriteLine($"   • Aggregated {labelValues.Count:N0} rows → {grouped.Count} {ts.Granularity} periods");

                if (grouped.Count < 4)
                {
                    Console.WriteLine($"   ❌ Only {grouped.Count} period(s) after aggregation — need ≥ 4.");
                    Console.WriteLine($"      Check date column '{ts.DateColumn}' and granularity '{ts.Granularity}'.");
                    return null;
                }

                // Print a preview of the aggregated series
                var preview = labelValues
                    .Zip(dateStrings, (val, date) => (Key: ToPeriodKey(date), Val: val))
                    .GroupBy(x => x.Key)
                    .OrderBy(g => g.Key)
                    .ToList();
                int show = Math.Min(preview.Count, 8);
                Console.WriteLine($"   • Series preview (first {show} of {preview.Count} periods):");
                for (int pi = 0; pi < show; pi++)
                    Console.WriteLine($"      {preview[pi].Key}  →  {preview[pi].Sum(x => x.Val):N2}");
                if (preview.Count > show)
                    Console.WriteLine($"      ... ({preview.Count - show} more periods)");

                seriesView = _ml.Data.LoadFromEnumerable(grouped);
            }
            else
            {
                // No date column — project label column → "Value", use rows in file order.
                // User must ensure data is pre-sorted chronologically.
                Console.WriteLine($"   • No date column configured — using rows in file order.");
                Console.WriteLine($"     ⚠️  Ensure data is sorted chronologically for accurate SSA results.");
                var projectStep2 = _ml.Transforms.CopyColumns("Value", _cfg.LabelColumnName);
                seriesView = projectStep2.Fit(rawData).Transform(rawData);
            }

            // ── Step 2: Row count on the aggregated series ────────────────────────
            long totalRows = seriesView.GetColumn<float>("Value").LongCount();
            Console.WriteLine($"   • Series length after aggregation: {totalRows:N0} steps");

            // ── Step 3: Compute SSA parameters ───────────────────────────────────
            int trainSize = ts.TrainSize > 0
                ? ts.TrainSize
                : (int)(totalRows * (1.0 - _cfg.TestFraction));

            if (trainSize < 4)
            {
                Console.WriteLine($"   ❌ Not enough rows for SSA training (need ≥ 4, got {trainSize}).");
                return null;
            }

            int windowSize = ts.WindowSize > 0 ? ts.WindowSize : Math.Max(ts.HorizonSteps + 1, (int)(totalRows * 0.5));
            int horizon = ts.HorizonSteps;
            int seriesLen = ts.SeriesLength > 0 ? ts.SeriesLength : trainSize;

            // Enforce all SSA constraints in strict dependency order.
            // SSA requires: windowSize > horizon, seriesLen > windowSize, windowSize <= seriesLen/2,
            // seriesLen <= trainSize. Wrong order causes the crash shown in the log:
            //   "The series length should be greater than the window size."
            // Common case: user sets SeriesLength=100, horizon=365 -> windowSize becomes 366,
            // but seriesLen=100 < 366 -> crash.

            // A: clamp seriesLen to available data first
            if (seriesLen > trainSize)
            {
                seriesLen = trainSize;
                Console.WriteLine($"   ⚠️  SeriesLength capped to {seriesLen} (cannot exceed trainSize {trainSize}).");
            }

            // B: windowSize must be > horizon
            if (windowSize <= horizon)
            {
                windowSize = horizon + 1;
                Console.WriteLine($"   ⚠️  WindowSize adjusted to {windowSize} (must be > horizon {horizon}).");
            }

            // C: seriesLen must be > windowSize (THE critical constraint that was missing)
            if (seriesLen <= windowSize)
            {
                int needed = Math.Min(windowSize + 1, trainSize);
                Console.WriteLine($"   ⚠️  SeriesLength {seriesLen} <= WindowSize {windowSize} - expanding to {needed}.");
                seriesLen = needed;
            }

            // D: windowSize must be <= seriesLen / 2
            if (windowSize > seriesLen / 2)
            {
                windowSize = Math.Max(horizon + 1, seriesLen / 2);
                Console.WriteLine($"   ⚠️  WindowSize capped to {windowSize} (must be <= SeriesLength/2 = {seriesLen / 2}).");
            }

            // E: re-check C after D (D may have reduced windowSize below the threshold again)
            if (seriesLen <= windowSize)
            {
                seriesLen = Math.Min(windowSize + 1, trainSize);
                Console.WriteLine($"   ⚠️  SeriesLength re-adjusted to {seriesLen}.");
            }

            // F: final guard - seriesLen cannot exceed trainSize
            if (seriesLen > trainSize)
            {
                seriesLen = trainSize;
                Console.WriteLine($"   ⚠️  SeriesLength capped to trainSize {trainSize}.");
            }

            Console.WriteLine($"   • Label column  : {_cfg.LabelColumnName} → aggregated 'Value'");
            Console.WriteLine($"   • Total periods : {totalRows:N0}");
            Console.WriteLine($"   • Train size    : {trainSize}");
            Console.WriteLine($"   • Horizon       : {horizon}");
            Console.WriteLine($"   • Window size   : {windowSize}");
            Console.WriteLine($"   • Series length : {seriesLen}");
            Console.WriteLine($"   • Confidence    : {ts.ConfidenceLevel:P0}");
            Console.WriteLine($"   • Granularity   : {ts.Granularity}");

            ct.ThrowIfCancellationRequested();

            try
            {
                // ── Step 4: Fit SSA on aggregated "Value" series ──────────────────
                var ssaPipeline = _ml.Forecasting.ForecastBySsa(
                    outputColumnName: "Forecast",
                    inputColumnName: "Value",
                    windowSize: windowSize,
                    seriesLength: seriesLen,
                    trainSize: trainSize,
                    horizon: horizon,
                    confidenceLevel: ts.ConfidenceLevel,
                    confidenceLowerBoundColumn: "LowerBound",
                    confidenceUpperBoundColumn: "UpperBound");

                Console.WriteLine("\n   ⏳ Fitting SSA model on aggregated series...");
                var ssaModel = ssaPipeline.Fit(seriesView);
                Console.WriteLine("   ✅ SSA model fitted");

                ct.ThrowIfCancellationRequested();

                // ── Step 5: In-sample evaluation ──────────────────────────────────
                var transformed = ssaModel.Transform(seriesView);
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

                Console.WriteLine($"\n📊 SSA In-sample metrics (on aggregated series):");
                Console.WriteLine($"   • MAE  : {mae:N2}");
                Console.WriteLine($"   • RMSE : {rmse:N2}");

                // ── Step 6: Print next-horizon forecast ───────────────────────────
                Console.WriteLine($"\n🔮 Next {horizon}-{ts.Granularity} forecast:");
                var eng = ssaModel.CreateTimeSeriesEngine<TsValueRow, TsForecastRow>(_ml);
                var forecast = eng.Predict();
                for (int i = 0; i < forecast.Forecast.Length; i++)
                {
                    string lb = forecast.LowerBound.Length > i ? forecast.LowerBound[i].ToString("N2") : "—";
                    string ub = forecast.UpperBound.Length > i ? forecast.UpperBound[i].ToString("N2") : "—";
                    Console.WriteLine($"   {ts.Granularity} {i + 1,3}: {forecast.Forecast[i]:N2}  [{lb} – {ub}]");
                }

                ct.ThrowIfCancellationRequested();

                // ── Step 7: Save ssaModel only (input schema = {"Value": Single}) ──
                // Do NOT save the combined CopyColumns→SSA pipeline — that would
                // bake in the original column name and break prediction.
                return SaveTimeSeriesModel(ssaModel, seriesView.Schema, mae, rmse);
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
                    // MapKeyToValue only applies to classification tasks.
                    // Regression has no PredictedLabel key column — appending it
                    // causes "Could not find input column 'PredictedLabel'".
                    IEstimator<ITransformer> fullPipeline =
                        _resolvedTask == TaskType.Regression
                            ? preprocess.Append(trainer)
                            : preprocess.Append(trainer)
                                        .Append(_ml.Transforms.Conversion.MapKeyToValue(
                                            "PredictedLabel", "PredictedLabel"));

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
                Granularity = _cfg.TimeSeries.Granularity,
                WindowSize = _cfg.TimeSeries.WindowSize,
                SeriesLength = _cfg.TimeSeries.SeriesLength,
                TrainedAt = DateTime.Now.ToString("o"),
                MAE = mae,
                RMSE = rmse,
                // Store the original raw data path so ModelPredictionForm can
                // re-read and filter by PARTY_NAME / INVENTORY_ITEM_ID for per-series forecasting.
                RawDataPath = _cfg.DataFilePath,
                FeatureColumns = _allColumns   // all non-label columns (includes date + categoricals)
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
            // Always honour an explicit user selection — never auto-detect over it.
            if (_cfg.Task != TaskType.Auto)
            {
                Console.WriteLine($"   (explicit task '{_cfg.Task}' honoured — skipping auto-detect)");
                return _cfg.Task;
            }

            // Auto: TimeSeries heuristic — HorizonSteps > 0 + numeric label
            if (_cfg.TimeSeries.HorizonSteps > 0 &&
                data.Schema[_cfg.LabelColumnName].Type != TextDataViewType.Instance)
            {
                Console.WriteLine($"   (auto-detected TimeSeries: HorizonSteps={_cfg.TimeSeries.HorizonSteps})");
                return TaskType.TimeSeries;
            }

            // Is label text → classification
            var labelType = data.Schema[_cfg.LabelColumnName].Type;
            if (labelType == TextDataViewType.Instance)
                return TaskType.MulticlassClassification;

            // Sample up to 20 distinct values from the label column
            var distinctVals = data.GetColumn<float>(_cfg.LabelColumnName)
                                   .Distinct()
                                   .Take(20)
                                   .ToList();

            // Binary: only 0 and 1
            if (distinctVals.All(v => v is 0f or 1f))
                return TaskType.BinaryClassification;

            // Multiclass: ≤ 10 distinct integers only.
            // Threshold intentionally kept low: continuous quantities like
            // QUANTITY_INVOICED have hundreds of distinct integer values and
            // must be treated as Regression, not Multiclass.
            if (distinctVals.Count <= 10 && distinctVals.All(v => v == MathF.Floor(v)))
                return TaskType.MulticlassClassification;

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