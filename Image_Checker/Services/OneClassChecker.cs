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
    /// <summary>
    /// Inference wrapper for a trained OneClassTrainer model.
    /// Loads .bin + .json sidecar, runs reconstruction,
    /// compares error to threshold → OK / NG.
    /// Drop-in compatible with TorchImageChecker.Prediction.
    /// </summary>
    public class OneClassChecker : IDisposable
    {
        // ══════════════════════════════════════════════════════════════
        //  PUBLIC TYPES
        // ══════════════════════════════════════════════════════════════

        public class Prediction
        {
            public string Label { get; init; } = "";
            /// <summary>
            /// For OK: 1 - normalised_error.
            /// For NG: normalised_error (how far above threshold).
            /// </summary>
            public float Confidence { get; init; }
            public float ReconError { get; init; }
            public float Threshold { get; init; }
            public bool IsAnomaly { get; init; }
            public Dictionary<string, float> ScoreMap { get; init; } = new();
        }

        // ══════════════════════════════════════════════════════════════
        //  SIDECAR META
        // ══════════════════════════════════════════════════════════════

        private class Meta
        {
            public int[] InputShape { get; set; } = { 1, 3, 128, 128 };
            public float Threshold { get; set; } = 0.01f;
            public float MeanValError { get; set; }
            public float StdValError { get; set; }
            public int LatentDim { get; set; } = 64;
            public int RoiX { get; set; }
            public int RoiY { get; set; }
            public int RoiW { get; set; }
            public int RoiH { get; set; }
        }

        // ══════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════

        private readonly OneClassTrainer.ConvAutoencoder _model;
        private readonly Meta _meta;
        private readonly Device _device;
        private readonly int _imgH, _imgW;
        private bool _disposed;

        // ══════════════════════════════════════════════════════════════
        //  CONSTRUCTION
        // ══════════════════════════════════════════════════════════════

        public OneClassChecker(string binPath, bool useGpu = false)
        {
            if (!File.Exists(binPath))
                throw new FileNotFoundException($"Model not found: {binPath}");

            _device = useGpu && cuda.is_available()
                ? new Device(DeviceType.CUDA)
                : new Device(DeviceType.CPU);

            _meta = LoadMeta(binPath);
            _imgH = _meta.InputShape.Length >= 4 ? _meta.InputShape[2] : 128;
            _imgW = _meta.InputShape.Length >= 4 ? _meta.InputShape[3] : 128;

            _model = new OneClassTrainer.ConvAutoencoder(_meta.LatentDim);
            _model.load(binPath);
            _model.to(_device);
            _model.eval();
        }

        private static Meta LoadMeta(string binPath)
        {
            var jsonPath = Path.ChangeExtension(binPath, ".json");
            if (!File.Exists(jsonPath)) return new Meta();

            try
            {
                return JsonSerializer.Deserialize<Meta>(
                    File.ReadAllText(jsonPath),
                    new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true })
                    ?? new Meta();
            }
            catch { return new Meta(); }
        }

        // ══════════════════════════════════════════════════════════════
        //  PREDICT
        // ══════════════════════════════════════════════════════════════

        public Prediction Predict(string imagePath)
        {
            using var bmp = new Bitmap(imagePath);
            return Predict(bmp);
        }

        public Prediction Predict(Bitmap srcBitmap)
        {
            float reconError = ComputeReconError(srcBitmap);

            // Classify
            bool isAnomaly = reconError > _meta.Threshold;
            string label = isAnomaly ? "NG" : "OK";

            // Confidence: normalise relative to threshold
            //   OK  → how far below threshold (0→1, 1 = perfect reconstruction)
            //   NG  → how far above threshold (0→1, clipped)
            float confidence;
            if (!isAnomaly)
            {
                // 1.0 when error=0, 0.0 when error=threshold
                confidence = Math.Max(0f,
                    1f - (reconError / Math.Max(_meta.Threshold, 1e-9f)));
            }
            else
            {
                // 0.5 at threshold, approaches 1.0 as error grows
                float excess = reconError - _meta.Threshold;
                float scale = Math.Max(_meta.StdValError, 1e-9f);
                confidence = Math.Min(1f, 0.5f + (excess / (2f * scale)));
            }

            return new Prediction
            {
                Label = label,
                Confidence = confidence,
                ReconError = reconError,
                Threshold = _meta.Threshold,
                IsAnomaly = isAnomaly,
                ScoreMap = new Dictionary<string, float>
                {
                    ["OK"] = isAnomaly ? 1f - confidence : confidence,
                    ["NG"] = isAnomaly ? confidence : 1f - confidence
                }
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  RECONSTRUCTION ERROR  (shared by Predict + evaluation methods)
        // ══════════════════════════════════════════════════════════════

        private float ComputeReconError(Bitmap srcBitmap)
        {
            // 1. ROI + resize
            Bitmap working;
            var roi = new Rectangle(
                _meta.RoiX, _meta.RoiY, _meta.RoiW, _meta.RoiH);

            if (roi.Width > 0 && roi.Height > 0)
            {
                int rx = Math.Max(0,
                    Math.Min(roi.X, srcBitmap.Width - roi.Width));
                int ry = Math.Max(0,
                    Math.Min(roi.Y, srcBitmap.Height - roi.Height));
                working = srcBitmap.Clone(
                    new Rectangle(rx, ry,
                        Math.Min(roi.Width, srcBitmap.Width),
                        Math.Min(roi.Height, srcBitmap.Height)),
                    srcBitmap.PixelFormat);
            }
            else { working = (Bitmap)srcBitmap.Clone(); }

            using var resized = new Bitmap(working, new System.Drawing.Size(_imgW, _imgH));
            working.Dispose();

            // 2. To NCHW tensor
            var xData = new float[3 * _imgH * _imgW];
            BitmapToNchw(resized, xData);

            using var X = tensor(xData,
                new long[] { 1, 3, _imgH, _imgW }).to(_device);

            // 3. Reconstruct and measure error
            using (no_grad())
            {
                using var recon = _model.forward(X);
                using var errTens = (recon - X).pow(2)
                    .mean(new long[] { 1, 2, 3 });
                return errTens.cpu().item<float>();
            }
        }

        private float ComputeReconErrorFromPath(string imagePath)
        {
            using var bmp = new Bitmap(imagePath);
            return ComputeReconError(bmp);
        }

        // ══════════════════════════════════════════════════════════════
        //  EVALUATION AGAINST REAL OK/NG DATA
        //  (the piece that was missing: verify the threshold actually
        //   separates real defects, not just OK-only validation stats)
        // ══════════════════════════════════════════════════════════════

        private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".bmp" };

        public class EvaluationResult
        {
            public int TotalOk { get; set; }
            public int TotalNg { get; set; }

            /// <summary>NG images correctly flagged as NG (the model caught the defect).</summary>
            public int TruePositiveNg { get; set; }
            /// <summary>NG images that slipped through as OK — the costly QC error.</summary>
            public int FalseNegativeNg { get; set; }
            /// <summary>OK images correctly passed.</summary>
            public int TrueNegativeOk { get; set; }
            /// <summary>OK images wrongly flagged as NG (false alarm).</summary>
            public int FalsePositiveOk { get; set; }

            /// <summary>Fraction of real defects actually caught. This is the headline QC number.</summary>
            public float NgRecall => TotalNg == 0 ? 0f : (float)TruePositiveNg / TotalNg;
            /// <summary>Fraction of good parts correctly passed (1 - false alarm rate).</summary>
            public float OkSpecificity => TotalOk == 0 ? 0f : (float)TrueNegativeOk / TotalOk;
            public float Accuracy => (TotalOk + TotalNg) == 0 ? 0f
                : (float)(TruePositiveNg + TrueNegativeOk) / (TotalOk + TotalNg);

            public List<string> MissedNgFiles { get; } = new();
            public List<string> FalseAlarmOkFiles { get; } = new();

            public override string ToString() =>
                $"Accuracy={Accuracy:P1}  NG-Recall={NgRecall:P1} ({TruePositiveNg}/{TotalNg} caught)  " +
                $"OK-Specificity={OkSpecificity:P1} ({TrueNegativeOk}/{TotalOk} passed)  " +
                $"Missed NG={FalseNegativeNg}  False alarms={FalsePositiveOk}";
        }

        /// <summary>
        /// Runs the model, at its currently configured threshold, against real labelled
        /// OK and NG folders. Use this before trusting a threshold that was only calibrated
        /// against OK validation error — it tells you whether real defects are actually caught.
        /// </summary>
        public EvaluationResult Evaluate(string okFolder, string ngFolder)
        {
            var result = new EvaluationResult();

            foreach (var file in EnumerateImages(okFolder))
            {
                result.TotalOk++;
                var pred = Predict(file);
                if (pred.IsAnomaly) { result.FalsePositiveOk++; result.FalseAlarmOkFiles.Add(file); }
                else result.TrueNegativeOk++;
            }

            foreach (var file in EnumerateImages(ngFolder))
            {
                result.TotalNg++;
                var pred = Predict(file);
                if (pred.IsAnomaly) result.TruePositiveNg++;
                else { result.FalseNegativeNg++; result.MissedNgFiles.Add(file); }
            }

            return result;
        }

        /// <summary>
        /// Tries several candidate thresholds against real OK/NG folders and returns the
        /// resulting confusion matrix for each, so you can pick a threshold that actually
        /// minimises missed defects instead of guessing from mean+sensitivity*std alone.
        /// Reconstruction errors are computed once per image and reused across all thresholds.
        /// </summary>
        public List<(float Threshold, EvaluationResult Result)> SweepThreshold(
            string okFolder, string ngFolder, IEnumerable<float> candidateThresholds)
        {
            var okErrors = EnumerateImages(okFolder)
                .Select(f => (File: f, Error: ComputeReconErrorFromPath(f)))
                .ToList();
            var ngErrors = EnumerateImages(ngFolder)
                .Select(f => (File: f, Error: ComputeReconErrorFromPath(f)))
                .ToList();

            var swept = new List<(float, EvaluationResult)>();
            foreach (var t in candidateThresholds)
            {
                var r = new EvaluationResult { TotalOk = okErrors.Count, TotalNg = ngErrors.Count };

                foreach (var (file, err) in okErrors)
                {
                    if (err > t) { r.FalsePositiveOk++; r.FalseAlarmOkFiles.Add(file); }
                    else r.TrueNegativeOk++;
                }
                foreach (var (file, err) in ngErrors)
                {
                    if (err > t) r.TruePositiveNg++;
                    else { r.FalseNegativeNg++; r.MissedNgFiles.Add(file); }
                }

                swept.Add((t, r));
            }
            return swept;
        }

        private static IEnumerable<string> EnumerateImages(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                yield break;

            foreach (var file in Directory.GetFiles(folder))
                if (ImageExts.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    yield return file;
        }

        // ══════════════════════════════════════════════════════════════
        //  THRESHOLD ADJUSTMENT (runtime tuning without retraining)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Allows the operator to tighten or loosen the decision boundary
        /// without retraining. Persists the new threshold to the sidecar.
        /// </summary>
        public void AdjustThreshold(float newThreshold, string binPath)
        {
            _meta.Threshold = newThreshold;

            var jsonPath = Path.ChangeExtension(binPath, ".json");
            if (!File.Exists(jsonPath)) return;

            try
            {
                var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                var dict = JsonSerializer.Deserialize
                    <Dictionary<string, JsonElement>>(
                    doc.RootElement.GetRawText())!;

                dict["Threshold"] = JsonSerializer
                    .SerializeToElement(newThreshold);

                File.WriteAllText(jsonPath,
                    JsonSerializer.Serialize(dict,
                        new JsonSerializerOptions
                        { WriteIndented = true }));
            }
            catch { /* non-fatal */ }
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private void BitmapToNchw(Bitmap bmp, float[] dest)
        {
            int h = bmp.Height, w = bmp.Width;
            var bd = bmp.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            try
            {
                int stride = bd.Stride;
                var px = new byte[Math.Abs(stride) * h];
                Marshal.Copy(bd.Scan0, px, 0, px.Length);

                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;
                        int pixN = y * w + x;
                        dest[0 * h * w + pixN] = px[off + 2] / 255f;
                        dest[1 * h * w + pixN] = px[off + 1] / 255f;
                        dest[2 * h * w + pixN] = px[off + 0] / 255f;
                    }
                }
            }
            finally { bmp.UnlockBits(bd); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _model.Dispose();
            _disposed = true;
        }
    }
}