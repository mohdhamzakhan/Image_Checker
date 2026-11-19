using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Image_Checker.Services
{
    public class MonitorProfile
    {
        public string Name { get; set; } = "";
        /// <summary>Base folder. With date folders it contains the date sub-folders; otherwise it contains OK / NG directly.</summary>
        public string FolderPath { get; set; } = "";
        public string ModelPath { get; set; } = "";
        public bool Enabled { get; set; } = true;

        /// <summary>true = the base folder contains date folders (e.g. 26\11\18).</summary>
        public bool UseDateFolders { get; set; } = true;
        /// <summary>Folder pattern, segments separated by \ (e.g. yy\MM\dd, yyyy-MM-dd, yyyyMMdd).</summary>
        public string DateFormat { get; set; } = @"yy\MM\dd";

        [JsonIgnore]
        public string LayoutText => UseDateFolders ? DateFormat : "(scan folder directly)";

        /// <summary>The folder to watch on the given day.</summary>
        public string ResolveRoot(DateTime day) =>
            UseDateFolders ? Path.Combine(FolderPath, DateFolderFormat.Build(DateFormat, day)) : FolderPath;

        public MonitorProfile Clone() => (MonitorProfile)MemberwiseClone();

        public override string ToString() => string.IsNullOrWhiteSpace(Name) ? FolderPath : Name;
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
    }

    public static class DateFolderFormat
    {
        public static readonly string[] Presets =
        {
            @"yy\MM\dd", @"yyyy\MM\dd", "yyyy-MM-dd", "yyyyMMdd", "dd-MM-yyyy", @"yyyy\MM"
        };

        /// <summary>Formats each segment on its own, so "\" is a folder separator and not a .NET escape character.</summary>
        public static string Build(string format, DateTime date)
        {
            var parts = format.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(p => date.ToString(p.Trim(), CultureInfo.InvariantCulture))
                              .ToArray();
            return Path.Combine(parts);
        }

        public static bool TryValidate(string format, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(format)) { error = "Enter a date format."; return false; }

            try
            {
                var a = new DateTime(2026, 11, 18);
                var b = new DateTime(2027, 12, 19);
                string fa = Build(format, a), fb = Build(format, b);

                if (fa == fb) { error = "The format does not contain a date (use yy, MM, dd, yyyy)."; return false; }

                var invalid = Path.GetInvalidFileNameChars();
                foreach (var seg in fa.Split(Path.DirectorySeparatorChar))
                    if (seg.Length == 0 || seg.Any(invalid.Contains))
                    { error = "The format produces characters that are not allowed in a folder name. Use two-letter codes such as MM and dd."; return false; }

                return true;
            }
            catch (FormatException ex) { error = ex.Message; return false; }
        }
    }

    public static class MonitorProfileStore
    {
        private const string FileName = "monitor_profiles.json";
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static List<MonitorProfile> Load(string basePath)
        {
            try
            {
                var path = Path.Combine(basePath, FileName);
                if (!File.Exists(path)) return new();
                return JsonSerializer.Deserialize<List<MonitorProfile>>(File.ReadAllText(path)) ?? new();
            }
            catch { return new(); }
        }

        public static void Save(string basePath, IEnumerable<MonitorProfile> profiles)
        {
            Directory.CreateDirectory(basePath);
            File.WriteAllText(Path.Combine(basePath, FileName),
                JsonSerializer.Serialize(profiles.ToList(), Options));
        }
    }
}