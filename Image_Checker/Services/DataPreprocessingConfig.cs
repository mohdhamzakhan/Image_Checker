// ══════════════════════════════════════════════════════════════════════════════
//  DataPreprocessingConfig.cs
//  All enums and configuration POCOs for the preprocessing pipeline.
//  These are serialised to JSON alongside the saved model so downstream
//  applications know exactly what transforms were applied.
// ══════════════════════════════════════════════════════════════════════════════

namespace Image_Checker.Services
{
    // ── Cleaning ──────────────────────────────────────────────────────────────

    public enum MissingValueStrategy
    {
        Mean,       // replace with column mean   (numeric)
        Median,     // replace with column median (numeric, robust to outliers)
        Mode,       // replace with most frequent value (any type)
        DeleteRow   // remove the row entirely
    }

    public enum OutlierDetectionMethod { None, IQR, ZScore }
    public enum OutlierAction          { Cap, Remove }

    // ── Transformation ────────────────────────────────────────────────────────

    public enum NormalizationMethod
    {
        None,
        MinMax,         // scale to [0, 1]
        ZScore,         // (x - mean) / stddev
        DecimalScaling, // divide by 10^k so max(|x|) < 1
        Log             // ln(x+1), use when data is right-skewed
    }

    public enum CategoricalEncoding
    {
        OneHot,         // create binary dummy columns (ML.NET handles this)
        LabelEncoding   // replace category with integer index
    }

    // ── Reduction ─────────────────────────────────────────────────────────────

    public enum DimensionalityReductionMethod { None, PCA }
    public enum FeatureSelectionMethod        { None, VarianceFilter, TopNCorrelation }
    public enum SamplingMethod                { None, Random, Stratified }

    // ══════════════════════════════════════════════════════════════════════════
    //  Config POCOs
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Step 1 – Data Cleaning options.</summary>
    public class DataCleaningConfig
    {
        public MissingValueStrategy  MissingValueStrategy { get; set; } = MissingValueStrategy.Mean;
        public bool                  RemoveDuplicates     { get; set; } = true;
        public OutlierDetectionMethod OutlierMethod       { get; set; } = OutlierDetectionMethod.None;
        public OutlierAction         OutlierAction        { get; set; } = OutlierAction.Cap;
        public double                ZScoreThreshold      { get; set; } = 3.0;
        public double                IQRMultiplier        { get; set; } = 1.5;
    }

    /// <summary>Step 3 – Data Transformation options.</summary>
    public class DataTransformationConfig
    {
        public NormalizationMethod  Normalization   { get; set; } = NormalizationMethod.MinMax;
        public CategoricalEncoding  Encoding        { get; set; } = CategoricalEncoding.OneHot;
        public bool                 EnableBinning   { get; set; } = false;
        public int                  NumberOfBins    { get; set; } = 10;
    }

    /// <summary>Step 4 – Data Reduction options.</summary>
    public class DataReductionConfig
    {
        public DimensionalityReductionMethod DimReduction     { get; set; } = DimensionalityReductionMethod.None;
        public int                           PCAComponents     { get; set; } = 10;
        public FeatureSelectionMethod        FeatureSelection  { get; set; } = FeatureSelectionMethod.None;
        public int                           TopNFeatures      { get; set; } = 10;
        public double                        VarianceThreshold { get; set; } = 0.01;
        public SamplingMethod                Sampling          { get; set; } = SamplingMethod.None;
        public double                        SampleFraction    { get; set; } = 0.8;
        public int                           SamplingSeed      { get; set; } = 42;
    }

    /// <summary>Complete preprocessing config – all 3 steps bundled together.</summary>
    public class FullPreprocessingConfig
    {
        public DataCleaningConfig        Cleaning       { get; set; } = new();
        public DataTransformationConfig  Transformation { get; set; } = new();
        public DataReductionConfig       Reduction      { get; set; } = new();
    }

    /// <summary>Summary returned by DataPreprocessor.Run() for display in the log.</summary>
    public class PreprocessingSummary
    {
        public int    OriginalRows        { get; set; }
        public int    FinalRows           { get; set; }
        public int    OriginalColumns     { get; set; }
        public int    FinalColumns        { get; set; }
        public int    DuplicatesRemoved   { get; set; }
        public int    OutliersHandled     { get; set; }
        public int    MissingValuesFixed  { get; set; }
        public string TempCsvPath         { get; set; } = string.Empty;
        public List<string> FinalFeatureCols { get; set; } = new();
        public List<string> Log           { get; set; } = new();
    }
}