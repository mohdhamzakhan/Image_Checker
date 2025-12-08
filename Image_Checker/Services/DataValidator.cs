using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Image_Checker.Services
{
    public static class DataValidator
    {
        /// <summary>
        /// Creates a CSV file from a dataset with any number of class folders
        /// </summary>
        /// <param name="basePath">Root directory containing class subfolders</param>
        public static void CreateCsv(string basePath)
        {
            // Get all subdirectories as class labels
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

            var allImagePaths = new List<string>();
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

            foreach (var classDir in classDirectories)
            {
                var className = classDir.Name;

                // Get all image files in this class directory
                var imageFiles = Directory.GetFiles(classDir.FullName, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                if (imageFiles.Count == 0)
                {
                    Console.WriteLine($"⚠️  Warning: No images found in '{className}' folder");
                    continue;
                }

                Console.WriteLine($"   ✓ {className}: {imageFiles.Count} images");

                // Add each image with its label to the list
                // Format: "ImagePath","Label"
                foreach (var imagePath in imageFiles)
                {
                    // Escape the path and label properly for CSV
                    var escapedPath = EscapeCsvValue(imagePath);
                    var escapedLabel = EscapeCsvValue(className);
                    allImagePaths.Add($"{escapedPath},{escapedLabel}");
                }
            }

            if (allImagePaths.Count == 0)
            {
                throw new InvalidOperationException(
                    "No images found in any class folders.\n" +
                    "Supported formats: .jpg, .jpeg, .png, .bmp, .gif");
            }

            // Write CSV file
            var csvPath = Path.Combine(basePath, "images.csv");
            File.WriteAllLines(csvPath, allImagePaths);

            Console.WriteLine($"✅ CSV created at {csvPath}");
            Console.WriteLine($"   Total images: {allImagePaths.Count}");
        }

        /// <summary>
        /// Prints the distribution of labels in the dataset
        /// </summary>
        /// <param name="csvPath">Path to the CSV file</param>
        public static void PrintLabelDistribution(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                Console.WriteLine($"❌ CSV file not found: {csvPath}");
                return;
            }

            var lines = File.ReadAllLines(csvPath);

            if (lines.Length == 0)
            {
                Console.WriteLine("❌ CSV file is empty");
                return;
            }

            // Parse labels from CSV (format: "path","label")
            var labelCounts = lines
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
                var barLength = (int)(percentage / 2); // Scale to max 50 chars
                var bar = new string('█', Math.Max(1, barLength));

                Console.WriteLine($"  {labelInfo.Label.PadRight(maxLabelLength)}: {labelInfo.Count,5} images ({percentage,5:F1}%) {bar}");
            }

            Console.WriteLine("  " + new string('─', 50));
            Console.WriteLine($"  {"TOTAL".PadRight(maxLabelLength)}: {totalImages,5} images");
            Console.WriteLine();

            // Show balance warning if needed
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
        /// <param name="csvPath">Path to the CSV file</param>
        /// <returns>List of unique class labels</returns>
        public static List<string> GetClassLabels(string csvPath)
        {
            if (!File.Exists(csvPath))
                return new List<string>();

            var lines = File.ReadAllLines(csvPath);

            return lines
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
        /// <param name="basePath">Root directory to validate</param>
        /// <returns>Validation result with details</returns>
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
                {
                    result.Warnings.Add($"Class '{className}' has no images");
                }
                else if (imageCount < 10)
                {
                    result.Warnings.Add($"Class '{className}' has only {imageCount} images (recommend at least 10)");
                }
            }

            //result.TotalImages = result.ClassCounts.Values.Sum();

            if (result.TotalImages == 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "No images found in any class folders.";
            }

            return result;
        }

        /// <summary>
        /// Escapes a value for safe CSV storage
        /// </summary>
        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // If value contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            // Otherwise, just wrap in quotes for consistency
            return $"\"{value}\"";
        }

        /// <summary>
        /// Parses a CSV line into its component parts
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return Array.Empty<string>();

            // Simple CSV parser - handles quoted values
            var parts = new List<string>();
            var currentPart = "";
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        currentPart += '"';
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    parts.Add(currentPart);
                    currentPart = "";
                }
                else
                {
                    currentPart += c;
                }
            }

            parts.Add(currentPart);
            return parts.ToArray();
        }
    }

    /// <summary>
    /// Result of dataset validation
    /// </summary>
    public class DatasetValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, int> ClassCounts { get; set; } = new Dictionary<string, int>();
        public List<string> Warnings { get; set; } = new List<string>();
        public int TotalImages => ClassCounts.Values.Sum();
    }
}