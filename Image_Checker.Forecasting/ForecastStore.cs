using System.Text.Json;
using System.Text.Json.Serialization;

namespace Image_Checker.Forecasting
{
    /// <summary>What is written to disk: every series with its history, forecast, interval and backtest accuracy.</summary>
    public sealed class ForecastFile
    {
        public string Task { get; set; } = "Forecast";
        public string GranularityName { get; set; } = "Month";
        public int Horizon { get; set; }
        public string QuantityColumn { get; set; } = "";
        public string[] KeyColumns { get; set; } = Array.Empty<string>();
        public string TrainedAt { get; set; } = "";
        public double OverallWape { get; set; } = double.NaN;
        public double BaselineWape { get; set; } = double.NaN;
        public double ShareBeatingBaseline { get; set; } = double.NaN;
        public List<SeriesForecast> Series { get; set; } = new();

        /// <summary>Distinct values per key column, for the dropdowns in the Prediction form.</summary>
        public Dictionary<string, List<string>> UniqueKeyValues()
        {
            var d = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < KeyColumns.Length; i++)
            {
                int idx = i;
                d[KeyColumns[i]] = Series
                    .Select(s => ForecastStore.KeyPart(s.Key, idx))
                    .Where(v => v.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            return d;
        }
    }

    /// <summary>One or many series combined (when the filter matches several items).</summary>
    public sealed class ForecastSlice
    {
        public int SeriesCount { get; set; }
        public DateTime[] HistoryPeriods { get; set; } = Array.Empty<DateTime>();
        public double[] History { get; set; } = Array.Empty<double>();
        public DateTime[] ForecastPeriods { get; set; } = Array.Empty<DateTime>();
        public double[] Forecast { get; set; } = Array.Empty<double>();
        public double[] Lower { get; set; } = Array.Empty<double>();
        public double[] Upper { get; set; } = Array.Empty<double>();
        public string Summary { get; set; } = "";
    }

    public static class ForecastStore
    {
        public const string KeySeparator = " | ";
        public const string Extension = ".forecast.json";

        private static readonly JsonSerializerOptions Opt = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static bool IsForecastFile(string path) =>
            path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

        public static string Save(ForecastReport rep, IEnumerable<string> keyColumns, string quantityColumn, string outDir)
        {
            var file = new ForecastFile
            {
                GranularityName = rep.Options.Granularity.ToString(),
                Horizon = rep.Options.Horizon,
                QuantityColumn = quantityColumn,
                KeyColumns = keyColumns.ToArray(),
                TrainedAt = DateTime.Now.ToString("o"),
                OverallWape = rep.OverallWape,
                BaselineWape = rep.BaselineWape,
                ShareBeatingBaseline = rep.ShareBeatingBaseline,
                Series = rep.Series
            };
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, $"bestModel-Forecast-{DateTime.Now:yyyyMMddHHmmss}{Extension}");
            File.WriteAllText(path, JsonSerializer.Serialize(file, Opt));
            return path;
        }

        public static ForecastFile Load(string path) =>
            JsonSerializer.Deserialize<ForecastFile>(File.ReadAllText(path), Opt)
            ?? throw new InvalidDataException("Forecast file is empty or corrupt.");

        public static string KeyPart(string key, int index)
        {
            var parts = key.Split(KeySeparator);
            return index < parts.Length ? parts[index].Trim() : "";
        }

        /// <summary>
        /// Sums every series that matches the filters (blank filters = everything).
        /// Forecasts add up exactly; the interval of a sum is combined in quadrature
        /// (assumes the items' errors are independent - a reasonable approximation).
        /// </summary>
        public static ForecastSlice Slice(ForecastFile f, IReadOnlyDictionary<string, string> filters,
                                          int horizon, int historyPeriods = 24)
        {
            var rules = filters
                .Select(kv => (Idx: Array.FindIndex(f.KeyColumns, c => c.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)), Val: kv.Value))
                .Where(x => x.Idx >= 0)
                .ToList();

            var matched = f.Series
                .Where(s => rules.All(r => string.Equals(KeyPart(s.Key, r.Idx), r.Val, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var slice = new ForecastSlice { SeriesCount = matched.Count };
            if (matched.Count == 0) return slice;

            // history, summed by period
            var hist = new SortedDictionary<DateTime, double>();
            foreach (var s in matched)
                for (int i = 0; i < s.History.Length; i++)
                    hist[s.HistoryPeriods[i]] = (hist.TryGetValue(s.HistoryPeriods[i], out var cur) ? cur : 0) + s.History[i];
            var tail = hist.TakeLast(historyPeriods).ToList();
            slice.HistoryPeriods = tail.Select(t => t.Key).ToArray();
            slice.History = tail.Select(t => t.Value).ToArray();

            // forecast, summed; interval in quadrature
            int h = Math.Min(horizon, matched.Min(s => s.Forecast.Length));
            slice.ForecastPeriods = matched[0].ForecastPeriods.Take(h).ToArray();
            slice.Forecast = new double[h]; slice.Lower = new double[h]; slice.Upper = new double[h];
            for (int i = 0; i < h; i++)
            {
                double fc = matched.Sum(s => s.Forecast[i]);
                double dl = Math.Sqrt(matched.Sum(s => Math.Pow(s.Forecast[i] - s.Lower[i], 2)));
                double du = Math.Sqrt(matched.Sum(s => Math.Pow(s.Upper[i] - s.Forecast[i], 2)));
                slice.Forecast[i] = fc;
                slice.Lower[i] = Math.Max(0, fc - dl);
                slice.Upper[i] = fc + du;
            }

            // one-line explanation shown above the table
            if (matched.Count == 1)
            {
                var s = matched[0];
                slice.Summary = $"{s.Model} | {s.Pattern}" +
                    (double.IsNaN(s.BacktestWape) ? "" : $" | backtest WAPE {s.BacktestWape:P0}") +
                    (double.IsNaN(s.Skill) ? "" : $" | vs baseline {s.Skill:+0%;-0%}") +
                    (s.Note.Length > 0 ? " | " + s.Note : "");
            }
            else
            {
                slice.Summary = $"{matched.Count:N0} series combined | " +
                    string.Join(", ", matched.GroupBy(s => s.Pattern).OrderByDescending(g => g.Count())
                                             .Take(3).Select(g => $"{g.Count()} {g.Key}"));
            }
            return slice;
        }
    }
}