using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Image_Checker.Services
{
    public class CorrectionManager
    {
        private readonly string _correctionsPath;
        private readonly object _fileLock = new object();
        private static readonly int MaxRetries = 5;
        private static readonly int RetryDelayMs = 100;

        public CorrectionManager(string basePath)
        {
            _correctionsPath = Path.Combine(basePath, "corrections.csv");
            EnsureCorrectionsFileExists();
        }

        private void EnsureCorrectionsFileExists()
        {
            if (!File.Exists(_correctionsPath))
            {
                lock (_fileLock)
                {
                    if (!File.Exists(_correctionsPath))
                    {
                        var header = "Timestamp,ImagePath,OriginalPrediction,OriginalConfidence,CorrectedLabel";
                        File.WriteAllText(_correctionsPath, header + Environment.NewLine);
                    }
                }
            }
        }

        /// <summary>
        /// Saves a correction with retry logic and proper file locking
        /// </summary>
        public bool SaveCorrection(
            string imagePath,
            string originalPrediction,
            float originalConfidence,
            string correctedLabel,
            out string errorMessage)
        {
            errorMessage = null;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var line = $"{timestamp},\"{imagePath}\",\"{originalPrediction}\",{originalConfidence:F4},\"{correctedLabel}\"";

            // Try multiple times with delays
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        // Use FileShare.Read to allow other processes to read while we write
                        using (var fileStream = new FileStream(
                            _correctionsPath,
                            FileMode.Append,
                            FileAccess.Write,
                            FileShare.Read))
                        using (var writer = new StreamWriter(fileStream))
                        {
                            writer.WriteLine(line);
                            writer.Flush();
                        }
                    }

                    Console.WriteLine($"✅ Correction saved: {Path.GetFileName(imagePath)} → {correctedLabel}");
                    return true;
                }
                catch (IOException ex) when (IsFileLocked(ex))
                {
                    if (attempt < MaxRetries - 1)
                    {
                        Console.WriteLine($"⚠️ File locked, retry {attempt + 1}/{MaxRetries}...");
                        Thread.Sleep(RetryDelayMs * (attempt + 1)); // Exponential backoff
                    }
                    else
                    {
                        errorMessage = $"File is locked after {MaxRetries} attempts. Please close any programs using corrections.csv and try again.";
                        Console.WriteLine($"❌ {errorMessage}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"Failed to save correction: {ex.Message}";
                    Console.WriteLine($"❌ {errorMessage}");
                    return false;
                }
            }

            errorMessage = "Failed to save correction after multiple retries.";
            return false;
        }

        /// <summary>
        /// Saves multiple corrections in batch
        /// </summary>
        public bool SaveCorrectionsBatch(
            List<(string ImagePath, string OriginalPrediction, float OriginalConfidence, string CorrectedLabel)> corrections,
            out string errorMessage)
        {
            errorMessage = null;

            var lines = new List<string>();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var correction in corrections)
            {
                var line = $"{timestamp},\"{correction.ImagePath}\",\"{correction.OriginalPrediction}\",{correction.OriginalConfidence:F4},\"{correction.CorrectedLabel}\"";
                lines.Add(line);
            }

            // Try multiple times with delays
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        using (var fileStream = new FileStream(
                            _correctionsPath,
                            FileMode.Append,
                            FileAccess.Write,
                            FileShare.Read))
                        using (var writer = new StreamWriter(fileStream))
                        {
                            foreach (var line in lines)
                            {
                                writer.WriteLine(line);
                            }
                            writer.Flush();
                        }
                    }

                    Console.WriteLine($"✅ Saved {corrections.Count} corrections in batch");
                    return true;
                }
                catch (IOException ex) when (IsFileLocked(ex))
                {
                    if (attempt < MaxRetries - 1)
                    {
                        Console.WriteLine($"⚠️ File locked, retry {attempt + 1}/{MaxRetries}...");
                        Thread.Sleep(RetryDelayMs * (attempt + 1));
                    }
                    else
                    {
                        errorMessage = $"File is locked after {MaxRetries} attempts. Please close any programs using corrections.csv and try again.";
                        Console.WriteLine($"❌ {errorMessage}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"Failed to save corrections: {ex.Message}";
                    Console.WriteLine($"❌ {errorMessage}");
                    return false;
                }
            }

            errorMessage = "Failed to save corrections after multiple retries.";
            return false;
        }

        /// <summary>
        /// Gets all corrections from the file
        /// </summary>
        public List<CorrectionRecord> GetAllCorrections(out string errorMessage)
        {
            errorMessage = null;
            var corrections = new List<CorrectionRecord>();

            if (!File.Exists(_correctionsPath))
            {
                return corrections;
            }

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                try
                {
                    lock (_fileLock)
                    {
                        using (var fileStream = new FileStream(
                            _correctionsPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite)) // Allow other processes to read/write
                        using (var reader = new StreamReader(fileStream))
                        {
                            string line;
                            bool isFirstLine = true;

                            while ((line = reader.ReadLine()) != null)
                            {
                                if (isFirstLine)
                                {
                                    isFirstLine = false;
                                    continue; // Skip header
                                }

                                var parts = SplitCsvLine(line);
                                if (parts.Count >= 5)
                                {
                                    corrections.Add(new CorrectionRecord
                                    {
                                        Timestamp = parts[0],
                                        ImagePath = parts[1],
                                        OriginalPrediction = parts[2],
                                        OriginalConfidence = float.TryParse(parts[3], out var conf) ? conf : 0f,
                                        CorrectedLabel = parts[4]
                                    });
                                }
                            }
                        }
                    }

                    return corrections;
                }
                catch (IOException ex) when (IsFileLocked(ex))
                {
                    if (attempt < MaxRetries - 1)
                    {
                        Thread.Sleep(RetryDelayMs * (attempt + 1));
                    }
                    else
                    {
                        errorMessage = "Could not read corrections file (file is locked).";
                        return corrections;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"Error reading corrections: {ex.Message}";
                    return corrections;
                }
            }

            return corrections;
        }

        /// <summary>
        /// Gets correction count without reading entire file
        /// </summary>
        public int GetCorrectionCount()
        {
            try
            {
                if (!File.Exists(_correctionsPath))
                    return 0;

                lock (_fileLock)
                {
                    using (var fileStream = new FileStream(
                        _correctionsPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite))
                    using (var reader = new StreamReader(fileStream))
                    {
                        int count = 0;
                        bool isFirstLine = true;

                        while (reader.ReadLine() != null)
                        {
                            if (isFirstLine)
                            {
                                isFirstLine = false;
                                continue;
                            }
                            count++;
                        }

                        return count;
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Clears all corrections (with backup)
        /// </summary>
        public bool ClearCorrections(bool createBackup, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                lock (_fileLock)
                {
                    if (createBackup && File.Exists(_correctionsPath))
                    {
                        var backupPath = _correctionsPath.Replace(".csv", $"_backup_{DateTime.Now:yyyyMMddHHmmss}.csv");
                        File.Copy(_correctionsPath, backupPath, true);
                        Console.WriteLine($"📋 Backup created: {Path.GetFileName(backupPath)}");
                    }

                    var header = "Timestamp,ImagePath,OriginalPrediction,OriginalConfidence,CorrectedLabel";
                    File.WriteAllText(_correctionsPath, header + Environment.NewLine);
                    Console.WriteLine("✅ Corrections cleared");
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to clear corrections: {ex.Message}";
                Console.WriteLine($"❌ {errorMessage}");
                return false;
            }
        }

        /// <summary>
        /// Checks if an IOException is due to file locking
        /// </summary>
        private bool IsFileLocked(IOException exception)
        {
            int errorCode = System.Runtime.InteropServices.Marshal.GetHRForException(exception) & 0xFFFF;
            return errorCode == 32 || errorCode == 33; // ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION
        }

        /// <summary>
        /// Properly splits CSV line handling quoted fields
        /// </summary>
        private List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var currentField = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            result.Add(currentField.ToString());
            return result;
        }
    }

    /// <summary>
    /// Represents a single correction record
    /// </summary>
    public class CorrectionRecord
    {
        public string Timestamp { get; set; }
        public string ImagePath { get; set; }
        public string OriginalPrediction { get; set; }
        public float OriginalConfidence { get; set; }
        public string CorrectedLabel { get; set; }
    }
}