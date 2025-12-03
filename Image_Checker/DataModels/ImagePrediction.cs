using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Image_Checker.DataModels
{
    public class ImagePrediction
    {
        public string? PredictedLabel { get; set; }

        [ColumnName("Score")]
        public float[] Score { get; set; } = Array.Empty<float>();
    }
}
