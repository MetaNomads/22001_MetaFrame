using System;
using UnityEngine;

namespace MetaFrame.Data
{
    /// <summary>
    /// Owns survey response state for the current trial.
    /// Call StartReport() when the survey UI appears (auto-wired in SurveyControl).
    /// Call Collect() to package and reset — only valid when IsReady.
    /// </summary>
    public class SurveyDataRecorder : MonoBehaviour
    {
        // ── Response state ────────────────────────────────────────────────────────
        //
        // q1 + q2 + q3 are always asked.
        // q4 is asked only when q3 == q3ShowQ4Value.
        // Unanswered slots stay null and serialise as omitted (matches the existing
        // NullValueHandling.Ignore behaviour for SurveyEntry).

        private string _q1;
        private string _q2;
        private string _q3;
        private string _q4;
        private long?  _reportStart;

        // ── Public setters — wire these to your UI ────────────────────────────────

        public void SetQ1(string value) => _q1 = value;
        public void SetQ2(string value) => _q2 = value;
        public void SetQ3(string value) => _q3 = value;
        public void SetQ4(string value) => _q4 = value;

        /// <summary>
        /// Stamps report start time on first call per trial.
        /// No-op if already stamped — safe to call from every toggle interaction.
        /// Reset() nulls _reportStart so this re-arms automatically each trial.
        /// </summary>
        public void StartReport()
        {
            if (_reportStart == null)
                _reportStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // ── Gate ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// True when q1, q2, q3, and reportStart are all filled.
        /// q1, q2, q3 are always asked — q4 is conditional on the Q3 answer in
        /// SurveyControl, so it isn't part of the gate.
        /// </summary>
        public bool IsReady =>
            !string.IsNullOrEmpty(_q1) &&
            !string.IsNullOrEmpty(_q2) &&
            !string.IsNullOrEmpty(_q3) &&
            _reportStart.HasValue;

        // ── Collect ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Packages current responses into a SurveyData and resets for the next trial.
        /// Only call after confirming IsReady.
        ///
        /// NOTE: SurveyData (defined in ExperimentDataRecorder.cs) must expose
        /// matching fields: string q1, q2, q3, q4; long? reportStart.
        /// </summary>
        public SurveyData Collect()
        {
            var data = new SurveyData
            {
                q1          = _q1,
                q2          = _q2,
                q3          = _q3,
                q4          = _q4,
                reportStart = _reportStart,
            };

            Reset();
            return data;
        }

        /// <summary>Clears all response state. Called automatically by Collect().</summary>
        public void Reset()
        {
            _q1          = null;
            _q2          = null;
            _q3          = null;
            _q4          = null;
            _reportStart = null;
        }
    }
}
