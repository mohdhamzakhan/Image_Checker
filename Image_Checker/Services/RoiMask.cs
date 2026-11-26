using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Image_Checker.Services
{
    /// <summary>
    /// A round part (a ball) sits inside a rectangular ROI, so the four corners show background that only adds
    /// noise. This paints everything outside the inscribed circle/ellipse in one neutral gray.
    ///
    /// It must be applied EVERYWHERE the ROI is cropped (training dataset, prediction, corrections, Why? window),
    /// otherwise training and prediction see different pictures. One flag controls it: the model's .json holds
    /// "CircleMask": true, and Enabled is set from it whenever a model is loaded.
    /// Old models have no flag, so they behave exactly as before.
    /// </summary>
    public static class RoiMask
    {
        /// <summary>true = every ROI crop of the loaded model gets the circular mask.</summary>
        public static bool Enabled { get; set; }

        private const string DatasetMarker = "circle_mask.flag";
        private static readonly Color Fill = Color.FromArgb(128, 128, 128);

        /// <summary>A new 24-bit copy with everything outside the inscribed ellipse gray. The caller disposes it.</summary>
        public static Bitmap CircleMasked(Bitmap crop)
        {
            var result = new Bitmap(crop.Width, crop.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(result))
            {
                g.DrawImage(crop, 0, 0, crop.Width, crop.Height);

                // rectangle + ellipse in one path: the "alternate" fill leaves only the four corners
                using (var corners = new GraphicsPath())
                using (var brush = new SolidBrush(Fill))
                {
                    corners.AddRectangle(new Rectangle(0, 0, crop.Width, crop.Height));
                    corners.AddEllipse(0, 0, crop.Width, crop.Height);
                    g.FillPath(brush, corners);
                }
            }
            return result;
        }

        /// <summary>
        /// For prediction-time crops: when Enabled, returns the masked copy and disposes the original;
        /// otherwise returns the original unchanged. Use as:  using var c = RoiMask.Replace(bmp.Clone(...));
        /// </summary>
        public static Bitmap Replace(Bitmap crop)
        {
            if (!Enabled) return crop;
            var masked = CircleMasked(crop);
            crop.Dispose();
            return masked;
        }

        // ── remembers that a cropped dataset was built with the mask ─────────────

        public static void MarkDataset(string datasetDir)
        {
            File.WriteAllText(Path.Combine(datasetDir, DatasetMarker), "1");
        }

        public static bool DatasetMasked(string datasetDir)
        {
            return !string.IsNullOrEmpty(datasetDir) && File.Exists(Path.Combine(datasetDir, DatasetMarker));
        }

        // ── the flag in the model's .json ────────────────────────────────────────

        public static bool ReadFlag(string modelPath)
        {
            try
            {
                string json = Path.ChangeExtension(modelPath, ".json");
                if (!File.Exists(json)) return false;
                var node = JsonNode.Parse(File.ReadAllText(json)) as JsonObject;
                return node?["CircleMask"]?.GetValue<bool>() == true;
            }
            catch
            {
                return false;
            }
        }

        public static void WriteFlag(string modelPath, bool value)
        {
            string json = Path.ChangeExtension(modelPath, ".json");
            JsonObject node = File.Exists(json)
                ? (JsonNode.Parse(File.ReadAllText(json)) as JsonObject ?? new JsonObject())
                : new JsonObject();
            node["CircleMask"] = value;
            File.WriteAllText(json, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}