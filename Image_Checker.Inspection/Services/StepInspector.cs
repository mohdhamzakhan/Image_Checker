using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Image_Checker.Inspection.Models;
using Image_Checker.Services;

namespace Image_Checker.Inspection.Services;

public enum StepVerdict { Ok, Ng, Uncertain, Error }

public sealed class StepResult
{
    public int StepNumber { get; set; }
    public string StepName { get; set; } = "";
    public StepVerdict Verdict { get; set; }
    public float NgScore { get; set; }
    public float Threshold { get; set; }
    public string Detail { get; set; } = "";
    public string FullImagePath { get; set; } = "";
    public string CropImagePath { get; set; } = "";
    /// <summary>The cropped area the model saw. Owned by whoever holds the result.</summary>
    public Bitmap? Crop { get; set; }
    public bool Overridden { get; set; }
    public double Ms { get; set; }

    public float Confidence => Verdict == StepVerdict.Ng ? NgScore : 1f - NgScore;

    public string Text => Overridden ? "OK (override)" : Verdict switch
    {
        StepVerdict.Ok => "OK",
        StepVerdict.Ng => "NG",
        StepVerdict.Uncertain => "CHECK",
        _ => "ERROR"
    };
}

/// <summary>Holds one trained model per step and judges a camera frame for a given step.</summary>
public sealed class StepInspector : IDisposable
{
    private sealed class Loaded
    {
        public Predictor Predictor = null!;
        public float NgThreshold = 0.5f;
        public readonly object Gate = new();
    }

    private readonly Dictionary<int, Loaded> _loaded = new();

    public bool HasStep(int number) => _loaded.ContainsKey(number);

    /// <summary>Loads every step's model. Returns problems (missing model, ROI changed since training...).</summary>
    public List<string> Load(InspectionRecipe recipe)
    {
        Clear();
        var issues = new List<string>();
        foreach (var step in recipe.Steps)
        {
            if (!LoadStep(step, out var error)) issues.Add(error);
            else if (_loaded.ContainsKey(step.Number))
            {
                var (_, roi) = ReadSidecar(step.ModelPath);
                if (roi.HasValue && roi.Value != step.Roi)
                    issues.Add($"{step.Title}: ROI was changed after training - retrain this step.");
            }
        }
        return issues;
    }

    public bool LoadStep(StepConfig step, out string error)
    {
        error = "";
        try
        {
            if (!step.HasModel)
            {
                error = $"{step.Title}: no trained model.";
                return false;
            }
            if (_loaded.TryGetValue(step.Number, out var old))
            {
                old.Predictor.Dispose();
                _loaded.Remove(step.Number);
            }
            var (thr, _) = ReadSidecar(step.ModelPath);
            _loaded[step.Number] = new Loaded { Predictor = new Predictor(step.ModelPath), NgThreshold = thr };
            return true;
        }
        catch (Exception ex)
        {
            error = $"{step.Title}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Crops the step's ROI, saves the photos, scores the crop and decides.
    /// NG when the NG score reaches the threshold saved at training time; "CHECK" when the model is unsure.
    /// </summary>
    public StepResult Check(StepConfig step, Bitmap frame, string saveDir, string stamp)
    {
        var sw = Stopwatch.StartNew();
        var result = new StepResult { StepNumber = step.Number, StepName = step.Name };

        try
        {
            if (!_loaded.TryGetValue(step.Number, out var m))
                throw new InvalidOperationException("No model loaded for this step.");

            Directory.CreateDirectory(saveDir);
            string baseName = $"S{step.Number}_{stamp}";
            result.FullImagePath = Path.Combine(saveDir, baseName + "_full.jpg");
            result.CropImagePath = Path.Combine(saveDir, baseName + "_crop.png");

            frame.Save(result.FullImagePath, ImageFormat.Jpeg);
            var crop = ExplainImaging.CropToRoi(frame, step.Roi);
            crop.Save(result.CropImagePath, ImageFormat.Png);
            result.Crop = crop;

            ScoreResult s;
            lock (m.Gate) s = m.Predictor.PredictScores(result.CropImagePath);

            int ngIdx = s.ClassNames == null
                ? -1
                : Array.FindIndex(s.ClassNames, n => n.Equals("NG", StringComparison.OrdinalIgnoreCase));
            float top = s.Scores.Length == 0 ? 0f : s.Scores.Max();
            bool labelNg = s.PredictedLabel.Equals("NG", StringComparison.OrdinalIgnoreCase);

            bool ng;
            float ngScore;
            if (ngIdx >= 0 && ngIdx < s.Scores.Length)
            {
                ngScore = s.Scores[ngIdx];
                ng = ngScore >= m.NgThreshold;
            }
            else
            {
                ngScore = labelNg ? top : 1f - top;
                ng = labelNg;
            }

            result.NgScore = ngScore;
            result.Threshold = m.NgThreshold;
            result.Verdict = ng ? StepVerdict.Ng : (s.IsOutOfDistribution ? StepVerdict.Uncertain : StepVerdict.Ok);
            result.Detail = $"NG score {ngScore:P1} (NG limit {m.NgThreshold:P0})";
            if (result.Verdict == StepVerdict.Uncertain)
                result.Detail += $". The model is not sure ({s.OodReason}). Check the product position and lighting, then retake.";
        }
        catch (Exception ex)
        {
            result.Verdict = StepVerdict.Error;
            result.Detail = ex.Message;
        }

        result.Ms = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <summary>Reads NgThreshold and the ROI that ModelTrainer wrote into "&lt;model&gt;.json".</summary>
    private static (float Threshold, Rectangle? Roi) ReadSidecar(string modelPath)
    {
        float thr = 0.5f;
        Rectangle? roi = null;
        try
        {
            var path = Path.ChangeExtension(modelPath, ".json");
            if (!File.Exists(path)) return (thr, roi);

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var e = doc.RootElement;
            if (e.TryGetProperty("NgThreshold", out var t) && t.TryGetSingle(out var f) && f > 0f && f < 1f)
                thr = f;
            if (e.TryGetProperty("RoiX", out var x) && e.TryGetProperty("RoiY", out var y) &&
                e.TryGetProperty("RoiW", out var w) && e.TryGetProperty("RoiH", out var h))
                roi = new Rectangle(x.GetInt32(), y.GetInt32(), w.GetInt32(), h.GetInt32());
        }
        catch { /* defaults */ }
        return (thr, roi);
    }

    private void Clear()
    {
        foreach (var l in _loaded.Values)
        {
            try { l.Predictor.Dispose(); } catch { }
        }
        _loaded.Clear();
    }

    public void Dispose() => Clear();
}