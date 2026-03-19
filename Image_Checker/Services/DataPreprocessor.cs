// ══════════════════════════════════════════════════════════════════════════════
//  DataPreprocessor.cs
//  Applies all 4 preprocessing steps on a DataTable, then writes the cleaned
//  data to a temp CSV so DataModelTrainer can load it via ML.NET's TextLoader.
//
//  NuGet dependencies required in your .csproj:
//    <PackageReference Include="ExcelDataReader"         Version="3.6.0" />
//    <PackageReference Include="ExcelDataReader.DataSet" Version="3.6.0" />
// ══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace Image_Checker.Services
{
    public class DataPreprocessor
    {
        // ── Label-encoding map (exported so caller can decode predictions) ────
        public Dictionary<string, Dictionary<string, int>> LabelEncodingMaps { get; } = new();

        // ─── Public Entry Point ───────────────────────────────────────────────

        /// <summary>
        /// Loads the file, runs all configured preprocessing steps in order,
        /// writes the result to a temp CSV and returns a summary with the path.
        /// </summary>
        public PreprocessingSummary Run(
            string filePath,
            string labelColumn,
            List<string> featureColumns,
            List<string> categoricalColumns,
            List<string> ignoreColumns,
            FullPreprocessingConfig cfg)
        {
            var summary = new PreprocessingSummary();
            summary.Log.Add($"═══ DataPreprocessor started at {DateTime.Now:HH:mm:ss} ═══");

            // ── 1. Load file ──────────────────────────────────────────────────
            summary.Log.Add("\n📂 Step 0 – Load File");
            var dt = LoadFile(filePath, summary);
            if (dt == null || dt.Rows.Count == 0)
                throw new InvalidOperationException("File could not be loaded or is empty.");

            summary.OriginalRows = dt.Rows.Count;
            summary.OriginalColumns = dt.Columns.Count;
            summary.Log.Add($"   Loaded {dt.Rows.Count} rows × {dt.Columns.Count} cols");

            // Drop ignored columns
            foreach (var col in ignoreColumns)
                if (dt.Columns.Contains(col))
                    dt.Columns.Remove(col);

            // Resolve feature columns (defaults to everything except label)
            var allCols = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            var usedFeatures = featureColumns.Count > 0
                ? featureColumns.Where(f => dt.Columns.Contains(f)).ToList()
                : allCols.Where(c => c != labelColumn).ToList();

            // Separate numeric vs categorical features
            var numericFeatures = usedFeatures
                .Where(c => IsNumericColumn(dt, c) && !categoricalColumns.Contains(c))
                .ToList();
            var catFeatures = usedFeatures
                .Where(c => !IsNumericColumn(dt, c) || categoricalColumns.Contains(c))
                .ToList();

            // ── 2. Data Cleaning ─────────────────────────────────────────────
            summary.Log.Add("\n🧹 Step 1 – Data Cleaning");

            if (cfg.Cleaning.RemoveDuplicates)
            {
                int before = dt.Rows.Count;
                dt = RemoveDuplicates(dt);
                summary.DuplicatesRemoved = before - dt.Rows.Count;
                summary.Log.Add($"   Duplicates removed : {summary.DuplicatesRemoved}");
            }

            int missingFixed = 0;
            ImputeMissingValues(dt, numericFeatures, catFeatures,
                cfg.Cleaning.MissingValueStrategy, ref missingFixed);
            summary.MissingValuesFixed = missingFixed;
            summary.Log.Add($"   Missing values fixed: {missingFixed}");

            int outliersHandled = 0;
            if (cfg.Cleaning.OutlierMethod != OutlierDetectionMethod.None)
            {
                HandleOutliers(dt, numericFeatures, cfg.Cleaning, ref outliersHandled, summary.Log);
                summary.OutliersHandled = outliersHandled;
                summary.Log.Add($"   Outliers handled   : {outliersHandled}");
            }

            // ── 3. Data Transformation ───────────────────────────────────────
            summary.Log.Add("\n🔄 Step 2 – Data Transformation");

            // Normalization (numeric feature columns only – not the label)
            if (cfg.Transformation.Normalization != NormalizationMethod.None)
            {
                NormalizeColumns(dt, numericFeatures, cfg.Transformation.Normalization);
                summary.Log.Add($"   Normalization: {cfg.Transformation.Normalization} on {numericFeatures.Count} col(s)");
            }

            // Categorical encoding
            var newCatCols = new List<string>();
            if (cfg.Transformation.Encoding == CategoricalEncoding.LabelEncoding)
            {
                foreach (var col in catFeatures.Where(c => c != labelColumn))
                {
                    ApplyLabelEncoding(dt, col);
                    newCatCols.Add(col);  // column stays, now numeric
                }
                summary.Log.Add($"   Label encoding     : {catFeatures.Count} col(s)");
            }
            else  // OneHot – mark cols; ML.NET pipeline handles OHE
            {
                summary.Log.Add($"   One-hot encoding   : deferred to ML.NET pipeline ({catFeatures.Count} col(s))");
                newCatCols.AddRange(catFeatures.Where(c => c != labelColumn));
            }

            // Binning
            if (cfg.Transformation.EnableBinning)
            {
                foreach (var col in numericFeatures)
                {
                    ApplyBinning(dt, col, cfg.Transformation.NumberOfBins);
                }
                summary.Log.Add($"   Binning ({cfg.Transformation.NumberOfBins} bins): {numericFeatures.Count} col(s)");
            }

            // ── 4. Data Reduction ────────────────────────────────────────────
            summary.Log.Add("\n📉 Step 3 – Data Reduction");

            // Feature selection before sampling
            var selectedFeatures = usedFeatures.ToList();
            if (cfg.Reduction.FeatureSelection == FeatureSelectionMethod.VarianceFilter)
            {
                selectedFeatures = FilterByVariance(dt, numericFeatures, cfg.Reduction.VarianceThreshold, summary.Log);
                selectedFeatures.AddRange(catFeatures.Where(c => c != labelColumn));
                summary.Log.Add($"   Variance filter: kept {selectedFeatures.Count} / {usedFeatures.Count} feature(s)");
            }
            else if (cfg.Reduction.FeatureSelection == FeatureSelectionMethod.TopNCorrelation)
            {
                selectedFeatures = SelectTopNByCorrelation(dt, numericFeatures, labelColumn,
                    cfg.Reduction.TopNFeatures, summary.Log);
                selectedFeatures.AddRange(catFeatures.Where(c => c != labelColumn));
                summary.Log.Add($"   Top-{cfg.Reduction.TopNFeatures} correlation: kept {selectedFeatures.Count} feature(s)");
            }

            // Drop unselected feature columns
            var toDrop = usedFeatures
                .Except(selectedFeatures)
                .Except(new[] { labelColumn })
                .ToList();
            foreach (var col in toDrop)
                if (dt.Columns.Contains(col))
                    dt.Columns.Remove(col);

            // Sampling
            if (cfg.Reduction.Sampling != SamplingMethod.None)
            {
                int before = dt.Rows.Count;
                dt = SampleData(dt, cfg.Reduction.SampleFraction,
                    cfg.Reduction.Sampling, labelColumn, cfg.Reduction.SamplingSeed);
                summary.Log.Add($"   Sampling ({cfg.Reduction.Sampling}): {before} → {dt.Rows.Count} rows");
            }

            // Note: PCA is applied inside the ML.NET pipeline (see DataModelTrainer)
            if (cfg.Reduction.DimReduction == DimensionalityReductionMethod.PCA)
                summary.Log.Add($"   PCA ({cfg.Reduction.PCAComponents} components): will be applied in ML.NET pipeline");

            // ── 5. Write temp CSV ─────────────────────────────────────────────
            summary.Log.Add("\n💾 Saving cleaned data to temp CSV...");
            var tempPath = Path.Combine(Path.GetTempPath(),
                $"preprocessed_{DateTime.Now:yyyyMMddHHmmss}.csv");
            WriteCsv(dt, tempPath);

            summary.FinalRows = dt.Rows.Count;
            summary.FinalColumns = dt.Columns.Count;
            summary.TempCsvPath = tempPath;
            summary.FinalFeatureCols = dt.Columns.Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .Where(c => c != labelColumn)
                .ToList();

            summary.Log.Add($"   Saved to: {tempPath}");
            summary.Log.Add($"\n✅ Preprocessing complete: {summary.FinalRows} rows × {summary.FinalColumns} cols");
            summary.Log.Add($"   Features: {string.Join(", ", summary.FinalFeatureCols)}");

            return summary;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  FILE LOADING
        // ─────────────────────────────────────────────────────────────────────

        public DataTable? LoadFile(string path, PreprocessingSummary? summary = null)
        {
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                return ext is ".xlsx" or ".xls"
                    ? LoadExcel(path)
                    : LoadCsv(path);
            }
            catch (Exception ex)
            {
                summary?.Log.Add($"   ❌ Load error: {ex.Message}");
                return null;
            }
        }

        private DataTable LoadExcel(string path)
        {
            // Requires ExcelDataReader + ExcelDataReader.DataSet NuGet packages
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);
            var ds = reader.AsDataSet(new ExcelDataReader.ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataReader.ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });
            return ds.Tables[0];
        }

        private DataTable LoadCsv(string path)
        {
            var dt = new DataTable();
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length == 0) return dt;

            // Parse header
            var headers = SplitCsvLine(lines[0]);
            foreach (var h in headers)
                dt.Columns.Add(h.Trim().Trim('"'));

            // Parse rows
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var parts = SplitCsvLine(lines[i]);
                var row = dt.NewRow();
                for (int j = 0; j < dt.Columns.Count && j < parts.Length; j++)
                    row[j] = parts[j].Trim().Trim('"');
                dt.Rows.Add(row);
            }
            return dt;
        }

        private static string[] SplitCsvLine(string line)
        {
            // Handles quoted fields containing commas
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CLEANING METHODS
        // ─────────────────────────────────────────────────────────────────────

        private static DataTable RemoveDuplicates(DataTable dt)
        {
            var seen = new HashSet<string>();
            var clean = dt.Clone();
            foreach (DataRow row in dt.Rows)
            {
                var key = string.Join("|", row.ItemArray);
                if (seen.Add(key))
                    clean.ImportRow(row);
            }
            return clean;
        }

        private void ImputeMissingValues(
            DataTable dt,
            List<string> numericCols,
            List<string> categoricalCols,
            MissingValueStrategy strategy,
            ref int fixedCount)
        {
            if (strategy == MissingValueStrategy.DeleteRow)
            {
                for (int i = dt.Rows.Count - 1; i >= 0; i--)
                {
                    var row = dt.Rows[i];
                    bool hasMissing = row.ItemArray.Any(v =>
                        v == null || v == DBNull.Value || string.IsNullOrWhiteSpace(v.ToString()));
                    if (hasMissing) { fixedCount++; dt.Rows.RemoveAt(i); }
                }
                return;
            }

            // Numeric columns
            foreach (var col in numericCols.Where(c => dt.Columns.Contains(c)))
            {
                var vals = dt.AsEnumerable()
                    .Select(r => TryParseDouble(r[col]?.ToString()))
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                if (!vals.Any()) continue;
                double fill = strategy switch
                {
                    MissingValueStrategy.Mean => vals.Average(),
                    MissingValueStrategy.Median => Median(vals),
                    MissingValueStrategy.Mode => vals.GroupBy(v => v)
                                                       .OrderByDescending(g => g.Count())
                                                       .First().Key,
                    _ => vals.Average()
                };

                foreach (DataRow row in dt.Rows)
                {
                    if (row[col] == DBNull.Value || string.IsNullOrWhiteSpace(row[col]?.ToString()))
                    {
                        row[col] = fill.ToString("G");
                        fixedCount++;
                    }
                }
            }

            // Categorical columns
            foreach (var col in categoricalCols.Where(c => dt.Columns.Contains(c)))
            {
                var mode = dt.AsEnumerable()
                    .Select(r => r[col]?.ToString() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .GroupBy(s => s)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "Unknown";

                foreach (DataRow row in dt.Rows)
                {
                    if (row[col] == DBNull.Value || string.IsNullOrWhiteSpace(row[col]?.ToString()))
                    {
                        row[col] = mode;
                        fixedCount++;
                    }
                }
            }
        }

        private void HandleOutliers(
            DataTable dt,
            List<string> numericCols,
            DataCleaningConfig cfg,
            ref int count,
            List<string> log)
        {
            var rowsToRemove = new HashSet<DataRow>();

            foreach (var col in numericCols.Where(c => dt.Columns.Contains(c)))
            {
                var vals = dt.AsEnumerable()
                    .Select(r => (Row: r, Val: TryParseDouble(r[col]?.ToString())))
                    .Where(x => x.Val.HasValue)
                    .ToList();

                if (!vals.Any()) continue;

                double lower, upper;

                if (cfg.OutlierMethod == OutlierDetectionMethod.IQR)
                {
                    var sorted = vals.Select(x => x.Val!.Value).OrderBy(v => v).ToList();
                    double q1 = Percentile(sorted, 0.25);
                    double q3 = Percentile(sorted, 0.75);
                    double iqr = q3 - q1;
                    lower = q1 - cfg.IQRMultiplier * iqr;
                    upper = q3 + cfg.IQRMultiplier * iqr;
                }
                else  // Z-Score
                {
                    double mean = vals.Average(x => x.Val!.Value);
                    double std = Math.Sqrt(vals.Average(x => Math.Pow(x.Val!.Value - mean, 2)));
                    lower = mean - cfg.ZScoreThreshold * std;
                    upper = mean + cfg.ZScoreThreshold * std;
                }

                foreach (var (row, val) in vals)
                {
                    if (val!.Value < lower || val.Value > upper)
                    {
                        count++;
                        if (cfg.OutlierAction == OutlierAction.Cap)
                        {
                            row[col] = Math.Clamp(val.Value, lower, upper).ToString("G");
                        }
                        else
                        {
                            rowsToRemove.Add(row);
                        }
                    }
                }
            }

            foreach (var row in rowsToRemove)
                dt.Rows.Remove(row);

            if (rowsToRemove.Any())
                log.Add($"   Outlier rows removed: {rowsToRemove.Count}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TRANSFORMATION METHODS
        // ─────────────────────────────────────────────────────────────────────

        private void NormalizeColumns(DataTable dt, List<string> cols, NormalizationMethod method)
        {
            foreach (var col in cols.Where(c => dt.Columns.Contains(c)))
            {
                var vals = dt.AsEnumerable()
                    .Select(r => TryParseDouble(r[col]?.ToString()))
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                if (!vals.Any()) continue;

                double min = vals.Min();
                double max = vals.Max();
                double mean = vals.Average();
                double std = Math.Sqrt(vals.Average(v => Math.Pow(v - mean, 2)));

                // Decimal scaling factor
                double k = 0;
                if (method == NormalizationMethod.DecimalScaling)
                {
                    double maxAbs = vals.Max(Math.Abs);
                    while (maxAbs / Math.Pow(10, k) >= 1) k++;
                }

                foreach (DataRow row in dt.Rows)
                {
                    if (!TryParseDouble(row[col]?.ToString()).HasValue) continue;
                    double v = double.Parse(row[col]!.ToString()!);
                    row[col] = method switch
                    {
                        NormalizationMethod.MinMax =>
                            (max - min) < 1e-10 ? "0" : ((v - min) / (max - min)).ToString("G"),
                        NormalizationMethod.ZScore =>
                            std < 1e-10 ? "0" : ((v - mean) / std).ToString("G"),
                        NormalizationMethod.DecimalScaling =>
                            (v / Math.Pow(10, k)).ToString("G"),
                        NormalizationMethod.Log =>
                            Math.Log(v + 1).ToString("G"),
                        _ => row[col]
                    };
                }
            }
        }

        private void ApplyLabelEncoding(DataTable dt, string col)
        {
            var map = new Dictionary<string, int>();
            int idx = 0;
            foreach (DataRow row in dt.Rows)
            {
                var val = row[col]?.ToString() ?? "";
                if (!map.ContainsKey(val))
                    map[val] = idx++;
                row[col] = map[val].ToString();
            }
            LabelEncodingMaps[col] = map;
        }

        private static void ApplyBinning(DataTable dt, string col, int bins)
        {
            var vals = dt.AsEnumerable()
                .Select(r => TryParseDouble(r[col]?.ToString()))
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (!vals.Any()) return;
            double min = vals.Min(), max = vals.Max();
            double binWidth = (max - min) / bins;
            if (binWidth < 1e-10) return;

            foreach (DataRow row in dt.Rows)
            {
                if (!TryParseDouble(row[col]?.ToString()).HasValue) continue;
                double v = double.Parse(row[col]!.ToString()!);
                int bin = Math.Min((int)((v - min) / binWidth), bins - 1);
                row[col] = bin.ToString();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  REDUCTION METHODS
        // ─────────────────────────────────────────────────────────────────────

        private static List<string> FilterByVariance(
            DataTable dt, List<string> numericCols, double threshold, List<string> log)
        {
            var kept = new List<string>();
            foreach (var col in numericCols.Where(c => dt.Columns.Contains(c)))
            {
                var vals = dt.AsEnumerable()
                    .Select(r => TryParseDouble(r[col]?.ToString()))
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                if (!vals.Any()) continue;
                double mean = vals.Average();
                double variance = vals.Average(v => Math.Pow(v - mean, 2));
                if (variance >= threshold) kept.Add(col);
                else log.Add($"   Dropped (low variance {variance:F4}): {col}");
            }
            return kept;
        }

        private static List<string> SelectTopNByCorrelation(
            DataTable dt, List<string> numericCols, string labelCol,
            int topN, List<string> log)
        {
            if (!dt.Columns.Contains(labelCol)) return numericCols.Take(topN).ToList();

            var labelVals = dt.AsEnumerable()
                .Select(r => TryParseDouble(r[labelCol]?.ToString()))
                .Select(v => v ?? 0.0)
                .ToList();

            var correlations = new Dictionary<string, double>();
            foreach (var col in numericCols.Where(c => dt.Columns.Contains(c)))
            {
                var vals = dt.AsEnumerable()
                    .Select(r => TryParseDouble(r[col]?.ToString()) ?? 0.0)
                    .ToList();
                correlations[col] = Math.Abs(PearsonCorrelation(vals, labelVals));
            }

            var ranked = correlations.OrderByDescending(kv => kv.Value).ToList();
            log.Add("   Correlation ranking:");
            foreach (var kv in ranked.Take(topN))
                log.Add($"     {kv.Key}: r={kv.Value:F4}");

            return ranked.Take(topN).Select(kv => kv.Key).ToList();
        }

        private static DataTable SampleData(
            DataTable dt, double fraction, SamplingMethod method,
            string labelCol, int seed)
        {
            int target = (int)(dt.Rows.Count * fraction);
            if (target >= dt.Rows.Count) return dt;

            var rng = new Random(seed);
            var result = dt.Clone();

            if (method == SamplingMethod.Random)
            {
                var indices = Enumerable.Range(0, dt.Rows.Count)
                    .OrderBy(_ => rng.Next())
                    .Take(target);
                foreach (var i in indices)
                    result.ImportRow(dt.Rows[i]);
            }
            else  // Stratified
            {
                var groups = dt.AsEnumerable()
                    .GroupBy(r => r[labelCol]?.ToString() ?? "")
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var group in groups.Values)
                {
                    int take = (int)(group.Count * fraction);
                    foreach (var row in group.OrderBy(_ => rng.Next()).Take(take))
                        result.ImportRow(row);
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  CSV OUTPUT
        // ─────────────────────────────────────────────────────────────────────

        private static void WriteCsv(DataTable dt, string path)
        {
            using var sw = new StreamWriter(path, false, Encoding.UTF8);

            // Header
            sw.WriteLine(string.Join(",",
                dt.Columns.Cast<DataColumn>().Select(c => EscapeCsv(c.ColumnName))));

            // Rows
            foreach (DataRow row in dt.Rows)
            {
                sw.WriteLine(string.Join(",",
                    row.ItemArray.Select(v => EscapeCsv(v?.ToString() ?? ""))));
            }
        }

        private static string EscapeCsv(string s)
            => s.Contains(',') || s.Contains('"') || s.Contains('\n')
                ? $"\"{s.Replace("\"", "\"\"")}\""
                : s;

        // ─────────────────────────────────────────────────────────────────────
        //  MATH HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private static double Median(List<double> vals)
        {
            var sorted = vals.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }

        private static double Percentile(List<double> sorted, double p)
        {
            double pos = p * (sorted.Count - 1);
            int lo = (int)pos;
            int hi = Math.Min(lo + 1, sorted.Count - 1);
            return sorted[lo] + (pos - lo) * (sorted[hi] - sorted[lo]);
        }

        private static double PearsonCorrelation(List<double> x, List<double> y)
        {
            int n = Math.Min(x.Count, y.Count);
            if (n < 2) return 0;
            double mx = x.Take(n).Average(), my = y.Take(n).Average();
            double num = 0, dx2 = 0, dy2 = 0;
            for (int i = 0; i < n; i++)
            {
                double ex = x[i] - mx, ey = y[i] - my;
                num += ex * ey; dx2 += ex * ex; dy2 += ey * ey;
            }
            double denom = Math.Sqrt(dx2 * dy2);
            return denom < 1e-10 ? 0 : num / denom;
        }

        private static double? TryParseDouble(string? s)
            => double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

        private static bool IsNumericColumn(DataTable dt, string col)
        {
            int numericCount = 0, total = 0;
            foreach (DataRow row in dt.Rows)
            {
                var s = row[col]?.ToString();
                if (string.IsNullOrWhiteSpace(s)) continue;
                total++;
                if (TryParseDouble(s).HasValue) numericCount++;
                if (total >= 20) break;  // sample first 20 rows
            }
            return total > 0 && (double)numericCount / total > 0.8;
        }
    }
}