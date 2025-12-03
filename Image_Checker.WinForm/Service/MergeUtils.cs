using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Image_Checker.WinForm.Service
{
    public class MergeUtils
    {
        public void MergeCorrections(string baseCsv, string correctionsCsv)
        {
            var baseData = File.ReadAllLines(baseCsv).Select(l => l.Split(',')).ToList();
            var corrections = File.ReadAllLines(correctionsCsv)
                .Skip(1)
                .Select(l => l.Split(','))
                .ToDictionary(x => x[1], x => x[4]); // key: image path, value: corrected label

            for (int i = 0; i < baseData.Count; i++)
            {
                var imgPath = baseData[i][0];
                if (corrections.ContainsKey(imgPath))
                {
                    baseData[i][1] = corrections[imgPath];
                }
            }

            File.WriteAllLines(baseCsv, baseData.Select(a => string.Join(",", a)));
            Console.WriteLine("✅ Corrections merged into training CSV.");
        }

    }
}
