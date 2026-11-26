using System.Data;
using System.Globalization;
using System.Text;

namespace Image_Checker.Forecasting
{
    // ══════════════════════════════════════════════════════════════════════
    //  Public types
    // ══════════════════════════════════════════════════════════════════════

    public enum Granularity { Day, Week, Month }

    public enum DemandPattern { Smooth, Erratic, Intermittent, Lumpy, Dormant, TooShort }

    public sealed class SalesRecord
    {
        public DateTime Date { get; init; }
        /// <summary>What is forecast separately: item, item|customer, plant|line ...</summary>
        public string Key { get; init; } = "";
        public double Quantity { get; init; }
    }

    public sealed class ForecastOptions
    {
        public Granularity Granularity { get; set; } = Granularity.Month;
        public int Horizon { get; set; } = 6;
        /// <summary>How many rolling hold-out windows are used to judge each model.</summary>
        public int BacktestOrigins { get; set; } = 3;
        /// <summary>Series shorter than this get a simple moving average and no model competition.</summary>
        public int MinHistory { get; set; } = 12;
        /// <summary>Trailing zero periods after which a series is called dormant (forecast = 0).</summary>
        public int DormantAfterPeriods { get; set; } = 12;
        /// <summary>Average the best N models. 1 = pick a single winner.</summary>
        public int CombineTop { get; set; } = 3;
        /// <summary>0.80, 0.90 or 0.95.</summary>
        public double IntervalLevel { get; set; } = 0.80;
        /// <summary>Drop the last period if the data stops before it ends (a half month is not a month).</summary>
        public bool DropPartialLastPeriod { get; set; } = true;

        // Only used for Granularity.Day: sales on these days roll forward to the next working day.
        public HashSet<DayOfWeek> NonWorkingDays { get; } = new() { DayOfWeek.Sunday };
        public HashSet<DateTime> Holidays { get; } = new();

        public int SeasonLength => Granularity switch
        {
            Granularity.Month => 12,
            Granularity.Week => 52,
            _ => Math.Max(2, 7 - NonWorkingDays.Count)
        };
    }

    public sealed class SeriesForecast
    {
        public string Key { get; set; } = "";
        public DemandPattern Pattern { get; set; }
        public double Adi { get; set; }
        public double Cv2 { get; set; }
        public string Model { get; set; } = "";
        public string Note { get; set; } = "";

        public DateTime[] HistoryPeriods { get; set; } = Array.Empty<DateTime>();
        public double[] History { get; set; } = Array.Empty<double>();
        public DateTime[] ForecastPeriods { get; set; } = Array.Empty<DateTime>();
        public double[] Forecast { get; set; } = Array.Empty<double>();
        public double[] Lower { get; set; } = Array.Empty<double>();
        public double[] Upper { get; set; } = Array.Empty<double>();

        // Backtest quality (NaN when there was no backtest)
        public double BacktestRmse { get; set; } = double.NaN;
        public double BacktestWape { get; set; } = double.NaN;
        public double BacktestBias { get; set; } = double.NaN;   // + = over-forecast
        public double BaselineRmse { get; set; } = double.NaN;
        public double Skill { get; set; } = double.NaN;          // 1 - rmse/baselineRmse ; > 0 beats the baseline

        internal double AbsErr, SumActual, BaselineAbsErr;
    }

    public sealed class ForecastReport
    {
        public ForecastOptions Options { get; set; } = new();
        public List<SeriesForecast> Series { get; } = new();
        public List<string> Log { get; } = new();
        public DateTime[] Periods { get; set; } = Array.Empty<DateTime>();

        public double OverallWape { get; set; } = double.NaN;
        public double BaselineWape { get; set; } = double.NaN;
        public double ShareBeatingBaseline { get; set; } = double.NaN;

        public void ExportForecastCsv(string path)
        {
            var sb = new StringBuilder("Key,Pattern,Model,Period,Forecast,Lower,Upper\r\n");
            foreach (var s in Series)
                for (int i = 0; i < s.Forecast.Length; i++)
                    sb.Append(string.Join(",", Csv(s.Key), s.Pattern, Csv(s.Model),
                        s.ForecastPeriods[i].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        N(s.Forecast[i]), N(s.Lower[i]), N(s.Upper[i]))).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public void ExportAccuracyCsv(string path)
        {
            var sb = new StringBuilder("Key,Pattern,ADI,CV2,Model,BacktestRMSE,WAPE,Bias,BaselineRMSE,Skill,Note\r\n");
            foreach (var s in Series)
                sb.Append(string.Join(",", Csv(s.Key), s.Pattern, N(s.Adi), N(s.Cv2), Csv(s.Model),
                    N(s.BacktestRmse), N(s.BacktestWape), N(s.BacktestBias),
                    N(s.BaselineRmse), N(s.Skill), Csv(s.Note))).Append("\r\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public string SummaryText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Series forecast: {Series.Count:N0}  |  horizon {Options.Horizon} {Options.Granularity}(s)");
            foreach (var g in Series.GroupBy(s => s.Pattern).OrderByDescending(g => g.Count()))
                sb.AppendLine($"  {g.Key,-13}: {g.Count():N0}");
            if (!double.IsNaN(OverallWape))
            {
                sb.AppendLine($"Backtest WAPE (volume-weighted): {OverallWape:P1}");
                sb.AppendLine($"Same-as-last-year / naive baseline: {BaselineWape:P1}");
                sb.AppendLine($"Series where the chosen model beats the baseline: {ShareBeatingBaseline:P0}");
            }
            sb.AppendLine("Models chosen:");
            foreach (var g in Series.GroupBy(s => s.Model).OrderByDescending(g => g.Count()).Take(8))
                sb.AppendLine($"  {g.Count(),5}  {g.Key}");
            return sb.ToString();
        }

        private static string N(double d) => double.IsNaN(d) || double.IsInfinity(d) ? "" : d.ToString("0.####", CultureInfo.InvariantCulture);
        private static string Csv(string s) => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Loading from the DataTable your DataPreprocessor.LoadFile already returns
    // ══════════════════════════════════════════════════════════════════════

    public static class ForecastDataLoader
    {
        public static List<SalesRecord> FromDataTable(
            DataTable dt, string dateColumn, string quantityColumn,
            IReadOnlyList<string> keyColumns, Action<string>? log = null)
        {
            foreach (var c in new[] { dateColumn, quantityColumn }.Concat(keyColumns))
                if (!dt.Columns.Contains(c))
                    throw new ArgumentException($"Column '{c}' was not found in the file.");

            var list = new List<SalesRecord>(dt.Rows.Count);
            int badDate = 0, badQty = 0;

            foreach (DataRow row in dt.Rows)
            {
                var date = ParseDate(row[dateColumn]);
                if (date == null) { badDate++; continue; }
                var qty = ParseNumber(row[quantityColumn]);
                if (qty == null) { badQty++; continue; }

                string key = string.Join(ForecastStore.KeySeparator, keyColumns.Select(c => Convert.ToString(row[c])?.Trim() ?? ""));
                list.Add(new SalesRecord { Date = date.Value, Key = key, Quantity = qty.Value });
            }

            log?.Invoke($"Loaded {list.Count:N0} rows ({badDate:N0} bad dates, {badQty:N0} bad quantities skipped).");
            return list;
        }

        private static double? ParseNumber(object? o)
        {
            if (o == null || o is DBNull) return null;
            if (o is double d) return d;
            return double.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture)?.Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        private static DateTime? ParseDate(object? o)
        {
            if (o == null || o is DBNull) return null;
            if (o is DateTime dt) return dt;
            if (o is double oa && oa > 20000 && oa < 80000) return DateTime.FromOADate(oa);

            var raw = Convert.ToString(o, CultureInfo.InvariantCulture)?.Trim().Trim('"');
            if (string.IsNullOrEmpty(raw)) return null;

            string[] fmts =
            {
                "dd-MM-yyyy HH:mm", "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy",
                "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                "yyyy/MM/dd", "dd-MMM-yyyy", "MMM-yyyy", "MMMM yyyy"
            };
            if (DateTime.TryParseExact(raw, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1)) return d1;
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2) ? d2 : null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Calendar
    // ══════════════════════════════════════════════════════════════════════

    internal sealed class PeriodCalendar
    {
        private readonly ForecastOptions _o;
        public PeriodCalendar(ForecastOptions o) { _o = o; }

        public bool IsWorking(DateTime d) =>
            !_o.NonWorkingDays.Contains(d.DayOfWeek) && !_o.Holidays.Contains(d.Date);

        public DateTime PeriodStart(DateTime d)
        {
            d = d.Date;
            switch (_o.Granularity)
            {
                case Granularity.Month: return new DateTime(d.Year, d.Month, 1);
                case Granularity.Week: return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));   // Monday
                default:
                    // sales on a weekend/holiday roll forward: the quantity is kept, not deleted
                    for (int i = 0; i < 14 && !IsWorking(d); i++) d = d.AddDays(1);
                    return d;
            }
        }

        public DateTime Advance(DateTime p)
        {
            switch (_o.Granularity)
            {
                case Granularity.Month: return p.AddMonths(1);
                case Granularity.Week: return p.AddDays(7);
                default:
                    var d = p.AddDays(1);
                    for (int i = 0; i < 14 && !IsWorking(d); i++) d = d.AddDays(1);
                    return d;
            }
        }

        public bool IsPartial(DateTime lastPeriod, DateTime maxDate) => _o.Granularity switch
        {
            Granularity.Month => maxDate < lastPeriod.AddMonths(1).AddDays(-4),   // tolerate a weekend / holiday at month end
            Granularity.Week => maxDate < lastPeriod.AddDays(4),                  // before Friday
            _ => false
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Engine
    // ══════════════════════════════════════════════════════════════════════

    public static class ForecastEngine
    {
        public static ForecastReport Run(
            IEnumerable<SalesRecord> records, ForecastOptions opt,
            Action<string>? log = null, CancellationToken ct = default)
        {
            var report = new ForecastReport { Options = opt };
            void L(string m) { log?.Invoke(m); report.Log.Add(m); }

            var data = records.Where(r => !string.IsNullOrWhiteSpace(r.Key)).ToList();
            if (data.Count == 0) throw new InvalidOperationException("No usable sales records.");
            if (opt.Horizon < 1) throw new ArgumentException("Horizon must be at least 1.");

            var cal = new PeriodCalendar(opt);

            // 1) bucket into periods, per series
            var buckets = new Dictionary<string, Dictionary<DateTime, double>>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in data)
            {
                var p = cal.PeriodStart(r.Date);
                if (!buckets.TryGetValue(r.Key, out var map))
                    buckets[r.Key] = map = new Dictionary<DateTime, double>();
                map[p] = (map.TryGetValue(p, out var cur) ? cur : 0) + r.Quantity;   // returns net off sales
            }

            // 2) ONE shared calendar with every period present, so quiet periods become real zeros
            var maxDate = data.Max(r => r.Date.Date);
            var first = buckets.Values.SelectMany(m => m.Keys).Min();
            var periods = new List<DateTime>();
            for (var p = first; p <= cal.PeriodStart(maxDate); p = cal.Advance(p)) periods.Add(p);
            if (periods.Count > 0 && periods[^1] > maxDate) periods.RemoveAt(periods.Count - 1);
            if (opt.DropPartialLastPeriod && periods.Count > 1 && cal.IsPartial(periods[^1], maxDate))
            {
                L($"Last period {periods[^1]:yyyy-MM-dd} is incomplete (data ends {maxDate:yyyy-MM-dd}); it was left out so it does not drag forecasts down.");
                periods.RemoveAt(periods.Count - 1);
            }
            report.Periods = periods.ToArray();
            L($"Calendar: {periods.Count} {opt.Granularity} period(s), {periods[0]:yyyy-MM-dd} to {periods[^1]:yyyy-MM-dd}; {buckets.Count:N0} series.");

            var future = new DateTime[opt.Horizon];
            var cursor = periods[^1];
            for (int i = 0; i < opt.Horizon; i++) { cursor = cal.Advance(cursor); future[i] = cursor; }

            // 3) series with leading zeros trimmed (a part is not "zero" before it launched)
            var items = new List<(string Key, double[] Y, DateTime[] Hist)>();
            foreach (var (key, map) in buckets)
            {
                var full = periods.Select(p => map.TryGetValue(p, out var v) ? Math.Max(0, v) : 0.0).ToArray();
                int start = Array.FindIndex(full, v => v > 0);
                if (start < 0) continue;
                items.Add((key, full[start..], periods.Skip(start).ToArray()));
            }

            // 4) forecast each series
            var results = items.AsParallel().WithCancellation(ct).AsOrdered()
                .Select(it => ForecastOne(it.Key, it.Y, it.Hist, future, opt))
                .ToList();
            report.Series.AddRange(results);

            // 5) overall scorecard
            var bt = results.Where(s => s.SumActual > 0).ToList();
            if (bt.Count > 0)
            {
                double act = bt.Sum(s => s.SumActual);
                report.OverallWape = bt.Sum(s => s.AbsErr) / act;
                report.BaselineWape = bt.Sum(s => s.BaselineAbsErr) / act;
                report.ShareBeatingBaseline = bt.Count(s => s.Skill > 0) / (double)bt.Count;
            }
            L(report.SummaryText());
            return report;
        }

        // ──────────────────────────────────────────────────────────────────

        private static SeriesForecast ForecastOne(string key, double[] y, DateTime[] hist, DateTime[] future, ForecastOptions opt)
        {
            int H = opt.Horizon, season = opt.SeasonLength;
            var res = new SeriesForecast { Key = key, History = y, HistoryPeriods = hist, ForecastPeriods = future };
            res.Pattern = Classify(y, opt, out var adi, out var cv2);
            res.Adi = adi; res.Cv2 = cv2;

            if (res.Pattern == DemandPattern.Dormant)
            {
                int trailing = TrailingZeros(y);
                double months = PeriodsToMonths(trailing, opt.Granularity);
                res.Model = "Dormant (zero forecast)";
                res.Note = $"No sales for {months:F0} month(s). " +
                           (months > 24 ? "Consider discontinuation." : months > 18 ? "Strategic stock review." : "Monitor - may reactivate.");
                SetForecast(res, ForecastModels.Repeat(0, H), 0, opt);
                return res;
            }

            int minTrain = Math.Max(4, opt.MinHistory / 2);
            int eh = Math.Min(H, y.Length - minTrain);          // test window length
            if (res.Pattern == DemandPattern.TooShort || eh < 1)
            {
                res.Pattern = DemandPattern.TooShort;
                res.Model = "MA6 (short history)";
                res.Note = $"Only {y.Length} period(s) of history - no backtest possible.";
                SetForecast(res, new MovingAverageModel(6).Forecast(y, H, season)!, StdDev(y), opt);
                return res;
            }

            // rolling-origin cuts, newest first
            int step = Math.Max(1, eh / 2);
            var cuts = new List<int>();
            for (int k = 0; k < opt.BacktestOrigins; k++)
            {
                int cut = y.Length - eh - k * step;
                if (cut < minTrain) break;
                cuts.Add(cut);
            }

            // every candidate forecasts every origin
            var evals = new List<ModelEval>();
            foreach (var m in ForecastModels.All())
            {
                var preds = new List<double[]>();
                bool ok = true;
                foreach (var cut in cuts)
                {
                    var f = m.Forecast(y[..cut], eh, season);
                    if (f == null || f.Any(double.IsNaN)) { ok = false; break; }
                    preds.Add(f.Select(v => Math.Max(0, v)).ToArray());
                }
                if (ok) evals.Add(Score(new[] { m }, m.Name, preds, y, cuts, eh));
            }
            if (evals.Count == 0)
            {
                res.Model = "MA6 (fallback)";
                SetForecast(res, new MovingAverageModel(6).Forecast(y, H, season)!, StdDev(y), opt);
                return res;
            }

            // baseline = same period last year (or last value when there is no full season)
            var baseline = evals.FirstOrDefault(e => e.Name == "SeasonalNaive") ?? evals.FirstOrDefault(e => e.Name == "Naive") ?? evals[0];

            // combination of the best few models
            if (opt.CombineTop >= 2 && evals.Count >= opt.CombineTop)
            {
                var top = evals.OrderBy(e => e.Rmse).Take(opt.CombineTop).ToList();
                var comb = new List<double[]>();
                for (int k = 0; k < cuts.Count; k++)
                {
                    var avg = new double[eh];
                    for (int i = 0; i < eh; i++) avg[i] = top.Average(e => e.Preds[k][i]);
                    comb.Add(avg);
                }
                evals.Add(Score(top.SelectMany(e => e.Members).ToArray(),
                    "Combo(" + string.Join("+", top.Select(e => e.Name)) + ")", comb, y, cuts, eh));
            }

            var best = evals.OrderBy(e => e.Rmse).ThenBy(e => Math.Abs(e.Bias)).First();

            // refit the winner on ALL the history
            var fits = best.Members.Select(m => m.Forecast(y, H, season)).Where(f => f != null).Select(f => f!).ToList();
            var final = fits.Count == 0
                ? new MovingAverageModel(6).Forecast(y, H, season)!
                : Enumerable.Range(0, H).Select(i => fits.Average(f => f[i])).ToArray();

            res.Model = best.Name;
            res.BacktestRmse = best.Rmse;
            res.BacktestWape = best.SumActual > 0 ? best.AbsErr / best.SumActual : double.NaN;
            res.BacktestBias = best.SumActual > 0 ? (best.SumFc - best.SumActual) / best.SumActual : double.NaN;
            res.BaselineRmse = baseline.Rmse;
            res.Skill = baseline.Rmse > 1e-9 ? 1 - best.Rmse / baseline.Rmse : (best.Rmse <= 1e-9 ? 0 : double.NaN);
            res.AbsErr = best.AbsErr; res.SumActual = best.SumActual; res.BaselineAbsErr = baseline.AbsErr;
            if (res.Skill <= 0) res.Note = "No better than the baseline - treat this forecast with caution.";

            SetForecast(res, final, best.Rmse, opt);
            return res;
        }

        // ── scoring ──────────────────────────────────────────────────────

        private sealed class ModelEval
        {
            public string Name = "";
            public IForecaster[] Members = Array.Empty<IForecaster>();
            public List<double[]> Preds = new();
            public double Rmse, AbsErr, SumActual, SumFc;
            public double Bias => SumActual > 0 ? (SumFc - SumActual) / SumActual : 0;
        }

        private static ModelEval Score(IForecaster[] members, string name, List<double[]> preds, double[] y, List<int> cuts, int eh)
        {
            var e = new ModelEval { Name = name, Members = members, Preds = preds };
            double sse = 0; int n = 0;
            for (int k = 0; k < cuts.Count; k++)
                for (int i = 0; i < eh; i++)
                {
                    double a = y[cuts[k] + i], f = preds[k][i], err = f - a;
                    sse += err * err; e.AbsErr += Math.Abs(err); e.SumActual += a; e.SumFc += f; n++;
                }
            // RMSE (not MAE) on purpose: it rewards the MEAN, which is what stock planning needs.
            // MAE would reward forecasting zero for lumpy parts.
            e.Rmse = n > 0 ? Math.Sqrt(sse / n) : double.PositiveInfinity;
            return e;
        }

        // ── demand pattern (Syntetos-Boylan) ─────────────────────────────

        private static DemandPattern Classify(double[] y, ForecastOptions opt, out double adi, out double cv2)
        {
            adi = 0; cv2 = 0;
            if (TrailingZeros(y) >= opt.DormantAfterPeriods) return DemandPattern.Dormant;

            var nz = y.Where(v => v > 0).ToArray();
            if (nz.Length == 0) return DemandPattern.Dormant;
            adi = (double)y.Length / nz.Length;
            double mean = nz.Average();
            cv2 = nz.Length > 1 ? nz.Average(v => (v - mean) * (v - mean)) / (mean * mean) : 0;

            if (y.Length < opt.MinHistory) return DemandPattern.TooShort;
            bool lowAdi = adi < 1.32, lowCv = cv2 < 0.49;
            return lowAdi ? (lowCv ? DemandPattern.Smooth : DemandPattern.Erratic)
                          : (lowCv ? DemandPattern.Intermittent : DemandPattern.Lumpy);
        }

        // ── small helpers ────────────────────────────────────────────────

        private static void SetForecast(SeriesForecast r, double[] f, double sigma, ForecastOptions opt)
        {
            double z = opt.IntervalLevel >= 0.95 ? 1.96 : opt.IntervalLevel >= 0.90 ? 1.645 : 1.2816;
            r.Forecast = f.Select(v => Math.Max(0, v)).ToArray();
            r.Lower = r.Forecast.Select(v => Math.Max(0, v - z * sigma)).ToArray();
            r.Upper = r.Forecast.Select(v => v + z * sigma).ToArray();
        }

        private static int TrailingZeros(double[] y)
        {
            int c = 0;
            for (int i = y.Length - 1; i >= 0 && y[i] <= 0; i--) c++;
            return c;
        }

        private static double PeriodsToMonths(int periods, Granularity g) => g switch
        {
            Granularity.Month => periods,
            Granularity.Week => periods / 4.345,
            _ => periods / 21.0
        };

        private static double StdDev(double[] y)
        {
            if (y.Length < 2) return 0;
            double m = y.Average();
            return Math.Sqrt(y.Sum(v => (v - m) * (v - m)) / (y.Length - 1));
        }
    }
}