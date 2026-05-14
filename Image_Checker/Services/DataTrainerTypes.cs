// ══════════════════════════════════════════════════════════════════════════════
//  DataTrainerTypes.cs
//
//  All configuration POCOs and shared enums for DataModelTrainer.
//  Version 3.0 — adds working-day calendar to DataTrainerConfig so that
//  weekend rows and Indian national holidays are stripped BEFORE the
//  time-series aggregation step.  This prevents the "sawtooth" zero-demand
//  artefact that wrecked SSA accuracy in earlier builds.
// ══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace Image_Checker.Services
{
    // ════════════════════════════════════════════════════════════════════════
    //  TASK TYPE
    // ════════════════════════════════════════════════════════════════════════

    public enum TaskType
    {
        Auto,
        BinaryClassification,
        MulticlassClassification,
        Regression,
        TimeSeries
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ALGORITHM OPTIONS
    //  Controls which trainers are evaluated in the tabular pipeline.
    // ════════════════════════════════════════════════════════════════════════

    public class AlgorithmOptions
    {
        // Binary / multi-class
        public bool UseSDCA { get; set; } = true;
        public bool UseLBFGS { get; set; } = true;
        public bool UseAveragedPerceptron { get; set; } = false;
        public bool UseFastTree { get; set; } = true;
        public bool UseFastForest { get; set; } = true;
        public bool UseLightGBM { get; set; } = true;

        // Regression
        public bool UseSdcaRegression { get; set; } = true;
        public bool UseOlsRegression { get; set; } = true;
        public bool UseFastTreeRegression { get; set; } = true;
        public bool UseFastForestRegression { get; set; } = true;
        public bool UseLightGbmRegression { get; set; } = true;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  TIME-SERIES OPTIONS
    // ════════════════════════════════════════════════════════════════════════

    public class TimeSeriesOptions
    {
        /// <summary>Column that holds transaction / order dates.</summary>
        public string DateColumn { get; set; } = "TRX_DATE";

        /// <summary>Day | Week | Month | Year</summary>
        public string Granularity { get; set; } = "Month";

        /// <summary>How many future periods to forecast.</summary>
        public int HorizonSteps { get; set; } = 12;

        /// <summary>SSA window size (0 = auto-derive).</summary>
        public int WindowSize { get; set; } = 0;

        /// <summary>SSA series length (0 = auto-derive).</summary>
        public int SeriesLength { get; set; } = 0;

        /// <summary>Training rows (0 = use all).</summary>
        public int TrainSize { get; set; } = 0;

        /// <summary>Confidence-interval level (default 95 %).</summary>
        public float ConfidenceLevel { get; set; } = 0.95f;

        /// <summary>
        /// Items with no demand for this many months are classified DORMANT
        /// and receive a zero forecast instead of SSA extrapolation.
        /// </summary>
        public int DormantMonths { get; set; } = 12;

        /// <summary>
        /// Minimum number of aggregated periods required before SSA is used.
        /// Items with fewer periods receive a Weighted Moving Average forecast.
        /// </summary>
        public int MinActivePeriodsForSSA { get; set; } = 6;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MAIN TRAINER CONFIG
    // ════════════════════════════════════════════════════════════════════════

    public class DataTrainerConfig
    {
        // ── Data source ──────────────────────────────────────────────────────
        public string DataFilePath { get; set; } = "";
        public char Separator { get; set; } = ',';
        public bool HasHeader { get; set; } = true;

        // ── Column mapping ───────────────────────────────────────────────────
        public string LabelColumnName { get; set; } = "QUANTITY_INVOICED";

        /// <summary>Columns to use as features (empty = all non-label cols).</summary>
        public List<string> FeatureColumns { get; set; } = new();

        /// <summary>Columns to treat as categorical text (empty = auto-detect).</summary>
        public List<string> CategoricalColumns { get; set; } = new();

        /// <summary>Columns to ignore completely.</summary>
        public List<string> IgnoreColumns { get; set; } = new();

        // ── Training control ─────────────────────────────────────────────────
        public TaskType Task { get; set; } = TaskType.Auto;
        public double TestFraction { get; set; } = 0.2;
        public int Seed { get; set; } = 42;

        // ── Time-series ──────────────────────────────────────────────────────
        public TimeSeriesOptions TimeSeries { get; set; } = new();

        // ── Output ───────────────────────────────────────────────────────────
        public string? OutputDirectory { get; set; }

        // ── ✅ NEW: Working-day calendar ────────────────────────────────────
        //
        //  Rows whose date falls on a NonWorkingDay or in the Holidays set are
        //  excluded BEFORE the time-series aggregation step.
        //
        //  Why this matters: if a customer never places orders on Sundays,
        //  including Sunday rows (qty = 0) in the series creates artificial
        //  demand troughs.  SSA interprets these as structural seasonality and
        //  extrapolates a sawtooth pattern into the future forecast.
        //
        //  For monthly aggregation the effect is diluted but not zero — a
        //  transaction incorrectly dated on a Sunday (data-entry error) would
        //  still skew the period total if not caught here.

        /// <summary>Days of week with no customer supply / no valid orders.</summary>
        public DayOfWeek[] NonWorkingDays { get; set; } =
        {
            DayOfWeek.Saturday,
            DayOfWeek.Sunday
        };

        /// <summary>
        /// Fixed holiday dates to exclude from training data.
        /// Defaults to Indian national holidays for a ±10-year window.
        /// Add company-specific closures here; remove any that do not apply.
        /// </summary>
        public HashSet<DateTime> Holidays { get; set; } = BuildDefaultIndianHolidays();

        // ── ✅ NEW: Algorithms toggle ────────────────────────────────────────
        public AlgorithmOptions Algorithms { get; set; } = new();

        // ────────────────────────────────────────────────────────────────────
        private static HashSet<DateTime> BuildDefaultIndianHolidays()
        {
            var h = new HashSet<DateTime>();
            int from = DateTime.Today.Year - 10;
            int to = DateTime.Today.Year + 5;

            for (int yr = from; yr <= to; yr++)
            {
                // ── National / gazetted holidays ─────────────────────────────
                h.Add(new DateTime(yr, 1, 1));   // New Year's Day
                h.Add(new DateTime(yr, 1, 26));   // Republic Day
                h.Add(new DateTime(yr, 8, 15));   // Independence Day
                h.Add(new DateTime(yr, 10, 2));   // Gandhi Jayanti
                h.Add(new DateTime(yr, 11, 14));   // Children's Day

                // ── Common festival approximations (fixed-date proxies) ──────
                // Replace with exact dates from a calendar API if precision matters.
                h.Add(new DateTime(yr, 3, 1));   // Holi (approx)
                h.Add(new DateTime(yr, 8, 1));   // Raksha Bandhan (approx)
                h.Add(new DateTime(yr, 8, 8));   // Janmashtami (approx)
                h.Add(new DateTime(yr, 10, 24));   // Dussehra (approx)
                h.Add(new DateTime(yr, 11, 1));   // Diwali day 1 (approx)
                h.Add(new DateTime(yr, 11, 2));   // Diwali day 2 (approx)
                h.Add(new DateTime(yr, 12, 25));   // Christmas
            }
            return h;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MODEL RESULT  (carries the trained model + its metrics)
    // ════════════════════════════════════════════════════════════════════════

    public class ModelResult
    {
        public string Name { get; set; } = "";
        public TaskType Task { get; set; }
        public Microsoft.ML.ITransformer? Model { get; set; }

        // Primary metric (higher = better for all tasks):
        //   Binary classification  → Accuracy
        //   Multi-class            → MacroAccuracy
        //   Regression             → R²
        public double PrimaryMetric { get; set; }

        // Secondary / tertiary for display only
        public double SecondaryMetric { get; set; }
        public double TertiaryMetric { get; set; }
    }
}