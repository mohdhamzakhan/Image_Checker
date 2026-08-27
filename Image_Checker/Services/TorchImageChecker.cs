using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Image_Checker.Services
{
    public class TorchImageChecker : IDisposable
    {
        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC TYPES  (mirrors OnnxImageChecker.Prediction for drop-in swap)
        // ══════════════════════════════════════════════════════════════════════

        public class Prediction
        {
            public string Label { get; init; } = "";
            public float Confidence { get; init; }
            public float[] AllScores { get; init; } = Array.Empty<float>();
            public Dictionary<string, float> ScoreMap { get; init; } = new();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SIDECAR META  (same .json written by CnnTrainer.SaveOnnxMeta)
        // ══════════════════════════════════════════════════════════════════════

        private class ModelMeta
        {
            public int[] InputShape { get; set; } = { 1, 3, 224, 224 };
            public List<string> ClassNames { get; set; } = new();
            public string Architecture { get; set; } = "MiniCNN";
            public int RoiX { get; set; }
            public int RoiY { get; set; }
            public int RoiW { get; set; }
            public int RoiH { get; set; }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════════

        private readonly Module<Tensor, Tensor> _model;
        private readonly ModelMeta _meta;
        private readonly Device _device;
        private readonly int _imgH, _imgW;
        private bool _disposed;

        // ══════════════════════════════════════════════════════════════════════
        //  CONSTRUCTION
        // ══════════════════════════════════════════════════════════════════════

        /// <param name="binPath">Path to .bin saved by CnnTrainer (model.save)</param>
        /// <param name="useGpu">Use CUDA if available</param>
        public TorchImageChecker(string binPath, bool useGpu = false)
        {
            if (!File.Exists(binPath))
                throw new FileNotFoundException($"Model not found: {binPath}");

            _device = useGpu && cuda.is_available()
                ? new Device(DeviceType.CUDA)
                : new Device(DeviceType.CPU);

            _meta = LoadMeta(binPath);

            _imgH = _meta.InputShape.Length >= 4 ? _meta.InputShape[2] : 224;
            _imgW = _meta.InputShape.Length >= 4 ? _meta.InputShape[3] : 224;

            // Rebuild the same architecture used during training
            _model = _meta.Architecture == "ResidualCNN"
                ? new ResidualCNN(_meta.ClassNames.Count)
                : new MiniCNN(_meta.ClassNames.Count);

            _model.load(binPath);
            _model.to(_device);
            _model.eval();
        }

        private static ModelMeta LoadMeta(string binPath)
        {
            // Try .json sidecar first
            var jsonPath = Path.ChangeExtension(binPath, ".json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    var m = JsonSerializer.Deserialize<ModelMeta>(
                        File.ReadAllText(jsonPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (m != null) return m;
                }
                catch { /* fall through */ }
            }

            // Fallback: .labels sidecar
            var meta = new ModelMeta();
            var labelsPath = Path.ChangeExtension(binPath, ".labels");
            if (File.Exists(labelsPath))
                meta.ClassNames = File.ReadAllLines(labelsPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            return meta;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PREDICT
        // ══════════════════════════════════════════════════════════════════════

        public Prediction Predict(string imagePath)
        {
            using var bmp = new Bitmap(imagePath);
            return Predict(bmp);
        }

        public Prediction Predict(Bitmap srcBitmap)
        {
            // 1. ROI crop (same logic as CnnTrainer)
            Bitmap working;
            var roi = new Rectangle(_meta.RoiX, _meta.RoiY, _meta.RoiW, _meta.RoiH);
            if (roi.Width > 0 && roi.Height > 0)
            {
                int rx = Math.Max(0, Math.Min(roi.X, srcBitmap.Width - roi.Width));
                int ry = Math.Max(0, Math.Min(roi.Y, srcBitmap.Height - roi.Height));
                working = srcBitmap.Clone(
                    new Rectangle(rx, ry,
                        Math.Min(roi.Width, srcBitmap.Width),
                        Math.Min(roi.Height, srcBitmap.Height)),
                    srcBitmap.PixelFormat);
            }
            else
            {
                working = (Bitmap)srcBitmap.Clone();
            }

            // 2. Resize
            using var resized = new Bitmap(working, new System.Drawing.Size(_imgW, _imgH));
            working.Dispose();

            // 3. Bitmap → NCHW float array
            var xData = new float[3 * _imgH * _imgW];
            BitmapToNchw(resized, xData);

            // 4. Inference
            using var X = tensor(xData, new long[] { 1, 3, _imgH, _imgW }).to(_device);

            float[] scores;
            using (no_grad())
            {
                using var logits = _model.forward(X);
                scores = Softmax(logits.cpu().data<float>().ToArray());
            }

            // 5. Build result
            var classNames = _meta.ClassNames.Count > 0
                ? _meta.ClassNames
                : Enumerable.Range(0, scores.Length)
                    .Select(i => $"Class_{i}").ToList();

            int best = scores
                .Select((s, i) => (s, i))
                .OrderByDescending(x => x.s)
                .First().i;

            return new Prediction
            {
                Label = classNames[best],
                Confidence = scores[best],
                AllScores = scores,
                ScoreMap = classNames
                    .Take(scores.Length)
                    .Zip(scores, (name, score) => (name, score))
                    .ToDictionary(x => x.name, x => x.score)
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BATCH PREDICT
        // ══════════════════════════════════════════════════════════════════════

        public IEnumerable<(string Path, Prediction? Result, Exception? Error)>
            PredictBatch(IEnumerable<string> imagePaths)
        {
            foreach (var path in imagePaths)
            {
                Prediction? pred = null;
                Exception? err = null;
                try { pred = Predict(path); }
                catch (Exception ex) { err = ex; }
                yield return (path, pred, err);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void BitmapToNchw(Bitmap bmp, float[] dest)
        {
            int h = bmp.Height, w = bmp.Width;

            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int stride = bmpData.Stride;
                var pixBytes = new byte[Math.Abs(stride) * h];
                Marshal.Copy(bmpData.Scan0, pixBytes, 0, pixBytes.Length);

                // NCHW: R-plane, G-plane, B-plane  (matches CnnTrainer exactly)
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;
                        int pixN = y * w + x;
                        dest[0 * h * w + pixN] = pixBytes[off + 2] / 255f; // R
                        dest[1 * h * w + pixN] = pixBytes[off + 1] / 255f; // G
                        dest[2 * h * w + pixN] = pixBytes[off + 0] / 255f; // B
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        private static float[] Softmax(float[] logits)
        {
            float max = logits.Max();
            var exp = logits.Select(x => (float)Math.Exp(x - max)).ToArray();
            float sum = exp.Sum();
            return exp.Select(e => e / sum).ToArray();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DISPOSE
        // ══════════════════════════════════════════════════════════════════════

        public void Dispose()
        {
            if (_disposed) return;
            _model.Dispose();
            _disposed = true;
        }
    }
}