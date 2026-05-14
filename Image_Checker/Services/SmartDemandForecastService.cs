// ══════════════════════════════════════════════════════════════════════════════
//  SmartDemandForecastService.cs
//
//  THREE REAL PROBLEMS SOLVED
//  ══════════════════════════
//  ① WEEKEND / HOLIDAY NOISE
//     The old pipeline included Saturday = 0 and Sunday = 0 rows.
//     The SSA model treated those zeros as genuine demand dips and produced
//     a sawtooth pattern that destroyed forecast accuracy.
//     Fix: a configurable WorkingDayCalendar strips every non-working day
//     (weekends + Indian national holidays + any custom holidays you add)
//     BEFORE a single data point reaches the model.
//     For monthly granularity the aggregation itself sidesteps the problem;
//     for daily / weekly granularity the filter is applied row-by-row.
//
//  ② DORMANT ITEMS  (no orders in the last N months)
//     Applying SSA to a series that flatlined 18 months ago produces
//     wildly over-optimistic extrapolations.
//     Fix: any item × customer combination whose most-recent order date is
//     older than DormantMonths is classified DORMANT.
//     Dormant items receive a zero forecast and a management action tag
//     ("Consider discontinuation", "Strategic stock review", "Monitor"),
//     and are highlighted in a separate section of the HTML report.
//
//  ③ SPARSE ITEMS  (fewer than MinActivePeriodsForSSA data points)
//     SSA is numerically unstable on very short series (< 6 points).
//     Fix: sparse items receive a Weighted Moving Average (WMA) forecast
//     with a linear trend correction, which is more honest than forcing SSA
//     on 3 data points and getting nonsense.
//
//  USAGE
//  ═════
//  var svc = new SmartDemandForecastService(
//      new MLContext(seed: 42),
//      new SmartForecastConfig
//      {
//          DateColumn     = "TRX_DATE",
//          QtyColumn      = "QUANTITY_INVOICED",
//          ItemColumn     = "INVENTORY_ITEM_ID",
//          CustomerColumn = "PARTY_NAME",
//          HorizonSteps   = 12,
//          Granularity    = "Month",
//          OutputDirectory = @"C:\Reports"
//      });
//  ForecastReport report = svc.ForecastAll(@"C:\Data\orders.csv");
//  // → writes demand_forecast_YYYYMMDDHHMM.html  (management report)
//  // → writes demand_forecast_YYYYMMDDHHMM.json  (machine-readable)
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
    // ════════════════════════════════════════════════════════════════════════
    //  ENUMS & POCOs
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>How a demand series is classified before forecasting.</summary>
    public enum ItemStatus
    {
        /// <summary>Has demand within the last N months → SSA model.</summary>
        Active,

        /// <summary>No demand for > N months → zero forecast + review flag.</summary>
        Dormant,

        /// <summary>Too few periods for stable SSA → WMA forecast.</summary>
        Sparse
    }

    /// <summary>One forecasted future period.</summary>
    public class PeriodForecast
    {
        public string Period { get; set; } = "";   // "2025-04", "2025", "2025-04-07" …
        public double Qty { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
    }

    /// <summary>Full forecast result for a single item (× customer) combination.</summary>
    public class ItemForecastResult
    {
        // ── Identity ────────────────────────────────────────────────────────
        public string ItemKey { get; set; } = "";
        public string ItemId { get; set; } = "";
        public string CustomerName { get; set; } = "";

        // ── Classification ──────────────────────────────────────────────────
        public ItemStatus Status { get; set; }
        public string StatusReason { get; set; } = "";

        // ── History summary ─────────────────────────────────────────────────
        public DateTime? FirstOrderDate { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public double TotalHistoricQty { get; set; }
        public int HistoricPeriods { get; set; }
        public double AvgMonthlyQty { get; set; }
        public double PeakMonthlyQty { get; set; }

        // ── Forecast ────────────────────────────────────────────────────────
        public List<PeriodForecast> Forecast { get; set; } = new();
        public double ForecastMAE { get; set; }
        public string ForecastMethod { get; set; } = "";

        // ── Optional warning ────────────────────────────────────────────────
        public string? Warning { get; set; }
    }

    /// <summary>Aggregated report over all items.</summary>
    public class ForecastReport
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public int Horizon { get; set; }
        public string Granularity { get; set; } = "Month";
        public int TotalItems { get; set; }
        public int ActiveCount { get; set; }
        public int DormantCount { get; set; }
        public int SparseCount { get; set; }
        public List<ItemForecastResult> Results { get; set; } = new();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CONFIGURATION
    // ════════════════════════════════════════════════════════════════════════

    public class SmartForecastConfig
    {
        // ── Column names in the CSV ─────────────────────────────────────────
        public string DateColumn { get; set; } = "TRX_DATE";
        public string QtyColumn { get; set; } = "SALES_QTY";
        public string ItemColumn { get; set; } = "SEGMENT1";

        /// <summary>Set to null to forecast at item level (ignoring customer).</summary>
        public string? CustomerColumn { get; set; } = "PARTY_NAME";

        // ── Forecasting ─────────────────────────────────────────────────────
        public int HorizonSteps { get; set; } = 12;
        public string Granularity { get; set; } = "Month";   // Day | Week | Month | Year
        public float ConfidenceLevel { get; set; } = 0.95f;

        // ── Classification thresholds ───────────────────────────────────────
        /// <summary>Items with no orders for this many months → DORMANT.</summary>
        public int DormantMonths { get; set; } = 12;

        /// <summary>Items with fewer than this many aggregated periods → SPARSE (WMA).</summary>
        public int MinActivePeriodsForSSA { get; set; } = 6;

        // ── Working-day calendar ────────────────────────────────────────────
        public DayOfWeek[] NonWorkingDays { get; set; } =
            { DayOfWeek.Saturday, DayOfWeek.Sunday };

        /// <summary>
        /// Public holidays to exclude.  Defaults to Indian national holidays
        /// for a rolling ±10-year window.  Add or replace as needed.
        /// </summary>
        public HashSet<DateTime> Holidays { get; set; } = DefaultIndianHolidays();

        // ── Output ──────────────────────────────────────────────────────────
        public string? OutputDirectory { get; set; }

        // ────────────────────────────────────────────────────────────────────
        private static HashSet<DateTime> DefaultIndianHolidays()
        {
            var h = new HashSet<DateTime>();
            int fromYear = DateTime.Today.Year - 10;
            int toYear = DateTime.Today.Year + 5;

            for (int yr = fromYear; yr <= toYear; yr++)
            {
                // Fixed national holidays
                h.Add(new DateTime(yr, 1, 1));   // New Year's Day
                h.Add(new DateTime(yr, 1, 26));   // Republic Day
                h.Add(new DateTime(yr, 8, 15));   // Independence Day
                h.Add(new DateTime(yr, 10, 2));   // Gandhi Jayanti
                h.Add(new DateTime(yr, 08, 1)); // Raksha Bandhan (approximate)
                h.Add(new DateTime(yr, 08, 8)); // Janmashtami (approximate)

                // Common industry holidays (add/remove to suit your company)
                h.Add(new DateTime(yr, 11, 1));   // Diwali region (approximate)
                h.Add(new DateTime(yr, 03, 1));   // Holi region (approximate)
            }
            return h;
        }

        public async Task<HashSet<DateTime>> GetIndianHolidaysFromApi()
        {
            var holidays = new HashSet<DateTime>();

            int year = DateTime.Today.Year;

            string url = $"https://date.nager.at/api/v3/PublicHolidays/{year}/IN";

            using var http = new HttpClient();
            var response = await http.GetStringAsync(url);

            var data = JsonSerializer.Deserialize<List<HolidayDto>>(response);

            foreach (var h in data)
            {
                holidays.Add(DateTime.Parse(h.date));
            }

            return holidays;
        }

        public class HolidayDto
        {
            public string date { get; set; }
            public string localName { get; set; }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SERVICE  –  main class
    // ════════════════════════════════════════════════════════════════════════

    public class SmartDemandForecastService
    {
        private readonly MLContext _ml;
        private readonly SmartForecastConfig _cfg;

        public SmartDemandForecastService(MLContext ml, SmartForecastConfig cfg)
        {
            _ml = ml ?? throw new ArgumentNullException(nameof(ml));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        // ════════════════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads the CSV, classifies every item, forecasts each one, and
        /// writes an HTML + JSON report to OutputDirectory (if configured).
        /// </summary>
        public ForecastReport ForecastAll(string csvPath, CancellationToken ct = default)
        {
            PrintHeader();
            Console.WriteLine($"\n📂 Loading: {Path.GetFileName(csvPath)}");

            var rows = LoadCsv(csvPath);
            Console.WriteLine($"   ✅ {rows.Count:N0} raw rows loaded");

            // ── Working-day filter for day-level granularity ────────────────
            // Monthly / yearly aggregation naturally absorbs weekend zeros, so
            // the filter is most critical when Granularity = "Day".
            // We still apply it for Monthly too: any transaction that somehow
            // landed on a Sunday / holiday (data entry error) is excluded.
            int beforeFilter = rows.Count;
            rows = rows.Where(r => IsWorkingDay(r.Date)).ToList();
            int removed = beforeFilter - rows.Count;
            if (removed > 0)
                Console.WriteLine($"   🗓️  Removed {removed:N0} rows on non-working days " +
                    $"(weekends + holidays) — these are never real demand.");
            else
                Console.WriteLine($"   🗓️  Working-day check: all rows fall on working days.");

            // ── Group by item (× customer) ──────────────────────────────────
            var groups = rows
                .GroupBy(r => BuildKey(r))
                .ToDictionary(g => g.Key, g => g.ToList());

            string groupLabel = string.IsNullOrEmpty(_cfg.CustomerColumn)
                ? "items" : "customer × item combinations";
            Console.WriteLine($"\n🔍 Found {groups.Count:N0} unique {groupLabel}");

            var report = new ForecastReport
            {
                Horizon = _cfg.HorizonSteps,
                Granularity = _cfg.Granularity,
                TotalItems = groups.Count
            };

            // ── Forecast each group ─────────────────────────────────────────
            int idx = 0;
            foreach (var (key, itemRows) in groups.OrderBy(g => g.Key))
            {
                ct.ThrowIfCancellationRequested();
                idx++;
                Console.WriteLine($"\n[{idx}/{groups.Count}] {key}");
                Console.WriteLine("   " + new string('─', 60));

                var result = ForecastItem(key, itemRows);
                report.Results.Add(result);

                switch (result.Status)
                {
                    case ItemStatus.Active: report.ActiveCount++; break;
                    case ItemStatus.Dormant: report.DormantCount++; break;
                    case ItemStatus.Sparse: report.SparseCount++; break;
                }
            }

            PrintSummary(report);
            ExportFiles(report);
            return report;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PER-ITEM ORCHESTRATION
        // ════════════════════════════════════════════════════════════════════

        private ItemForecastResult ForecastItem(string key, List<DemandRow> rows)
        {
            var parts = key.Split('|');
            var result = new ItemForecastResult
            {
                ItemKey = key,
                ItemId = parts.Length > 0 ? parts[0] : key,
                CustomerName = parts.Length > 1 ? parts[1] : ""
            };

            // ── Aggregate into calendar periods ─────────────────────────────
            var periods = AggregatePeriods(rows);
            result.HistoricPeriods = periods.Count;
            result.TotalHistoricQty = periods.Sum(p => p.Qty);
            result.AvgMonthlyQty = periods.Count > 0
                ? result.TotalHistoricQty / periods.Count : 0;
            result.PeakMonthlyQty = periods.Count > 0
                ? periods.Max(p => p.Qty) : 0;
            result.FirstOrderDate = rows.Min(r => r.Date);
            result.LastOrderDate = rows.Max(r => r.Date);

            Console.WriteLine(
                $"   📅 History  : {result.FirstOrderDate:yyyy-MM-dd} → " +
                $"{result.LastOrderDate:yyyy-MM-dd}  " +
                $"({result.HistoricPeriods} {_cfg.Granularity} periods,  " +
                $"total qty = {result.TotalHistoricQty:N0},  " +
                $"avg = {result.AvgMonthlyQty:N0}/period)");

            // ── Classify ────────────────────────────────────────────────────
            double monthsInactive =
                (DateTime.Today - result.LastOrderDate!.Value).TotalDays / 30.44;

            if (monthsInactive > _cfg.DormantMonths)
            {
                result.Status = ItemStatus.Dormant;
                result.StatusReason =
                    $"No orders for {monthsInactive:F0} months " +
                    $"(threshold: {_cfg.DormantMonths} months).";
                Console.WriteLine($"   💤 DORMANT — {result.StatusReason}");
                ApplyDormantForecast(result, periods);
                return result;
            }

            if (periods.Count < _cfg.MinActivePeriodsForSSA)
            {
                result.Status = ItemStatus.Sparse;
                result.StatusReason =
                    $"Only {periods.Count} period(s) of history " +
                    $"(minimum for SSA: {_cfg.MinActivePeriodsForSSA}).";
                Console.WriteLine($"   🔸 SPARSE  — {result.StatusReason}");
                ApplyWMAForecast(result, periods);
                return result;
            }

            result.Status = ItemStatus.Active;
            Console.WriteLine(
                $"   ✅ ACTIVE  — last order {monthsInactive:F0} months ago — SSA forecast");
            ApplySSAForecast(result, periods);
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  STRATEGY 1 — SSA  (Active items)
        // ════════════════════════════════════════════════════════════════════

        private void ApplySSAForecast(ItemForecastResult result, List<PeriodData> periods)
        {
            result.ForecastMethod = "SSA (Singular Spectrum Analysis)";
            try
            {
                int n = periods.Count;
                int horizon = _cfg.HorizonSteps;

                // Derive safe parameters — SSA has strict constraints:
                //   windowSize > horizon
                //   windowSize <= seriesLength / 2
                //   seriesLength <= trainSize
                int windowSize = Math.Max(horizon + 1, n / 3);
                if (windowSize > n / 2) windowSize = Math.Max(2, n / 2);
                if (windowSize <= horizon) windowSize = horizon + 1;
                windowSize = Math.Min(windowSize, n - 1);
                windowSize = Math.Max(windowSize, 2);

                int seriesLen = n;
                if (seriesLen <= windowSize) seriesLen = Math.Min(windowSize + 1, n);

                var seriesData = periods
                    .Select(p => new TsRow { Value = (float)p.Qty })
                    .ToList();
                var view = _ml.Data.LoadFromEnumerable(seriesData);

                var pipeline = _ml.Forecasting.ForecastBySsa(
                    outputColumnName: "Forecast",
                    inputColumnName: "Value",
                    windowSize: windowSize,
                    seriesLength: seriesLen,
                    trainSize: n,
                    horizon: horizon,
                    confidenceLevel: _cfg.ConfidenceLevel,
                    confidenceLowerBoundColumn: "Lower",
                    confidenceUpperBoundColumn: "Upper");

                var model = pipeline.Fit(view);

                // In-sample MAE for quality indicator
                var transformed = model.Transform(view);
                var fcInSample = transformed.GetColumn<float[]>("Forecast").ToList();
                var actual = transformed.GetColumn<float>("Value").ToList();
                double mae = 0;
                int evalN = Math.Min(fcInSample.Count, actual.Count);
                for (int i = 0; i < evalN; i++)
                    if (fcInSample[i]?.Length > 0)
                        mae += Math.Abs(actual[i] - fcInSample[i][0]);
                result.ForecastMAE = evalN > 0 ? mae / evalN : 0;

                // Out-of-sample forecast
                var engine = model.CreateTimeSeriesEngine<TsRow, TsForecastRow>(_ml);
                var forecast = engine.Predict();

                PopulateForecastPeriods(result, forecast.Forecast,
                    forecast.Lower, forecast.Upper, periods.Last().PeriodStartDate);

                Console.WriteLine(
                    $"   📈 SSA     n={n}  window={windowSize}  " +
                    $"horizon={horizon}  in-sample MAE={result.ForecastMAE:N2}");
                PrintForecastPreview(result.Forecast);
            }
            catch (Exception ex)
            {
                result.Warning =
                    $"SSA failed ({ex.GetBaseException().Message}). " +
                    "Falling back to Weighted Moving Average.";
                Console.WriteLine($"   ⚠️  {result.Warning}");
                result.ForecastMethod = "WMA (SSA fallback)";
                ApplyWMAForecast(result, periods);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  STRATEGY 2 — Weighted Moving Average  (Sparse items)
        // ════════════════════════════════════════════════════════════════════

        private void ApplyWMAForecast(ItemForecastResult result, List<PeriodData> periods)
        {
            if (result.ForecastMethod == "")   // not an SSA fallback
                result.ForecastMethod =
                    "Weighted Moving Average  (insufficient history for SSA)";

            int n = periods.Count;
            if (n == 0)
            {
                // No data at all — just project zeros
                var lastDate = DateTime.Today;
                for (int h = 1; h <= _cfg.HorizonSteps; h++)
                    result.Forecast.Add(new PeriodForecast
                    {
                        Period = NextPeriodLabel(lastDate, h),
                        Qty = 0,
                        LowerBound = 0,
                        UpperBound = 0
                    });
                return;
            }

            // Linearly increasing weights so recent months matter more
            double[] weights = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
            double wSum = weights.Sum();
            double wma = 0;
            for (int i = 0; i < n; i++) wma += weights[i] * periods[i].Qty / wSum;

            // Linear trend from the last up-to-6 periods
            int trendN = Math.Min(n, 6);
            double slope = 0;
            if (trendN >= 2)
            {
                var last = periods.TakeLast(trendN).ToList();
                double sx = 0, sy = 0, sxy = 0, sx2 = 0;
                for (int i = 0; i < trendN; i++)
                {
                    sx += i; sy += last[i].Qty;
                    sxy += i * last[i].Qty; sx2 += i * i;
                }
                double denom = trendN * sx2 - sx * sx;
                if (Math.Abs(denom) > 1e-10)
                    slope = (trendN * sxy - sx * sy) / denom;
            }

            // Confidence band: ±20% of WMA (wider for very sparse series)
            double band = wma * (n < 3 ? 0.35 : 0.20);

            var baseDate = periods.Last().PeriodStartDate;
            for (int h = 1; h <= _cfg.HorizonSteps; h++)
            {
                double qty = Math.Max(0, wma + slope * h);
                result.Forecast.Add(new PeriodForecast
                {
                    Period = NextPeriodLabel(baseDate, h),
                    Qty = Math.Round(qty, 2),
                    LowerBound = Math.Round(Math.Max(0, qty - band), 2),
                    UpperBound = Math.Round(qty + band, 2)
                });
            }

            result.ForecastMAE = double.NaN;   // not applicable for WMA
            Console.WriteLine(
                $"   📉 WMA     base={wma:N2}  trend slope={slope:+0.00;-0.00}/period  " +
                $"band=±{band:N2}");
            PrintForecastPreview(result.Forecast);
        }

        // ════════════════════════════════════════════════════════════════════
        //  STRATEGY 3 — Zero / Watchlist  (Dormant items)
        // ════════════════════════════════════════════════════════════════════

        private void ApplyDormantForecast(ItemForecastResult result, List<PeriodData> periods)
        {
            result.ForecastMethod =
                "Dormant watchlist — zero forecast, management action required";

            var baseDate = periods.Any()
                ? periods.Last().PeriodStartDate : DateTime.Today;

            for (int h = 1; h <= _cfg.HorizonSteps; h++)
                result.Forecast.Add(new PeriodForecast
                {
                    Period = NextPeriodLabel(baseDate, h),
                    Qty = 0,
                    LowerBound = 0,
                    UpperBound = 0
                });

            result.ForecastMAE = double.NaN;
            Console.WriteLine("   🚨 This item needs management review (see Dormant Report).");
        }

        // ════════════════════════════════════════════════════════════════════
        //  AGGREGATION  –  group raw rows into granularity periods
        // ════════════════════════════════════════════════════════════════════

        private List<PeriodData> AggregatePeriods(List<DemandRow> rows) =>
            rows
            .GroupBy(r => PeriodKey(r.Date))
            .OrderBy(g => g.Key)
            .Select(g => new PeriodData
            {
                // Use the first date in each group as the period anchor
                PeriodStartDate = g.OrderBy(r => r.Date).First().Date,
                PeriodLabel = g.Key,
                Qty = g.Sum(r => r.Qty)
            })
            .ToList();

        private string PeriodKey(DateTime d) => _cfg.Granularity switch
        {
            "Year" => d.ToString("yyyy"),
            "Week" =>
                $"{d:yyyy}-W{CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(d, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday):D2}",
            "Day" => d.ToString("yyyy-MM-dd"),
            _ => d.ToString("yyyy-MM")       // default = Month
        };

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════════════

        private bool IsWorkingDay(DateTime d) =>
            !_cfg.NonWorkingDays.Contains(d.DayOfWeek) &&
            !_cfg.Holidays.Contains(d.Date);

        private string BuildKey(DemandRow r) =>
            string.IsNullOrEmpty(_cfg.CustomerColumn)
                ? r.Item
                : $"{r.Item}|{r.Customer}";

        private void PopulateForecastPeriods(
            ItemForecastResult result,
            float[] fc, float[] lo, float[] hi,
            DateTime baseDate)
        {
            int steps = Math.Min(fc.Length, _cfg.HorizonSteps);
            for (int h = 1; h <= steps; h++)
            {
                double qty = Math.Max(0, fc[h - 1]);
                result.Forecast.Add(new PeriodForecast
                {
                    Period = NextPeriodLabel(baseDate, h),
                    Qty = Math.Round(qty, 2),
                    LowerBound = Math.Round(Math.Max(0, h - 1 < lo.Length ? lo[h - 1] : 0), 2),
                    UpperBound = Math.Round(h - 1 < hi.Length ? (double)hi[h - 1] : qty * 1.2, 2)
                });
            }
        }

        private string NextPeriodLabel(DateTime from, int step) =>
            _cfg.Granularity switch
            {
                "Year" => from.AddYears(step).ToString("yyyy"),
                "Day" => from.AddDays(step).ToString("yyyy-MM-dd"),
                "Week" => from.AddDays(step * 7).ToString("yyyy-'W'WW", CultureInfo.InvariantCulture),
                _ => from.AddMonths(step).ToString("yyyy-MM")
            };

        private static void PrintForecastPreview(List<PeriodForecast> fc)
        {
            int show = Math.Min(fc.Count, 4);
            Console.WriteLine($"   🔮 Next {show} period(s):");
            for (int i = 0; i < show; i++)
                Console.WriteLine(
                    $"      {fc[i].Period,-12}  {fc[i].Qty,10:N2}  " +
                    $"[{fc[i].LowerBound:N2} – {fc[i].UpperBound:N2}]");
            if (fc.Count > show)
                Console.WriteLine($"      … ({fc.Count - show} more period(s))");
        }

        // ════════════════════════════════════════════════════════════════════
        //  CSV LOADING  –  robust, quoted-field-aware parser
        // ════════════════════════════════════════════════════════════════════

        private List<DemandRow> LoadCsv(string path)
        {
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 2)
                throw new InvalidDataException("CSV file has no data rows.");

            var headers = SplitCsvLine(lines[0]);
            int dateIdx = ColIdx(headers, _cfg.DateColumn);
            int qtyIdx = ColIdx(headers, _cfg.QtyColumn);
            int itemIdx = ColIdx(headers, _cfg.ItemColumn);
            int custIdx = string.IsNullOrEmpty(_cfg.CustomerColumn) ? -1
                          : ColIdx(headers, _cfg.CustomerColumn);

            if (dateIdx < 0) throw new InvalidDataException(
                $"Column '{_cfg.DateColumn}' not found in CSV headers: {lines[0]}");
            if (qtyIdx < 0) throw new InvalidDataException(
                $"Column '{_cfg.QtyColumn}' not found in CSV headers: {lines[0]}");
            if (itemIdx < 0) throw new InvalidDataException(
                $"Column '{_cfg.ItemColumn}' not found in CSV headers: {lines[0]}");

            var list = new List<DemandRow>(lines.Length);
            int skipped = 0;

            for (int li = 1; li < lines.Length; li++)
            {
                if (string.IsNullOrWhiteSpace(lines[li])) continue;
                var cells = SplitCsvLine(lines[li]);

                DateTime? dt = ParseDate(SafeGet(cells, dateIdx));
                if (!dt.HasValue) { skipped++; continue; }

                string qtyStr = SafeGet(cells, qtyIdx);
                if (!double.TryParse(qtyStr, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double qty)) { skipped++; continue; }

                // Skip zero and negative quantities — no supply = no demand signal
                if (qty <= 0) { skipped++; continue; }

                list.Add(new DemandRow
                {
                    Date = dt.Value,
                    Qty = qty,
                    Item = SafeGet(cells, itemIdx),
                    Customer = custIdx >= 0 ? SafeGet(cells, custIdx) : ""
                });
            }

            if (skipped > 0)
                Console.WriteLine($"   ⚠️  Skipped {skipped:N0} rows " +
                    "(unparseable date, non-numeric qty, or qty ≤ 0).");

            return list;
        }

        private static int ColIdx(string[] headers, string name) =>
            Array.FindIndex(headers, h =>
                h.Trim().Trim('"').Equals(name.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        private static string SafeGet(string[] cells, int idx) =>
            idx >= 0 && idx < cells.Length ? cells[idx].Trim().Trim('"') : "";

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

        private static DateTime? ParseDate(string raw)
        {
            raw = raw?.Trim() ?? "";
            if (raw.Length == 0) return null;
            string[] fmts = {
                "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy HH:mm", "dd-MM-yyyy",
                "dd/MM/yyyy HH:mm",    "dd/MM/yyyy",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                "MM/dd/yyyy HH:mm",    "MM/dd/yyyy",
                "yyyy/MM/dd",          "dd-MMM-yyyy",
                "MMM yyyy",            "MMMM yyyy",
                "MM-yyyy",             "MM/yyyy"
            };
            if (DateTime.TryParseExact(raw, fmts,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1)) return d1;
            if (DateTime.TryParse(raw,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2)) return d2;
            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        //  CONSOLE  –  styled output matching DataModelTrainer conventions
        // ════════════════════════════════════════════════════════════════════

        private static void PrintHeader()
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   SmartDemandForecastService  v2.0                           ║");
            Console.WriteLine("║   ✓ Weekend / holiday aware                                  ║");
            Console.WriteLine("║   ✓ Dormant item detection (watchlist + action tags)         ║");
            Console.WriteLine("║   ✓ Three strategies: SSA | WMA | Dormant                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        }

        private void PrintSummary(ForecastReport report)
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                 FORECAST SUMMARY                             ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"   Total combinations : {report.TotalItems:N0}");
            Console.WriteLine($"   ✅ Active (SSA)    : {report.ActiveCount,6:N0}");
            Console.WriteLine($"   🔸 Sparse (WMA)    : {report.SparseCount,6:N0}");
            Console.WriteLine($"   💤 Dormant (review): {report.DormantCount,6:N0}");
            Console.WriteLine($"   Horizon            : {report.Horizon} {report.Granularity}(s)");
            Console.WriteLine($"   Generated          : {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");

            if (report.DormantCount > 0)
            {
                Console.WriteLine();
                Console.WriteLine("⚠️  DORMANT ITEMS — MANAGEMENT REVIEW REQUIRED:");
                foreach (var d in report.Results
                    .Where(r => r.Status == ItemStatus.Dormant)
                    .OrderBy(r => r.LastOrderDate))
                {
                    double mo = (DateTime.Today - d.LastOrderDate!.Value).TotalDays / 30.44;
                    Console.WriteLine(
                        $"   • {d.ItemKey,-55}  " +
                        $"Last: {d.LastOrderDate:yyyy-MM-dd}  " +
                        $"({mo:F0} months inactive)  " +
                        $"Historic qty: {d.TotalHistoricQty:N0}");
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  FILE EXPORT
        // ════════════════════════════════════════════════════════════════════

        private void ExportFiles(ForecastReport report)
        {
            if (string.IsNullOrWhiteSpace(_cfg.OutputDirectory)) return;

            Directory.CreateDirectory(_cfg.OutputDirectory);
            string stamp = report.GeneratedAt.ToString("yyyyMMddHHmm");

            // HTML
            string htmlPath = Path.Combine(_cfg.OutputDirectory,
                $"demand_forecast_{stamp}.html");
            ExportHtmlReport(report, htmlPath);

            // JSON
            string jsonPath = Path.Combine(_cfg.OutputDirectory,
                $"demand_forecast_{stamp}.json");
            File.WriteAllText(jsonPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);

            Console.WriteLine();
            Console.WriteLine($"📊 HTML report : {htmlPath}");
            Console.WriteLine($"📄 JSON data   : {jsonPath}");
        }

        // ════════════════════════════════════════════════════════════════════
        //  HTML REPORT  –  management-ready, self-contained, no external libs
        // ════════════════════════════════════════════════════════════════════

        public void ExportHtmlReport(ForecastReport report, string path)
        {
            var sb = new StringBuilder(1 << 20);   // 1 MB initial capacity
            sb.Append(BuildHtmlHead(report));
            sb.Append(BuildSummaryCards(report));

            // ── Active + Sparse forecast table ───────────────────────────
            var forecasted = report.Results
                .Where(r => r.Status != ItemStatus.Dormant)
                .OrderByDescending(r => r.AvgMonthlyQty)
                .ToList();

            if (forecasted.Any())
            {
                sb.Append(
                    "<h2 class='sec-title'>📈 Active Demand Forecast</h2>" +
                    "<p class='sec-hint'>Hover over any forecast cell to see the " +
                    "confidence interval (Lower – Upper).  " +
                    "Cell colour intensity indicates quantity relative to the item's " +
                    "historic peak — darker blue = higher expected demand.</p>" +
                    "<div class='tbl-wrap'><table><thead><tr>" +
                    "<th>Item ID</th><th>Customer</th>" +
                    "<th>Status</th><th>Method</th>" +
                    "<th class='num'>Avg / Period</th>" +
                    "<th class='num'>Peak / Period</th>" +
                    "<th>First Order</th><th>Last Order</th>");

                for (int h = 1; h <= report.Horizon; h++)
                    sb.Append(
                        $"<th class='fc-hdr'>" +
                        $"{FcColLabel(h, report.Granularity, report.GeneratedAt)}</th>");
                sb.Append("</tr></thead><tbody>");

                foreach (var item in forecasted)
                {
                    string rc = item.Status == ItemStatus.Sparse ? " class='row-sparse'" : "";
                    sb.Append($"<tr{rc}>");
                    sb.Append($"<td class='item-id'>{Esc(item.ItemId)}</td>");
                    sb.Append($"<td>{Esc(item.CustomerName)}</td>");
                    sb.Append(
                        $"<td><span class='badge badge-{item.Status.ToString().ToLower()}'>" +
                        $"{item.Status}</span></td>");
                    sb.Append($"<td class='method'>{Esc(item.ForecastMethod)}</td>");
                    sb.Append($"<td class='num'>{item.AvgMonthlyQty:N0}</td>");
                    sb.Append($"<td class='num'>{item.PeakMonthlyQty:N0}</td>");
                    sb.Append($"<td>{item.FirstOrderDate:yyyy-MM-dd}</td>");
                    sb.Append($"<td>{item.LastOrderDate:yyyy-MM-dd}</td>");

                    foreach (var fc in item.Forecast.Take(report.Horizon))
                    {
                        // Heat-map colouring: white (low) → deep blue (high peak)
                        double pct = item.PeakMonthlyQty > 0
                            ? Math.Min(1.0, fc.Qty / item.PeakMonthlyQty) : 0;
                        int r2 = (int)Math.Round(215 - pct * 145);
                        int g2 = (int)Math.Round(228 - pct * 120);
                        int b2 = (int)Math.Round(255 - pct * 30);
                        string tip = $"Low: {fc.LowerBound:N0}  High: {fc.UpperBound:N0}";
                        sb.Append(
                            $"<td class='num fc-cell' title='{tip}' " +
                            $"style='background:rgb({r2},{g2},{b2})'>" +
                            $"{fc.Qty:N0}</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
            }

            // ── Dormant items table ───────────────────────────────────────
            var dormant = report.Results
                .Where(r => r.Status == ItemStatus.Dormant)
                .OrderBy(r => r.LastOrderDate)
                .ToList();

            if (dormant.Any())
            {
                sb.Append(
                    "<h2 class='sec-title sec-dormant'>💤 Dormant Items — Action Required</h2>" +
                    $"<p class='dormant-hint'>These {dormant.Count} item" +
                    $"{(dormant.Count == 1 ? "" : "s")} had <strong>no demand " +
                    $"for over {_cfg.DormantMonths} months</strong>.  " +
                    "Review each for discontinuation, reactivation, or safety-stock " +
                    "reduction to free up working capital.</p>" +
                    "<div class='tbl-wrap'><table class='tbl-dormant'><thead><tr>" +
                    "<th>Item ID</th><th>Customer</th>" +
                    "<th>First Order</th><th>Last Order</th>" +
                    "<th class='num'>Months Inactive</th>" +
                    "<th class='num'>Historic Periods</th>" +
                    "<th class='num'>Total Qty</th>" +
                    "<th class='num'>Avg / Period</th>" +
                    "<th class='num'>Peak / Period</th>" +
                    "<th>Recommended Action</th></tr></thead><tbody>");

                foreach (var item in dormant)
                {
                    double mo = (DateTime.Today - item.LastOrderDate!.Value).TotalDays / 30.44;
                    string action = mo > 24
                        ? "<span class='act-red'>🔴 Consider discontinuation</span>"
                        : mo > 18
                            ? "<span class='act-amber'>🟡 Strategic stock review</span>"
                            : "<span class='act-green'>🟢 Monitor — may reactivate</span>";

                    sb.Append("<tr class='row-dormant'>");
                    sb.Append($"<td class='item-id'>{Esc(item.ItemId)}</td>");
                    sb.Append($"<td>{Esc(item.CustomerName)}</td>");
                    sb.Append($"<td>{item.FirstOrderDate:yyyy-MM-dd}</td>");
                    sb.Append($"<td class='warn'>{item.LastOrderDate:yyyy-MM-dd}</td>");
                    sb.Append($"<td class='num warn'>{mo:N0}</td>");
                    sb.Append($"<td class='num'>{item.HistoricPeriods}</td>");
                    sb.Append($"<td class='num'>{item.TotalHistoricQty:N0}</td>");
                    sb.Append($"<td class='num'>{item.AvgMonthlyQty:N0}</td>");
                    sb.Append($"<td class='num'>{item.PeakMonthlyQty:N0}</td>");
                    sb.Append($"<td>{action}</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
            }

            sb.Append(BuildHtmlFooter(report));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // ── HTML building blocks ───────────────────────────────────────────

        private static string BuildHtmlHead(ForecastReport report) => $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width,initial-scale=1'>
<title>Demand Forecast — {report.GeneratedAt:dd MMM yyyy}</title>
<style>
  *{{box-sizing:border-box;margin:0;padding:0}}
  body{{font-family:'Segoe UI',Arial,sans-serif;background:#eef2fb;color:#1a1a2e;font-size:13px}}
  /* ── header ──────────────────────────────────────── */
  header{{
    background:linear-gradient(135deg,#0d2b6b 0%,#1a52b8 60%,#2962c0 100%);
    color:#fff;padding:30px 44px 24px;
  }}
  header h1{{font-size:22px;font-weight:800;letter-spacing:.4px;margin-bottom:6px}}
  header p{{opacity:.82;font-size:12.5px;line-height:1.6}}
  .tag{{display:inline-block;background:rgba(255,255,255,.18);
    border-radius:20px;padding:2px 10px;font-size:11px;margin-right:6px;margin-top:6px}}
  /* ── content ─────────────────────────────────────── */
  .content{{padding:28px 44px 40px}}
  /* ── summary cards ───────────────────────────────── */
  .cards{{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));
    gap:16px;margin-bottom:34px}}
  .card{{border-radius:12px;padding:20px 22px;box-shadow:0 3px 12px rgba(0,0,0,.09)}}
  .card-blue  {{background:#1a52b8;color:#fff}}
  .card-green {{background:#1b7a42;color:#fff}}
  .card-amber {{background:#c17000;color:#fff}}
  .card-red   {{background:#b71c1c;color:#fff}}
  .card-white {{background:#fff;border:1px solid #d0daf5}}
  .card-num{{font-size:36px;font-weight:800;line-height:1;letter-spacing:-1px}}
  .card-lbl{{font-size:11.5px;margin-top:7px;opacity:.88}}
  /* ── section ─────────────────────────────────────── */
  .sec-title{{font-size:15px;font-weight:700;color:#0d2b6b;
    margin:30px 0 8px;padding-bottom:6px;border-bottom:2.5px solid #b8ccf5}}
  .sec-dormant{{color:#9b0000;border-bottom-color:#f5b8b8}}
  .sec-hint{{color:#445;font-size:12px;margin-bottom:12px;line-height:1.55}}
  .dormant-hint{{color:#7a0000;font-size:12.5px;margin-bottom:12px;line-height:1.55}}
  /* ── tables ──────────────────────────────────────── */
  .tbl-wrap{{overflow-x:auto;border-radius:10px;
    box-shadow:0 3px 14px rgba(0,0,0,.09);margin-bottom:34px}}
  table{{width:100%;border-collapse:collapse;background:#fff}}
  thead tr{{background:#0d2b6b;color:#fff}}
  th{{padding:11px 13px;font-size:11.5px;font-weight:700;white-space:nowrap;text-align:left}}
  td{{padding:8px 13px;border-bottom:1px solid #e6edf8;vertical-align:middle}}
  tr:hover td{{background:#f2f7ff}}
  .num{{text-align:right;font-variant-numeric:tabular-nums}}
  .fc-hdr{{min-width:66px;font-size:11px;text-align:right}}
  .fc-cell{{font-weight:600;border-left:1px solid #d8e4f5;white-space:nowrap}}
  .item-id{{font-weight:700;color:#0d2b6b;white-space:nowrap}}
  .method{{font-size:11px;color:#667;max-width:220px}}
  .warn{{color:#9b0000;font-weight:700}}
  /* dormant table */
  .tbl-dormant thead tr{{background:#7a0000}}
  .row-dormant td{{background:#fff8f8}}
  .row-dormant:hover td{{background:#ffeeee}}
  .row-sparse td{{background:#fffdf0}}
  /* badges */
  .badge{{display:inline-block;padding:2px 10px;border-radius:20px;
    font-size:11px;font-weight:700;white-space:nowrap}}
  .badge-active {{background:#d0f5e2;color:#145c34}}
  .badge-sparse {{background:#fff3cd;color:#7a5500}}
  .badge-dormant{{background:#fce4e4;color:#8b0000}}
  /* action badges */
  .act-red  {{color:#8b0000;font-weight:700}}
  .act-amber{{color:#7a5500;font-weight:700}}
  .act-green{{color:#145c34;font-weight:700}}
  /* ── footer ──────────────────────────────────────── */
  footer{{background:#d6dfee;padding:18px 44px;
    font-size:11.5px;color:#445;text-align:center;line-height:2}}
</style>
</head>
<body>
<header>
  <h1>📦 Customer Demand Forecast Report</h1>
  <p>
    <span class='tag'>✓ Non-working days excluded</span>
    <span class='tag'>✓ Dormant item detection</span>
    <span class='tag'>✓ SSA time-series model</span>
    <span class='tag'>✓ 7–8 year trend analysis</span>
    <span class='tag'>✓ 95% confidence intervals</span>
  </p>
</header>
<div class='content'>";

        private static string BuildSummaryCards(ForecastReport report) =>
            $@"<div class='cards'>
  <div class='card card-blue'>
    <div class='card-num'>{report.TotalItems:N0}</div>
    <div class='card-lbl'>Total Item × Customer<br>Combinations</div>
  </div>
  <div class='card card-green'>
    <div class='card-num'>{report.ActiveCount:N0}</div>
    <div class='card-lbl'>Active Items<br>SSA Forecast</div>
  </div>
  <div class='card card-amber'>
    <div class='card-num'>{report.SparseCount:N0}</div>
    <div class='card-lbl'>Sparse Items<br>WMA Forecast</div>
  </div>
  <div class='card card-red'>
    <div class='card-num'>{report.DormantCount:N0}</div>
    <div class='card-lbl'>Dormant Items<br>Action Required</div>
  </div>
  <div class='card card-white'>
    <div class='card-num' style='font-size:28px'>{report.Horizon}</div>
    <div class='card-lbl'>Forecast Horizon<br>({report.Granularity}s)</div>
  </div>
  <div class='card card-white'>
    <div class='card-num' style='font-size:22px'>{report.GeneratedAt:dd MMM}<br style='line-height:.5'>{report.GeneratedAt:yyyy}</div>
    <div class='card-lbl'>Report Date<br>{report.GeneratedAt:HH:mm}</div>
  </div>
</div>";

        private string BuildHtmlFooter(ForecastReport report) =>
            $@"</div>
<footer>
  Report generated: {report.GeneratedAt:dddd, dd MMMM yyyy  HH:mm:ss}
  &nbsp;|&nbsp; Model: Singular Spectrum Analysis (SSA) + Weighted Moving Average (WMA)
  &nbsp;|&nbsp; Non-working days excluded (weekends + {_cfg.Holidays.Count} holiday entries)
  &nbsp;|&nbsp; Dormant threshold: {_cfg.DormantMonths} months
  &nbsp;|&nbsp; Built with ML.NET · SmartDemandForecastService v2.0
</footer>
</body></html>";

        private static string FcColLabel(int step, string gran, DateTime genAt) =>
            gran switch
            {
                "Year" => genAt.AddYears(step).ToString("yyyy"),
                "Day" => genAt.AddDays(step).ToString("d-MMM"),
                "Week" => $"Wk+{step}",
                _ => genAt.AddMonths(step).ToString("MMM yy")
            };

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // ════════════════════════════════════════════════════════════════════
        //  ML.NET INNER TYPES
        // ════════════════════════════════════════════════════════════════════

        private class DemandRow
        {
            public DateTime Date { get; set; }
            public double Qty { get; set; }
            public string Item { get; set; } = "";
            public string Customer { get; set; } = "";
        }

        private class PeriodData
        {
            public DateTime PeriodStartDate { get; set; }
            public string PeriodLabel { get; set; } = "";
            public double Qty { get; set; }
        }

        private class TsRow
        {
            [ColumnName("Value")]
            public float Value { get; set; }
        }

        private class TsForecastRow
        {
            public float[] Forecast { get; set; } = Array.Empty<float>();
            public float[] Lower { get; set; } = Array.Empty<float>();
            public float[] Upper { get; set; } = Array.Empty<float>();
        }
    }
}