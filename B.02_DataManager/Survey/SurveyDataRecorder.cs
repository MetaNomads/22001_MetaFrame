using System;
using UnityEngine;

namespace MetaFrame.Data
{
    /// <summary>
    /// Owns survey response state for the current trial.
    /// Call StartReport() when the survey UI appears.
    /// Call Collect() to package and reset — only valid when IsReady.
    /// </summary>
    public class SurveyDataRecorder : MonoBehaviour
    {
        // ── Response state ────────────────────────────────────────────────────────

        private string _detection;
        private string _confidence;
        private string _plausibility;
        private string _explanation;
        private long? _reportStart;

        // ── Public setters — wire these to your UI ────────────────────────────────

        public void SetDetection(string value) => _detection = value;
        public void SetConfidence(string value) => _confidence = value;
        public void SetPlausibility(string value) => _plausibility = value;
        public void SetExplanation(string value) => _explanation = value;

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

        /// <summary>True when detection, confidence, and reportStart are all filled.</summary>
        public bool IsReady =>
            !string.IsNullOrEmpty(_detection) &&
            !string.IsNullOrEmpty(_confidence) &&
            _reportStart.HasValue;

        // ── Collect ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Packages current responses into a SurveyData and resets for the next trial.
        /// Only call after confirming IsReady.
        /// </summary>
        public SurveyData Collect()
        {
            var data = new SurveyData
            {
                detection = _detection,
                confidence = _confidence,
                plausibility = _plausibility,
                explanation = _explanation,
                reportStart = _reportStart,
            };

            Reset();
            return data;
        }

        /// <summary>Clears all response state. Called automatically by Collect().</summary>
        public void Reset()
        {
            _detection = null;
            _confidence = null;
            _plausibility = null;
            _explanation = null;
            _reportStart = null;
        }
    }
}