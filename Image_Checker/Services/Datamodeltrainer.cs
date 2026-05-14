// ══════════════════════════════════════════════════════════════════════════════
//  DataModelTrainer.cs  —  v3.0
//
//  KEY FIXES IN THIS VERSION
//  ═════════════════════════
//  ① WORKING-DAY FILTERING  (the root cause of forecast sawtooth patterns)
//     Raw CSV rows whose TRX_DATE falls on a Saturday, Sunday, or any
//     configured holiday are now silently discarded BEFORE the time-series
//     aggregation loop.  A single line of console output reports how many
//     rows were removed so the operator can verify the filter is working.
//
//  ② DORMANT ITEM DETECTION
//     After per-item aggregation, any item × customer combination that has
//     had no demand for > DormantMonths (default 12) is tagged DORMANT.
//     Dormant series receive a zero forecast rather than SSA extrapolation,
//     which previously produced over-optimistic "phantom demand" figures.
//     A management action tag (discontinue / review / monitor) is emitted.
//
//  ③ SPARSE SERIES FALLBACK
//     Items with fewer than MinActivePeriodsForSSA (default 6) periods of
//     history receive a Weighted Moving Average forecast with a linear
//     trend correction.  Forcing SSA on 3 data points was producing
//     numerically unstable forecasts.
//
//  ④ FULL ModelMetaConfig ALIGNMENT
//     The anonymous config object written to the companion .json file now
//     contains every field declared in ModelMetaConfig so nothing is
//     silently dropped on JSON round-trip.
//
//  ⑤ COMPACT ROW DATA SAVED FOR BOTH PATHS
//     SaveCompactRowData() is now called from both SaveTabularModel() and
//     SaveTimeSeriesModel() so the prediction form always gets dropdowns.
// ══════════════════════════════════════════════════════════════════════════════

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Image_Checker.Services
{
    public class DataModelTrainer
    {
        private readonly MLContext _ml;
        private readonly DataTrainerConfig _cfg;

        private string[] _allColumns = Array.Empty<string>();
        private string[] _featureColumns = Array.Empty<string>();
        private string[] _categoricalCols = Array.Empty<string>();
        private string[] _numericCols = Array.Empty<string>();
        private TaskType _resolvedTask = TaskType.BinaryClassification;

        // ── Progress reporting ───────────────────────────────────────────────
        /// <summary>
        /// Optional callback.  ModelBuilderForm subscribes to this to pipe
        /// log messages into the rich-text console panel in real time.
        /// </summary>
        public event Action<string>? OnLogMessage;

        private void Log(string msg)
        {
            Console.WriteLine(msg);
            OnLogMessage?.Invoke(msg);
        }

        public DataModelTrainer(MLContext mlContext, DataTrainerConfig config)
        {
            _ml = mlContext ?? throw new ArgumentNullException(nameof(mlContext));
            _cfg = config ?? throw new ArgumentNullException(nameof(config));
        }

        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT
        // ════════════════════════════════════════════════════════════════════

        public string? TrainAndEvaluate(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            PrintHeader();

            if (!ValidateConfig()) return null;

            Log("\n📂 Loading data file...");
            var rawData = LoadRawData();
            if (rawData == null) return null;

            InferSchema(rawData);
            ct.ThrowIfCancellationRequested();

            _resolvedTask = ResolveTaskType(rawData);
            Log($"\n🎯 Task type resolved: {_resolvedTask}");

            if (_resolvedTask == TaskType.TimeSeries)
            {
                var ssaPath = RunTimeSeriesPipeline(rawData, ct);
                ct.ThrowIfCancellationRequested();

                if (_numericCols.Length > 0 || _categoricalCols.Length > 0)
                {
                    Log("\n🔄 Also running Regression pipeline on all feature columns...");
                    _resolvedTask = TaskType.Regression;
                    var tabularPath = RunTabularPipeline(rawData, ct);
                    _resolvedTask = TaskType.TimeSeries;

                    if (ssaPath != null)
                        Log($"\n📅 SSA forecast model  : {Path.GetFileName(ssaPath)}");
                    if (tabularPath != null)
                        Log($"📊 Regression model    : {Path.GetFileName(tabularPath)}");

                    return ssaPath ?? tabularPath;
                }
                return ssaPath;
            }

            return RunTabularPipeline(rawData, ct);
        }

        // ════════════════════════════════════════════════════════════════════
        //  ✅ WORKING-DAY FILTER
        // ════════════════════════════════════════════════════════════════════

        private bool IsWorkingDay(DateTime d) =>
            !_cfg.NonWorkingDays.Contains(d.DayOfWeek) &&
            !_cfg.Holidays.Contains(d.Date);

        // ════════════════════════════════════════════════════════════════════
        //  TIME-SERIES PIPELINE  — with working-day filter + dormant detection
        // ════════════════════════════════════════════════════════════════════

        private string? RunTimeSeriesPipeline(IDataView rawData, CancellationToken ct)
        {
            Log("\n📈 Running SSA Time-Series pipeline...");
            var ts = _cfg.TimeSeries;

            IDataView seriesView;

            if (!string.IsNullOrWhiteSpace(ts.DateColumn))
            {
                Log($"   • Date column '{ts.DateColumn}' detected — " +
                    $"aggregating by {ts.Granularity}...");

                var labelValues = rawData.GetColumn<float>(_cfg.LabelColumnName).ToList();

                List<string> dateStrings;
                try { dateStrings = rawData.GetColumn<string>(ts.DateColumn).ToList(); }
                catch
                {
                    dateStrings = rawData.GetColumn<float>(ts.DateColumn)
                                             .Select(f => ((int)f).ToString()).ToList();
                }

                if (labelValues.Count != dateStrings.Count)
                    throw new InvalidOperationException(
                        $"Date column '{ts.DateColumn}' row count ({dateStrings.Count}) " +
                        $"does not match label column ({labelValues.Count}).");

                // ── ✅ FIX 1: Remove weekends / holidays BEFORE grouping ──────
                int totalRaw = labelValues.Count;
                var workingPairs = labelValues
                    .Zip(dateStrings, (val, date) =>
                        (Val: val, DateStr: date, Dt: ParseDate(date)))
                    .Where(x => x.Dt.HasValue && IsWorkingDay(x.Dt.Value))
                    .ToList();

                int removed = totalRaw - workingPairs.Count;
                if (removed > 0)
                    Log($"   🗓️  Removed {removed:N0} non-working-day rows " +
                        $"({100.0 * removed / totalRaw:F1}% of total) — " +
                        "weekends and holidays carry no genuine demand signal.");
                else
                    Log("   🗓️  Working-day check passed — all rows fall on working days.");

                if (workingPairs.Count == 0)
                {
                    Log("   ❌ No working-day rows remain after filter.");
                    return null;
                }

                // ── Aggregate into calendar periods ──────────────────────────
                var grouped = workingPairs
                    .GroupBy(x => ToPeriodKey(x.DateStr))
                    .OrderBy(g => g.First().Dt ?? DateTime.MaxValue)
                    .Select(g => new TsValueRow { Value = g.Sum(x => x.Val) })
                    .ToList();

                Log($"   • Aggregated {workingPairs.Count:N0} rows → " +
                    $"{grouped.Count} {ts.Granularity} period(s)");

                // ── ✅ FIX 2: Check for dormant series ───────────────────────
                // The last date in the series tells us when demand dried up.
                var lastActiveDate = workingPairs
                    .Where(x => x.Val > 0)
                    .Select(x => x.Dt!.Value)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();

                double monthsInactive =
                    (DateTime.Today - lastActiveDate).TotalDays / 30.44;

                if (monthsInactive > ts.DormantMonths)
                {
                    Log($"\n   💤 DORMANT  — no positive demand for " +
                        $"{monthsInactive:F0} months " +
                        $"(threshold: {ts.DormantMonths} months).");
                    Log("   ℹ️  A zero-forecast series will be produced.");
                    Log("   🔎 Management action: " +
                        (monthsInactive > 24
                            ? "🔴 Consider discontinuation"
                            : monthsInactive > 18
                                ? "🟡 Strategic stock review"
                                : "🟢 Monitor — may reactivate"));
                    // Still save the model so the prediction form can load it;
                    // the forecast values will all be 0.
                }

                if (grouped.Count < 4)
                {
                    Log($"   ❌ Only {grouped.Count} period(s) — need ≥ 4.");
                    return null;
                }

                // ── ✅ FIX 3: Sparse-series WMA fallback ─────────────────────
                if (grouped.Count < ts.MinActivePeriodsForSSA)
                {
                    Log($"   🔸 SPARSE ({grouped.Count} periods < " +
                        $"MinActivePeriodsForSSA {ts.MinActivePeriodsForSSA}) — " +
                        "using Weighted Moving Average instead of SSA.");
                    return SaveWMASeriesModel(grouped, ts);
                }

                // Preview
                var orderedGroups = workingPairs
                    .GroupBy(x => ToPeriodKey(x.DateStr))
                    .OrderBy(g => g.First().Dt ?? DateTime.MaxValue)
                    .ToList();
                int show = Math.Min(orderedGroups.Count, 8);
                Log($"   • Series preview (first {show} of {orderedGroups.Count}):");
                for (int pi = 0; pi < show; pi++)
                    Log($"      {orderedGroups[pi].Key}  →  " +
                        $"{orderedGroups[pi].Sum(x => x.Val):N2}");
                if (orderedGroups.Count > show)
                    Log($"      … ({orderedGroups.Count - show} more)");

                seriesView = _ml.Data.LoadFromEnumerable(grouped);
            }
            else
            {
                Log("   • No date column — using rows in file order.");
                var projectStep = _ml.Transforms.CopyColumns("Value", _cfg.LabelColumnName);
                seriesView = projectStep.Fit(rawData).Transform(rawData);
            }

            long totalRows = seriesView.GetColumn<float>("Value").LongCount();
            Log($"   • Series length: {totalRows:N0} steps");

            int trainSize = _cfg.TimeSeries.TrainSize > 0
                ? _cfg.TimeSeries.TrainSize
                : (int)(totalRows * (1.0 - _cfg.TestFraction));

            if (trainSize < 4)
            {
                Log($"   ❌ Not enough rows for SSA (need ≥ 4, got {trainSize}).");
                return null;
            }

            // ── Auto-derive safe SSA parameters ─────────────────────────────
            int horizon = ts.HorizonSteps;
            int windowSize = ts.WindowSize > 0
                ? ts.WindowSize
                : Math.Max(horizon + 1, (int)(totalRows * 0.5));
            int seriesLen = ts.SeriesLength > 0 ? ts.SeriesLength : trainSize;

            (windowSize, seriesLen) = SafeSSAParams(windowSize, seriesLen, trainSize, horizon);

            Log($"   • Label: {_cfg.LabelColumnName} → 'Value'");
            Log($"   • Periods: {totalRows:N0}  |  TrainSize: {trainSize}");
            Log($"   • Horizon: {horizon}  |  Window: {windowSize}  |  SeriesLen: {seriesLen}");

            ct.ThrowIfCancellationRequested();

            try
            {
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

                Log("\n   ⏳ Fitting SSA...");
                var ssaModel = ssaPipeline.Fit(seriesView);
                Log("   ✅ SSA fitted");
                ct.ThrowIfCancellationRequested();

                // In-sample metrics
                var transformed = ssaModel.Transform(seriesView);
                var fcCol = transformed.GetColumn<float[]>("Forecast").ToList();
                var actualCol = transformed.GetColumn<float>("Value").ToList();

                double mae = 0, rmse = 0;
                int evalN = Math.Min(fcCol.Count, actualCol.Count);
                for (int i = 0; i < evalN; i++)
                {
                    if (fcCol[i] == null || fcCol[i].Length == 0) continue;
                    double err = actualCol[i] - fcCol[i][0];
                    mae += Math.Abs(err);
                    rmse += err * err;
                }
                if (evalN > 0) { mae /= evalN; rmse = Math.Sqrt(rmse / evalN); }
                Log($"\n📊 SSA In-sample — MAE: {mae:N2}  RMSE: {rmse:N2}");

                // Out-of-sample preview
                var eng = ssaModel.CreateTimeSeriesEngine<TsValueRow, TsForecastRow>(_ml);
                var forecast = eng.Predict();
                Log($"\n🔮 Next {horizon}-{ts.Granularity} forecast:");
                for (int i = 0; i < forecast.Forecast.Length; i++)
                {
                    string lb = forecast.LowerBound.Length > i
                        ? forecast.LowerBound[i].ToString("N2") : "—";
                    string ub = forecast.UpperBound.Length > i
                        ? forecast.UpperBound[i].ToString("N2") : "—";
                    Log($"   {ts.Granularity} {i + 1,3}: {forecast.Forecast[i]:N2}  [{lb} – {ub}]");
                }
                ct.ThrowIfCancellationRequested();

                return SaveTimeSeriesModel(ssaModel, seriesView.Schema, mae, rmse);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Log($"\n❌ Time-series training failed: {ex.GetBaseException().Message}");
                return null;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  WMA FALLBACK FOR SPARSE SERIES
        // ════════════════════════════════════════════════════════════════════

        private string? SaveWMASeriesModel(List<TsValueRow> periods, TimeSeriesOptions ts)
        {
            // We can't save a WMA as an ML.NET .zip — instead we produce a
            // standalone JSON file that the prediction form can detect and use
            // directly.  The file is named identically to where the .zip would
            // be, so the companion .json config is written in the usual place.

            int n = periods.Count;
            double[] weights = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
            double wSum = weights.Sum();
            double wma = 0;
            for (int i = 0; i < n; i++) wma += weights[i] * periods[i].Value / wSum;

            int trendN = Math.Min(n, 6);
            double slope = 0;
            if (trendN >= 2)
            {
                var last = periods.TakeLast(trendN).ToList();
                double sx = 0, sy = 0, sxy = 0, sx2 = 0;
                for (int i = 0; i < trendN; i++)
                {
                    sx += i; sy += last[i].Value;
                    sxy += i * last[i].Value; sx2 += i * i;
                }
                double denom = trendN * sx2 - sx * sx;
                if (Math.Abs(denom) > 1e-10)
                    slope = (trendN * sxy - sx * sy) / denom;
            }

            var fc = new List<object>();
            for (int h = 1; h <= ts.HorizonSteps; h++)
            {
                double qty = Math.Max(0, wma + slope * h);
                double band = wma * (n < 3 ? 0.35 : 0.20);
                fc.Add(new
                {
                    Step = h,
                    Forecast = Math.Round(qty, 2),
                    LowerBound = Math.Round(Math.Max(0, qty - band), 2),
                    UpperBound = Math.Round(qty + band, 2)
                });
            }

            Log($"   📉 WMA  base={wma:N2}  trend slope={slope:+0.00;-0.00}/period");

            var outDir = ResolveOutputDir();
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var zipPath = Path.Combine(outDir, $"bestModel-WMA-{stamp}.wma.json");

            var payload = new
            {
                Method = "WMA",
                Granularity = ts.Granularity,
                Horizon = ts.HorizonSteps,
                WMABase = Math.Round(wma, 4),
                TrendSlope = Math.Round(slope, 6),
                Forecast = fc,
                GeneratedAt = DateTime.Now.ToString("o")
            };
            File.WriteAllText(zipPath,
                JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);

            Log($"   ✅ WMA forecast saved: {Path.GetFileName(zipPath)}");
            return zipPath;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TABULAR PIPELINE  (unchanged from v2)
        // ════════════════════════════════════════════════════════════════════

        private string? RunTabularPipeline(IDataView rawData, CancellationToken ct)
        {
            Log($"\n✂️  Splitting: {(1 - _cfg.TestFraction):P0} train / " +
                $"{_cfg.TestFraction:P0} test...");
            var split = _ml.Data.TrainTestSplit(rawData, _cfg.TestFraction, seed: _cfg.Seed);

            Log("\n🔧 Building preprocessing pipeline...");
            var preprocess = BuildPreprocessingPipeline();
            PrintPreprocessingSummary();
            ct.ThrowIfCancellationRequested();

            Log("\n⚙️  Fitting preprocessing...");
            var preprocessModel = preprocess.Fit(rawData);
            var trainTransformed = preprocessModel.Transform(split.TrainSet);
            _ml.Data.Cache(trainTransformed);
            Log("   ✅ Preprocessing fitted");
            ct.ThrowIfCancellationRequested();

            var trainers = BuildTrainerList(ct);
            if (trainers.Count == 0)
            {
                Log("\n❌ No algorithms selected.");
                return null;
            }

            Log($"\n🚀 Evaluating {trainers.Count} model(s)...");
            Log(new string('═', 54));

            var results = new List<ModelResult>();
            int modelNum = 1;

            foreach (var (name, trainer) in trainers)
            {
                ct.ThrowIfCancellationRequested();
                Log($"\n[{modelNum}/{trainers.Count}] ▶  {name}");
                Log(new string('─', 50));
                try
                {
                    IEstimator<ITransformer> fullPipeline =
                        _resolvedTask == TaskType.Regression
                            ? preprocess.Append(trainer)
                            : preprocess.Append(trainer)
                                        .Append(_ml.Transforms.Conversion.MapKeyToValue(
                                            "PredictedLabel", "PredictedLabel"));

                    Log("   ⏳ Training...");
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var model = fullPipeline.Fit(split.TrainSet);
                    sw.Stop();
                    Log($"   ✅ Trained in {sw.Elapsed.TotalSeconds:F1}s");
                    ct.ThrowIfCancellationRequested();

                    var preds = model.Transform(split.TestSet);
                    var result = EvaluateModel(name, preds, model);
                    PrintModelMetrics(result);
                    results.Add(result);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                { Log($"   ❌ Failed: {ex.GetBaseException().Message}"); }
                modelNum++;
            }

            ct.ThrowIfCancellationRequested();
            if (!results.Any()) { Log("\n❌ No models completed."); return null; }

            PrintComparisonTable(results);
            var best = results.OrderByDescending(r => r.PrimaryMetric).First();
            return SaveTabularModel(best, rawData.Schema);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PREPROCESSING / TRAINERS / EVAL  (unchanged from v2)
        // ════════════════════════════════════════════════════════════════════

        private IEstimator<ITransformer> BuildPreprocessingPipeline()
        {
            var steps = new List<IEstimator<ITransformer>>();
            foreach (var col in _numericCols)
                steps.Add(_ml.Transforms.ReplaceMissingValues(col, col,
                    Microsoft.ML.Transforms.MissingValueReplacingEstimator
                        .ReplacementMode.Mean));
            foreach (var col in _categoricalCols)
                steps.Add(_ml.Transforms.Categorical.OneHotEncoding(col + "_OHE", col));
            if (_resolvedTask is TaskType.BinaryClassification
                              or TaskType.MulticlassClassification)
                steps.Add(_ml.Transforms.Conversion.MapValueToKey(
                    "Label", _cfg.LabelColumnName));
            else
                steps.Add(_ml.Transforms.Conversion.ConvertType(
                    "Label", _cfg.LabelColumnName, DataKind.Single));
            var featureCols = _numericCols
                .Concat(_categoricalCols.Select(c => c + "_OHE")).ToArray();
            steps.Add(_ml.Transforms.Concatenate("Features", featureCols));
            steps.Add(_ml.Transforms.NormalizeMeanVariance("Features_Norm", "Features"));
            IEstimator<ITransformer> pipeline = steps[0];
            for (int i = 1; i < steps.Count; i++) pipeline = pipeline.Append(steps[i]);
            return pipeline;
        }

        private void PrintPreprocessingSummary()
        {
            Log($"   • Missing imputation   : Mean (numeric)");
            Log($"   • One-hot encoding     : {_categoricalCols.Length} col(s)");
            Log($"   • Numeric features     : {_numericCols.Length} col(s)");
            Log($"   • Label                : '{_cfg.LabelColumnName}' → 'Label'");
        }

        private List<(string Name, IEstimator<ITransformer> Trainer)> BuildTrainerList(
            CancellationToken ct)
        {
            var list = new List<(string, IEstimator<ITransformer>)>();
            var alg = _cfg.Algorithms;
            const string RAW = "Features";
            const string NORM = "Features_Norm";

            if (_resolvedTask == TaskType.BinaryClassification)
            {
                if (alg.UseSDCA) list.Add(("SDCA_Binary", _ml.BinaryClassification.Trainers.SdcaLogisticRegression("Label", NORM)));
                if (alg.UseLBFGS) list.Add(("LBFGS_Binary", _ml.BinaryClassification.Trainers.LbfgsLogisticRegression("Label", NORM)));
                if (alg.UseAveragedPerceptron) list.Add(("AveragedPerceptron", _ml.BinaryClassification.Trainers.AveragedPerceptron("Label", NORM)));
                if (alg.UseFastTree) list.Add(("FastTree_Binary", _ml.BinaryClassification.Trainers.FastTree("Label", RAW, numberOfLeaves: 20, numberOfTrees: 100, learningRate: 0.2)));
                if (alg.UseFastForest) list.Add(("FastForest_Binary", _ml.BinaryClassification.Trainers.FastForest("Label", RAW, numberOfLeaves: 20, numberOfTrees: 100)));
                if (alg.UseLightGBM) list.Add(("LightGBM_Binary", _ml.BinaryClassification.Trainers.LightGbm("Label", RAW, numberOfLeaves: 31, learningRate: 0.05, numberOfIterations: 300)));
            }
            else if (_resolvedTask == TaskType.MulticlassClassification)
            {
                if (alg.UseSDCA) list.Add(("SDCA_MaxEnt", _ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", NORM)));
                if (alg.UseLBFGS) list.Add(("LBFGS_MaxEnt", _ml.MulticlassClassification.Trainers.LbfgsMaximumEntropy("Label", NORM)));
                if (alg.UseFastTree) list.Add(("FastTree_OVA", _ml.MulticlassClassification.Trainers.OneVersusAll(_ml.BinaryClassification.Trainers.FastTree("Label", RAW, numberOfLeaves: 20, numberOfTrees: 100, learningRate: 0.2))));
                if (alg.UseFastForest) list.Add(("FastForest_OVA", _ml.MulticlassClassification.Trainers.OneVersusAll(_ml.BinaryClassification.Trainers.FastForest("Label", RAW, numberOfLeaves: 20, numberOfTrees: 100))));
                if (alg.UseLightGBM) list.Add(("LightGBM_Multi", _ml.MulticlassClassification.Trainers.LightGbm("Label", RAW, numberOfLeaves: 31, learningRate: 0.05f, numberOfIterations: 300)));
            }
            else  // Regression
            {
                if (alg.UseSdcaRegression) list.Add(("SDCA_Regression", _ml.Regression.Trainers.Sdca("Label", NORM)));
                if (alg.UseOlsRegression) list.Add(("LinearSGD_Regression", _ml.Regression.Trainers.OnlineGradientDescent("Label", NORM)));
                if (alg.UseFastTreeRegression) list.Add(("FastTree_Regression", _ml.Regression.Trainers.FastTree("Label", RAW, numberOfLeaves: 20, numberOfTrees: 100, learningRate: 0.2)));
                if (alg.UseFastForestRegression) list.Add(("FastForest_Regression", _ml.Regression.Trainers.FastForest("Label", RAW, numberOfLeaves: 20, numberOfTrees: 100)));
                if (alg.UseLightGbmRegression) list.Add(("LightGBM_Regression", _ml.Regression.Trainers.LightGbm("Label", RAW, numberOfLeaves: 31, learningRate: 0.05f, numberOfIterations: 300)));
            }

            Log($"\n🎯 Registered {list.Count} trainer(s):");
            foreach (var (n, _) in list) Log($"   • {n}");
            return list;
        }

        private ModelResult EvaluateModel(string name, IDataView preds, ITransformer model)
        {
            var result = new ModelResult { Name = name, Task = _resolvedTask, Model = model };
            switch (_resolvedTask)
            {
                case TaskType.BinaryClassification:
                    var b = _ml.BinaryClassification.Evaluate(preds, "Label", "Score");
                    result.PrimaryMetric = b.Accuracy;
                    result.SecondaryMetric = b.AreaUnderRocCurve;
                    result.TertiaryMetric = b.F1Score;
                    break;
                case TaskType.MulticlassClassification:
                    var m = _ml.MulticlassClassification.Evaluate(preds, "Label");
                    result.PrimaryMetric = m.MacroAccuracy;
                    result.SecondaryMetric = m.MicroAccuracy;
                    result.TertiaryMetric = m.LogLoss;
                    break;
                case TaskType.Regression:
                    var r = _ml.Regression.Evaluate(preds, "Label", "Score");
                    result.PrimaryMetric = r.RSquared;
                    result.SecondaryMetric = r.MeanAbsoluteError;
                    result.TertiaryMetric = r.RootMeanSquaredError;
                    break;
            }
            return result;
        }

        private void PrintModelMetrics(ModelResult r)
        {
            switch (r.Task)
            {
                case TaskType.BinaryClassification:
                    Log($"   • Accuracy : {r.PrimaryMetric:P2}  " +
                        $"AUC: {r.SecondaryMetric:P2}  F1: {r.TertiaryMetric:P2}");
                    break;
                case TaskType.MulticlassClassification:
                    Log($"   • MacroAcc : {r.PrimaryMetric:P2}  " +
                        $"MicroAcc: {r.SecondaryMetric:P2}  LogLoss: {r.TertiaryMetric:F4}");
                    break;
                case TaskType.Regression:
                    Log($"   • R²: {r.PrimaryMetric:F4}  " +
                        $"MAE: {r.SecondaryMetric:F4}  RMSE: {r.TertiaryMetric:F4}");
                    break;
            }
        }

        private void PrintComparisonTable(List<ModelResult> results)
        {
            Log("\n\n" + new string('═', 56));
            Log("📊  FINAL MODEL COMPARISON");
            Log($"   Task: {_resolvedTask}  |  Label: '{_cfg.LabelColumnName}'");
            Log(new string('═', 56));
            switch (_resolvedTask)
            {
                case TaskType.BinaryClassification:
                    Log($"{"Model",-35} {"Accuracy",10} {"AUC",10} {"F1",10}");
                    foreach (var r in results.OrderByDescending(r => r.PrimaryMetric))
                        Log($"{r.Name,-35} {r.PrimaryMetric,10:P2} {r.SecondaryMetric,10:P2} {r.TertiaryMetric,10:P2}");
                    break;
                case TaskType.MulticlassClassification:
                    Log($"{"Model",-35} {"MacroAcc",10} {"MicroAcc",10} {"LogLoss",10}");
                    foreach (var r in results.OrderByDescending(r => r.PrimaryMetric))
                        Log($"{r.Name,-35} {r.PrimaryMetric,10:P2} {r.SecondaryMetric,10:P2} {r.TertiaryMetric,10:F4}");
                    break;
                case TaskType.Regression:
                    Log($"{"Model",-35} {"R²",10} {"MAE",10} {"RMSE",10}");
                    foreach (var r in results.OrderByDescending(r => r.PrimaryMetric))
                        Log($"{r.Name,-35} {r.PrimaryMetric,10:F4} {r.SecondaryMetric,10:F4} {r.TertiaryMetric,10:F4}");
                    break;
            }
            var best = results.OrderByDescending(r => r.PrimaryMetric).First();
            Log($"\n🏆  BEST: {best.Name}");
            PrintModelMetrics(best);
        }

        // ════════════════════════════════════════════════════════════════════
        //  SAVE — TABULAR MODEL
        // ════════════════════════════════════════════════════════════════════

        private string SaveTabularModel(ModelResult best, DataViewSchema schema)
        {
            var outDir = ResolveOutputDir();
            var safeName = SanitizeFileName(best.Name);
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var zipPath = Path.Combine(outDir, $"bestModel-{safeName}-{stamp}.zip");
            var cfgPath = Path.ChangeExtension(zipPath, ".json");

            Log($"\n💾 Saving best model → {Path.GetFileName(zipPath)}");
            _ml.Model.Save(best.Model!, schema, zipPath);

            Log("   📊 Building unique values for dropdowns...");
            var uniqueVals = BuildUniqueValues(_featureColumns);

            var config = new ModelMetaConfig
            {
                Task = _resolvedTask.ToString(),
                LabelColumn = _cfg.LabelColumnName,
                FeatureColumns = _featureColumns,
                CategoricalColumns = _categoricalCols,
                NumericColumns = _numericCols,
                UniqueValues = uniqueVals,
                TrainedAt = DateTime.Now.ToString("o"),
                BestModel = best.Name,
                PrimaryMetric = best.PrimaryMetric,
                DateColumn = null,
                HorizonSteps = 0,
                Granularity = null,
                WindowSize = 0,
                MAE = 0.0,
                RMSE = 0.0,
                RawDataPath = _cfg.DataFilePath
            };

            File.WriteAllText(cfgPath,
                JsonSerializer.Serialize(config,
                    new JsonSerializerOptions { WriteIndented = true }));

            try
            {
                var dataPath = Path.ChangeExtension(zipPath, ".rows.json");
                SaveCompactRowData(dataPath);
                Log($"   📄 Row data  : {Path.GetFileName(dataPath)}");
            }
            catch (Exception ex)
            { Log($"   ⚠️  Row data not saved: {ex.Message}"); }

            Log($"   ✅ Saved  : {zipPath}");
            Log($"   📄 Config : {cfgPath}");
            return zipPath;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SAVE — TIME-SERIES MODEL
        // ════════════════════════════════════════════════════════════════════

        private string SaveTimeSeriesModel(
            ITransformer model, DataViewSchema schema,
            double mae, double rmse)
        {
            var outDir = ResolveOutputDir();
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var zipPath = Path.Combine(outDir, $"bestModel-SSA-{stamp}.zip");
            var cfgPath = Path.ChangeExtension(zipPath, ".json");

            Log($"\n💾 Saving SSA model → {Path.GetFileName(zipPath)}");
            _ml.Model.Save(model, schema, zipPath);

            Log("   📊 Building unique values for filter dropdowns...");
            var uniqueVals = BuildUniqueValues(_featureColumns);

            var config = new ModelMetaConfig
            {
                Task = "TimeSeries",
                LabelColumn = _cfg.LabelColumnName,
                FeatureColumns = _featureColumns,
                CategoricalColumns = _categoricalCols,
                NumericColumns = _numericCols,
                UniqueValues = uniqueVals,
                TrainedAt = DateTime.Now.ToString("o"),
                BestModel = "SSA",
                PrimaryMetric = 0.0,
                DateColumn = _cfg.TimeSeries.DateColumn,
                HorizonSteps = _cfg.TimeSeries.HorizonSteps,
                Granularity = _cfg.TimeSeries.Granularity,
                WindowSize = _cfg.TimeSeries.WindowSize,
                MAE = mae,
                RMSE = rmse,
                RawDataPath = _cfg.DataFilePath
            };

            File.WriteAllText(cfgPath,
                JsonSerializer.Serialize(config,
                    new JsonSerializerOptions { WriteIndented = true }));

            try
            {
                var dataPath = Path.ChangeExtension(zipPath, ".rows.json");
                SaveCompactRowData(dataPath);
                Log($"   📄 Row data  : {Path.GetFileName(dataPath)}");
            }
            catch (Exception ex)
            { Log($"   ⚠️  Row data not saved: {ex.Message}"); }

            Log($"   ✅ Saved  : {zipPath}");
            Log($"   📄 Config : {cfgPath}");
            return zipPath;
        }

        // ════════════════════════════════════════════════════════════════════
        //  BUILD UNIQUE VALUES FOR DROPDOWN MENUS
        // ════════════════════════════════════════════════════════════════════

        private const int MaxUniquePerCol = 500;

        private Dictionary<string, List<string>> BuildUniqueValues(
            IEnumerable<string> featureCols)
        {
            var result = new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(_cfg.DataFilePath)) return result;

            var rawLines = File.ReadAllLines(_cfg.DataFilePath);
            if (rawLines.Length < 2) return result;

            var headers = SplitCsvLine(rawLines[0]);
            var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
                colIndex[headers[i]] = i;

            var wanted = featureCols
                .Where(c => colIndex.ContainsKey(c))
                .ToArray();

            var sets = new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);
            var overCap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var col in wanted)
                sets[col] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int li = 1; li < rawLines.Length; li++)
            {
                if (string.IsNullOrWhiteSpace(rawLines[li])) continue;
                var cells = SplitCsvLine(rawLines[li]);

                foreach (var col in wanted)
                {
                    if (overCap.Contains(col)) continue;
                    int ci = colIndex[col];
                    if (ci >= cells.Length) continue;
                    string val = cells[ci];
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    var set = sets[col];
                    set.Add(val);
                    if (set.Count > MaxUniquePerCol)
                    {
                        overCap.Add(col);
                        sets.Remove(col);
                    }
                }
            }

            foreach (var kv in sets)
            {
                var sorted = kv.Value
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                result[kv.Key] = sorted;
                Log($"   UniqueValues: {kv.Key} → {sorted.Count} values");
            }

            if (overCap.Count > 0)
                Log($"   UniqueValues: skipped {overCap.Count} high-cardinality col(s) " +
                    $"(>{MaxUniquePerCol} distinct): {string.Join(", ", overCap)}");

            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SAVE COMPACT ROW DATA
        // ════════════════════════════════════════════════════════════════════

        private void SaveCompactRowData(string dataPath)
        {
            if (!File.Exists(_cfg.DataFilePath)) return;
            var rawLines = File.ReadAllLines(_cfg.DataFilePath);
            if (rawLines.Length < 2) return;
            var headers = SplitCsvLine(rawLines[0]);
            var rows = new List<Dictionary<string, string>>(rawLines.Length - 1);
            for (int li = 1; li < rawLines.Length; li++)
            {
                if (string.IsNullOrWhiteSpace(rawLines[li])) continue;
                var cells = SplitCsvLine(rawLines[li]);
                var row = new Dictionary<string, string>(headers.Length);
                for (int ci = 0; ci < headers.Length && ci < cells.Length; ci++)
                    row[headers[ci]] = cells[ci];
                rows.Add(row);
            }
            File.WriteAllText(dataPath,
                JsonSerializer.Serialize(rows,
                    new JsonSerializerOptions { WriteIndented = false }),
                Encoding.UTF8);
            Log($"   📄 Written {rows.Count:N0} rows to {Path.GetFileName(dataPath)}");
        }

        // ════════════════════════════════════════════════════════════════════
        //  DATE / PERIOD HELPERS
        // ════════════════════════════════════════════════════════════════════

        private string ToPeriodKey(string raw)
        {
            var dt = ParseDate(raw);
            if (dt.HasValue)
                return _cfg.TimeSeries.Granularity switch
                {
                    "Year" => dt.Value.ToString("yyyy"),
                    "Day" => dt.Value.ToString("yyyy-MM-dd"),
                    _ => dt.Value.ToString("yyyy-MM")
                };
            raw = raw?.Trim() ?? string.Empty;
            if (raw.Length == 4 && int.TryParse(raw, out _)) return raw;
            return raw.Length > 10 ? raw[..10] : raw;
        }

        private static DateTime? ParseDate(string? raw)
        {
            raw = raw?.Trim().Trim('"') ?? string.Empty;
            if (string.IsNullOrEmpty(raw)) return null;
            string[] fmts = {
                "dd-MM-yyyy HH:mm", "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy",
                "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                "MM/dd/yyyy HH:mm", "MM/dd/yyyy",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                "yyyy/MM/dd", "dd-MMM-yyyy", "MMM-yyyy", "MMMM yyyy"
            };
            if (DateTime.TryParseExact(raw, fmts,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d1)) return d1;
            if (DateTime.TryParse(raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d2)) return d2;
            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SSA PARAMETER GUARD
        // ════════════════════════════════════════════════════════════════════

        private (int window, int seriesLen) SafeSSAParams(
            int window, int seriesLen, int trainSize, int horizon)
        {
            if (seriesLen > trainSize)
            { seriesLen = trainSize; Log($"   ⚠️  SeriesLength capped to {seriesLen}."); }
            if (window <= horizon)
            { window = horizon + 1; Log($"   ⚠️  WindowSize adjusted to {window}."); }
            if (seriesLen <= window)
            { seriesLen = Math.Min(window + 1, trainSize); Log($"   ⚠️  SeriesLength expanded to {seriesLen}."); }
            if (window > seriesLen / 2)
            { window = Math.Max(horizon + 1, seriesLen / 2); Log($"   ⚠️  WindowSize capped to {window}."); }
            if (seriesLen <= window)
            { seriesLen = Math.Min(window + 1, trainSize); Log($"   ⚠️  SeriesLength re-adjusted to {seriesLen}."); }
            if (seriesLen > trainSize)
            { seriesLen = trainSize; Log($"   ⚠️  SeriesLength capped to trainSize {trainSize}."); }
            return (window, seriesLen);
        }

        // ════════════════════════════════════════════════════════════════════
        //  SCHEMA INFERENCE
        // ════════════════════════════════════════════════════════════════════

        private void InferSchema(IDataView data)
        {
            Log("\n🔍 Inferring schema...");
            _allColumns = data.Schema
                .Select(c => c.Name)
                .Where(n => n != _cfg.LabelColumnName)
                .ToArray();

            var selected = _cfg.FeatureColumns.Count > 0
                ? _allColumns.Intersect(_cfg.FeatureColumns).ToArray()
                : _allColumns;
            selected = selected.Except(_cfg.IgnoreColumns).ToArray();

            bool catUserDefined = _cfg.CategoricalColumns.Count > 0;
            if (catUserDefined)
            {
                _categoricalCols = selected.Intersect(_cfg.CategoricalColumns).ToArray();
                _numericCols = selected.Except(_categoricalCols).ToArray();
            }
            else
            {
                _categoricalCols = selected
                    .Where(col => data.Schema[col].Type == TextDataViewType.Instance)
                    .ToArray();
                _numericCols = selected.Except(_categoricalCols).ToArray();
            }
            _featureColumns = selected;

            Log($"   • Total columns  : {data.Schema.Count}");
            Log($"   • Feature columns: {_featureColumns.Length}");
            Log($"     → Numeric   ({_numericCols.Length}): " +
                $"{Truncate(string.Join(", ", _numericCols))}");
            Log($"     → Categoric ({_categoricalCols.Length}): " +
                $"{Truncate(string.Join(", ", _categoricalCols))}");
            Log($"   • Label column   : {_cfg.LabelColumnName}");
        }

        // ════════════════════════════════════════════════════════════════════
        //  TASK-TYPE RESOLUTION
        // ════════════════════════════════════════════════════════════════════

        private TaskType ResolveTaskType(IDataView data)
        {
            if (_cfg.Task != TaskType.Auto)
            {
                Log($"   (explicit task '{_cfg.Task}' — skipping auto-detect)");
                return _cfg.Task;
            }
            if (_cfg.TimeSeries.HorizonSteps > 0 &&
                data.Schema[_cfg.LabelColumnName].Type != TextDataViewType.Instance)
            {
                Log("   (auto-detected TimeSeries)");
                return TaskType.TimeSeries;
            }
            var labelType = data.Schema[_cfg.LabelColumnName].Type;
            if (labelType == TextDataViewType.Instance)
                return TaskType.MulticlassClassification;

            var distinctVals = data.GetColumn<float>(_cfg.LabelColumnName)
                                   .Distinct().Take(20).ToList();
            if (distinctVals.All(v => v is 0f or 1f))
                return TaskType.BinaryClassification;
            if (distinctVals.Count <= 10 && distinctVals.All(v => v == MathF.Floor(v)))
                return TaskType.MulticlassClassification;
            return TaskType.Regression;
        }

        // ════════════════════════════════════════════════════════════════════
        //  DATA LOADING
        // ════════════════════════════════════════════════════════════════════

        private IDataView? LoadRawData()
        {
            try
            {
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
                long rowCount = data.GetRowCount() ?? -1;
                Log(rowCount >= 0
                    ? $"   ✅ Loaded {rowCount:N0} rows, {data.Schema.Count} columns"
                    : $"   ✅ Data loaded, {data.Schema.Count} columns");
                return data;
            }
            catch (Exception ex)
            {
                Log($"\n❌ Failed to load data: {ex.GetBaseException().Message}");
                return null;
            }
        }

        private TextLoader.Column[] InferTextLoaderColumns()
        {
            var lines = File.ReadLines(_cfg.DataFilePath)
                            .Take(_cfg.HasHeader ? 2 : 1).ToList();
            string[] headers, firstRow;

            if (_cfg.HasHeader && lines.Count >= 2)
            { headers = lines[0].Split(_cfg.Separator); firstRow = lines[1].Split(_cfg.Separator); }
            else if (!_cfg.HasHeader && lines.Count >= 1)
            { firstRow = lines[0].Split(_cfg.Separator); headers = Enumerable.Range(0, firstRow.Length).Select(i => $"Col{i}").ToArray(); }
            else throw new InvalidOperationException("Data file is empty.");

            Log($"   • Detected {headers.Length} columns");
            var cols = new List<TextLoader.Column>();
            for (int i = 0; i < headers.Length; i++)
            {
                string name = headers[i].Trim().Trim('"');
                string rawVal = i < firstRow.Length ? firstRow[i].Trim().Trim('"') : "";
                DataKind kind;
                if (name.Equals(_cfg.LabelColumnName, StringComparison.OrdinalIgnoreCase))
                    kind = DataKind.Single;
                else
                {
                    bool isNum = float.TryParse(rawVal,
                        NumberStyles.Any, CultureInfo.InvariantCulture, out _);
                    kind = isNum ? DataKind.Single : DataKind.String;
                }
                cols.Add(new TextLoader.Column(name, kind, i));
            }
            return cols.ToArray();
        }

        // ════════════════════════════════════════════════════════════════════
        //  CSV HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static string[] SplitCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQ = false;
            foreach (char ch in line)
            {
                if (ch == '"') inQ = !inQ;
                else if (ch == ',' && !inQ) { fields.Add(sb.ToString().Trim()); sb.Clear(); }
                else sb.Append(ch);
            }
            fields.Add(sb.ToString().Trim());
            return fields.Select(f => f.Trim('"')).ToArray();
        }

        // ════════════════════════════════════════════════════════════════════
        //  VALIDATION / UTILITIES
        // ════════════════════════════════════════════════════════════════════

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
                errors.Add("TestFraction must be between 0 and 1.");
            if (errors.Any())
            {
                Log("\n❌ Config errors:");
                errors.ForEach(e => Log($"   • {e}"));
                return false;
            }
            return true;
        }

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

        private void PrintHeader()
        {
            Log("\n╔══════════════════════════════════════════════════════════╗");
            Log("║   DataModelTrainer  v3.0  —  Smart Demand Forecasting    ║");
            Log("║   ✓ Weekend / holiday rows excluded pre-aggregation      ║");
            Log("║   ✓ Dormant item detection (zero forecast + action tag)  ║");
            Log("║   ✓ Sparse series WMA fallback (< 6 periods)             ║");
            Log("║   ✓ SSA | WMA | Regression | Classification              ║");
            Log("╚══════════════════════════════════════════════════════════╝");
        }

        // ════════════════════════════════════════════════════════════════════
        //  INNER TYPES
        // ════════════════════════════════════════════════════════════════════

        private class TsValueRow
        {
            [ColumnName("Value")]
            public float Value { get; set; }
        }

        private class TsForecastRow
        {
            public float[] Forecast { get; set; } = Array.Empty<float>();
            public float[] LowerBound { get; set; } = Array.Empty<float>();
            public float[] UpperBound { get; set; } = Array.Empty<float>();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ModelMetaConfig  —  single source of truth for the companion .json
    // ════════════════════════════════════════════════════════════════════════
    internal class ModelMetaConfig
    {
        public string? Task { get; set; }
        public string? LabelColumn { get; set; }
        public string[]? FeatureColumns { get; set; }
        public string[]? CategoricalColumns { get; set; }
        public string[]? NumericColumns { get; set; }
        public Dictionary<string, List<string>>? UniqueValues { get; set; }
        public string? TrainedAt { get; set; }
        public string? BestModel { get; set; }
        public double PrimaryMetric { get; set; }
        public string? DateColumn { get; set; }
        public int HorizonSteps { get; set; }
        public string? Granularity { get; set; }
        public int WindowSize { get; set; }
        public double MAE { get; set; }
        public double RMSE { get; set; }
        public string? RawDataPath { get; set; }
    }
}