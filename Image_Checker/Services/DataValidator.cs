using System;
using System.IO;
using System.Linq;

namespace Image_Checker.Services
{
    public static class DataValidator
    {
        public static void CreateCsv(string basePath)
        {
            var okDir = Path.Combine(basePath, "OK");
            var ngDir = Path.Combine(basePath, "NG");

            if (!Directory.Exists(okDir) || !Directory.Exists(ngDir))
                throw new DirectoryNotFoundException("Expected 'OK' and 'NG' folders under dataset directory.");

            var okFiles = Directory.GetFiles(okDir).Select(p => $"\"{p}\",\"OK\"");
            var ngFiles = Directory.GetFiles(ngDir).Select(p => $"\"{p}\",\"NG\"");
            var all = okFiles.Concat(ngFiles);

            var csvPath = Path.Combine(basePath, "images.csv");
            File.WriteAllLines(csvPath, all);
            Console.WriteLine($"✅ CSV created at {csvPath}");
        }

        public static void PrintLabelDistribution(string csvPath)
        {
            var lines = File.ReadAllLines(csvPath);
            var labelCounts = lines
                .Select(line => line.Split(',')[1].Trim('"'))
                .GroupBy(label => label)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .ToList();

            Console.WriteLine("\n=== Dataset Distribution ===");
            foreach (var l in labelCounts)
                Console.WriteLine($"{l.Label}: {l.Count} images");

            Console.WriteLine($"Total: {lines.Length} images\n");
        }
    }
}
