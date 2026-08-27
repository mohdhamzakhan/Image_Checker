using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

/*  NuGet:
 *    Microsoft.ML.OnnxRuntime          (CPU inference)
 *    Microsoft.ML.OnnxRuntime.Gpu      (optional, CUDA)
 *
 *  Reads ONNX models produced by CnnTrainer (TorchSharp).
 *  Expects NCHW layout — [1, 3, H, W] float32 — pixels / 255.
 */

namespace Image_Checker.Services
{
    public class OnnxImageChecker : IDisposable
    {
        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC TYPES
        // ══════════════════════════════════════════════════════════════════════

        public class Prediction
        {
            public string Label { get; init; } = "";
            public float Confidence { get; init; }
            public float[] AllScores { get; init; } = Array.Empty<float>();
            public Dictionary<string, float> ScoreMap { get; init; } = new();
        }

        public class ModelInfo
        {
            public string InputName { get; init; } = "";
            public int[] InputShape { get; init; } = Array.Empty<int>();
            public string Layout { get; init; } = "";
            public List<string> ClassNames { get; init; } = new();
            public Rectangle RoiRect { get; init; }
            public List<string> InputNodes { get; init; } = new();
            public List<string> OutputNodes { get; init; } = new();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  METADATA  (sidecar .json written by CnnTrainer)
        // ══════════════════════════════════════════════════════════════════════

        private class OnnxMeta
        {
            public string InputName { get; set; } = "input_image";
            // [1, 3, H, W]  (NCHW from TorchSharp)
            public int[] InputShape { get; set; } = { 1, 3, 224, 224 };
            public string Layout { get; set; } = "NCHW";
            public string Normalization { get; set; } = "divide_255";
            public List<string> ClassNames { get; set; } = new();
            public int RoiX { get; set; }
            public int RoiY { get; set; }
            public int RoiW { get; set; }
            public int RoiH { get; set; }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════════

        private readonly InferenceSession _session;
        private readonly OnnxMeta _meta;
        // Derived from InputShape [1, 3, H, W]
        private readonly int _imgH;
        private readonly int _imgW;
        private bool _disposed;

        // ══════════════════════════════════════════════════════════════════════
        //  CONSTRUCTION
        // ══════════════════════════════════════════════════════════════════════

        /// <param name="onnxPath">Path to .onnx produced by CnnTrainer</param>
        /// <param name="useGpu">Set true if OnnxRuntime.Gpu NuGet is installed</param>
        public OnnxImageChecker(string onnxPath, bool useGpu = false)
        {
            if (!File.Exists(onnxPath))
                throw new FileNotFoundException($"ONNX model not found: {onnxPath}");

            var opts = new SessionOptions();
            if (useGpu)
            {
                try { opts.AppendExecutionProvider_CUDA(0); }
                catch { /* fall back to CPU silently */       }
            }
            opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

            _session = new InferenceSession(onnxPath, opts);
            _meta = LoadMeta(onnxPath);

            // Shape is NCHW → index 2 = H, index 3 = W
            _imgH = _meta.InputShape.Length >= 4 ? _meta.InputShape[2] : 224;
            _imgW = _meta.InputShape.Length >= 4 ? _meta.InputShape[3] : 224;
        }

        private static OnnxMeta LoadMeta(string onnxPath)
        {
            var jsonPath = Path.ChangeExtension(onnxPath, ".json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    return JsonSerializer.Deserialize<OnnxMeta>(
                               File.ReadAllText(jsonPath))
                           ?? new OnnxMeta();
                }
                catch { /* ignore malformed JSON */ }
            }

            // Fallback: try .labels sidecar for class names
            var meta = new OnnxMeta();
            var labelsPath = Path.ChangeExtension(onnxPath, ".labels");
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
            // 1. ROI crop
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

            // 2. Resize to model input size
            using var resized = new Bitmap(working, new Size(_imgW, _imgH));
            working.Dispose();

            // 3. Bitmap → NCHW float tensor
            var tensor = BitmapToNchw(resized);

            // 4. Resolve input node name
            string inputName = _meta.InputName;
            if (!_session.InputMetadata.ContainsKey(inputName))
                inputName = _session.InputMetadata.Keys.First();

            // 5. Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var results = _session.Run(inputs);
            var raw = results.First().AsEnumerable<float>().ToArray();

            // 6. Softmax (safe to apply even if output is already normalised)
            var scores = Softmax(raw);

            // 7. Build result
            int bestIdx = scores
                .Select((s, i) => (s, i))
                .OrderByDescending(x => x.s)
                .First().i;

            var classNames = _meta.ClassNames.Count > 0
                ? _meta.ClassNames
                : Enumerable.Range(0, scores.Length)
                    .Select(i => $"Class_{i}").ToList();

            while (classNames.Count < scores.Length)
                classNames.Add($"Class_{classNames.Count}");

            var scoreMap = classNames
                .Take(scores.Length)
                .Zip(scores, (name, score) => (name, score))
                .ToDictionary(x => x.name, x => x.score);

            return new Prediction
            {
                Label = classNames[bestIdx],
                Confidence = scores[bestIdx],
                AllScores = scores,
                ScoreMap = scoreMap
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
        //  MODEL INFO
        // ══════════════════════════════════════════════════════════════════════

        public ModelInfo GetModelInfo() => new ModelInfo
        {
            InputName = _meta.InputName,
            InputShape = _meta.InputShape,
            Layout = _meta.Layout,
            ClassNames = _meta.ClassNames,
            RoiRect = new Rectangle(_meta.RoiX, _meta.RoiY, _meta.RoiW, _meta.RoiH),
            InputNodes = _session.InputMetadata.Keys.ToList(),
            OutputNodes = _session.OutputMetadata.Keys.ToList()
        };

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        /// Converts Bitmap → DenseTensor<float> in NCHW layout [1, 3, H, W].
        private DenseTensor<float> BitmapToNchw(Bitmap bmp)
        {
            int h = bmp.Height, w = bmp.Width;

            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            var pixBytes = new byte[Math.Abs(bmpData.Stride) * h];
            Marshal.Copy(bmpData.Scan0, pixBytes, 0, pixBytes.Length);
            bmp.UnlockBits(bmpData);

            int stride = bmpData.Stride;

            // Shape: [1, 3, H, W]
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });

            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int off = row + x * 3;
                    // Format24bppRgb stores BGR on disk
                    tensor[0, 0, y, x] = pixBytes[off + 2] / 255f; // R
                    tensor[0, 1, y, x] = pixBytes[off + 1] / 255f; // G
                    tensor[0, 2, y, x] = pixBytes[off + 0] / 255f; // B
                }
            }

            return tensor;
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
            _session.Dispose();
            _disposed = true;
        }
    }
}