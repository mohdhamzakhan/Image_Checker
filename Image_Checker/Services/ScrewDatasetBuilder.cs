using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Image_Checker.Services
{
    public static class ScrewDatasetBuilder
    {
        private static readonly string[] Ext = { ".jpg", ".jpeg", ".png", ".bmp" };

        public class Config
        {
            public int ImageWidth { get; set; } = 128;
            public int ImageHeight { get; set; } = 128;
            public List<ScrewPosition> Positions { get; set; } = new();
        }

        public static Config LoadConfig(string rawRoot) =>
            JsonSerializer.Deserialize<Config>(
                File.ReadAllText(Path.Combine(rawRoot, "positions.json")),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("positions.json is empty.");
        [SupportedOSPlatform("Windows10.0.19041")]
        /// <summary>Cuts every photo into one labelled crop per position. Returns crops per class.</summary>
        public static Dictionary<string, int> Build(string rawRoot, string datasetRoot, Config cfg, Action<string>? log = null)
        {
            if (cfg.Positions.Count == 0) throw new InvalidOperationException("No positions defined.");
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in Directory.GetDirectories(rawRoot))
            {
                // "Missing_S1_S3" -> label Missing, bad = {S1, S3};  "OK" -> everything OK
                var parts = new DirectoryInfo(dir).Name.Split('_', StringSplitOptions.RemoveEmptyEntries);
                string label = parts[0];
                var bad = new HashSet<string>(parts.Skip(1), StringComparer.OrdinalIgnoreCase);
                bool isOk = label.Equals("OK", StringComparison.OrdinalIgnoreCase);

                if (!isOk && bad.Count == 0)
                {
                    log?.Invoke($"⚠️ Skipped '{dir}': name a position, e.g. {label}_S1");
                    continue;
                }
                foreach (var b in bad)
                    if (!cfg.Positions.Any(p => p.Name.Equals(b, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException($"Folder '{dir}' names unknown position '{b}'.");

                foreach (var file in Directory.GetFiles(dir)
                             .Where(f => Ext.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
                {
                    foreach (var pos in cfg.Positions)
                    {
                        string lab = !isOk && bad.Contains(pos.Name) ? label : "OK";
                        // the folder name keeps crops of different photos with the same file name apart
                        string unique = $"{new DirectoryInfo(dir).Name}_{Path.GetFileNameWithoutExtension(file)}";
                        //string tmpName = Path.Combine(Path.GetTempPath(), unique + Path.GetExtension(file));
                        PositionCropper.SaveCrop(file, pos, lab, datasetRoot, unique);
                        counts[lab] = counts.GetValueOrDefault(lab) + 1;
                    }
                }
            }
            return counts;
        }

        /// <summary>Adds Positions to the model's .json and clears the whole-image ROI.</summary>
        public static void PatchModelJson(string modelZipPath, Config cfg)
        {
            string json = Path.ChangeExtension(modelZipPath, ".json");
            var node = File.Exists(json) ? JsonNode.Parse(File.ReadAllText(json))!.AsObject() : new JsonObject();
            node["RoiX"] = 0; node["RoiY"] = 0; node["RoiW"] = 0; node["RoiH"] = 0;
            node["ImageWidth"] = cfg.ImageWidth;
            node["ImageHeight"] = cfg.ImageHeight;
            node["Positions"] = JsonSerializer.SerializeToNode(cfg.Positions);
            File.WriteAllText(json, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}