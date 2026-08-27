using Microsoft.ML;
using System.Collections.Generic;

namespace Image_Checker.Services
{
    /// <summary>
    /// Shared result type for all hyperparameter tuners.
    /// Eliminates the duplicate TuningResult class that previously existed
    /// in both FastTreeTuner and LightGbmTuner.
    /// </summary>
    public class TuningResult
    {
        public IEstimator<ITransformer> BestEstimator { get; set; }
        public IDictionary<string, object> Params { get; set; }
        public double Score { get; set; }
    }
}