using System.Text;

namespace Piglet
{
    /// <summary>
    /// <para>
    /// Represents a histogram of integer values.
    /// </para>
    /// <para>
    /// The histogram has a fixed number of bins and the
    /// first bin always begins at 0. Negative values
    /// are added to `UnderflowBin` and values greater
    /// than the largest bin are added to `OverflowBin`.
    /// </para>
    /// </summary>
    public class SimpleProfilerHistogram
    {
        /// <summary>
        /// The width of each bin.
        /// </summary>
        public readonly int BinWidth;

        /// <summary>
        /// <para>
        /// The number of histogram bins.
        /// </para>
        ///
        /// <para>
        /// The interval for the first bin begins at 0.
        /// For example, if the bin width is 10, then
        /// the first bin will cover the interval
        /// [0, 10).
        /// </para>
        /// </summary>
        public readonly int[] Bins;

        /// <summary>
        /// Stores the number of negative values that were
        /// were inserted into the histogram.
        /// </summary>
        public int UnderflowBin;

        /// <summary>
        /// Stores the number values that were too
        /// large to fit in any of the histogram bins.
        /// For example, if the largest histogram bin
        /// is for the interval [100,110), then a
        /// value of 130 would be inserted into the
        /// OverflowBin.
        /// </summary>
        public int OverflowBin;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="binWidth">
        /// The width of each histogram bin. For example, if the
        /// binWidth is 10, then the first bin contains values
        /// in the interval of [0,10).
        /// </param>
        /// <param name="numBins">
        /// The number of bins. For example, if the binWidth
        /// is 10 and numBins is 3, then the histogram
        /// will represent the bins: [0, 10), [10, 20), [20, 30).
        /// </param>
        public SimpleProfilerHistogram(int binWidth, int numBins)
        {
            BinWidth = binWidth;
            Bins = new int[numBins];
        }

        /// <summary>
        /// Add a new value to the histogram.
        /// </summary>
        public void Insert(int value)
        {
            var binIndex = value / BinWidth;

            if (binIndex < 0)
                UnderflowBin++;
            else if (binIndex >= Bins.Length)
                OverflowBin++;
            else
                Bins[binIndex]++;
        }

        /// <summary>
        /// Serialize the histogram to a multi-line TSV format.
        /// </summary>
        public override string ToString()
        {
            var str = new StringBuilder();

            var firstLine = true;

            if (UnderflowBin > 0)
            {
                str.Append($"<\t0\t{UnderflowBin}");
                firstLine = false;
            }

            for (var i = 0; i < Bins.Length; ++i)
            {
                if (Bins[i] == 0)
                    continue;

                var binMin = i * BinWidth;
                var binMax = binMin + BinWidth - 1;

                if (!firstLine)
                    str.Append("\n");

                str.Append($"{binMin}\t{binMax}\t{Bins[i]}");

                firstLine = false;
            }

            if (OverflowBin > 0)
            {
                if (!firstLine)
                    str.Append("\n");

                str.Append($">=\t{BinWidth * Bins.Length}\t{OverflowBin}");
            }

            return str.ToString();
        }
    }
}