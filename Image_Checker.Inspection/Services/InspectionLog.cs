using System.Globalization;
using System.Text;
using Image_Checker.Inspection.Models;

namespace Image_Checker.Inspection.Services;

/// <summary>Appends one CSV line per check, override and final part verdict: Results\inspection_log.csv.</summary>
public static class InspectionLog
{
    private static readonly object Gate = new();

    /// <returns>false when the file could not be written (for example it is open in Excel).</returns>
    public static bool Append(string product, string serial, int step, string stepName,
                              string verdict, float ngScore, string image, string note)
    {
        try
        {
            lock (Gate)
            {
                string dir = RecipeStore.ResultsDir(product);
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "inspection_log.csv");
                bool isNew = !File.Exists(path);

                using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var w = new StreamWriter(fs, new UTF8Encoding(false));
                if (isNew) w.WriteLine("Timestamp,Product,Serial,Step,StepName,Verdict,NgScore,Image,Note");

                w.WriteLine(string.Join(",",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    Q(product), Q(serial),
                    step.ToString(CultureInfo.InvariantCulture), Q(stepName), Q(verdict),
                    ngScore.ToString("0.0000", CultureInfo.InvariantCulture),
                    Q(image), Q(note)));
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
}