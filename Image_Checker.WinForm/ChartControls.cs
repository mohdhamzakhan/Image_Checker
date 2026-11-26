using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Image_Checker.WinForm
{
    public readonly record struct Slice(string Label, double Value, Color Color);

    public sealed record BarSeries(string Name, Color Color, int[] Values);

    /// <summary>Pie chart with a legend (label, count, percent). Drawn with GDI+, so no chart package is needed.</summary>
    public sealed class PieChartControl : Control
    {
        public string Title { get; private set; } = "";
        private List<Slice> _slices = new();

        public PieChartControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        public void SetData(string title, IEnumerable<Slice> slices)
        {
            Title = title;
            _slices = slices.Where(s => s.Value > 0).ToList();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold))
                g.DrawString(Title, titleFont, Brushes.Black, 12, 8);

            double total = _slices.Sum(s => s.Value);
            if (total <= 0) { ChartText.Centered(g, Size, "No data for this day"); return; }

            const int top = 44, bottomPad = 16;
            int size = Math.Min(Height - top - bottomPad, (int)(Width * 0.5));
            if (size < 40) return;
            var pie = new Rectangle(16, top + (Height - top - bottomPad - size) / 2, size, size);

            using (var pctFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                float start = -90f;
                foreach (var s in _slices)
                {
                    float sweep = (float)(s.Value / total * 360.0);
                    using (var brush = new SolidBrush(s.Color)) g.FillPie(brush, pie, start, sweep);
                    using (var pen = new Pen(Color.White, 2f)) g.DrawPie(pen, pie, start, sweep);

                    if (sweep >= 24f)   // percent inside the slice
                    {
                        double mid = (start + sweep / 2f) * Math.PI / 180.0;
                        float cx = pie.X + pie.Width / 2f + (float)Math.Cos(mid) * pie.Width * 0.32f;
                        float cy = pie.Y + pie.Height / 2f + (float)Math.Sin(mid) * pie.Height * 0.32f;
                        string text = (s.Value / total).ToString("0%");
                        var sz = g.MeasureString(text, pctFont);
                        g.DrawString(text, pctFont, Brushes.White, cx - sz.Width / 2f, cy - sz.Height / 2f);
                    }
                    start += sweep;
                }
            }

            using (var font = new Font("Segoe UI", 10f))
            {
                int lx = pie.Right + 24, ly = pie.Top + 4;
                foreach (var s in _slices)
                {
                    using (var brush = new SolidBrush(s.Color)) g.FillRectangle(brush, lx, ly + 3, 14, 14);
                    g.DrawString($"{s.Label}: {s.Value:0} ({s.Value / total:0.#%})", font, Brushes.Black, lx + 22, ly);
                    ly += 26;
                }
            }
        }
    }

    /// <summary>Stacked bar chart (for example scans per hour, split into OK / NG / other).</summary>
    public sealed class StackedBarChartControl : Control
    {
        public string Title { get; private set; } = "";
        private string[] _labels = Array.Empty<string>();
        private List<BarSeries> _series = new();

        public StackedBarChartControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        public void SetData(string title, string[] labels, IEnumerable<BarSeries> series)
        {
            Title = title;
            _labels = labels;
            _series = series.ToList();
            Invalidate();
        }

        private static int NiceStep(int max)
        {
            int raw = Math.Max(1, (int)Math.Ceiling(max / 5.0));
            int pow = (int)Math.Pow(10, Math.Floor(Math.Log10(raw)));
            foreach (int m in new[] { 1, 2, 5, 10 })
                if (raw <= m * pow) return m * pow;
            return 10 * pow;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold))
                g.DrawString(Title, titleFont, Brushes.Black, 12, 8);

            int n = _labels.Length;
            var totals = new int[n];
            foreach (var s in _series)
                for (int i = 0; i < n && i < s.Values.Length; i++) totals[i] += s.Values[i];
            int max = n == 0 ? 0 : totals.Max();
            if (max == 0) { ChartText.Centered(g, Size, "No data for this day"); return; }

            // legend, right aligned
            using (var legendFont = new Font("Segoe UI", 9.5f))
            {
                float lx = Width - 16;
                for (int i = _series.Count - 1; i >= 0; i--)
                {
                    var sz = g.MeasureString(_series[i].Name, legendFont);
                    lx -= sz.Width + 20;
                    using (var brush = new SolidBrush(_series[i].Color)) g.FillRectangle(brush, lx, 14, 14, 14);
                    g.DrawString(_series[i].Name, legendFont, Brushes.Black, lx + 20, 11);
                    lx -= 14;
                }
            }

            var plot = new Rectangle(56, 52, Width - 56 - 16, Height - 52 - 36);
            if (plot.Width < 50 || plot.Height < 50) return;

            int step = NiceStep(max);
            int niceMax = (max + step - 1) / step * step;
            int lines = niceMax / step;

            using (var axisFont = new Font("Segoe UI", 8.5f))
            using (var gridPen = new Pen(Color.FromArgb(225, 225, 225)))
            using (var right = new StringFormat { Alignment = StringAlignment.Far })
            {
                for (int i = 0; i <= lines; i++)
                {
                    float y = plot.Bottom - plot.Height * i / (float)lines;
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                    g.DrawString((i * step).ToString(), axisFont, Brushes.Gray,
                                 new RectangleF(0, y - 9, plot.Left - 8, 18), right);
                }

                float slot = plot.Width / (float)n;
                float barWidth = slot * 0.68f;
                for (int i = 0; i < n; i++)
                {
                    float x = plot.Left + slot * i + (slot - barWidth) / 2f;
                    int cumulative = 0;
                    foreach (var s in _series)
                    {
                        int v = i < s.Values.Length ? s.Values[i] : 0;
                        if (v == 0) continue;
                        float yTop = plot.Bottom - plot.Height * (cumulative + v) / (float)niceMax;
                        float h = plot.Height * v / (float)niceMax;
                        using (var brush = new SolidBrush(s.Color)) g.FillRectangle(brush, x, yTop, barWidth, h);
                        cumulative += v;
                    }

                    if (totals[i] > 0 && slot >= 26f)   // total above the bar
                    {
                        string t = totals[i].ToString();
                        var sz = g.MeasureString(t, axisFont);
                        float y = plot.Bottom - plot.Height * totals[i] / (float)niceMax - sz.Height;
                        g.DrawString(t, axisFont, Brushes.DimGray, x + barWidth / 2f - sz.Width / 2f, y);
                    }

                    if (slot >= 22f || i % 2 == 0)      // hour label
                    {
                        var sz = g.MeasureString(_labels[i], axisFont);
                        g.DrawString(_labels[i], axisFont, Brushes.Gray, x + barWidth / 2f - sz.Width / 2f, plot.Bottom + 6);
                    }
                }
            }
            g.DrawLine(Pens.Gray, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        }
    }

    internal static class ChartText
    {
        public static void Centered(Graphics g, Size size, string text)
        {
            using var font = new Font("Segoe UI", 10f);
            var sz = g.MeasureString(text, font);
            g.DrawString(text, font, Brushes.Gray, (size.Width - sz.Width) / 2f, (size.Height - sz.Height) / 2f);
        }
    }
}