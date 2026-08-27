using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace Image_Checker.Services
{
    /// <summary>
    /// One-Class Classifier — trains exclusively on OK images.
    /// At inference, images that look sufficiently different from OK
    /// are flagged as NG (anomaly detection / novelty detection).
    ///
    /// Architecture: Autoencoder
    ///   Encoder compresses OK images to a compact latent vector.
    ///   Decoder reconstructs the original image from that vector.
    ///   Training loss = reconstruction error on OK images only.
    ///
    /// Inference rule:
    ///   reconstruction_error > threshold  →  NG  (anomaly)
    ///   reconstruction_error ≤ threshold  →  OK  (normal)
    ///
    /// Threshold is calibrated automatically on a held-out OK
    /// validation split: mean + (sensitivity * std_dev).
    /// Lower sensitivity  → stricter (more NG flags).
    /// Higher sensitivity → looser  (fewer NG flags).
    /// </summary>
    public class OneClassTrainer
    {
        // ══════════════════════════════════════════════════════════════
        //  PUBLIC CONFIG / RESULT
        // ══════════════════════════════════════════════════════════════

        public class Config
        {
            public int ImageWidth { get; init; } = 128;
            public int ImageHeight { get; init; } = 128;
            public int LatentDim { get; init; } = 64;
            public int Epochs { get; init; } = 30;
            public int BatchSize { get; init; } = 16;
            public float LearningRate { get; init; } = 0.001f;
            public float ValidationSplit { get; init; } = 0.2f;
            public bool Augment { get; init; } = true;
            public int EarlyStopPatience { get; init; } = 5;
            /// <summary>
            /// Threshold multiplier: threshold = mean_err + Sensitivity * std_err.
            /// Range 1.0 (strict) – 3.0 (lenient). Default 2.0.
            /// </summary>
            public float Sensitivity { get; init; } = 2.0f;
            public bool UseGpu { get; init; } = false;
        }

        public class TrainResult
        {
            public string ModelPath { get; init; } = "";
            public float Threshold { get; init; }
            public float MeanValError { get; init; }
            public float StdValError { get; init; }
            public int TrainCount { get; init; }
            public int ValCount { get; init; }
        }

        // ══════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════

        private readonly string _okFolderPath;
        private readonly string _outputPath;
        private readonly Config _cfg;
        private readonly Rectangle _roiRect;
        private readonly Action<string>? _log;
        private readonly Device _device;

        public OneClassTrainer(
            string okFolderPath,
            string outputPath,
            Config config,
            Rectangle roiRect,
            Action<string>? log = null)
        {
            _okFolderPath = okFolderPath;
            _outputPath = outputPath;
            _cfg = config;
            _roiRect = roiRect;
            _log = log;

            _device = config.UseGpu && cuda.is_available()
                ? new Device(DeviceType.CUDA)
                : new Device(DeviceType.CPU);
        }

        // ══════════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════════════

        public TrainResult Train(CancellationToken ct = default)
        {
            Log("═══════════════════════════════════════════════");
            Log("🔵 ONE-CLASS (ANOMALY DETECTION) TRAINER");
            Log($"   Device : {_device.type}");
            Log($"   Image  : {_cfg.ImageWidth}×{_cfg.ImageHeight}");
            Log($"   Latent : {_cfg.LatentDim}  Epochs={_cfg.Epochs}");
            Log($"   Sensitivity: {_cfg.Sensitivity} " +
                "(lower = stricter NG detection)");
            Log("═══════════════════════════════════════════════");

            // 1. Load OK images
            var allPaths = LoadOkImages();
            Log($"✅ {allPaths.Count} OK images found");

            if (allPaths.Count < 10)
                throw new InvalidOperationException(
                    "Need at least 10 OK images for one-class training.");

            ct.ThrowIfCancellationRequested();

            // 2. Split train / val
            var rng = new Random(42);
            var shuffled = allPaths.OrderBy(_ => rng.Next()).ToList();
            int valCount = Math.Max(2, (int)(shuffled.Count * _cfg.ValidationSplit));
            var valPaths = shuffled.Take(valCount).ToList();
            var trainPaths = shuffled.Skip(valCount).ToList();
            Log($"   Train={trainPaths.Count}  Val={valCount}");

            ct.ThrowIfCancellationRequested();

            // 3. Build autoencoder
            using var model = new ConvAutoencoder(_cfg.LatentDim);
            model.to(_device);
            Log($"   Parameters: {model.parameters().Sum(p => p.numel()):N0}");

            // 4. Optimizer
            var optimizer = optim.Adam(model.parameters(), lr: _cfg.LearningRate);

            // 5. Training loop
            RunTrainingLoop(model, optimizer, trainPaths, valPaths, ct);

            ct.ThrowIfCancellationRequested();

            // 6. Calibrate threshold on validation set
            var (meanErr, stdErr, threshold) = CalibrateThreshold(model, valPaths);
            Log($"\n📏 Threshold calibration:");
            Log($"   Val mean error : {meanErr:F6}");
            Log($"   Val std  error : {stdErr:F6}");
            Log($"   Threshold      : {threshold:F6}  " +
                $"(mean + {_cfg.Sensitivity}×std)");

            ct.ThrowIfCancellationRequested();

            // 7. Save model + sidecar
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var modelPath = Path.Combine(_outputPath,
                $"oneclass_autoencoder_{stamp}.bin");

            model.eval();
            model.save(modelPath);
            SaveSidecar(modelPath, threshold, meanErr, stdErr);

            Log($"\n💾 Model saved → {Path.GetFileName(modelPath)}");
            Log("═══════════════════════════════════════════════");
            Log("✅ ONE-CLASS TRAINING COMPLETE");
            Log($"   Threshold : {threshold:F6}");
            Log($"   Any image with reconstruction error > {threshold:F6}");
            Log($"   will be classified as NG.");
            Log("═══════════════════════════════════════════════");

            return new TrainResult
            {
                ModelPath = modelPath,
                Threshold = threshold,
                MeanValError = meanErr,
                StdValError = stdErr,
                TrainCount = trainPaths.Count,
                ValCount = valCount
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  AUTOENCODER ARCHITECTURE
        //  Encoder: 3 → 32 → 64 → latent (FC)
        //  Decoder: latent → 64 → 32 → 3
        // ══════════════════════════════════════════════════════════════

        internal sealed class ConvAutoencoder : Module<Tensor, Tensor>
        {
            private readonly Sequential _encoder;
            private readonly Sequential _decoder;
            private readonly Linear _fcEnc;
            private readonly Linear _fcDec;

            // spatial size after 3 × MaxPool2d(2) on a 128×128 input = 16×16
            private const int SpatialAfterPool = 16;
            private const int ChannelsAfterPool = 64;

            public ConvAutoencoder(int latentDim) : base("ConvAutoencoder")
            {
                _encoder = Sequential(
                    // 3 → 32,  128×128 → 64×64
                    Conv2d(3, 32, kernel_size: 3, padding: 1),
                    BatchNorm2d(32), ReLU(inplace: true),
                    MaxPool2d(2, 2),

                    // 32 → 64,  64×64 → 32×32
                    Conv2d(32, 64, kernel_size: 3, padding: 1),
                    BatchNorm2d(64), ReLU(inplace: true),
                    MaxPool2d(2, 2),

                    // 64 → 64,  32×32 → 16×16
                    Conv2d(64, 64, kernel_size: 3, padding: 1),
                    BatchNorm2d(64), ReLU(inplace: true),
                    MaxPool2d(2, 2)
                );

                int flatSize = ChannelsAfterPool *
                               SpatialAfterPool * SpatialAfterPool; // 64×16×16 = 16384

                _fcEnc = Linear(flatSize, latentDim);
                _fcDec = Linear(latentDim, flatSize);

                _decoder = Sequential(
                    // 64×16×16 → 64×32×32
                    Upsample(scale_factor: new double[] { 2, 2 }),
                    Conv2d(64, 64, kernel_size: 3, padding: 1),
                    BatchNorm2d(64), ReLU(inplace: true),

                    // 64×32×32 → 32×64×64
                    Upsample(scale_factor: new double[] { 2, 2 }),
                    Conv2d(64, 32, kernel_size: 3, padding: 1),
                    BatchNorm2d(32), ReLU(inplace: true),

                    // 32×64×64 → 3×128×128
                    Upsample(scale_factor: new double[] { 2, 2 }),
                    Conv2d(32, 3, kernel_size: 3, padding: 1),
                    Sigmoid()   // output in [0,1] to match normalised input
                );

                RegisterComponents();
            }

            public override Tensor forward(Tensor x)
            {
                // Encode
                using var ef = _encoder.forward(x);
                long batchSize = ef.shape[0];
                using var flat = ef.reshape(batchSize, -1);
                using var z = functional.relu(_fcEnc.forward(flat));

                // Decode
                using var df = functional.relu(_fcDec.forward(z));
                int sp = SpatialAfterPool;
                using var unfl = df.reshape(batchSize, ChannelsAfterPool, sp, sp);
                return _decoder.forward(unfl);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _encoder.Dispose(); _decoder.Dispose();
                    _fcEnc.Dispose(); _fcDec.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  TRAINING LOOP
        // ══════════════════════════════════════════════════════════════

        private void RunTrainingLoop(
            ConvAutoencoder model,
            optim.Optimizer optimizer,
            List<string> trainPaths,
            List<string> valPaths,
            CancellationToken ct)
        {
            float bestValLoss = float.MaxValue;
            int patience = 0;
            var rng = new Random(42);

            for (int epoch = 1; epoch <= _cfg.Epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();

                // ── Train ──────────────────────────────────────────
                model.train();
                float trainLoss = 0f;
                int batches = 0;

                var shuffled = trainPaths.OrderBy(_ => rng.Next()).ToList();

                for (int b = 0; b < shuffled.Count; b += _cfg.BatchSize)
                {
                    ct.ThrowIfCancellationRequested();

                    var batch = shuffled.Skip(b).Take(_cfg.BatchSize).ToList();
                    using var X = MakeBatch(batch, augment: _cfg.Augment);

                    optimizer.zero_grad();

                    using var recon = model.forward(X);
                    using var loss = MseLoss(recon, X);

                    loss.backward();
                    optimizer.step();

                    trainLoss += loss.item<float>();
                    batches++;
                }

                // ── Validate ───────────────────────────────────────
                model.eval();
                float valLoss = 0f;
                int vBatches = 0;

                using (no_grad())
                {
                    for (int b = 0; b < valPaths.Count; b += _cfg.BatchSize)
                    {
                        int take = Math.Min(_cfg.BatchSize, valPaths.Count - b);
                        using var X = MakeBatch(
                            valPaths.GetRange(b, take), augment: false);

                        using var recon = model.forward(X);
                        using var loss = MseLoss(recon, X);

                        valLoss += loss.item<float>();
                        vBatches++;
                    }
                }

                float avgTrain = trainLoss / Math.Max(1, batches);
                float avgVal = valLoss / Math.Max(1, vBatches);

                Log($"   Epoch {epoch,3}/{_cfg.Epochs} — " +
                    $"loss: {avgTrain:F6}  val_loss: {avgVal:F6}");

                // ── Early stopping ─────────────────────────────────
                if (avgVal < bestValLoss - 1e-6f)
                {
                    bestValLoss = avgVal;
                    patience = 0;
                }
                else if (++patience >= _cfg.EarlyStopPatience)
                {
                    Log($"   ⏹ Early stop at epoch {epoch}");
                    break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  THRESHOLD CALIBRATION
        // ══════════════════════════════════════════════════════════════

        private (float mean, float std, float threshold)
            CalibrateThreshold(ConvAutoencoder model, List<string> valPaths)
        {
            Log("\n📐 Calibrating threshold on validation set...");
            model.eval();

            var errors = new List<float>();

            using (no_grad())
            {
                for (int b = 0; b < valPaths.Count; b += _cfg.BatchSize)
                {
                    int take = Math.Min(_cfg.BatchSize, valPaths.Count - b);
                    using var X = MakeBatch(
                        valPaths.GetRange(b, take), augment: false);

                    using var recon = model.forward(X);

                    // Per-image MSE: mean over C×H×W dims
                    using var perImg = (recon - X).pow(2)
                        .mean(new long[] { 1, 2, 3 });

                    var data = perImg.cpu().data<float>().ToArray();
                    errors.AddRange(data);
                }
            }

            float mean = errors.Average();
            float std = (float)Math.Sqrt(
                errors.Average(e => (e - mean) * (e - mean)));
            float thr = mean + _cfg.Sensitivity * std;

            return (mean, std, thr);
        }

        // ══════════════════════════════════════════════════════════════
        //  BATCH BUILDER
        // ══════════════════════════════════════════════════════════════

        private Tensor MakeBatch(List<string> paths, bool augment)
        {
            int n = paths.Count, h = _cfg.ImageHeight, w = _cfg.ImageWidth;
            var data = new float[n * 3 * h * w];
            var rng = augment ? new Random() : null;

            for (int i = 0; i < n; i++)
                LoadImage(paths[i], data, i, h, w, rng);

            return tensor(data, new long[] { n, 3, h, w }).to(_device);
        }

        private void LoadImage(
            string path, float[] dest, int idx,
            int h, int w, Random? rng)
        {
            using var src = new Bitmap(path);

            // ROI crop
            Bitmap working;
            if (_roiRect.Width > 0 && _roiRect.Height > 0)
            {
                int rx = Math.Max(0,
                    Math.Min(_roiRect.X, src.Width - _roiRect.Width));
                int ry = Math.Max(0,
                    Math.Min(_roiRect.Y, src.Height - _roiRect.Height));
                working = src.Clone(
                    new Rectangle(rx, ry,
                        Math.Min(_roiRect.Width, src.Width),
                        Math.Min(_roiRect.Height, src.Height)),
                    src.PixelFormat);
            }
            else
            {
                working = (Bitmap)src.Clone();
            }

            using var resized = new Bitmap(working, new System.Drawing.Size(w, h));
            working.Dispose();

            using var final = rng != null ? Augment(resized, rng) : resized;

            var bmpData = final.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int stride = bmpData.Stride;
                var pixBytes = new byte[Math.Abs(stride) * h];
                Marshal.Copy(bmpData.Scan0, pixBytes, 0, pixBytes.Length);

                int baseOff = idx * 3 * h * w;
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;
                        int pixN = y * w + x;
                        dest[baseOff + 0 * h * w + pixN] =
                            pixBytes[off + 2] / 255f; // R
                        dest[baseOff + 1 * h * w + pixN] =
                            pixBytes[off + 1] / 255f; // G
                        dest[baseOff + 2 * h * w + pixN] =
                            pixBytes[off + 0] / 255f; // B
                    }
                }
            }
            finally { final.UnlockBits(bmpData); }
        }

        private static Bitmap Augment(Bitmap src, Random rng)
        {
            var bmp = (Bitmap)src.Clone();
            if (rng.NextDouble() < 0.5)
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);

            float b = (float)(1.0 + (rng.NextDouble() - 0.5) * 0.3);
            using var g = Graphics.FromImage(bmp);
            var cm = new System.Drawing.Imaging.ColorMatrix(new[]
            {
                new[] { b,  0f, 0f, 0f, 0f },
                new[] { 0f, b,  0f, 0f, 0f },
                new[] { 0f, 0f, b,  0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { 0f, 0f, 0f, 0f, 1f }
            });
            var ia = new System.Drawing.Imaging.ImageAttributes();
            ia.SetColorMatrix(cm);
            g.DrawImage(src,
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                0, 0, src.Width, src.Height,
                GraphicsUnit.Pixel, ia);
            return bmp;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private static Tensor MseLoss(Tensor recon, Tensor target)
            => functional.mse_loss(recon, target);

        private List<string> LoadOkImages()
        {
            var exts = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
            return Directory
                .EnumerateFiles(_okFolderPath, "*.*",
                    SearchOption.AllDirectories)
                .Where(f => exts.Contains(
                    Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
        }

        private void SaveSidecar(
            string modelPath, float threshold,
            float mean, float std)
        {
            var meta = new
            {
                ModelType = "OneClassAutoencoder",
                Architecture = "ConvAutoencoder",
                InputShape = new[] { 1, 3,
                    _cfg.ImageHeight, _cfg.ImageWidth },
                Layout = "NCHW",
                Normalization = "divide_255",
                Threshold = threshold,
                MeanValError = mean,
                StdValError = std,
                Sensitivity = _cfg.Sensitivity,
                LatentDim = _cfg.LatentDim,
                ClassNames = new[] { "OK", "NG" },
                RoiX = _roiRect.X,
                RoiY = _roiRect.Y,
                RoiW = _roiRect.Width,
                RoiH = _roiRect.Height
            };

            File.WriteAllText(
                Path.ChangeExtension(modelPath, ".json"),
                System.Text.Json.JsonSerializer.Serialize(meta,
                    new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true }));

            File.WriteAllLines(
                Path.ChangeExtension(modelPath, ".labels"),
                new[] { "OK", "NG" });
        }

        private void Log(string msg) => _log?.Invoke(msg);
    }
}