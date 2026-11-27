using System.Globalization;
using System.Text;
using Image_Checker.Services;

namespace Image_Checker.WinForm
{
    public enum ShiftKind { All = 0, A = 1, B = 2, C = 3, CalendarDay = 4 }

    /// <summary>
    /// Shift A 06:00-14:30, B 14:30-23:00, C 23:00-06:00 (next day).
    /// A shift belongs to the date it STARTS on: shift C of 26 Nov runs 26 Nov 23:00 to 27 Nov 06:00.
    /// </summary>
    public static class Shifts
    {
        public static readonly TimeSpan AStart = new(6, 0, 0);
        public static readonly TimeSpan BStart = new(14, 30, 0);
        public static readonly TimeSpan CStart = new(23, 0, 0);

        public static string ShiftOf(TimeSpan timeOfDay) =>
            timeOfDay >= AStart && timeOfDay < BStart ? "A"
            : timeOfDay >= BStart && timeOfDay < CStart ? "B"
            : "C";

        /// <summary>The production date now: before 06:00 we are still in the previous day's shift C.</summary>
        public static DateTime ProductionDate(DateTime now) =>
            now.TimeOfDay < AStart ? now.Date.AddDays(-1) : now.Date;

        public static (DateTime Start, DateTime End, string Title) Window(DateTime date, ShiftKind kind)
        {
            date = date.Date;
            switch (kind)
            {
                case ShiftKind.A: return (date + AStart, date + BStart, "Shift A (06:00 - 14:30)");
                case ShiftKind.B: return (date + BStart, date + CStart, "Shift B (14:30 - 23:00)");
                case ShiftKind.C: return (date + CStart, date.AddDays(1) + AStart, "Shift C (23:00 - 06:00)");
                case ShiftKind.CalendarDay: return (date, date.AddDays(1), "Calendar day (00:00 - 24:00)");
                default: return (date + AStart, date.AddDays(1) + AStart, "All shifts (06:00 - 06:00)");
            }
        }
    }

    /// <summary>Everything the report shows for one time window.</summary>
    public sealed class DayStats
    {
        public DateTime Start, End;
        public string WindowTitle = "";

        public string[] BucketLabels = Array.Empty<string>();   // one bar per clock hour of the window
        public int[] OkPerBucket = Array.Empty<int>();
        public int[] NgPerBucket = Array.Empty<int>();
        public int[] OtherPerBucket = Array.Empty<int>();

        public Dictionary<string, int> Results = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> Secondary = new(StringComparer.OrdinalIgnoreCase);
        public string SecondaryTitle = "";
        public int Overrides;
        public string Note = "";

        private DateTime _firstBucket;

        public int Total => Results.Values.Sum();
        public int Count(string name) => Results.TryGetValue(name, out var n) ? n : 0;
        public int Other => Total - Count("OK") - Count("NG");

        public static DayStats ForWindow(DateTime start, DateTime end, string title)
        {
            var first = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
            int n = Math.Max(1, (int)Math.Ceiling((end - first).TotalHours));
            var s = new DayStats { Start = start, End = end, WindowTitle = title, _firstBucket = first };
            s.BucketLabels = Enumerable.Range(0, n).Select(i => first.AddHours(i).Hour.ToString("00")).ToArray();
            s.OkPerBucket = new int[n];
            s.NgPerBucket = new int[n];
            s.OtherPerBucket = new int[n];
            return s;
        }

        public void Add(string result, DateTime t, int count = 1)
        {
            if (count <= 0 || t < Start || t >= End) return;

            if (result.Equals("OK", StringComparison.OrdinalIgnoreCase)) result = "OK";
            else if (result.Equals("NG", StringComparison.OrdinalIgnoreCase)) result = "NG";

            Results[result] = Count(result) + count;
            int b = Math.Clamp((int)(t - _firstBucket).TotalHours, 0, OkPerBucket.Length - 1);
            if (result == "OK") OkPerBucket[b] += count;
            else if (result == "NG") NgPerBucket[b] += count;
            else OtherPerBucket[b] += count;
        }
    }

    /// <summary>Raw counts of ONE calendar day: per source and class, per minute of the day. Cached once the day is over.</summary>
    public sealed class DayScan
    {
        public DateTime Date;
        public int Files;
        public string Note = "";
        public Dictionary<(string Source, string Class), int[]> Minutes = new();

        public void Add(string source, string cls, DateTime t)
        {
            if (cls.Equals("OK", StringComparison.OrdinalIgnoreCase)) cls = "OK";
            else if (cls.Equals("NG", StringComparison.OrdinalIgnoreCase)) cls = "NG";

            var key = (source, cls);
            if (!Minutes.TryGetValue(key, out var arr)) Minutes[key] = arr = new int[1440];
            arr[t.Hour * 60 + t.Minute]++;
            Files++;
        }
    }

    public static class DailyStatsReader
    {
        private static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png", ".bmp" };
        private static readonly Dictionary<string, DayScan> Cache = new();

        /// <summary>
        /// Counts the images of one calendar day in the monitored folders. Fast path: the file time comes from the
        /// folder listing itself (FileInfo from EnumerateFiles), not from one disk request per file.
        /// A finished day never changes, so it is kept in memory; today is always read again.
        /// </summary>
        public static DayScan ScanDay(IEnumerable<MonitorProfile> profiles, DateTime day,
                                      Action<int>? progress, CancellationToken ct)
        {
            var list = profiles.ToList();
            string key = day.ToString("yyyyMMdd") + "|" + string.Join(",", list.Select(p => p.Id).OrderBy(x => x));
            bool finishedDay = day.Date < DateTime.Today;

            if (finishedDay)
                lock (Cache)
                    if (Cache.TryGetValue(key, out var hit)) return hit;

            var scan = new DayScan { Date = day.Date };
            var missing = new List<string>();

            foreach (var p in list)
            {
                ct.ThrowIfCancellationRequested();
                string root;
                try { root = p.ResolveRoot(day); } catch { continue; }
                if (!Directory.Exists(root)) { missing.Add(p.Name); continue; }
                ScanFolder(new DirectoryInfo(root), p.Name, true, scan, progress, ct);
            }

            if (missing.Count > 0) scan.Note = "No folder on " + day.ToString("dd MMM") + " for: " + string.Join(", ", missing) + ".";
            if (finishedDay) lock (Cache) Cache[key] = scan;
            return scan;
        }

        private static void ScanFolder(DirectoryInfo dir, string source, bool isRoot,
                                       DayScan scan, Action<int>? progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string cls = isRoot ? "(no class folder)" : dir.Name;

            try
            {
                foreach (var fi in dir.EnumerateFiles())
                {
                    if (!ImageExt.Contains(fi.Extension, StringComparer.OrdinalIgnoreCase)) continue;
                    scan.Add(source, cls, fi.LastWriteTime);
                    if (scan.Files % 500 == 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        progress?.Invoke(scan.Files);
                    }
                }
                foreach (var sub in dir.EnumerateDirectories())
                    ScanFolder(sub, source, false, scan, progress, ct);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        /// <summary>Cuts the window [start, end) out of the scanned days (a shift can span two calendar days).</summary>
        public static DayStats Build(IEnumerable<DayScan> scans, DateTime start, DateTime end, string title, bool splitByShift)
        {
            var stats = DayStats.ForWindow(start, end, title);
            stats.SecondaryTitle = splitByShift ? "Scans by shift" : "Scans by source";
            var notes = new List<string>();

            foreach (var scan in scans)
            {
                if (scan.Note.Length > 0) notes.Add(scan.Note);
                foreach (var kv in scan.Minutes)
                {
                    var (source, cls) = kv.Key;
                    var minutes = kv.Value;
                    for (int m = 0; m < 1440; m++)
                    {
                        int n = minutes[m];
                        if (n == 0) continue;
                        var t = scan.Date.AddMinutes(m);
                        if (t < start || t >= end) continue;

                        stats.Add(cls, t, n);
                        string key = splitByShift ? "Shift " + Shifts.ShiftOf(t.TimeOfDay) : source;
                        stats.Secondary[key] = stats.Secondary.TryGetValue(key, out var old) ? old + n : n;
                    }
                }
            }

            if (end > DateTime.Now) notes.Add("Still running: figures until " + DateTime.Now.ToString("HH:mm") + ".");
            stats.Note = string.Join("  ", notes.Distinct());
            return stats;
        }

        /// <summary>Reads Results\inspection_log.csv written by the inspection app.</summary>
        public static DayStats FromInspectionLog(string csvPath, DateTime start, DateTime end, string title)
        {
            var stats = DayStats.ForWindow(start, end, title);
            stats.SecondaryTitle = "Parts (final result)";
            if (!File.Exists(csvPath)) { stats.Note = "Log file not found."; return stats; }

            using var fs = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            reader.ReadLine();   // header

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var f = SplitCsv(line);
                // Timestamp,Product,Serial,Step,StepName,Verdict,NgScore,Image,Note
                if (f.Count < 6) continue;
                if (!DateTime.TryParse(f[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) continue;
                if (t < start || t >= end) continue;
                if (!int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int step)) continue;

                string verdict = f[5].Trim();
                if (step == 0)   // the final result of a part
                {
                    string name = verdict switch
                    {
                        "FINAL_PASS" => "Passed",
                        "FINAL_PASS_WITH_OVERRIDE" => "Passed (override)",
                        "FINAL_FAIL" => "Rejected",
                        _ => verdict
                    };
                    stats.Secondary[name] = stats.Secondary.TryGetValue(name, out var n) ? n + 1 : 1;
                    continue;
                }
                if (verdict == "OVERRIDE_OK") { stats.Overrides++; continue; }

                stats.Add(verdict, t);   // OK, NG, CHECK, ERROR: one line per check
            }

            if (end > DateTime.Now) stats.Note = "Still running: figures until " + DateTime.Now.ToString("HH:mm") + ".";
            return stats;
        }

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
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted) { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            result.Add(sb.ToString());
            return result;
        }
    }
}