using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Image_Checker.Utils;

namespace Image_Checker.Inspection.Models;

/// <summary>One fixed viewing side of the product (e.g. "Front"). Has its own ROI and its own model.</summary>
public sealed class StepConfig
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public int RoiX { get; set; }
    public int RoiY { get; set; }
    public int RoiW { get; set; }
    public int RoiH { get; set; }

    [JsonIgnore]
    public Rectangle Roi
    {
        get => new(RoiX, RoiY, RoiW, RoiH);
        set { RoiX = value.X; RoiY = value.Y; RoiW = value.Width; RoiH = value.Height; }
    }

    [JsonIgnore] public bool HasRoi => RoiW > 0 && RoiH > 0;
    [JsonIgnore] public bool HasModel => !string.IsNullOrWhiteSpace(ModelPath) && File.Exists(ModelPath);
    [JsonIgnore] public string Title => string.IsNullOrWhiteSpace(Name) ? $"Step {Number}" : $"Step {Number} - {Name}";
}

/// <summary>A product: camera settings plus the ordered list of inspection steps.</summary>
public sealed class InspectionRecipe
{
    public string Name { get; set; } = "";
    public int CameraIndex { get; set; }
    public int FrameWidth { get; set; } = 1280;
    public int FrameHeight { get; set; } = 720;
    public List<StepConfig> Steps { get; set; } = new();

    public void SetStepCount(int count)
    {
        count = Math.Max(1, count);
        while (Steps.Count < count)
            Steps.Add(new StepConfig { Number = Steps.Count + 1, Name = $"Side {Steps.Count + 1}" });
        while (Steps.Count > count)
            Steps.RemoveAt(Steps.Count - 1);
    }
}

/// <summary>
/// Folder layout:
///   Root\Product\recipe.json
///   Root\Product\Step1\Dataset\OK|NG   (cropped training images + trained model)
///   Root\Product\Results\inspection_log.csv and images\
/// </summary>
public static class RecipeStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private static string ConfigFile => Path.Combine(AppContext.BaseDirectory, "inspection_root.txt");

    public static string Root { get; private set; } = LoadRoot();

    private static string LoadRoot()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var p = File.ReadAllText(ConfigFile).Trim();
                if (p.Length > 0) return p;
            }
        }
        catch { /* use default */ }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ImageChecker_Inspection");
    }

    public static void SetRoot(string path)
    {
        Root = path;
        Directory.CreateDirectory(path);
        try { File.WriteAllText(ConfigFile, path); } catch { /* folder not writable: setting lasts for this run */ }
    }

    public static string ProductDir(string name) => Path.Combine(Root, PathUtils.SanitizeFileName(name));
    public static string StepDatasetDir(string product, int step) => Path.Combine(ProductDir(product), $"Step{step}", "Dataset");
    public static string ResultsDir(string product) => Path.Combine(ProductDir(product), "Results");
    private static string RecipeFile(string name) => Path.Combine(ProductDir(name), "recipe.json");

    public static List<string> ListProducts()
    {
        if (!Directory.Exists(Root)) return new List<string>();
        return Directory.GetDirectories(Root)
            .Where(d => File.Exists(Path.Combine(d, "recipe.json")))
            .Select(d => Path.GetFileName(d)!)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static InspectionRecipe? Load(string name)
    {
        try
        {
            var file = RecipeFile(name);
            if (!File.Exists(file)) return null;
            var r = JsonSerializer.Deserialize<InspectionRecipe>(File.ReadAllText(file), Json);
            if (r == null) return null;
            if (string.IsNullOrWhiteSpace(r.Name)) r.Name = name;
            if (r.Steps.Count == 0) r.SetStepCount(1);
            for (int i = 0; i < r.Steps.Count; i++) r.Steps[i].Number = i + 1;
            return r;
        }
        catch { return null; }
    }

    public static void Save(InspectionRecipe recipe)
    {
        Directory.CreateDirectory(ProductDir(recipe.Name));
        File.WriteAllText(RecipeFile(recipe.Name), JsonSerializer.Serialize(recipe, Json));
    }
}