using System;
using System.Collections.Generic;
using System.Linq;

namespace OcrSharp.Service.Extensions
{
    public static class NumericCollectionExtensions
    {
        public static double GetMedian(this double[] sourceNumbers)
        {
            if (sourceNumbers == null || sourceNumbers.Length == 0)
                throw new InvalidOperationException("Median of empty array not defined.");

            double[] sortedPNumbers = (double[])sourceNumbers.Clone();
            Array.Sort(sortedPNumbers);

            int size = sortedPNumbers.Length;
            int mid = size / 2;
            double median = (size % 2 != 0) ? (double)sortedPNumbers[mid] : ((double)sortedPNumbers[mid] + (double)sortedPNumbers[mid - 1]) / 2;
            return median;
        }

        public static double GetMedian(this IEnumerable<double> sourceNumbers)
        {
            if (sourceNumbers == null || sourceNumbers.Count() == 0)
                throw new InvalidOperationException("Median of empty array not defined.");

            double[] sortedPNumbers = (double[])sourceNumbers.ToArray().Clone();
            Array.Sort(sortedPNumbers);

            int size = sortedPNumbers.Length;
            int mid = size / 2;
            double median = (size % 2 != 0) ? (double)sortedPNumbers[mid] : ((double)sortedPNumbers[mid] + (double)sortedPNumbers[mid - 1]) / 2;
            return median;
        }
    }
}
