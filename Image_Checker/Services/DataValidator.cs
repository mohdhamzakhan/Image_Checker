using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Image_Checker.Services
{
    public static class DataValidator
    {
        /// <summary>
        /// Creates a CSV file from a dataset with any number of class folders
        /// </summary>
        public static void CreateCsv(string basePath)
        {
            var classDirectories = Directory.GetDirectories(basePath)
                .Select(d => new DirectoryInfo(d))
                .ToList();

            if (classDirectories.Count < 2)
            {
                throw new DirectoryNotFoundException(
                    $"Expected at least 2 class folders in dataset directory.\n" +
                    $"Found: {classDirectories.Count} folder(s)\n" +
                    $"Example structure: Dataset/Class1/, Dataset/Class2/, etc.");
            }

            Console.WriteLine($"📁 Scanning {classDirectories.Count} class folders...");

            var csvPath = Path.Combine(basePath, "images.csv");
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            int totalImages = 0;

            // FIX #3: Stream directly to file instead of building a List<string> in RAM
            using var writer = new StreamWriter(csvPath, append: false);

            foreach (var classDir in classDirectories)
            {
                var className = classDir.Name;

                var imageFiles = Directory.GetFiles(classDir.FullName, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                if (imageFiles.Count == 0)
                {
                    Console.WriteLine($"⚠️  Warning: No images found in '{className}' folder");
                    continue;
                }

                Console.WriteLine($"   ✓ {className}: {imageFiles.Count} images");

                foreach (var imagePath in imageFiles)
                {
                    writer.WriteLine($"{EscapeCsvValue(imagePath)},{EscapeCsvValue(className)}");
                    totalImages++;
                }
            }

            if (totalImages == 0)
            {
                throw new InvalidOperationException(
                    "No images found in any class folders.\n" +
                    "Supported formats: .jpg, .jpeg, .png, .bmp, .gif");
            }

            Console.WriteLine($"✅ CSV created at {csvPath}");
            Console.WriteLine($"   Total images: {totalImages}");
        }

        /// <summary>
        /// Prints the distribution of labels in the dataset
        /// </summary>
        public static void PrintLabelDistribution(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"❌ CSV file not found: {csvPath}");
                return;
            }

            // FIX #3: Stream lines instead of ReadAllLines
            var labelCounts = File.ReadLines(csvPath)
                .Select(line => ParseCsvLine(line))
                .Where(parts => parts.Length >= 2)
                .Select(parts => parts[1].Trim('"').Trim())
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .GroupBy(label => label)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            if (labelCounts.Count == 0)
            {
                Console.WriteLine("❌ No valid labels found in CSV");
                return;
            }

            Console.WriteLine("\n╔════════════════════════════════════════╗");
            Console.WriteLine("║      DATASET DISTRIBUTION              ║");
            Console.WriteLine("╚════════════════════════════════════════╝");

            int maxLabelLength = labelCounts.Max(l => l.Label.Length);
            int totalImages = labelCounts.Sum(l => l.Count);

            foreach (var labelInfo in labelCounts)
            {
                var percentage = (labelInfo.Count * 100.0) / totalImages;
                var barLength = (int)(percentage / 2);
                var bar = new string('█', Math.Max(1, barLength));
                Console.WriteLine($"  {labelInfo.Label.PadRight(maxLabelLength)}: {labelInfo.Count,5} images ({percentage,5:F1}%) {bar}");
            }

            Console.WriteLine("  " + new string('─', 50));
            Console.WriteLine($"  {"TOTAL".PadRight(maxLabelLength)}: {totalImages,5} images");
            Console.WriteLine();

            if (labelCounts.Count >= 2)
            {
                var maxCount = labelCounts.Max(l => l.Count);
                var minCount = labelCounts.Min(l => l.Count);
                var imbalanceRatio = (double)maxCount / minCount;

                if (imbalanceRatio > 3)
                {
                    Console.WriteLine("⚠️  WARNING: Dataset is imbalanced!");
                    Console.WriteLine($"   Ratio: {imbalanceRatio:F1}:1 (largest to smallest class)");
                    Console.WriteLine("   Consider balancing your dataset for better performance.");
                    Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// Gets the list of unique class labels from a CSV file
        /// </summary>
        public static List<string> GetClassLabels(string csvPath)
        {
            if (!File.Exists(csvPath))
                return new List<string>();

            // FIX #3: Stream lines instead of ReadAllLines
            return File.ReadLines(csvPath)
                .Select(line => ParseCsvLine(line))
                .Where(parts => parts.Length >= 2)
                .Select(parts => parts[1].Trim('"').Trim())
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct()
                .OrderBy(label => label)
                .ToList();
        }

        /// <summary>
        /// Validates that a dataset directory has the correct structure
        /// </summary>
        public static DatasetValidationResult ValidateDataset(string basePath)
        {
            var result = new DatasetValidationResult { IsValid = true };

            if (!Directory.Exists(basePath))
            {
                result.IsValid = false;
                result.ErrorMessage = "Dataset directory does not exist.";
                return result;
            }

            var classDirectories = Directory.GetDirectories(basePath);

            if (classDirectories.Length < 2)
            {
                result.IsValid = false;
                result.ErrorMessage = $"At least 2 class folders required. Found: {classDirectories.Length}";
                return result;
            }

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

            foreach (var classDir in classDirectories)
            {
                var className = new DirectoryInfo(classDir).Name;
                var imageCount = Directory.GetFiles(classDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Count(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()));

                result.ClassCounts[className] = imageCount;

                if (imageCount == 0)
                    result.Warnings.Add($"Class '{className}' has no images");
                else if (imageCount < 10)
                    result.Warnings.Add($"Class '{className}' has only {imageCount} images (recommend at least 10)");
            }

            if (result.TotalImages == 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "No images found in any class folders.";
            }

            return result;
        }

        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            return $"\"{value}\"";
        }

        /// <summary>
        /// Parses a CSV line into its component parts.
        /// FIX #6: Uses StringBuilder instead of string += to avoid O(n²) allocations in long paths.
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return Array.Empty<string>();

            var parts = new List<string>();
            // FIX #6: StringBuilder replaces currentPart += c
            var currentPart = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentPart.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    parts.Add(currentPart.ToString());
                    currentPart.Clear();
                }
                else
                {
                    currentPart.Append(c);
                }
            }

            parts.Add(currentPart.ToString());
            return parts.ToArray();
        }
    }

    public class DatasetValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, int> ClassCounts { get; set; } = new Dictionary<string, int>();
        public List<string> Warnings { get; set; } = new List<string>();
        public int TotalImages => ClassCounts.Values.Sum();
    }
}