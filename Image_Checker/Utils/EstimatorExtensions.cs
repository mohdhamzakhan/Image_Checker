using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Image_Checker.Utils
{
    public static class EstimatorExtensions
    {
        public static IEstimator<ITransformer> AppendIf(
            this IEstimator<ITransformer> chain,
            bool condition,
            IEstimator<ITransformer> ifTrue,
            IEstimator<ITransformer> ifFalse)
        {
            return condition ? chain.Append(ifTrue) : chain.Append(ifFalse);
        }
    }
}
