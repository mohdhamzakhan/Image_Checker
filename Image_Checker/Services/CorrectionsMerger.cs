using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Image_Checker.Services
{
    /// <summary>
    /// Turns saved corrections into permanent training data for a FULL retrain.
    ///
    /// ModelTrainer reads ONLY images.csv, so a correction has no effect on a full
    /// retrain unless it is in that file. For every correction this class:
    ///   1. crops the photo to the ROI exactly like inference does
    ///      (Form1.PreprocessForPrediction), saves it as a PNG inside the dataset's
    ///      class folder, and appends "path,label" to images.csv; or
    ///   2. if the photo is already a dataset image listed in images.csv, just
    ///      fixes its label there.
    /// It is idempotent: running it twice never adds the same image twice, and an
    /// image corrected again to a different label is moved to the new class.
    /// images.csv is backed up first (the 5 newest backups are kept).
    /// </summary>
    public static class CorrectionsMerger
    {
        public class MergeResult
        {
            public int Added { get; set; }       // new cropped images added to the dataset
            public int Relabeled { get; set; }   // dataset images whose label was changed
            public int Unchanged { get; set; }   // already merged earlier
            public int Skipped { get; set; }     // photo missing / unreadable
            public string? BackupPath { get; set; }
            public List<string> Warnings { get; } = new();
        }

        private class Row
        {
            public string Text = "";       // the line as it will be written
            public string RawPath = "";    // path as written in the file
            public string ImagePath = "";  // normalised full path ("" = not an image row)
            public string Label = "";
            public bool Removed;
        }

        // ─────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────

        public static MergeResult Merge(
            string basePath,
            IEnumerable<CorrectionRecord> corrections,
            Rectangle roi,
            Action<string>? log = null)
        {
            var result = new MergeResult();
            string csvPath = Path.Combine(basePath, "images.csv");

            if (!File.Exists(csvPath))
                throw new FileNotFoundException(
                    $"images.csv was not found in {basePath}. " +
                    "A full retrain needs it (build the model once to create it).", csvPath);

            // 1) newest correction per source photo wins (file order is chronological)
            var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in corrections)
            {
                if (string.IsNullOrWhiteSpace(c.ImagePath) || string.IsNullOrWhiteSpace(c.CorrectedLabel))
                    continue;
                wanted[Normalize(basePath, c.ImagePath.Trim().Trim('"'))] = c.CorrectedLabel.Trim().Trim('"');
            }
            if (wanted.Count == 0) return result;

            // 2) load images.csv, keeping every line exactly as it was
            var rows = new List<Row>();
            var byPath = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(csvPath))
            {
                var row = new Row { Text = line };
                var parsed = ParseLine(line);
                if (parsed != null)
                {
                    row.RawPath = parsed.Value.path;
                    row.ImagePath = Normalize(basePath, parsed.Value.path);
                    row.Label = parsed.Value.label;
                    byPath[row.ImagePath] = row;
                }
                rows.Add(row);
            }

            // 3) find each class's folder from where its existing images live
            var classes = new Dictionary<string, (string Label, string Folder)>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (row.ImagePath.Length == 0 || classes.ContainsKey(row.Label)) continue;
                var dir = Path.GetDirectoryName(row.ImagePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    classes[row.Label] = (row.Label, dir);
            }
            if (classes.Count == 0)
                throw new InvalidOperationException(
                    "Could not locate the dataset class folders from images.csv " +
                    "(none of the listed image folders exist).");

            string datasetRoot = Path.GetDirectoryName(classes.Values.First().Folder) ?? basePath;
            bool changed = false;

            // 4) apply every correction
            foreach (var (src, rawLabel) in wanted)
            {
                if (!classes.TryGetValue(rawLabel, out var cls))
                {
                    var folder = Path.Combine(datasetRoot, rawLabel);
                    Directory.CreateDirectory(folder);
                    cls = (rawLabel, folder);
                    classes[rawLabel] = cls;
                    result.Warnings.Add($"New class '{rawLabel}' was created.");
                }

                // 4a) already a dataset image -> only the label may change
                if (byPath.TryGetValue(src, out var existing))
                {
                    if (existing.Label.Equals(cls.Label, StringComparison.Ordinal))
                    {
                        result.Unchanged++;
                    }
                    else
                    {
                        existing.Label = cls.Label;
                        existing.Text = $"{Escape(existing.RawPath)},{Escape(cls.Label)}";
                        result.Relabeled++;
                        changed = true;
                        log?.Invoke($"   ↻ relabeled in images.csv: {Path.GetFileName(src)} → {cls.Label}");
                    }
                    continue;
                }

                // 4b) external photo -> crop into the class folder
                if (!File.Exists(src))
                {
                    result.Skipped++;
                    result.Warnings.Add($"Photo not found, skipped: {Path.GetFileName(src)}");
                    continue;
                }

                string fileName = $"corr_{ShortHash(src)}_{SafeName(Path.GetFileNameWithoutExtension(src))}.png";
                string destPath = Path.Combine(cls.Folder, fileName);

                // if the same photo was merged earlier under ANOTHER label, move it
                foreach (var other in classes.Values)
                {
                    if (string.Equals(other.Folder, cls.Folder, StringComparison.OrdinalIgnoreCase)) continue;
                    string oldPath = Path.Combine(other.Folder, fileName);
                    if (File.Exists(oldPath))
                    {
                        try { File.Delete(oldPath); } catch { /* row removal below still fixes the CSV */ }
                    }
                    string oldKey = Normalize(basePath, oldPath);
                    if (byPath.TryGetValue(oldKey, out var oldRow))
                    {
                        oldRow.Removed = true;
                        byPath.Remove(oldKey);
                        changed = true;
                        log?.Invoke($"   ↻ moved {fileName} from class {other.Label} to {cls.Label}");
                    }
                }

                try
                {
                    if (!File.Exists(destPath))
                        CropAndSave(src, destPath, roi);
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    result.Warnings.Add($"Could not crop {Path.GetFileName(src)}: {ex.Message}");
                    continue;
                }

                string key = Normalize(basePath, destPath);
                if (byPath.TryGetValue(key, out var have))
                {
                    if (have.Label.Equals(cls.Label, StringComparison.Ordinal))
                    {
                        result.Unchanged++;      // merged by an earlier run
                        continue;
                    }
                    have.Label = cls.Label;
                    have.Text = $"{Escape(have.RawPath)},{Escape(cls.Label)}";
                    result.Relabeled++;
                    changed = true;
                    continue;
                }

                var newRow = new Row
                {
                    RawPath = destPath,
                    ImagePath = key,
                    Label = cls.Label,
                    Text = $"{Escape(destPath)},{Escape(cls.Label)}"
                };
                rows.Add(newRow);
                byPath[key] = newRow;
                result.Added++;
                changed = true;
                log?.Invoke($"   ✂️ added cropped correction: {fileName} → {cls.Label}");
            }

            // 5) write images.csv safely (backup, temp file, replace)
            if (changed)
            {
                string backup = csvPath + ".bak_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(csvPath, backup, true);
                result.BackupPath = backup;

                string tmp = csvPath + ".tmp";
                File.WriteAllLines(tmp, rows.Where(r => !r.Removed).Select(r => r.Text), new UTF8Encoding(false));
                File.Move(tmp, csvPath, overwrite: true);

                foreach (var old in Directory.GetFiles(basePath, "images.csv.bak_*")
                                             .OrderByDescending(f => f).Skip(5))
                {
                    try { File.Delete(old); } catch { }
                }
            }

            return result;
        }

        /// <summary>
        /// Quick sanity check: are the training images already ROI-sized crops?
        /// true  = yes (consistent with inference)
        /// false = no  (the model is trained on full photos but predicts on crops)
        /// null  = cannot tell (no ROI, no images.csv, or no readable sample)
        /// </summary>
        public static bool? DatasetLooksCropped(string basePath, Rectangle roi)
        {
            if (roi == Rectangle.Empty || roi.Width <= 0 || roi.Height <= 0) return null;

            string csvPath = Path.Combine(basePath, "images.csv");
            if (!File.Exists(csvPath)) return null;

            var paths = new List<string>();
            foreach (var line in File.ReadLines(csvPath))
            {
                var p = ParseLine(line);
                if (p != null) paths.Add(Normalize(basePath, p.Value.path));
            }
            if (paths.Count == 0) return null;

            int step = Math.Max(1, paths.Count / 20);
            int sampled = 0, matches = 0;
            for (int i = 0; i < paths.Count && sampled < 20; i += step)
            {
                if (!File.Exists(paths[i])) continue;
                try
                {
                    using var img = Image.FromFile(paths[i]);
                    sampled++;
                    if (img.Width == roi.Width && img.Height == roi.Height) matches++;
                }
                catch { /* unreadable sample - ignore */ }
            }

            if (sampled == 0) return null;
            return matches * 2 >= sampled;
        }

        // ─────────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────────

        // Same clamping math as Form1.PreprocessForPrediction and the builder's Apply-ROI
        private static void CropAndSave(string sourcePath, string destPath, Rectangle roi)
        {
            using var bmp = new Bitmap(sourcePath);

            if (roi == Rectangle.Empty || roi.Width <= 0 || roi.Height <= 0)
            {
                bmp.Save(destPath, ImageFormat.Png);   // inference does not crop either
                return;
            }

            int x = Math.Max(0, Math.Min(roi.X, bmp.Width - roi.Width));
            int y = Math.Max(0, Math.Min(roi.Y, bmp.Height - roi.Height));
            var safe = new Rectangle(x, y,
                Math.Min(roi.Width, bmp.Width),
                Math.Min(roi.Height, bmp.Height));

            using var cropped = RoiMask.Replace(bmp.Clone(safe, bmp.PixelFormat));
            cropped.Save(destPath, ImageFormat.Png);
        }

        private static (string path, string label)? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            int i = line.LastIndexOf(',');
            if (i <= 0 || i >= line.Length - 1) return null;
            return (Unquote(line.Substring(0, i).Trim()), Unquote(line.Substring(i + 1).Trim()));
        }

        private static string Unquote(string s)
        {
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2).Replace("\"\"", "\"");
            return s;
        }

        private static string Escape(string v)
        {
            if (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        private static string Normalize(string baseDir, string p)
        {
            try
            {
                string combined = Path.IsPathRooted(p) ? p : Path.Combine(baseDir, p);
                return Path.GetFullPath(combined);
            }
            catch
            {
                return p;
            }
        }

        private static string ShortHash(string path)
        {
            var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
            return Convert.ToHexString(bytes).Substring(0, 8).ToLowerInvariant();
        }

        private static string SafeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(ch => invalid.Contains(ch) || ch == ',' ? '_' : ch).ToArray());
            return cleaned.Length > 60 ? cleaned.Substring(0, 60) : cleaned;
        }
    }
}