using System.Globalization;

namespace Image_Checker.Forecasting
{
    /// <summary>A univariate forecaster. Returns null when it cannot be fitted on this history.</summary>
    public interface IForecaster
    {
        string Name { get; }
        double[]? Forecast(double[] y, int h, int season);
    }

    public static class ForecastModels
    {
        /// <summary>The candidate pool. Each series picks its own winner by backtest.</summary>
        public static IReadOnlyList<IForecaster> All() => new IForecaster[]
        {
            new NaiveModel(),
            new SeasonalNaiveModel(),
            new MovingAverageModel(3),
            new MovingAverageModel(6),
            new MovingAverageModel(12),
            new SesModel(),
            new DampedHoltModel(),
            new HoltWintersModel(),
            new CrostonSbaModel(0.1),
            new CrostonSbaModel(0.2),
            new TsbModel(0.1, 0.1),
            new TsbModel(0.3, 0.3),
        };

        internal static double[] Repeat(double v, int h)
        {
            var a = new double[h];
            Array.Fill(a, v);
            return a;
        }

        internal static string Fmt(double d) => d.ToString("0.0#", CultureInfo.InvariantCulture);
    }

    // ── Baselines ────────────────────────────────────────────────────────────

    public sealed class NaiveModel : IForecaster
    {
        public string Name => "Naive";
        public double[]? Forecast(double[] y, int h, int season)
            => y.Length == 0 ? null : ForecastModels.Repeat(y[^1], h);
    }

    public sealed class SeasonalNaiveModel : IForecaster
    {
        public string Name => "SeasonalNaive";
        public double[]? Forecast(double[] y, int h, int season)
        {
            if (season < 2 || y.Length < season) return null;
            var f = new double[h];
            for (int i = 0; i < h; i++) f[i] = y[y.Length - season + (i % season)];
            return f;
        }
    }

    public sealed class MovingAverageModel : IForecaster
    {
        private readonly int _k;
        public MovingAverageModel(int k) { _k = k; }
        public string Name => "MA" + _k;
        public double[]? Forecast(double[] y, int h, int season)
        {
            if (y.Length == 0) return null;
            return ForecastModels.Repeat(y.TakeLast(Math.Min(_k, y.Length)).Average(), h);
        }
    }

    // ── Smooth / trending demand ─────────────────────────────────────────────

    /// <summary>Simple exponential smoothing; alpha picked by one-step-ahead SSE.</summary>
    public sealed class SesModel : IForecaster
    {
        public string Name => "SES";
        public double[]? Forecast(double[] y, int h, int season)
        {
            if (y.Length < 3) return null;
            double bestSse = double.MaxValue, bestLevel = y[^1];
            foreach (var a in new[] { 0.05, 0.1, 0.2, 0.3, 0.5, 0.7, 0.9 })
            {
                double l = y[0], sse = 0;
                for (int t = 1; t < y.Length; t++)
                {
                    double e = y[t] - l;
                    sse += e * e;
                    l += a * e;
                }
                if (sse < bestSse) { bestSse = sse; bestLevel = l; }
            }
            return ForecastModels.Repeat(bestLevel, h);
        }
    }

    /// <summary>Holt's linear trend with a damped trend, so forecasts never run away.</summary>
    public sealed class DampedHoltModel : IForecaster
    {
        public string Name => "DampedHolt";
        public double[]? Forecast(double[] y, int h, int season)
        {
            int n = y.Length;
            if (n < 6) return null;

            double bestSse = double.MaxValue, bL = y[^1], bT = 0, bPhi = 0.9;
            int k = Math.Min(5, n - 1);
            double t0 = (y[k] - y[0]) / k;

            foreach (var a in new[] { 0.1, 0.3, 0.5, 0.8 })
                foreach (var b in new[] { 0.05, 0.1, 0.2 })
                    foreach (var phi in new[] { 0.8, 0.9, 0.98 })
                    {
                        double l = y[0], tr = t0, sse = 0;
                        for (int t = 1; t < n; t++)
                        {
                            double fc = l + phi * tr;
                            double e = y[t] - fc;
                            sse += e * e;
                            double lNew = fc + a * e;
                            tr = phi * tr + b * (lNew - l - phi * tr);
                            l = lNew;
                        }
                        if (sse < bestSse) { bestSse = sse; bL = l; bT = tr; bPhi = phi; }
                    }

            var f = new double[h];
            double cum = 0, p = 1;
            for (int i = 0; i < h; i++)
            {
                p *= bPhi;
                cum += p;
                f[i] = bL + cum * bT;
            }
            return f;
        }
    }

    /// <summary>Additive Holt-Winters (damped trend). Needs at least two full seasons.</summary>
    public sealed class HoltWintersModel : IForecaster
    {
        public string Name => "HoltWinters";
        public double[]? Forecast(double[] y, int h, int m)
        {
            int n = y.Length;
            if (m < 2 || n < 2 * m) return null;
            const double phi = 0.95;

            double l0 = y.Take(m).Average();
            double b0 = (y.Skip(m).Take(m).Average() - l0) / m;

            double best = double.MaxValue;
            double[]? bestF = null;

            foreach (var a in new[] { 0.1, 0.3, 0.5 })
                foreach (var b in new[] { 0.01, 0.05, 0.1 })
                    foreach (var g in new[] { 0.1, 0.3, 0.5 })
                    {
                        double l = l0, tr = b0, sse = 0;
                        var s = new double[m];
                        for (int i = 0; i < m; i++) s[i] = y[i] - l0;

                        for (int t = m; t < n; t++)
                        {
                            int k = t % m;
                            double fc = l + phi * tr + s[k];
                            double e = y[t] - fc;
                            sse += e * e;
                            double lNew = a * (y[t] - s[k]) + (1 - a) * (l + phi * tr);
                            tr = b * (lNew - l) + (1 - b) * phi * tr;
                            s[k] = g * (y[t] - lNew) + (1 - g) * s[k];
                            l = lNew;
                        }

                        if (sse < best)
                        {
                            best = sse;
                            var f = new double[h];
                            double cum = 0, p = 1;
                            for (int i = 1; i <= h; i++)
                            {
                                p *= phi;
                                cum += p;
                                f[i - 1] = l + cum * tr + s[(n - 1 + i) % m];
                            }
                            bestF = f;
                        }
                    }
            return bestF;
        }
    }

    // ── Intermittent / lumpy demand (the usual case for spare components) ────

    /// <summary>Croston with the Syntetos-Boylan bias correction.</summary>
    public sealed class CrostonSbaModel : IForecaster
    {
        private readonly double _a;
        public CrostonSbaModel(double alpha) { _a = alpha; }
        public string Name => "Croston-SBA(" + ForecastModels.Fmt(_a) + ")";

        public double[]? Forecast(double[] y, int h, int season)
        {
            if (y.Length == 0) return null;
            double z = 0, p = 1, q = 1;
            bool init = false;
            foreach (var v in y)
            {
                if (v > 0)
                {
                    if (!init) { z = v; p = q; init = true; }
                    else { z += _a * (v - z); p += _a * (q - p); }
                    q = 1;
                }
                else q++;
            }
            if (!init) return ForecastModels.Repeat(0, h);
            return ForecastModels.Repeat((1 - _a / 2) * z / p, h);
        }
    }

    /// <summary>
    /// Teunter-Syntetos-Babai: updates the probability of demand EVERY period, so the
    /// forecast decays when a part stops selling (Croston cannot do this).
    /// </summary>
    public sealed class TsbModel : IForecaster
    {
        private readonly double _alpha, _beta;
        public TsbModel(double alpha, double beta) { _alpha = alpha; _beta = beta; }
        public string Name => "TSB(" + ForecastModels.Fmt(_alpha) + "," + ForecastModels.Fmt(_beta) + ")";

        public double[]? Forecast(double[] y, int h, int season)
        {
            if (y.Length == 0) return null;
            int first = Array.FindIndex(y, v => v > 0);
            if (first < 0) return ForecastModels.Repeat(0, h);

            int warm = Math.Min(12, y.Length);
            double prob = y.Take(warm).Count(v => v > 0) / (double)warm;
            double z = y[first];

            foreach (var v in y)
            {
                bool d = v > 0;
                prob += _beta * ((d ? 1.0 : 0.0) - prob);
                if (d) z += _alpha * (v - z);
            }
            return ForecastModels.Repeat(prob * z, h);
        }
    }
}