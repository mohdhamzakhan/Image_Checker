using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace Image_Checker.Services
{
    /// <summary>One image that looks like another image of the same class.</summary>
    public sealed class DuplicateItem
    {
        public string ClassName = "";
        public string Keep = "";        // the image that stays
        public string Duplicate = "";   // the image that can be moved away
        public int Distance;            // 0 = same picture; larger = less similar (out of 256)
    }

    /// <summary>The same-looking picture sits in TWO different classes: one of the labels is wrong.</summary>
    public sealed class ConflictItem
    {
        public string FileA = "", FileB = "", ClassA = "", ClassB = "";
        public int Distance;
    }

    public sealed class DuplicateReport
    {
        public List<DuplicateItem> Duplicates = new();
        public List<ConflictItem> Conflicts = new();
        public int Scanned, Unreadable;
    }

    public static class DuplicateFinder
    {
        private static readonly string[] Ext = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        private sealed class Entry
        {
            public string FilePath = "";
            public string ClassName = "";
            public ulong[] Hash = new ulong[4];
        }

        /// <summary>
        /// Scans datasetRoot\ClassName\*.jpg|png... and finds images that look alike inside each class.
        /// maxDistance (0-256): 0 = identical pictures, about 10 = almost identical, about 24 = very similar.
        /// The first image (by file name) of a group is kept; the others are listed as duplicates.
        /// </summary>
        public static DuplicateReport Scan(string datasetRoot, int maxDistance,
                                           Action<int, int>? progress = null, CancellationToken ct = default)
        {
            var report = new DuplicateReport();
            var byClass = new Dictionary<string, List<Entry>>(StringComparer.OrdinalIgnoreCase);

            var files = new List<(string File, string Class)>();
            foreach (var dir in Directory.GetDirectories(datasetRoot).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string cls = new DirectoryInfo(dir).Name;
                if (cls.StartsWith(".")) continue;
                foreach (var f in Directory.GetFiles(dir)
                             .Where(f => Ext.Contains(Path.GetExtension(f).ToLowerInvariant()))
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    files.Add((f, cls));
            }

            // 1) fingerprint every image
            int done = 0;
            foreach (var (file, cls) in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var img = Image.FromStream(fs, false, false);
                    var entry = new Entry { FilePath = file, ClassName = cls, Hash = DHash(img) };
                    if (!byClass.TryGetValue(cls, out var list)) byClass[cls] = list = new List<Entry>();
                    list.Add(entry);
                    report.Scanned++;
                }
                catch
                {
                    report.Unreadable++;
                }
                progress?.Invoke(++done, files.Count);
            }

            // 2) duplicates inside each class (greedy: the first image keeps, later look-alikes are duplicates)
            foreach (var (cls, list) in byClass)
            {
                var used = new bool[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    if (used[i]) continue;
                    ct.ThrowIfCancellationRequested();
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        if (used[j]) continue;
                        int d = Distance(list[i].Hash, list[j].Hash);
                        if (d > maxDistance) continue;
                        used[j] = true;
                        report.Duplicates.Add(new DuplicateItem
                        {
                            ClassName = cls,
                            Keep = list[i].FilePath,
                            Duplicate = list[j].FilePath,
                            Distance = d
                        });
                    }
                }
            }

            // 3) the same picture in two different classes = conflicting labels (never removed automatically)
            int conflictLimit = Math.Min(maxDistance, 6);
            var classes = byClass.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            for (int a = 0; a < classes.Count && report.Conflicts.Count < 300; a++)
                for (int b = a + 1; b < classes.Count && report.Conflicts.Count < 300; b++)
                {
                    ct.ThrowIfCancellationRequested();
                    foreach (var ea in byClass[classes[a]])
                        foreach (var eb in byClass[classes[b]])
                        {
                            int d = Distance(ea.Hash, eb.Hash);
                            if (d > conflictLimit) continue;
                            report.Conflicts.Add(new ConflictItem
                            {
                                FileA = ea.FilePath,
                                ClassA = classes[a],
                                FileB = eb.FilePath,
                                ClassB = classes[b],
                                Distance = d
                            });
                            if (report.Conflicts.Count >= 300) break;
                        }
                }

            return report;
        }

        /// <summary>
        /// Backup folder, placed NEXT TO the dataset (never inside it): the trainer treats every sub-folder
        /// of the dataset as a class, so a backup folder inside would become a class.
        /// </summary>
        public static string DuplicatesFolder(string datasetRoot)
        {
            var di = new DirectoryInfo(datasetRoot.TrimEnd('\\', '/'));
            return Path.Combine(di.Parent!.FullName, di.Name + "_duplicates");
        }

        /// <summary>Moves the duplicates to the backup folder and removes them from images.csv. Returns how many moved.</summary>
        public static int MoveDuplicates(string datasetRoot, IEnumerable<DuplicateItem> items)
        {
            string target = DuplicatesFolder(datasetRoot);
            var moved = new List<string>();

            foreach (var item in items)
            {
                try
                {
                    if (!File.Exists(item.Duplicate)) continue;
                    string dir = Path.Combine(target, item.ClassName);
                    Directory.CreateDirectory(dir);

                    string name = Path.GetFileNameWithoutExtension(item.Duplicate);
                    string ext = Path.GetExtension(item.Duplicate);
                    string dest = Path.Combine(dir, name + ext);
                    for (int n = 1; File.Exists(dest); n++)
                        dest = Path.Combine(dir, $"{name}_{n}{ext}");

                    File.Move(item.Duplicate, dest);
                    moved.Add(item.Duplicate);
                }
                catch
                {
                    // locked or missing file: leave it where it is
                }
            }

            RemoveFromCsv(datasetRoot, moved);
            return moved.Count;
        }

        // ── helpers ────────────────────────────────────────────────────────────

        /// <summary>Keeps images.csv in step, otherwise a later Full Retrain would look for files that were moved.</summary>
        private static void RemoveFromCsv(string datasetRoot, List<string> movedPaths)
        {
            string csv = Path.Combine(datasetRoot, "images.csv");
            if (movedPaths.Count == 0 || !File.Exists(csv)) return;

            var gone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in movedPaths)
            {
                try { gone.Add(Path.GetFullPath(p)); } catch { }
            }

            var kept = new List<string>();
            foreach (var line in File.ReadLines(csv))
            {
                int i = line.LastIndexOf(',');
                if (i > 0)
                {
                    string path = line.Substring(0, i).Trim().Trim('"');
                    try { if (gone.Contains(Path.GetFullPath(path))) continue; } catch { }
                }
                kept.Add(line);
            }
            File.WriteAllLines(csv, kept);
        }

        /// <summary>256-bit "difference hash": 16x16 left/right brightness comparisons of a shrunken picture.</summary>
        private static ulong[] DHash(Image img)
        {
            using var small = new Bitmap(17, 16, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(img, new Rectangle(0, 0, 17, 16));
            }

            var bits = new ulong[4];
            int k = 0;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++, k++)
                    if (Gray(small.GetPixel(x, y)) > Gray(small.GetPixel(x + 1, y)))
                        bits[k >> 6] |= 1UL << (k & 63);
            return bits;
        }

        private static int Gray(Color c) => c.R * 299 + c.G * 587 + c.B * 114;

        private static int Distance(ulong[] a, ulong[] b)
        {
            int d = 0;
            for (int i = 0; i < a.Length; i++) d += BitOperations.PopCount(a[i] ^ b[i]);
            return d;
        }
    }
}