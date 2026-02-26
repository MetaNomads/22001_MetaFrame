using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Piglet
{
    /// <summary>
    /// <para>
    /// A simple profiler class with an API that is similar to
    /// Unity's built-in `Profiler` class. Just like Unity's `Profiler`
    /// class, profiling samples are recorded with paired calls
    /// to `BeginSample`/`EndSample` methods.
    /// </para>
    /// <para>
    /// The main motivation for creating this class was to allow
    /// the profiling data to easily be exported in plain text
    /// formats (e.g. CSV, TSV), so that the data can be graphed
    /// and analyzed by external tools.
    /// </para>
    /// </summary>
    public class SimpleProfiler : Singleton<SimpleProfiler>
    {
        /// <summary>
        /// Stores aggregate statistics (e.g. min value, max value,
        /// histogram) for profiler samples, grouped by name.
        /// </summary>
        public Dictionary<string, SimpleProfilerResults> Results;

        /// <summary>
        /// Stack that keeps track of paired calls to
        /// `BeginSample`/`EndSample`.
        /// </summary>
        private Stack<(string, Stopwatch)> _sampleStack;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SimpleProfiler()
        {
            Reset();
        }

        /// <summary>
        /// Reset the profiler by clearing all stored data.
        /// </summary>
        public void Reset()
        {
            Results = new Dictionary<string, SimpleProfilerResults>();
            _sampleStack = new Stack<(string, Stopwatch)>();
        }

        /// <summary>
        /// Start recording a named sample (i.e. elapsed milliseconds
        /// until the next call to `EndSample`). Samples are
        /// grouped by name and aggregate statistics (e.g. min
        /// value, max value, histogram) are stored in `Results`.
        /// </summary>
        public void BeginSample(string name)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _sampleStack.Push((name, stopwatch));
        }

        /// <summary>
        /// Stop recording the current sample (i.e. elapsed milliseconds
        /// since the last call to `BeginSample`), and add the
        /// result to `Results`.
        /// </summary>
        public void EndSample()
        {
            if (_sampleStack.Count == 0)
                throw new Exception("called EndSample() without calling BeginSample(name) first");

            var (name, stopwatch) = _sampleStack.Pop();
            stopwatch.Stop();

            AddSample(name, (int)stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Add a profiling sample (i.e. elapsed milliseconds) that
        /// we recorded ourselves, rather than using `BeginSample`/`EndSample`.
        /// This is useful when we need access to the stopwatch
        /// state between calls to `BeginSample` and `EndSample`,
        /// or in situations where making paired calls to
        /// `BeginSample`/`EndSample` is impractical (e.g. across method
        /// invocations).
        /// </summary>
        public void AddSample(string name, int sample)
        {
            if (!Results.TryGetValue(name, out var sampleResults))
            {
                sampleResults = new SimpleProfilerResults();
                Results.Add(name, sampleResults);
            }

            sampleResults.Insert(sample);
        }
    }
}
