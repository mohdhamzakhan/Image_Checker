
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Image_Checker.Services;

namespace Image_Checker.WinForm
{
    /// <summary>
    /// Everything the daily report shows for one day.
    /// </summary>
    public sealed class DayStats
    {
        public DateTime Date;

        /// <summary>
        /// Result name (OK, NG, CHECK ...) to number of scans.
        /// </summary>
        public Dictionary<string, int> Results =
            new(StringComparer.OrdinalIgnoreCase);

        public int[] OkPerHour = new int[24];
        public int[] NgPerHour = new int[24];
        public int[] OtherPerHour = new int[24];

        /// <summary>
        /// Second pie: finished parts (inspection log)
        /// or scans per source (date folders).
        /// </summary>
        public Dictionary<string, int> Secondary =
            new(StringComparer.OrdinalIgnoreCase);

        public string SecondaryTitle = "";

        public int Overrides;
        public string Note = "";

        public int Total => Results.Values.Sum();

        public int Count(string name) =>
            Results.TryGetValue(name, out var n) ? n : 0;

        public int Other => Total - Count("OK") - Count("NG");

        public void Add(string result, int hour)
        {
            if (result.Equals("OK", StringComparison.OrdinalIgnoreCase))
                result = "OK";
            else if (result.Equals("NG", StringComparison.OrdinalIgnoreCase))
                result = "NG";

            Results[result] = Count(result) + 1;

            hour = Math.Clamp(hour, 0, 23);

            if (result == "OK")
                OkPerHour[hour]++;
            else if (result == "NG")
                NgPerHour[hour]++;
            else
                OtherPerHour[hour]++;
        }
    }

    public static class DailyStatsReader
    {
        private static readonly string[] ImageExt =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".bmp"
        };

        /// <summary>
        /// Counts images saved on one day in monitored folders:
        /// root\date-folder\OK, \NG ...
        ///
        /// The class is the name of the folder containing the image.
        /// The hour is based on the file's last-write time.
        /// </summary>
        public static DayStats FromFolders(
            IEnumerable<MonitorProfile> profiles,
            DateTime day)
        {
            var stats = new DayStats
            {
                Date = day.Date,
                SecondaryTitle = "Scans by source"
            };

            var missing = new List<string>();

            foreach (var p in profiles)
            {
                string root;

                try
                {
                    root = p.ResolveRoot(day);
                }
                catch
                {
                    continue;
                }

                if (!Directory.Exists(root))
                {
                    missing.Add(p.Name);
                    continue;
                }

                string fullRoot = Path.GetFullPath(root)
                    .TrimEnd('\\', '/');

                int count = 0;

                // Safe recursive enumeration:
                // inaccessible directories are skipped.
                foreach (var file in EnumerateImageFiles(root))
                {
                    if (!ImageExt.Contains(
                            Path.GetExtension(file),
                            StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        string dir = Path.GetDirectoryName(file)!;

                        bool inRoot = string.Equals(
                            Path.GetFullPath(dir).TrimEnd('\\', '/'),
                            fullRoot,
                            StringComparison.OrdinalIgnoreCase);

                        string cls = inRoot
                            ? "(no class folder)"
                            : new DirectoryInfo(dir).Name;

                        stats.Add(
                            cls,
                            File.GetLastWriteTime(file).Hour);

                        count++;
                    }
                    catch
                    {
                        // File may have been removed or become inaccessible.
                    }
                }

                if (count > 0)
                {
                    stats.Secondary[p.Name] = count;
                }
            }

            if (missing.Count > 0)
            {
                stats.Note =
                    "No folder for this day in: " +
                    string.Join(", ", missing) + ".";
            }

            return stats;
        }

        /// <summary>
        /// Safely enumerates files recursively.
        ///
        /// Handles:
        /// - Access denied directories
        /// - System Volume Information
        /// - Unavailable directories
        /// - Junctions and symbolic links
        /// </summary>
        private static IEnumerable<string> EnumerateImageFiles(
            string root)
        {
            var pending = new Stack<string>();

            pending.Push(root);

            while (pending.Count > 0)
            {
                string current = pending.Pop();

                string[] files;

                try
                {
                    files = Directory.GetFiles(current);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip folders without permission.
                    continue;
                }
                catch (IOException)
                {
                    // Skip unavailable folders.
                    continue;
                }

                foreach (string file in files)
                {
                    yield return file;
                }

                string[] directories;

                try
                {
                    directories = Directory.GetDirectories(current);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (string directory in directories)
                {
                    try
                    {
                        FileAttributes attributes =
                            File.GetAttributes(directory);

                        // Do not follow junctions or symbolic links.
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }

                        pending.Push(directory);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible directory.
                    }
                    catch (IOException)
                    {
                        // Skip unavailable directory.
                    }
                }
            }
        }

        /// <summary>
        /// Reads Results\inspection_log.csv written by the inspection app.
        /// </summary>
        public static DayStats FromInspectionLog(
            string csvPath,
            DateTime day)
        {
            var stats = new DayStats
            {
                Date = day.Date,
                SecondaryTitle = "Parts (final result)"
            };

            if (!File.Exists(csvPath))
            {
                stats.Note = "Log file not found.";
                return stats;
            }

            using var fs = new FileStream(
                csvPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            using var reader = new StreamReader(fs);

            reader.ReadLine(); // Header

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                var f = SplitCsv(line);

                // Timestamp,Product,Serial,Step,StepName,
                // Verdict,NgScore,Image,Note

                if (f.Count < 6)
                    continue;

                if (!DateTime.TryParse(
                        f[0],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var t))
                {
                    continue;
                }

                if (t.Date != day.Date)
                    continue;

                if (!int.TryParse(
                        f[3],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int step))
                {
                    continue;
                }

                string verdict = f[5].Trim();

                if (step == 0)
                {
                    // Final result of a part.
                    string name = verdict switch
                    {
                        "FINAL_PASS" =>
                            "Passed",

                        "FINAL_PASS_WITH_OVERRIDE" =>
                            "Passed (override)",

                        "FINAL_FAIL" =>
                            "Rejected",

                        _ => verdict
                    };

                    stats.Secondary[name] =
                        stats.Secondary.TryGetValue(name, out var n)
                            ? n + 1
                            : 1;

                    continue;
                }

                if (verdict == "OVERRIDE_OK")
                {
                    stats.Overrides++;
                    continue;
                }

                // OK, NG, CHECK, ERROR:
                // One line per inspection check.
                stats.Add(verdict, t.Hour);
            }

            return stats;
        }

        /// <summary>
        /// Splits a CSV line while handling quoted fields
        /// and escaped double quotes.
        /// </summary>
        private static List<string> SplitCsv(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();

            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (quoted &&
                        i + 1 < line.Length &&
                        line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (c == ',' && !quoted)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            result.Add(sb.ToString());

            return result;
        }
    }
}
