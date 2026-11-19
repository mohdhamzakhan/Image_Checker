using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;

namespace Image_Checker.Services
{
    public class ScrewPosition
    {
        public string Name { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        [JsonIgnore] public Rectangle Rect => new(X, Y, W, H);
    }

    public static class PositionCropper
    {
        static Rectangle Clamp(Rectangle r, int w, int h)
        {
            int rw = Math.Min(r.Width, w), rh = Math.Min(r.Height, h);
            return new Rectangle(Math.Max(0, Math.Min(r.X, w - rw)),
                                 Math.Max(0, Math.Min(r.Y, h - rh)), rw, rh);
        }

        /// <summary>One PNG per position in the temp folder. Caller deletes them.</summary>
        [SupportedOSPlatform("Windows10.0.19041")]
        public static List<(ScrewPosition Pos, string Path)> CropToTemp(
            string imagePath, IEnumerable<ScrewPosition> positions)
        {
            var list = new List<(ScrewPosition, string)>();
            using var bmp = new Bitmap(imagePath);
            foreach (var p in positions)
            {
                using var crop = bmp.Clone(Clamp(p.Rect, bmp.Width, bmp.Height), bmp.PixelFormat);
                string tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
                crop.Save(tmp, ImageFormat.Png);
                list.Add((p, tmp));
            }
            return list;
        }
        [SupportedOSPlatform("Windows10.0.19041")]
        /// <summary>Training data: saves one labelled crop into Dataset\&lt;label&gt;\.</summary>
        public static string SaveCrop(string imagePath, ScrewPosition p, string label, string datasetRoot)
        {
            string dir = Path.Combine(datasetRoot, label);
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir,
                $"{Path.GetFileNameWithoutExtension(imagePath)}_{p.Name}.png");
            using var bmp = new Bitmap(imagePath);
            using var crop = bmp.Clone(Clamp(p.Rect, bmp.Width, bmp.Height), bmp.PixelFormat);
            crop.Save(dest, ImageFormat.Png);
            return dest;
        }
        [SupportedOSPlatform("Windows10.0.19041")]
        public static string SaveCrop(string imagePath, ScrewPosition p, string label, string datasetRoot, string? uniqueName = null)
        {
            string dir = Path.Combine(datasetRoot, label);
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, $"{uniqueName ?? Path.GetFileNameWithoutExtension(imagePath)}_{p.Name}.png");
            using var bmp = new Bitmap(imagePath);
            using var crop = bmp.Clone(Clamp(p.Rect, bmp.Width, bmp.Height), bmp.PixelFormat);
            crop.Save(dest, ImageFormat.Png);
            return dest;
        }

        /// <summary>Part is OK only if every position is OK. Anything else, including Unknown, is NG.</summary>
        public static (string Label, float Confidence, string Detail) Aggregate(
            List<(string Pos, string Label, float Conf)> r)
        {
            var bad = r.Where(x => !x.Label.Equals("OK", StringComparison.OrdinalIgnoreCase)).ToList();
            if (bad.Count == 0)
                return ("OK", r.Count == 0 ? 0f : r.Min(x => x.Conf), "all positions OK");

            return ("NG", bad.Max(x => x.Conf),
                string.Join("; ", bad.Select(b => $"{b.Pos}: {b.Label} ({b.Conf:P0})")));
        }
    }
}