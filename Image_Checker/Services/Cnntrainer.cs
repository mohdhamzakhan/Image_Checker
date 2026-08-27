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

/*  NuGet packages required:
 *    TorchSharp                        (CPU)
 *    TorchSharp-cuda-windows           (optional, GPU on Windows)
 *    libtorch-cpu                      (native runtime - auto-pulled by TorchSharp)
 *
 *  ONNX export uses torch.jit.trace → export_to_onnx — zero Python, zero CLI tools.
 */

namespace Image_Checker.Services
{
    public class CnnTrainer
    {
        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC TYPES
        // ══════════════════════════════════════════════════════════════════════

        public class CnnConfig
        {
            public int ImageWidth { get; init; } = 224;
            public int ImageHeight { get; init; } = 224;
            public int Epochs { get; init; } = 20;
            public int BatchSize { get; init; } = 16;
            public float LearningRate { get; init; } = 0.001f;
            public float ValidationSplit { get; init; } = 0.2f;
            public bool Augment { get; init; } = true;
            public int EarlyStopPatience { get; init; } = 5;
            /// <summary>MiniCNN | ResidualCNN</summary>
            public string Architecture { get; init; } = "MiniCNN";
            public bool ExportOnnx { get; init; } = true;
            /// <summary>Use CUDA GPU if available</summary>
            public bool UseGpu { get; init; } = false;
        }

        public class CnnResult
        {
            public float TrainAccuracy { get; init; }
            public float ValAccuracy { get; init; }
            public float TrainLoss { get; init; }
            public float ValLoss { get; init; }
            /// <summary>Path to the TorchScript (.pt) model</summary>
            public string ModelPath { get; init; } = "";
            /// <summary>Path to the ONNX file (empty if skipped or failed)</summary>
            public string OnnxPath { get; init; } = "";
            public List<string> Labels { get; init; } = new();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════════════════

        private readonly string _basePath;
        private readonly CnnConfig _cfg;
        private readonly Rectangle _roiRect;
        private readonly Action<string>? _log;
        private readonly Device _device;

        public CnnTrainer(
            string basePath,
            CnnConfig config,
            Rectangle roiRect,
            Action<string>? log = null)
        {
            _basePath = basePath;
            _cfg = config;
            _roiRect = roiRect;
            _log = log;

            _device = config.UseGpu && cuda.is_available()
                ? new Device(DeviceType.CUDA)
                : new Device(DeviceType.CPU);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════════════════════

        public CnnResult Train(CancellationToken ct = default)
        {
            Log($"🧠 CNN Trainer  [{_cfg.Architecture}]  device={_device.type}");
            Log($"   Image : {_cfg.ImageWidth}×{_cfg.ImageHeight}" +
                $"  Epochs={_cfg.Epochs}  Batch={_cfg.BatchSize}  LR={_cfg.LearningRate}");
            Log($"   Augmentation: {(_cfg.Augment ? "ON" : "OFF")}  " +
                $"EarlyStop={_cfg.EarlyStopPatience}");

            // 1. Load dataset
            ct.ThrowIfCancellationRequested();
            var (allPaths, allLabels, classNames) = LoadDataset();
            Log($"✅ {allPaths.Count} images | {classNames.Count} classes: " +
                string.Join(", ", classNames));

            if (classNames.Count < 2)
                throw new InvalidOperationException("CNN training requires at least 2 classes.");

            // 2. Stratified split
            var (trainPaths, trainLabels, valPaths, valLabels) =
                StratifiedSplit(allPaths, allLabels, classNames.Count, _cfg.ValidationSplit);
            Log($"   Train={trainPaths.Count}  Val={valPaths.Count}");

            // 3. Build model
            ct.ThrowIfCancellationRequested();
            Log($"\n🔧 Building {_cfg.Architecture}...");
            using var model = BuildModel(classNames.Count);
            model.to(_device);

            long paramCount = model.parameters().Sum(p => p.numel());
            Log($"   Parameters: {paramCount:N0}");

            // 4. Optimizer + loss
            var optimizer = optim.Adam(model.parameters(), lr: _cfg.LearningRate);
            using var lossFn = nn.CrossEntropyLoss();

            // 5. Training loop
            ct.ThrowIfCancellationRequested();
            Log("\n🚀 Training...");
            var history = RunTrainingLoop(
                model, optimizer, lossFn,
                trainPaths, trainLabels,
                valPaths, valLabels,
                classNames.Count, ct);

            // 6. Trace → TorchScript (.pt)
            // 6. Save model
            ct.ThrowIfCancellationRequested();

            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            var modelPath = Path.Combine(
                _basePath,
                $"cnn_{_cfg.Architecture}_{stamp}.bin");

            model.eval();

            model.save(modelPath);

            Log($"\n💾 Model saved → {Path.GetFileName(modelPath)}");

            // 7. Sidecar files
            SaveSidecar(modelPath, classNames);

            string onnxPath = "";

            var result = new CnnResult
            {
                TrainAccuracy = history.FinalTrainAcc,
                ValAccuracy = history.FinalValAcc,
                TrainLoss = history.FinalTrainLoss,
                ValLoss = history.FinalValLoss,
                ModelPath = modelPath,
                OnnxPath = onnxPath,
                Labels = classNames
            };

            Log("\n═══════════════════════════════════════════════");
            Log("✅ CNN TRAINING COMPLETE");
            Log($"   Train Acc : {result.TrainAccuracy:P2}   Loss: {result.TrainLoss:F4}");
            Log($"   Val   Acc : {result.ValAccuracy:P2}   Loss: {result.ValLoss:F4}");
            if (!string.IsNullOrEmpty(onnxPath))
                Log($"   ONNX      : {Path.GetFileName(onnxPath)}");
            Log("═══════════════════════════════════════════════");

            return result;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  MODEL ARCHITECTURES
        // ══════════════════════════════════════════════════════════════════════

        private Module<Tensor, Tensor> BuildModel(int numClasses) =>
            _cfg.Architecture switch
            {
                "ResidualCNN" => new ResidualCNN(numClasses),
                _ => new MiniCNN(numClasses)
            };

       

       
        // ══════════════════════════════════════════════════════════════════════
        //  TRAINING LOOP
        // ══════════════════════════════════════════════════════════════════════

        private record TrainHistory(
            float FinalTrainAcc, float FinalValAcc,
            float FinalTrainLoss, float FinalValLoss);

        private TrainHistory RunTrainingLoop(
            Module<Tensor, Tensor> model,
            optim.Optimizer optimizer,
            CrossEntropyLoss lossFn,
            List<string> trainPaths,
            List<int> trainLabels,
            List<string> valPaths,
            List<int> valLabels,
            int numClasses,
            CancellationToken ct)
        {
            float bestValAcc = 0f;
            int patience = 0;
            float finalTrainAcc = 0f, finalValAcc = 0f;
            float finalTrainLoss = 0f, finalValLoss = 0f;
            var rng = new Random(42);

            for (int epoch = 1; epoch <= _cfg.Epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();

                // ── train ──────────────────────────────────────────────────
                model.train();
                float epochLoss = 0f, epochCorrect = 0f;
                int epochTotal = 0, batches = 0;

                var shuffled = Enumerable.Range(0, trainPaths.Count)
                    .OrderBy(_ => rng.Next()).ToList();

                for (int b = 0; b < shuffled.Count; b += _cfg.BatchSize)
                {
                    ct.ThrowIfCancellationRequested();

                    var batchIdx = shuffled.Skip(b).Take(_cfg.BatchSize).ToList();
                    var (bx, by) = MakeBatch(
                        batchIdx.Select(i => trainPaths[i]).ToList(),
                        batchIdx.Select(i => trainLabels[i]).ToList(),
                        augment: _cfg.Augment);

                    using (bx) using (by)
                    {
                        optimizer.zero_grad();

                        using var logits = model.forward(bx);
                        using var loss = lossFn.forward(logits, by);

                        loss.backward();
                        optimizer.step();

                        epochLoss += loss.item<float>();
                        epochCorrect += logits.argmax(1).eq(by).sum().item<long>();
                        epochTotal += batchIdx.Count;
                        batches++;
                    }
                }

                finalTrainLoss = epochLoss / batches;
                finalTrainAcc = epochCorrect / epochTotal;

                // ── validate ───────────────────────────────────────────────
                model.eval();
                float valLoss = 0f, valCorrect = 0f;
                int valTotal = 0, valBatches = 0;

                using (no_grad())
                {
                    for (int b = 0; b < valPaths.Count; b += _cfg.BatchSize)
                    {
                        ct.ThrowIfCancellationRequested();

                        int take = Math.Min(_cfg.BatchSize, valPaths.Count - b);
                        var (bx, by) = MakeBatch(
                            valPaths.GetRange(b, take),
                            valLabels.GetRange(b, take),
                            augment: false);

                        using (bx) using (by)
                        {
                            using var logits = model.forward(bx);
                            using var loss = lossFn.forward(logits, by);

                            valLoss += loss.item<float>();
                            valCorrect += logits.argmax(1).eq(by).sum().item<long>();
                            valTotal += take;
                            valBatches++;
                        }
                    }
                }

                finalValLoss = valLoss / valBatches;
                finalValAcc = valCorrect / valTotal;

                Log($"   Epoch {epoch,3}/{_cfg.Epochs} — " +
                    $"loss: {finalTrainLoss:F4}  acc: {finalTrainAcc:P1}  |  " +
                    $"val_loss: {finalValLoss:F4}  val_acc: {finalValAcc:P1}");

                // ── early stopping ─────────────────────────────────────────
                if (finalValAcc > bestValAcc + 1e-4f)
                {
                    bestValAcc = finalValAcc;
                    patience = 0;
                }
                else if (++patience >= _cfg.EarlyStopPatience)
                {
                    Log($"   ⏹ Early stop at epoch {epoch} " +
                        $"(no improvement for {_cfg.EarlyStopPatience} epochs)");
                    break;
                }
            }

            return new TrainHistory(finalTrainAcc, finalValAcc, finalTrainLoss, finalValLoss);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BATCH BUILDING  — channel-first NCHW layout for PyTorch/TorchSharp
        // ══════════════════════════════════════════════════════════════════════

        private (Tensor X, Tensor Y) MakeBatch(
            List<string> paths, List<int> labels, bool augment)
        {
            int n = paths.Count, h = _cfg.ImageHeight, w = _cfg.ImageWidth;
            var xData = new float[n * 3 * h * w];
            var yData = new long[n];
            var rng = augment ? new Random() : null;

            for (int i = 0; i < n; i++)
            {
                LoadImageIntoBuffer(paths[i], xData, i, h, w, rng);
                yData[i] = labels[i];
            }

            var X = tensor(xData, new long[] { n, 3, h, w }).to(_device);
            var Y = tensor(yData).to(_device);
            return (X, Y);
        }

        private void LoadImageIntoBuffer(
            string path, float[] dest, int idx,
            int h, int w, Random? rng)
        {
            using var src = new Bitmap(path);

            // ROI crop
            Bitmap working;
            if (_roiRect.Width > 0 && _roiRect.Height > 0)
            {
                int rx = Math.Max(0, Math.Min(_roiRect.X, src.Width - _roiRect.Width));
                int ry = Math.Max(0, Math.Min(_roiRect.Y, src.Height - _roiRect.Height));
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

            using var final = rng != null ? AugmentBitmap(resized, rng) : resized;

            var bmpData = final.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int stride = bmpData.Stride;
                var pixBytes = new byte[Math.Abs(stride) * h];
                Marshal.Copy(bmpData.Scan0, pixBytes, 0, pixBytes.Length);

                int baseOffset = idx * 3 * h * w;

                // PyTorch NCHW layout — R plane, G plane, B plane
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int off = row + x * 3;
                        int pixN = y * w + x;
                        dest[baseOffset + 0 * h * w + pixN] = pixBytes[off + 2] / 255f; // R
                        dest[baseOffset + 1 * h * w + pixN] = pixBytes[off + 1] / 255f; // G
                        dest[baseOffset + 2 * h * w + pixN] = pixBytes[off + 0] / 255f; // B
                    }
                }
            }
            finally
            {
                final.UnlockBits(bmpData);
            }
        }

        /// <summary>
        /// Horizontal flip (50%) + brightness jitter ±15%.
        /// Uses only System.Drawing — no extra packages.
        /// </summary>
        private static Bitmap AugmentBitmap(Bitmap src, Random rng)
        {
            var bmp = (Bitmap)src.Clone();

            if (rng.NextDouble() < 0.5)
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);

            float bright = (float)(1.0 + (rng.NextDouble() - 0.5) * 0.3);
            using (var g = Graphics.FromImage(bmp))
            {
                var cm = new System.Drawing.Imaging.ColorMatrix(new[]
                {
                    new[] { bright, 0f,     0f,     0f, 0f },
                    new[] { 0f,     bright, 0f,     0f, 0f },
                    new[] { 0f,     0f,     bright, 0f, 0f },
                    new[] { 0f,     0f,     0f,     1f, 0f },
                    new[] { 0f,     0f,     0f,     0f, 1f }
                });
                var ia = new System.Drawing.Imaging.ImageAttributes();
                ia.SetColorMatrix(cm);
                g.DrawImage(src,
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    0, 0, src.Width, src.Height,
                    GraphicsUnit.Pixel, ia);
            }

            return bmp;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ONNX EXPORT  — pure TorchSharp, no Python, no CLI
        // ══════════════════════════════════════════════════════════════════════

        //private string ExportOnnx(
        //    torch.jit.ScriptModule traced,
        //    string modelPath,
        //    List<string> classNames)
        //{
        //    Log("\n📦 Exporting to ONNX (TorchSharp native — no Python required)...");

        //    var onnxPath = Path.ChangeExtension(modelPath, ".onnx");

        //    try
        //    {
        //        // ONNX export must run on CPU
        //        traced.to(CPU);

        //        using var exampleInput = zeros(
        //            new long[] { 1, 3, _cfg.ImageHeight, _cfg.ImageWidth });

        //        traced.export_to_onnx(
        //            onnxPath,
        //            new[] { exampleInput },
        //            inputNames: new[] { "input_image" },
        //            outputNames: new[] { "output_scores" },
        //            opset_version: 13);

        //        // Move model back to original device
        //        traced.to(_device);

        //        long fileSizeKb = new FileInfo(onnxPath).Length / 1024;
        //        Log($"✅ ONNX exported → {Path.GetFileName(onnxPath)} ({fileSizeKb:N0} KB)");

        //        SaveOnnxMeta(onnxPath, classNames);
        //        return onnxPath;
        //    }
        //    catch (Exception ex)
        //    {
        //        Log($"⚠️ ONNX export failed: {ex.Message}");
        //        Log("   The TorchScript .pt model is still usable for inference via TorchSharp.");
        //        traced.to(_device); // ensure device is restored even on failure
        //        return "";
        //    }
        //}

        // ══════════════════════════════════════════════════════════════════════
        //  SIDECAR FILES
        // ══════════════════════════════════════════════════════════════════════

        private void SaveSidecar(string modelPath, List<string> classNames)
        {
            File.WriteAllLines(
                Path.ChangeExtension(modelPath, ".labels"),
                classNames);

            SaveOnnxMeta(modelPath, classNames);
        }

        private void SaveOnnxMeta(string basePath, List<string> classNames)
        {
            var meta = new
            {
                InputName = "input_image",
                InputShape = new[] { 1, 3, _cfg.ImageHeight, _cfg.ImageWidth },
                Layout = "NCHW",
                Normalization = "divide_255",
                ClassNames = classNames,
                Architecture = _cfg.Architecture,
                RoiX = _roiRect.X,
                RoiY = _roiRect.Y,
                RoiW = _roiRect.Width,
                RoiH = _roiRect.Height
            };

            File.WriteAllText(
                Path.ChangeExtension(basePath, ".json"),
                System.Text.Json.JsonSerializer.Serialize(meta,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DATASET LOADING
        // ══════════════════════════════════════════════════════════════════════

        private (List<string> Paths, List<int> Labels, List<string> Classes)
            LoadDataset()
        {
            var classDirs = Directory.GetDirectories(_basePath)
                .Select(d => new DirectoryInfo(d))
                .Where(d => !d.Name.StartsWith("."))
                .OrderBy(d => d.Name)
                .ToList();

            var classNames = classDirs.Select(d => d.Name).ToList();
            var paths = new List<string>();
            var labels = new List<int>();

            for (int i = 0; i < classDirs.Count; i++)
            {
                var files = classDirs[i]
                    .GetFiles("*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => IsImage(f.Extension))
                    .ToList();

                paths.AddRange(files.Select(f => f.FullName));
                labels.AddRange(Enumerable.Repeat(i, files.Count));

                Log($"   {classNames[i]}: {files.Count} images");
            }

            var rng = new Random(42);
            var order = Enumerable.Range(0, paths.Count).OrderBy(_ => rng.Next()).ToList();
            return (
                order.Select(i => paths[i]).ToList(),
                order.Select(i => labels[i]).ToList(),
                classNames
            );
        }

        private static bool IsImage(string ext) =>
            ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);

        // ══════════════════════════════════════════════════════════════════════
        //  STRATIFIED SPLIT
        // ══════════════════════════════════════════════════════════════════════

        private static (List<string> tp, List<int> tl, List<string> vp, List<int> vl)
            StratifiedSplit(
                List<string> paths, List<int> labels,
                int numClasses, float valFrac)
        {
            var tp = new List<string>(); var tl = new List<int>();
            var vp = new List<string>(); var vl = new List<int>();
            var rng = new Random(42);

            for (int c = 0; c < numClasses; c++)
            {
                var idx = labels
                    .Select((l, i) => (l, i))
                    .Where(x => x.l == c)
                    .Select(x => x.i)
                    .OrderBy(_ => rng.Next())
                    .ToList();

                int valCount = Math.Max(1, (int)(idx.Count * valFrac));
                foreach (var i in idx.Take(valCount)) { vp.Add(paths[i]); vl.Add(labels[i]); }
                foreach (var i in idx.Skip(valCount)) { tp.Add(paths[i]); tl.Add(labels[i]); }
            }

            return (tp, tl, vp, vl);
        }

        private void Log(string msg) => _log?.Invoke(msg);
    }
    // ── MiniCNN ───────────────────────────────────────────────────────────
    /// <summary>
    /// 3 conv blocks (32→64→128) + global avg pool + dense head.
    /// Fast; good baseline for most industrial inspection tasks.
    /// Input: [N, 3, H, W]   Output: [N, numClasses]
    /// </summary>
    internal sealed class MiniCNN : Module<Tensor, Tensor>
    {
        private readonly Sequential _features;
        private readonly Sequential _head;

        public MiniCNN(int numClasses) : base("MiniCNN")
        {
            _features = Sequential(
                // Block 1 — 3 → 32
                Conv2d(3, 32, kernel_size: 3, padding: 1),
                BatchNorm2d(32),
                ReLU(inplace: true),
                MaxPool2d(kernel_size: 2, stride: 2),
                Dropout2d(0.1),

                // Block 2 — 32 → 64
                Conv2d(32, 64, kernel_size: 3, padding: 1),
                BatchNorm2d(64),
                ReLU(inplace: true),
                MaxPool2d(kernel_size: 2, stride: 2),
                Dropout2d(0.2),

                // Block 3 — 64 → 128
                Conv2d(64, 128, kernel_size: 3, padding: 1),
                BatchNorm2d(128),
                ReLU(inplace: true),
                MaxPool2d(kernel_size: 2, stride: 2),
                Dropout2d(0.3)
            );

            _head = Sequential(
                Linear(128, 256),
                ReLU(inplace: true),
                Dropout(0.5),
                Linear(256, numClasses)
            );

            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            using var f = _features.forward(x);
            using var pooled = f.mean(new long[] { 2, 3 }); // global avg pool
            return _head.forward(pooled);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _features.Dispose(); _head.Dispose(); }
            base.Dispose(disposing);
        }
    }
    // ── ResidualCNN ───────────────────────────────────────────────────────
    /// <summary>
    /// Skip-connection CNN — better for larger datasets or subtle defects.
    /// Input: [N, 3, H, W]   Output: [N, numClasses]
    /// </summary>
    internal sealed class ResidualCNN : Module<Tensor, Tensor>
    {
        private readonly Sequential _stem;
        private readonly ResBlock _block1;
        private readonly ResBlock _block2;
        private readonly MaxPool2d _pool;
        private readonly Dropout _drop1, _drop2;
        private readonly Sequential _head;

        public ResidualCNN(int numClasses) : base("ResidualCNN")
        {
            _stem = Sequential(
                Conv2d(3, 32, kernel_size: 3, padding: 1),
                BatchNorm2d(32),
                ReLU(inplace: true),
                MaxPool2d(kernel_size: 2, stride: 2)
            );

            _block1 = new ResBlock(32, 64);
            _block2 = new ResBlock(64, 128);
            _pool = MaxPool2d(kernel_size: 2, stride: 2);
            _drop1 = Dropout(0.2);
            _drop2 = Dropout(0.3);

            _head = Sequential(
                Linear(128, 256),
                ReLU(inplace: true),
                Dropout(0.5),
                Linear(256, numClasses)
            );

            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            using var s = _stem.forward(x);
            using var b1 = _block1.forward(s);
            using var p1 = _pool.forward(b1);
            using var d1 = _drop1.forward(p1);
            using var b2 = _block2.forward(d1);
            using var p2 = _pool.forward(b2);
            using var d2 = _drop2.forward(p2);
            using var avg = d2.mean(new long[] { 2, 3 }); // global avg pool
            return _head.forward(avg);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stem.Dispose(); _block1.Dispose(); _block2.Dispose();
                _pool.Dispose(); _drop1.Dispose(); _drop2.Dispose();
                _head.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ── ResBlock ──────────────────────────────────────────────────────────
    internal sealed class ResBlock : Module<Tensor, Tensor>
    {
        private readonly Sequential _main;
        private readonly Conv2d _skipConv;
        private readonly BatchNorm2d _skipBn;

        public ResBlock(int inCh, int outCh) : base("ResBlock")
        {
            _main = Sequential(
                Conv2d(inCh, outCh, kernel_size: 3, padding: 1),
                BatchNorm2d(outCh),
                ReLU(inplace: true),
                Conv2d(outCh, outCh, kernel_size: 3, padding: 1),
                BatchNorm2d(outCh)
            );

            // 1×1 projection to match channel depth on skip path
            _skipConv = Conv2d(inCh, outCh, kernel_size: 1);
            _skipBn = BatchNorm2d(outCh);

            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            using var main = _main.forward(x);
            using var skip = _skipBn.forward(_skipConv.forward(x));
            return functional.relu(main + skip);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            { _main.Dispose(); _skipConv.Dispose(); _skipBn.Dispose(); }
            base.Dispose(disposing);
        }
    }

}