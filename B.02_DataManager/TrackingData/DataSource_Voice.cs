using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using Sirenix.OdinInspector;
using Newtonsoft.Json;

namespace MetaFrame.Data
{
    public class DataSource_Voice : DataSourceBase<DataSource_Voice.DataStructure, DataSource_Voice.RecordingConfig>
    {
        [SerializeField] private TrackingDataRecorder _trackingDataRecorder;

        public override string SourceName => "Voice";

        // ── Runtime state ──────────────────────────────────────────────────────────

        private AudioClip _clip;
        private string _deviceName;
        private bool _micOpen;
        private bool _segmentActive;
        private int _segmentIndex;
        private int _segmentStartSample;
        private DateTime _segmentStart;
        private StreamWriter _metadataWriter;

        private string SessionPath => _trackingDataRecorder != null
            ? _trackingDataRecorder.sessionPath
            : Application.persistentDataPath;

        // ── DataSourceBase ─────────────────────────────────────────────────────────

        protected override DataStructure CreateData() => new DataStructure();

        protected override void OnDataInitialized()
        {
            RequestMicrophonePermission();
            // Open synchronously so the mic is guaranteed ready before
            // OnRecordingStarted fires. The one-time stall happens here at
            // app start — never during recording.
            OpenMicrophone();
        }

        /// <summary>
        /// Voice does not participate in the per-frame CollectData pipeline.
        /// It manages its own Voice.json with one entry per segment.
        /// </summary>
        // FIX (D-4): override the new RegisterWithManager hook instead of the
        // entire Initialize() method. This lets the base class handle dataManager
        // assignment AND SourceNameLower caching (used by diagnostics / LSL),
        // while Voice cleanly opts out of the per-frame pipeline registration.
        protected override void RegisterWithManager(DataManager manager)
        {
            // Intentionally not calling manager.RegisterDataSource(this) —
            // Voice writes its own files and would just inflate _dataSources.
        }

        /// <summary>
        /// Unused — Voice is not in the frame pipeline.
        /// Returns empty so the interface contract is satisfied.
        /// </summary>
        public override Dictionary<string, object> CollectData() => new Dictionary<string, object>();

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_trackingDataRecorder == null)
            {
                Debug.LogError("[DataSource_Voice] No TrackingDataRecorder assigned.");
                return;
            }

            _trackingDataRecorder.OnRecordingStarted += OnRecordingStarted;
            _trackingDataRecorder.OnRecordingResumed += OnRecordingResumed;
            _trackingDataRecorder.OnRecordingPaused += OnRecordingPaused;
            _trackingDataRecorder.OnRecordingStopped += OnRecordingStopped;
        }

        private void OnDisable()
        {
            if (_trackingDataRecorder == null) return;

            // Save any active segment synchronously before unsubscribing.
            // This covers both play-mode stop in the Editor and app shutdown on device,
            // where OnRecordingStopped may fire after we have already unsubscribed.
            CloseSegment(forceSync: true);

            _trackingDataRecorder.OnRecordingStarted -= OnRecordingStarted;
            _trackingDataRecorder.OnRecordingResumed -= OnRecordingResumed;
            _trackingDataRecorder.OnRecordingPaused -= OnRecordingPaused;
            _trackingDataRecorder.OnRecordingStopped -= OnRecordingStopped;

            CloseMicrophone();
            CloseMetadataWriter();
        }

        // ── Recorder event handlers ────────────────────────────────────────────────

        private void OnRecordingStarted()
        {
            EnsureMetadataWriter();
            MarkSegmentStart();
        }

        private void OnRecordingResumed()
        {
            MarkSegmentStart();
        }

        private void OnRecordingPaused()
        {
            // Background write is fine here — not shutting down
            CloseSegment(forceSync: false);
        }

        private void OnRecordingStopped()
        {
            // Write synchronously — process may be about to exit
            CloseSegment(forceSync: true);
            CloseMicrophone();
            CloseMetadataWriter();
            _segmentIndex = 0;
        }

        // ── Microphone control ─────────────────────────────────────────────────────

        private void OpenMicrophone()
        {
            if (_micOpen) return;

            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.LogWarning("[DataSource_Voice] Microphone permission not granted.");
                return;
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[DataSource_Voice] No microphone devices found.");
                return;
            }

            _deviceName = Microphone.devices[0];

            // loop: true — mic runs continuously, buffer wraps, until explicitly stopped
            _clip = Microphone.Start(_deviceName, true, RecordConfig.MicBufferSeconds, RecordConfig.SampleRate);
            _micOpen = true;

            Debug.Log($"[DataSource_Voice] Microphone opened — device: {_deviceName}");
        }

        private void CloseMicrophone()
        {
            if (!_micOpen) return;

            Microphone.End(_deviceName);
            _clip = null;
            _micOpen = false;

            Debug.Log("[DataSource_Voice] Microphone closed.");
        }

        // ── Segment bookmarking ────────────────────────────────────────────────────

        private void MarkSegmentStart()
        {
            if (!_micOpen)
            {
                Debug.LogWarning("[DataSource_Voice] MarkSegmentStart called but mic is not open.");
                return;
            }

            _segmentStartSample = Microphone.GetPosition(_deviceName);
            _segmentStart = DateTime.Now;
            _segmentActive = true;
            _segmentIndex++;

            Debug.Log($"[DataSource_Voice] Segment {_segmentIndex:D3} started at {FormatTimestamp(_segmentStart)} | Sample: {_segmentStartSample}");
        }

        private void CloseSegment(bool forceSync)
        {
            if (!_micOpen || !_segmentActive) return;

            _segmentActive = false;

            DateTime end = DateTime.Now;
            int endSample = Microphone.GetPosition(_deviceName);
            string segName = $"Voice_seg{_segmentIndex:D3}";

            Debug.Log($"[DataSource_Voice] Segment {_segmentIndex:D3} closed at {FormatTimestamp(end)} | Sample: {endSample}");

            // Extract samples on main thread — Unity API requirement
            float[] samples = ExtractSamples(endSample, out int channels, out int frequency);

            WriteMetadata(segName, _segmentStart, end);

            if (samples == null) return;

            string path = Path.Combine(SessionPath, $"{segName}.wav");

            if (forceSync)
            {
                // Synchronous write — guarantees file is saved before process exits
                SaveWav(samples, channels, frequency, path);
            }
            else
            {
                // Background write — no main thread cost during normal pause
                System.Threading.Tasks.Task.Run(() => SaveWav(samples, channels, frequency, path));
            }
        }

        // ── Sample extraction ──────────────────────────────────────────────────────

        private float[] ExtractSamples(int endSample, out int channels, out int frequency)
        {
            channels = 0;
            frequency = 0;

            if (_clip == null) return null;

            channels = _clip.channels;
            frequency = _clip.frequency;

            int clipFrames = _clip.samples; // total frames in the loop buffer

            if (_segmentStartSample == endSample) return null;

            float[] samples;

            if (endSample > _segmentStartSample)
            {
                // Normal case — no wrap
                int frameCount = endSample - _segmentStartSample;
                samples = new float[frameCount * channels];
                _clip.GetData(samples, _segmentStartSample);
            }
            else
            {
                // Buffer wrapped around — stitch two slices together
                int firstFrames = clipFrames - _segmentStartSample;
                int secondFrames = endSample;
                int totalFrames = firstFrames + secondFrames;

                samples = new float[totalFrames * channels];

                float[] part1 = new float[firstFrames * channels];
                _clip.GetData(part1, _segmentStartSample);
                Array.Copy(part1, 0, samples, 0, part1.Length);

                float[] part2 = new float[secondFrames * channels];
                _clip.GetData(part2, 0);
                Array.Copy(part2, 0, samples, part1.Length, part2.Length);
            }

            return samples;
        }

        // ── WAV saving ────────────────────────────────────────────────────────────

        private static void SaveWav(float[] samples, int channels, int frequency, string path)
        {
            try
            {
                byte[] wav = WavEncoder.EncodeRaw(samples, channels, frequency);
                File.WriteAllBytes(path, wav);
                Debug.Log($"[DataSource_Voice] WAV saved: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataSource_Voice] Failed to save WAV: {e.Message}");
            }
        }

        // ── Metadata writing ───────────────────────────────────────────────────────

        private void EnsureMetadataWriter()
        {
            if (_metadataWriter != null) return;

            string path = Path.Combine(SessionPath, "Voice.json");
            var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _metadataWriter = new StreamWriter(fs, System.Text.Encoding.UTF8);
        }

        private void WriteMetadata(string segmentName, DateTime start, DateTime end)
        {
            if (_metadataWriter == null) return;

            try
            {
                var entry = new Dictionary<string, object>
                {
                    ["segment"] = segmentName,
                    ["start"] = FormatTimestamp(start),
                    ["end"] = FormatTimestamp(end),
                };
                _metadataWriter.WriteLine(JsonConvert.SerializeObject(entry));
                _metadataWriter.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataSource_Voice] Failed to write metadata: {e.Message}");
            }
        }

        private void CloseMetadataWriter()
        {
            try { _metadataWriter?.Flush(); _metadataWriter?.Close(); }
            catch { /* best effort on shutdown */ }
            _metadataWriter = null;
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private static string FormatTimestamp(DateTime dt) => dt.ToString("HH:mm:ss.fff");

        private static void RequestMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                Permission.RequestUserPermission(Permission.Microphone);
#endif
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// Data Structure — empty; Voice has no per-frame live data to expose.
        /// </summary>

        public class DataStructure { }

        /*=========================================================================================================================*/
        /// <summary>
        /// Recording Configuration
        /// </summary>

        [Serializable]
        public class RecordingConfig
        {
            [Tooltip("Sample rate for microphone recording (Hz).")]
            public int SampleRate = 44100;

            [Tooltip("Pre-allocated mic buffer size in seconds (Unity requirement). " +
                     "A segment will stop if it exceeds this — set higher than your longest expected uninterrupted recording.")]
            [Range(60, 600)]
            public int MicBufferSeconds = 300;
        }

        /*=========================================================================================================================*/
        /// <summary>
        /// WAV encoder — no UnityEngine dependency, safe to call from any thread.
        /// </summary>

        private static class WavEncoder
        {
            private const int HeaderSize = 44;
            private const short BitDepth = 16;
            private const float RescaleFactor = 32767f;

            public static byte[] EncodeRaw(float[] samples, int channels, int frequency)
            {
                int sampleCount = samples.Length;
                int dataSize = sampleCount * (BitDepth / 8);
                int fileSize = HeaderSize + dataSize;

                byte[] buffer = new byte[fileSize];
                int offset = 0;

                // RIFF header
                WriteString(buffer, ref offset, "RIFF");
                WriteInt32(buffer, ref offset, fileSize - 8);
                WriteString(buffer, ref offset, "WAVE");

                // fmt chunk
                WriteString(buffer, ref offset, "fmt ");
                WriteInt32(buffer, ref offset, 16);
                WriteInt16(buffer, ref offset, 1);                                     // PCM
                WriteInt16(buffer, ref offset, (short)channels);
                WriteInt32(buffer, ref offset, frequency);
                WriteInt32(buffer, ref offset, frequency * channels * (BitDepth / 8)); // byte rate
                WriteInt16(buffer, ref offset, (short)(channels * (BitDepth / 8)));    // block align
                WriteInt16(buffer, ref offset, BitDepth);

                // data chunk
                WriteString(buffer, ref offset, "data");
                WriteInt32(buffer, ref offset, dataSize);

                foreach (float s in samples)
                {
                    short v = (short)(Math.Max(-1f, Math.Min(1f, s)) * RescaleFactor);
                    buffer[offset++] = (byte)(v & 0xFF);
                    buffer[offset++] = (byte)((v >> 8) & 0xFF);
                }

                return buffer;
            }

            private static void WriteString(byte[] b, ref int o, string v) { foreach (char c in v) b[o++] = (byte)c; }
            private static void WriteInt16(byte[] b, ref int o, short v) { b[o++] = (byte)(v & 0xFF); b[o++] = (byte)((v >> 8) & 0xFF); }
            private static void WriteInt32(byte[] b, ref int o, int v) { b[o++] = (byte)(v & 0xFF); b[o++] = (byte)((v >> 8) & 0xFF); b[o++] = (byte)((v >> 16) & 0xFF); b[o++] = (byte)((v >> 24) & 0xFF); }
        }
    }
}
