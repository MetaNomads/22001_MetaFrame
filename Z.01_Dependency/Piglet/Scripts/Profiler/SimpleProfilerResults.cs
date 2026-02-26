namespace Piglet
{
    /// <summary>
    /// Stores aggregate statistics for a collection profiler samples.
    /// Profiler samples are grouped by name, where the name is specified
    /// in the calls to the BeginSample/AddSample methods.
    /// </summary>
    public class SimpleProfilerResults
    {
        /// <summary>
        /// The minimum sample value.
        /// </summary>
        public int? Min;

        /// <summary>
        /// The maximum sample value.
        /// </summary>
        public int? Max;

        /// <summary>
        /// The sum of all sample values.
        /// </summary>
        public int Sum;

        /// <summary>
        /// A histogram of the sample values.
        /// </summary>
        public SimpleProfilerHistogram Histogram;

        public SimpleProfilerResults()
        {
            Histogram = new SimpleProfilerHistogram(10, 10);
            Sum = 0;
        }

        /// <summary>
        /// Add a new profiling sample and update the aggregate statistics.
        /// </summary>
        public void Insert(int sample)
        {
            Histogram.Insert(sample);

            if (!Min.HasValue || sample < Min.Value)
                Min = sample;

            if (!Max.HasValue || sample > Max.Value)
                Max = sample;

            Sum += sample;
        }

        /// <summary>
        /// Serialize the aggregrate sample statistics in multi-line TSV format.
        /// </summary>
        public override string ToString()
        {
            return Histogram.ToString();
        }
    }
}