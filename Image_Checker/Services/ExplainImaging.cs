#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Image_Checker.Services
{
    /// <summary>Bitmap work for the explanation: crop, statistics, hiding a patch, drawing the heatmap.</summary>
    public static class ExplainImaging
    {
        /// <summary>The same clamping as Form1.PreprocessForPrediction and the builder's Apply-ROI.</summary>
        public static Rectangle ClampRoi(int width, int height, Rectangle roi)
        {
            if (roi.Width <= 0 || roi.Height <= 0)
                return new Rectangle(0, 0, width, height);   // no ROI: the whole image

            int x = Math.Max(0, Math.Min(roi.X, width - roi.Width));
            int y = Math.Max(0, Math.Min(roi.Y, height - roi.Height));
            return new Rectangle(x, y, Math.Min(roi.Width, width), Math.Min(roi.Height, height));
        }

        /// <summary>Loads an image completely into memory so the file is not left locked.</summary>
        public static Bitmap LoadCopy(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs))
            {
                return new Bitmap(img);
            }
        }

        /// <summary>The part of the photo the model actually sees (ROI crop, 24-bit).</summary>
        public static Bitmap CropToRoi(Bitmap src, Rectangle roi)
        {
            Rectangle r = ClampRoi(src.Width, src.Height, roi);
            return src.Clone(r, PixelFormat.Format24bppRgb);
        }

        public static ImageStats ComputeStats(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            Bitmap? converted = null;
            Bitmap src = bmp;
            if (bmp.PixelFormat != PixelFormat.Format24bppRgb)
            {
                converted = bmp.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format24bppRgb);
                src = converted;
            }

            try
            {
                BitmapData data = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    int stride = data.Stride;
                    var bytes = new byte[Math.Abs(stride) * h];
                    Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                    var gray = new float[w * h];
                    double sr = 0, sg = 0, sb = 0;
                    for (int y = 0; y < h; y++)
                    {
                        int row = y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            int i = row + x * 3;
                            byte b = bytes[i], g = bytes[i + 1], r = bytes[i + 2];
                            gray[y * w + x] = 0.299f * r + 0.587f * g + 0.114f * b;
                            sr += r; sg += g; sb += b;
                        }
                    }

                    ImageStats stats = ImageStatsMath.FromGray(gray, w, h);
                    double n = Math.Max(1, w * h);
                    stats.MeanR = sr / n; stats.MeanG = sg / n; stats.MeanB = sb / n;
                    return stats;
                }
                finally
                {
                    src.UnlockBits(data);
                }
            }
            finally
            {
                if (converted != null) converted.Dispose();
            }
        }

        /// <summary>Scores a bitmap through a temp PNG (the model loads images from files).</summary>
        public static ScoreResult ScoreBitmap(IImageScorer scorer, Bitmap bmp)
        {
            string path = Path.Combine(Path.GetTempPath(), "ic_explain_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                bmp.Save(path, ImageFormat.Png);
                return scorer.Score(path);
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { }
            }
        }

        /// <summary>A copy of the crop with one patch painted over in a neutral (average) colour.</summary>
        public static Bitmap Hide(Bitmap crop, Rectangle patch, Color fill)
        {
            var copy = new Bitmap(crop.Width, crop.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.DrawImage(crop, 0, 0, crop.Width, crop.Height);
                using (var brush = new SolidBrush(fill))
                    g.FillRectangle(brush, patch);
            }
            return copy;
        }

        public static Color MeanColor(ImageStats s)
        {
            return Color.FromArgb(Clamp255(s.MeanR), Clamp255(s.MeanG), Clamp255(s.MeanB));
        }

        /// <summary>The photo with the ROI outlined, so you can see what part was given to the model.</summary>
        public static Bitmap DrawRoi(Bitmap original, Rectangle clampedRoi)
        {
            var copy = new Bitmap(original.Width, original.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(copy))
            {
                g.DrawImage(original, 0, 0, original.Width, original.Height);
                float width = Math.Max(2f, original.Width / 200f);
                using (var pen = new Pen(Color.Yellow, width))
                    g.DrawRectangle(pen, clampedRoi.X, clampedRoi.Y, clampedRoi.Width, clampedRoi.Height);
            }
            return copy;
        }

        /// <summary>
        /// Overlay: red = this area pushed the model toward the class, blue = it argued against.
        /// Strength follows the size of the change; colours are normalised to the strongest cell.
        /// </summary>
        public static Bitmap RenderHeatmap(Bitmap crop, float[,] heat)
        {
            int rows = heat.GetLength(0), cols = heat.GetLength(1);
            float maxAbs = 0f;
            foreach (float v in heat) maxAbs = Math.Max(maxAbs, Math.Abs(v));
            if (maxAbs < 1e-6f) maxAbs = 1f;

            var small = new Bitmap(cols, rows, PixelFormat.Format32bppArgb);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float v = heat[r, c] / maxAbs;                       // -1 .. 1
                    int alpha = (int)(Math.Min(1f, Math.Abs(v)) * 200f);
                    small.SetPixel(c, r, v >= 0 ? Color.FromArgb(alpha, 255, 40, 40)
                                                 : Color.FromArgb(alpha, 40, 100, 255));
                }
            }

            var result = new Bitmap(crop.Width, crop.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.DrawImage(crop, 0, 0, crop.Width, crop.Height);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(small, new Rectangle(0, 0, crop.Width, crop.Height));
            }
            small.Dispose();
            return result;
        }

        private static int Clamp255(double v)
        {
            return (int)Math.Max(0, Math.Min(255, Math.Round(v)));
        }
    }
}