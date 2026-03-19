// ══════════════════════════════════════════════════════════════════════════════
//  DataTrainerTypes.cs
//  All shared configuration and result types used by DataModelTrainer,
//  ModelBuilderForm, and PredictionReportExporter.
//
//  After adding this file, remove these classes from DataModelTrainer.cs:
//    - AlgorithmOptions
//    - DataTrainerConfig
//    - TimeSeriesOptions
//    - ModelResult
// ══════════════════════════════════════════════════════════════════════════════

using Microsoft.ML;
using System.Collections.Generic;

namespace Image_Checker.Services
{

    public enum TaskType
    {
        Auto,
        BinaryClassification,
        MulticlassClassification,
        Regression,
        TimeSeries           // SSA-based forecasting
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  AlgorithmOptions
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Toggles which baseline / tunable algorithms are included in the
    /// comparison sweep. Separate flags exist for classification vs regression
    /// because the underlying ML.NET trainers are different types.
    /// </summary>
    public class AlgorithmOptions
    {
        // ── Classification ────────────────────────────────────────────────────
        /// <summary>SDCA Maximum Entropy (fast linear, good baseline).</summary>
        public bool UseSDCA { get; set; } = true;

        /// <summary>LBFGS Maximum Entropy (more accurate linear, slower).</summary>
        public bool UseLBFGS { get; set; } = true;

        /// <summary>FastTree gradient-boosted decision trees.</summary>
        public bool UseFastTree { get; set; } = true;

        /// <summary>FastForest random forest.</summary>
        public bool UseFastForest { get; set; } = true;

        /// <summary>LightGBM gradient boosting (fast, high accuracy).</summary>
        public bool UseLightGBM { get; set; } = true;

        /// <summary>Averaged Perceptron – binary classification only.</summary>
        public bool UseAveragedPerceptron { get; set; } = false;

        // ── Regression ────────────────────────────────────────────────────────
        /// <summary>SDCA regression.</summary>
        public bool UseSdcaRegression { get; set; } = true;

        /// <summary>FastTree regression.</summary>
        public bool UseFastTreeRegression { get; set; } = true;

        /// <summary>LightGBM regression.</summary>
        public bool UseLightGbmRegression { get; set; } = true;

        /// <summary>FastForest regression.</summary>
        public bool UseFastForestRegression { get; set; } = true;

        /// <summary>
        /// Linear SGD regression (OnlineGradientDescent) – fast interpretable baseline.
        /// Swap for Ols() if Microsoft.ML.Mkl.Components NuGet is installed.
        /// </summary>
        public bool UseOlsRegression { get; set; } = true;

        // ── Hyperparameter tuning ─────────────────────────────────────────────
        /// <summary>Number of random hyperparameter trials per tunable trainer.</summary>
        public int TuningTrials { get; set; } = 5;

        /// <summary>Number of cross-validation folds used during tuning.</summary>
        public int CvFolds { get; set; } = 3;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TimeSeriesOptions
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Configuration for SSA-based time-series forecasting.
    /// Only used when <see cref="DataTrainerConfig.Task"/> ==
    /// <see cref="TaskType.TimeSeries"/>.
    /// </summary>
    public class TimeSeriesOptions
    {
        /// <summary>
        /// Name of the date/time column used for ordering.
        /// The data should already be sorted by this column for best results.
        /// </summary>
        public string? DateColumn { get; set; }

        /// <summary>Number of future time steps to forecast. Default: 12.</summary>
        public int HorizonSteps { get; set; } = 12;

        /// <summary>
        /// SSA sliding window size. Must be greater than
        /// <see cref="HorizonSteps"/>. Default: 24.
        /// </summary>
        public int WindowSize { get; set; } = 24;

        /// <summary>
        /// Number of historical observations fed into the SSA decomposition.
        /// Set to 0 to use all training rows automatically.
        /// </summary>
        public int SeriesLength { get; set; } = 100;

        /// <summary>
        /// Number of rows used for training.
        /// 0 = derived automatically from <see cref="DataTrainerConfig.TestFraction"/>.
        /// </summary>
        public int TrainSize { get; set; } = 0;

        /// <summary>Confidence level for prediction interval bands. Default: 0.95.</summary>
        public float ConfidenceLevel { get; set; } = 0.95f;

        /// <summary>
        /// Step granularity used when forecasting: "Day", "Month", or "Year".
        /// Controls how period labels are generated in the prediction output.
        /// Default: "Month".
        /// </summary>
        public string Granularity { get; set; } = "Month";
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DataTrainerConfig
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Complete configuration object passed to <see cref="DataModelTrainer"/>.
    /// Covers data source, column selection, task type, train/test split,
    /// algorithm selection and output directory.
    /// </summary>
    public class DataTrainerConfig
    {
        // ── Data source ───────────────────────────────────────────────────────

        /// <summary>Full path to the CSV or TSV data file.</summary>
        public string DataFilePath { get; set; } = string.Empty;

        /// <summary>Column separator character. Default: ','.</summary>
        public char Separator { get; set; } = ',';

        /// <summary>Whether the file contains a header row. Default: true.</summary>
        public bool HasHeader { get; set; } = true;

        // ── Column selection ──────────────────────────────────────────────────

        /// <summary>
        /// Name of the column the model should predict (the label / target).
        /// For TimeSeries this is the numeric value column to forecast.
        /// </summary>
        public string LabelColumnName { get; set; } = "Label";

        /// <summary>
        /// Columns to include as features.
        /// Leave empty to use ALL columns except the label automatically.
        /// </summary>
        public List<string> FeatureColumns { get; set; } = new();

        /// <summary>
        /// Columns to explicitly ignore (e.g. ID columns, free-text notes).
        /// Applied after <see cref="FeatureColumns"/> selection.
        /// </summary>
        public List<string> IgnoreColumns { get; set; } = new();

        /// <summary>
        /// Columns that hold categorical / enum text values requiring encoding.
        /// If left empty the trainer auto-detects non-numeric columns.
        /// </summary>
        public List<string> CategoricalColumns { get; set; } = new();

        // ── Task ──────────────────────────────────────────────────────────────

        /// <summary>
        /// ML task to perform. Use <see cref="TaskType.Auto"/> to let the
        /// trainer detect it from the label column's values.
        /// </summary>
        public TaskType Task { get; set; } = TaskType.Auto;

        // ── Train / Test split ────────────────────────────────────────────────

        /// <summary>
        /// Fraction of data reserved for testing. Must be between 0 and 1.
        /// Default: 0.20 (20% test, 80% train).
        /// </summary>
        public double TestFraction { get; set; } = 0.20;

        /// <summary>Random seed for reproducible train/test splits.</summary>
        public int Seed { get; set; } = 42;

        // ── Time-series options ───────────────────────────────────────────────

        /// <summary>
        /// SSA forecasting options. Only used when
        /// <see cref="Task"/> == <see cref="TaskType.TimeSeries"/>.
        /// </summary>
        public TimeSeriesOptions TimeSeries { get; set; } = new();

        // ── Algorithm selection ───────────────────────────────────────────────

        /// <summary>
        /// Toggles for individual algorithms. Algorithms not applicable to
        /// the resolved task type are silently skipped.
        /// </summary>
        public AlgorithmOptions Algorithms { get; set; } = new();

        // ── Output ────────────────────────────────────────────────────────────

        /// <summary>
        /// Directory where the best model .zip and its companion .json
        /// config file are saved.
        /// Defaults to the same directory as <see cref="DataFilePath"/>.
        /// </summary>
        public string OutputDirectory { get; set; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ModelResult
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Holds the evaluation metrics and trained model for a single algorithm
    /// after the comparison sweep. The meaning of each metric depends on the
    /// task type:
    /// <list type="bullet">
    ///   <item>Binary        → PrimaryMetric = Accuracy,      Secondary = AUC-ROC, Tertiary = F1</item>
    ///   <item>Multiclass    → PrimaryMetric = MacroAccuracy,  Secondary = MicroAccuracy, Tertiary = LogLoss</item>
    ///   <item>Regression    → PrimaryMetric = R²,             Secondary = MAE,     Tertiary = RMSE</item>
    ///   <item>TimeSeries    → PrimaryMetric = −MAE (negated so higher = better), Secondary = RMSE</item>
    /// </list>
    /// </summary>
    public class ModelResult
    {
        /// <summary>Human-readable algorithm name, e.g. "LightGBM_Multi".</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Primary ranking metric (higher is always better).
        /// Accuracy / MacroAccuracy / R² / −MAE depending on task.
        /// </summary>
        public double PrimaryMetric { get; set; }

        /// <summary>Secondary metric (AUC-ROC / MicroAccuracy / MAE / RMSE).</summary>
        public double SecondaryMetric { get; set; }

        /// <summary>Tertiary metric (F1 / LogLoss / RMSE / 0 for TS).</summary>
        public double TertiaryMetric { get; set; }

        /// <summary>The fitted ITransformer. Null if training failed.</summary>
        public ITransformer? Model { get; set; }

        /// <summary>The task type this result was evaluated under.</summary>
        public TaskType Task { get; set; }
    }
}