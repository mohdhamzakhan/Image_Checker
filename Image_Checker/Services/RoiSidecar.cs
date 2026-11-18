#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Image_Checker.Services
{
    /// <summary>What a model's .json settings file says about the cropped area (ROI) it was trained on.</summary>
    public sealed class RoiInfo
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int W { get; set; }
        public int H { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        /// <summary>true when the file really defines a crop (width and height above zero).</summary>
        public bool HasRoi { get { return W > 0 && H > 0; } }

        public Rectangle ToRectangle() { return new Rectangle(X, Y, W, H); }
    }

    /// <summary>
    /// Reads the "&lt;model&gt;.json" that ModelTrainer writes next to every model. That file is the ONLY place
    /// the ROI is stored: if it is missing, the app gives the model whole photos instead of the crop it was
    /// trained on, and good images start to look wrong.
    /// </summary>
    public static class RoiSidecar
    {
        public static string PathFor(string modelPath)
        {
            return Path.ChangeExtension(modelPath, ".json");
        }

        /// <summary>
        /// false = the file is missing/unreadable or has none of the ROI keys (not a model settings file).
        /// true  = it was read; check HasRoi to see whether it defines a crop (zeros mean "whole photo").
        /// </summary>
        public static bool TryRead(string jsonPath, out RoiInfo info)
        {
            info = new RoiInfo();
            try
            {
                if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath)) return false;

                string text = File.ReadAllText(jsonPath);
                bool any = false;
                info.X = ReadInt(text, "RoiX", ref any);
                info.Y = ReadInt(text, "RoiY", ref any);
                info.W = ReadInt(text, "RoiW", ref any);
                info.H = ReadInt(text, "RoiH", ref any);
                info.ImageWidth = ReadInt(text, "ImageWidth", ref any);
                info.ImageHeight = ReadInt(text, "ImageHeight", ref any);
                return any;
            }
            catch
            {
                info = new RoiInfo();
                return false;
            }
        }

        private static int ReadInt(string json, string key, ref bool found)
        {
            // flat JSON written by System.Text.Json: "RoiX": 220  (key matching is case-insensitive)
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)", RegexOptions.IgnoreCase);
            if (!m.Success) return 0;
            found = true;
            int value;
            return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }
    }
}