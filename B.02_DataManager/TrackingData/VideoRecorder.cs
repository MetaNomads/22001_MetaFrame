using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Sirenix.OdinInspector;
using MetaFrame.Data;
using System.Collections.Generic;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace MetaFrame.Data
{
    /// <summary>
    /// Records a fixed in-scene camera (MP4) and microphone (WAV) in sync with
    /// TrackingDataRecorder's start/pause/resume/stop events.
    ///
    /// Video pipeline (zero main-thread stall):
    ///   1. A fixed scene Camera renders into a RenderTexture every frame.
    ///   2. AsyncGPUReadback reads the texture asynchronously — no GPU sync, no stall.
    ///   3. Raw RGBA frames are queued and piped into FFmpeg's stdin on a background thread.
    ///   4. FFmpeg encodes to H.264 MP4 using hardware (NVENC/QuickSync/AMF) or libx264.
    ///
    /// Audio strategy:
    ///   • Mic audio — always saved as a separate WAV (DataSource_Video pattern).
    ///                 Does NOT route through AudioSource, so VR/Oculus audio is unaffected.
    /// </summary>
    public class VideoRecorder : MonoBehaviour
    {
        // ── References ─────────────────────────────────────────────────────────────

        [BoxGroup("References")]
        [SerializeField] private TrackingDataRecorder _trackingDataRecorder;

        // ── Camera ─────────────────────────────────────────────────────────────────

        [BoxGroup("Camera")]
        [Tooltip("Fixed in-scene camera to record from. Separate from the VR headset camera.")]
        [SerializeField] private Camera _surveillanceCamera;

        [BoxGroup("Camera")]
        [SerializeField] private int _videoWidth = 1280;

        [BoxGroup("Camera")]
        [SerializeField] private int _videoHeight = 720;

        [BoxGroup("Camera")]
        [Range(15, 60)]
        [SerializeField] private int _videoFrameRate = 30;

        // ── FFmpeg ─────────────────────────────────────────────────────────────────

        [BoxGroup("FFmpeg")]
        [Tooltip("Path to ffmpeg.exe. Leave blank to search the application folder and PATH.")]
        [SerializeField] private string _ffmpegPath = "";

        [BoxGroup("FFmpeg")]
        [Tooltip("Try NVENC / QuickSync / AMF hardware encoding first. " +
                 "Falls back to libx264 software encoding automatically.")]
        [SerializeField] private bool _useHardwareEncoding = true;

        // ── Microphone ─────────────────────────────────────────────────────────────

        [BoxGroup("Microphone")]
        [ValueDropdown("_availableDevices")]
        [Tooltip("Select a microphone device. Hit Refresh Devices if the list is empty.")]
        [SerializeField] private string _microphoneDevice;

        [BoxGroup("Microphone")]
        [Button("Refresh Devices"), GUIColor(0.6f, 0.85f, 1f)]
        [OnInspectorInit("RefreshDevices")]
        private void RefreshDevices()
        {
            _availableDevices = new List<string>(Microphone.devices);
        }

        [HideInInspector]
        [SerializeField] private List<string> _availableDevices = new();

        [BoxGroup("Microphone")]
        [Tooltip("Sample rate for microphone recording (Hz).")]
        [SerializeField] private int _sampleRate = 44100;

        [BoxGroup("Microphone")]
        [Range(60, 3599)]
        [Tooltip("Mic buffer size in seconds. Unity's hard limit is under one hour (max 3599).")]
        [SerializeField] private int _micBufferSeconds = 3599;

        // ── Runtime state ──────────────────────────────────────────────────────────

        private AudioClip    _micClip;
        private bool         _micOpen;
        private bool         _segmentActive;
        private int          _segmentIndex;
        private int          _segmentStartSample;
        private DateTime     _segmentStart;
        private StreamWriter _metadataWriter;

        // Video
        private RenderTexture              _renderTexture;
        private bool                       _videoRecording;
        private float                      _frameInterval;
        private float                      _nextFrameTime;
        private Process                    _ffmpegProcess;
        private BlockingCollection<byte[]> _frameQueue;
        private Thread                     _encoderThread;

        private string SessionPath => _trackingDataRecorder != null
            ? _trackingDataRecorder.sessionPath
            : Application.persistentDataPath;

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_trackingDataRecorder == null)
            {
                Debug.LogError("[VideoRecorder] No TrackingDataRecorder assigned.");
                return;
            }

            _trackingDataRecorder.OnRecordingStarted += OnRecordingStarted;
            _trackingDataRecorder.OnRecordingResumed += OnRecordingResumed;
            _trackingDataRecorder.OnRecordingPaused  += OnRecordingPaused;
            _trackingDataRecorder.OnRecordingStopped += OnRecordingStopped;

            OpenMicrophone();

            // TrackingDataRecorder fires OnRecordingStarted in Awake — before this
            // OnEnable subscribes. Catch up if recording is already active.
            if (_trackingDataRecorder.startRecord)
            {
                Debug.Log("[VideoRecorder] Recording already active — catching up.");
                OnRecordingStarted();
            }
        }

        private void OnDisable()
        {
            if (_trackingDataRecorder == null) return;

            CloseSegment(forceSync: true);

            _trackingDataRecorder.OnRecordingStarted -= OnRecordingStarted;
            _trackingDataRecorder.OnRecordingResumed -= OnRecordingResumed;
            _trackingDataRecorder.OnRecordingPaused  -= OnRecordingPaused;
            _trackingDataRecorder.OnRecordingStopped -= OnRecordingStopped;

            CloseMicrophone();
            CloseMetadataWriter();
        }

        private void Update()
        {
            if (!_videoRecording) return;
            if (Time.unscaledTime < _nextFrameTime) return;

            _nextFrameTime += _frameInterval;

            // Request async GPU readback of the RenderTexture.
            // RGBA32 is always supported; no format conversion needed.
            // The callback fires on the main thread once the GPU is done — no stall.
            AsyncGPUReadback.Request(_renderTexture, 0, TextureFormat.RGBA32, OnFrameReady);
        }

        // ── TrackingDataRecorder event handlers ────────────────────────────────────

        private void OnRecordingStarted()
        {
            EnsureMetadataWriter();
            MarkSegmentStart();
            StartVideoRecording();
        }

        private void OnRecordingResumed()
        {
            MarkSegmentStart();
            StartVideoRecording();
        }

        private void OnRecordingPaused()
        {
            CloseSegment(forceSync: false);
            StopVideoRecording();
        }

        private void OnRecordingStopped()
        {
            CloseSegment(forceSync: true);
            StopVideoRecording();
            CloseMicrophone();
            CloseMetadataWriter();
            _segmentIndex = 0;
        }

        // ── Microphone ─────────────────────────────────────────────────────────────

        private void OpenMicrophone()
        {
            if (_micOpen) return;

            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[VideoRecorder] No microphone devices found.");
                return;
            }

            _micClip = Microphone.Start(SelectedDevice(), true, _micBufferSeconds, _sampleRate);
            StartCoroutine(WaitForMicReady());
        }

        private IEnumerator WaitForMicReady()
        {
            while (Microphone.GetPosition(SelectedDevice()) <= 0)
                yield return null;

            _micOpen = true;
            Debug.Log($"[VideoRecorder] Microphone opened — device: {SelectedDevice()}");
        }

        private void CloseMicrophone()
        {
            if (!_micOpen) return;
            Microphone.End(SelectedDevice());
            _micClip = null;
            _micOpen = false;
            Debug.Log("[VideoRecorder] Microphone closed.");
        }

        // ── Segment bookmarking ────────────────────────────────────────────────────

        private void MarkSegmentStart()
        {
            if (!_micOpen)
            {
                Debug.LogWarning("[VideoRecorder] MarkSegmentStart — mic not open yet.");
                return;
            }

            _segmentStartSample = Microphone.GetPosition(SelectedDevice());
            _segmentStart       = DateTime.Now;
            _segmentActive      = true;
            _segmentIndex++;

            Debug.Log($"[VideoRecorder] Segment {_segmentIndex:D3} started | Sample: {_segmentStartSample}");
        }

        private void CloseSegment(bool forceSync)
        {
            if (!_micOpen || !_segmentActive) return;

            _segmentActive = false;

            DateTime end       = DateTime.Now;
            int      endSample = Microphone.GetPosition(SelectedDevice());
            string   segName   = $"DataSource_Video_seg{_segmentIndex:D3}";

            float[] samples = ExtractSamples(endSample, out int channels, out int frequency);

            WriteMetadata(segName, _segmentStart, end);

            if (samples == null) return;

            string path = Path.Combine(SessionPath, $"{segName}.wav");

            if (forceSync)
                SaveWav(samples, channels, frequency, path);
            else
                System.Threading.Tasks.Task.Run(() => SaveWav(samples, channels, frequency, path));
        }

        // ── Sample extraction ──────────────────────────────────────────────────────

        private float[] ExtractSamples(int endSample, out int channels, out int frequency)
        {
            channels  = 0;
            frequency = 0;

            if (_micClip == null) return null;

            channels  = _micClip.channels;
            frequency = _micClip.frequency;

            int clipFrames = _micClip.samples;

            if (_segmentStartSample == endSample) return null;

            if (endSample > _segmentStartSample)
            {
                int frameCount = endSample - _segmentStartSample;
                var samples    = new float[frameCount * channels];
                _micClip.GetData(samples, _segmentStartSample);
                return samples;
            }
            else
            {
                // Buffer wrapped — stitch two slices
                int firstFrames  = clipFrames - _segmentStartSample;
                int secondFrames = endSample;
                var samples      = new float[(firstFrames + secondFrames) * channels];

                var part1 = new float[firstFrames * channels];
                _micClip.GetData(part1, _segmentStartSample);
                Array.Copy(part1, 0, samples, 0, part1.Length);

                var part2 = new float[secondFrames * channels];
                _micClip.GetData(part2, 0);
                Array.Copy(part2, 0, samples, part1.Length, part2.Length);

                return samples;
            }
        }

        // ── WAV saving ─────────────────────────────────────────────────────────────

        private static void SaveWav(float[] samples, int channels, int frequency, string path)
        {
            try
            {
                File.WriteAllBytes(path, WavEncoder.Encode(samples, channels, frequency));
                Debug.Log($"[VideoRecorder] WAV saved: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VideoRecorder] Failed to save WAV: {e.Message}");
            }
        }

        // ── Metadata ───────────────────────────────────────────────────────────────

        private void EnsureMetadataWriter()
        {
            if (_metadataWriter != null) return;
            string path = Path.Combine(SessionPath, "DataSource_Video.json");
            var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _metadataWriter = new StreamWriter(fs, System.Text.Encoding.UTF8);
        }

        private void WriteMetadata(string segmentName, DateTime start, DateTime end)
        {
            if (_metadataWriter == null) return;
            try
            {
                var entry = new
                {
                    segment = segmentName,
                    start   = new DateTimeOffset(start).ToUnixTimeMilliseconds(),
                    end     = new DateTimeOffset(end).ToUnixTimeMilliseconds(),
                };
                _metadataWriter.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(entry));
                _metadataWriter.Flush();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VideoRecorder] Failed to write metadata: {e.Message}");
            }
        }

        private void CloseMetadataWriter()
        {
            try { _metadataWriter?.Flush(); _metadataWriter?.Close(); }
            catch { /* best effort on shutdown */ }
            _metadataWriter = null;
        }

        // ── Video recording ────────────────────────────────────────────────────────
        //
        // Pipeline:
        //   Camera → RenderTexture
        //       → AsyncGPUReadback (no GPU stall, callback on main thread)
        //           → BlockingCollection<byte[]> (bounded, drops frame if full)
        //               → background Thread → FFmpeg stdin pipe → MP4

        private void StartVideoRecording()
        {
            if (_surveillanceCamera == null)
            {
                Debug.LogError("[VideoRecorder] No surveillance camera assigned.");
                return;
            }

            string ffmpeg = ResolveFfmpegPath();
            if (string.IsNullOrEmpty(SessionPath))
            {
                Debug.LogError("[VideoRecorder] SessionPath is empty — TrackingDataRecorder not initialised yet.");
                return;
            }

            // Create RenderTexture and point the camera at it
            _renderTexture = new RenderTexture(_videoWidth, _videoHeight, 24, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
            _surveillanceCamera.targetTexture = _renderTexture;

            string outputFile = Path.Combine(SessionPath, $"DataSource_Video_seg{_segmentIndex:D3}.mp4");

            // FFmpeg reads raw RGBA frames from stdin and encodes to MP4.
            // rgba pixel format matches our AsyncGPUReadback TextureFormat.RGBA32 readback.
            string videoCodec = _useHardwareEncoding
                ? "h264_nvenc -rc vbr -cq 23"      // NVENC (NVIDIA); swap for h264_qsv / h264_amf
                : "libx264 -preset veryfast -crf 23";

            string args = $"-y " +
                          $"-f rawvideo -pixel_format rgba -video_size {_videoWidth}x{_videoHeight} " +
                          $"-framerate {_videoFrameRate} -i pipe:0 " +
                          $"-c:v {videoCodec} " +
                          $"-pix_fmt yuv420p " +
                          $"\"{outputFile}\"";

            var psi = new ProcessStartInfo
            {
                FileName              = ffmpeg,
                Arguments             = args,
                UseShellExecute       = false,
                CreateNoWindow        = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
            };

            try
            {
                _ffmpegProcess = Process.Start(psi);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VideoRecorder] Failed to start FFmpeg: {e.Message}");
                ReleaseRenderTexture();
                return;
            }

            // Bounded queue: if the encoder thread falls behind, new frames are dropped
            // rather than letting memory grow unbounded.
            _frameQueue    = new BlockingCollection<byte[]>(boundedCapacity: 120);
            _encoderThread = new Thread(EncoderLoop) { IsBackground = true, Name = "FFmpegEncoder" };
            _encoderThread.Start();

            _frameInterval  = 1f / _videoFrameRate;
            _nextFrameTime  = Time.unscaledTime;
            _videoRecording = true;

            Debug.Log($"[VideoRecorder] Recording started (PID {_ffmpegProcess.Id}) → {outputFile}");
        }

        private void StopVideoRecording()
        {
            if (!_videoRecording) return;
            _videoRecording = false;

            // Signal the encoder thread that no more frames are coming, then wait for it
            // to drain the queue and close the stdin pipe.
            _frameQueue?.CompleteAdding();
            _encoderThread?.Join(10000);

            // Wait for FFmpeg to finish writing the MP4 container.
            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                if (!_ffmpegProcess.WaitForExit(5000))
                {
                    _ffmpegProcess.Kill();
                    Debug.LogWarning("[VideoRecorder] FFmpeg did not exit in time — killed.");
                }
            }
            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;

            _frameQueue?.Dispose();
            _frameQueue = null;

            ReleaseRenderTexture();

            Debug.Log("[VideoRecorder] Recording stopped.");
        }

        /// <summary>
        /// AsyncGPUReadback callback — fires on the main thread once the GPU is done.
        /// Copies the raw pixel bytes into the frame queue for the encoder thread.
        /// </summary>
        private void OnFrameReady(AsyncGPUReadbackRequest request)
        {
            if (request.hasError || !_videoRecording || _frameQueue == null) return;

            // ToArray() copies out of the native buffer before it is recycled.
            byte[] frame = request.GetData<byte>().ToArray();

            // TryAdd is non-blocking — drops the frame silently if the queue is full.
            _frameQueue.TryAdd(frame);
        }

        /// <summary>
        /// Runs on a background thread. Drains the frame queue and writes raw bytes
        /// directly into FFmpeg's stdin pipe for encoding.
        /// </summary>
        private void EncoderLoop()
        {
            try
            {
                Stream stdin = _ffmpegProcess.StandardInput.BaseStream;

                // GetConsumingEnumerable blocks until a frame is available and exits
                // cleanly once CompleteAdding() is called and the queue is empty.
                foreach (byte[] frame in _frameQueue.GetConsumingEnumerable())
                {
                    stdin.Write(frame, 0, frame.Length);
                }

                stdin.Flush();
                stdin.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VideoRecorder] Encoder thread error: {e.Message}");
            }
        }

        private void ReleaseRenderTexture()
        {
            if (_surveillanceCamera != null)
                _surveillanceCamera.targetTexture = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }
        }

        /// <summary>
        /// Resolves the path to ffmpeg.exe.
        /// Checks: (1) Inspector field, (2) application folder, (3) system PATH.
        /// </summary>
        private string ResolveFfmpegPath()
        {
            if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
                return _ffmpegPath;

            string appFolder = Path.GetDirectoryName(Application.dataPath) ?? "";
            string local     = Path.Combine(appFolder, "ffmpeg.exe");
            if (File.Exists(local)) return local;

            return "ffmpeg";
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        private string SelectedDevice() =>
            string.IsNullOrEmpty(_microphoneDevice) ? Microphone.devices[0] : _microphoneDevice;

        // ── WAV Encoder ────────────────────────────────────────────────────────────

        private static class WavEncoder
        {
            private const int   HeaderSize    = 44;
            private const short BitDepth      = 16;
            private const float RescaleFactor = 32767f;

            public static byte[] Encode(float[] samples, int channels, int frequency)
            {
                int dataSize = samples.Length * (BitDepth / 8);
                var buffer   = new byte[HeaderSize + dataSize];
                int offset   = 0;

                WriteString(buffer, ref offset, "RIFF");
                WriteInt32 (buffer, ref offset, buffer.Length - 8);
                WriteString(buffer, ref offset, "WAVE");
                WriteString(buffer, ref offset, "fmt ");
                WriteInt32 (buffer, ref offset, 16);
                WriteInt16 (buffer, ref offset, 1);
                WriteInt16 (buffer, ref offset, (short)channels);
                WriteInt32 (buffer, ref offset, frequency);
                WriteInt32 (buffer, ref offset, frequency * channels * (BitDepth / 8));
                WriteInt16 (buffer, ref offset, (short)(channels * (BitDepth / 8)));
                WriteInt16 (buffer, ref offset, BitDepth);
                WriteString(buffer, ref offset, "data");
                WriteInt32 (buffer, ref offset, dataSize);

                foreach (float s in samples)
                {
                    short v = (short)(Math.Max(-1f, Math.Min(1f, s)) * RescaleFactor);
                    buffer[offset++] = (byte)(v & 0xFF);
                    buffer[offset++] = (byte)((v >> 8) & 0xFF);
                }

                return buffer;
            }

            private static void WriteString(byte[] b, ref int o, string v) { foreach (char c in v) b[o++] = (byte)c; }
            private static void WriteInt16 (byte[] b, ref int o, short v)  { b[o++] = (byte)(v & 0xFF); b[o++] = (byte)((v >> 8) & 0xFF); }
            private static void WriteInt32 (byte[] b, ref int o, int v)    { b[o++] = (byte)(v & 0xFF); b[o++] = (byte)((v >> 8) & 0xFF); b[o++] = (byte)((v >> 16) & 0xFF); b[o++] = (byte)((v >> 24) & 0xFF); }
        }
    }
}