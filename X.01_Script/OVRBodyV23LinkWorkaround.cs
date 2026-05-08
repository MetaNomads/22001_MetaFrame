// OVRBodyV23LinkWorkaround.cs
//
// Workaround for: https://github.com/oculus-samples/Unity-Movement/issues/136
//
// Symptom (Quest Pro, Quest OS v2.3, Link cable, Meta XR Core SDK 201/85):
//   [OVRPlugin] [RequestBodyTrackingFidelity] body tracking fidelity is not supported
//   [OVRBody]   Failed to set Body Tracking fidelity to: High
//   ...then OVRBody.BodyState has no value every frame indefinitely,
//   even though OVRPlugin.StartBodyTracking2 returned true.
//
// Quest OS v2.1.1034 works fine; the regression is firmware-side over Link.
//
// What this script does:
//   - Watches OVRBody.BodyState. If it stays empty for `restartAfterSecondsWithoutBody`,
//     stops body tracking via OVRPlugin and starts it again — explicitly skipping
//     RequestBodyTrackingFidelity, so a fidelity failure can't poison body tracking
//     for the rest of the session.
//   - Logs ONE warning when the failure mode is detected, instead of letting the
//     "BodyState has no value" path silently spam tick logs.
//
// Place this on any GameObject in the scene. Drag your OVRBody into the inspector,
// or leave it empty and the script will find one at Start.
//
// IMPORTANT: enum names below match Meta XR Core SDK 85+. If your SDK is older,
// adjust the two clearly-marked lines.

using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public class OVRBodyV23LinkWorkaround : MonoBehaviour
{
    [Tooltip("If OVRBody.BodyState stays empty for this many seconds, try a manual restart via OVRPlugin (skipping fidelity request entirely).")]
    public float restartAfterSecondsWithoutBody = 3f;

    [Tooltip("Maximum number of manual restart attempts before giving up.")]
    public int maxManualRestartAttempts = 2;

    [Tooltip("Print a one-shot diagnostic when we detect the v2.3 failure mode.")]
    public bool logDiagnostic = true;

    [Tooltip("Optional: drag your OVRBody here. If left empty, the script will FindFirstObjectByType<OVRBody>() at Start.")]
    public OVRBody ovrBody;

    private float _timeWithoutBody;
    private int _restartAttempts;
    private bool _diagnosticLogged;

    private void Start()
    {
        if (ovrBody == null)
        {
#if UNITY_2022_2_OR_NEWER
            ovrBody = FindFirstObjectByType<OVRBody>();
#else
            ovrBody = FindObjectOfType<OVRBody>();
#endif
        }

        if (ovrBody == null)
        {
            Debug.LogWarning("[OVRBodyV23LinkWorkaround] No OVRBody found in scene; this script has nothing to watch.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (ovrBody == null || !ovrBody.isActiveAndEnabled) return;

        // Public API; matches what your provider checks
        // (the original log line was "ovrBody.BodyState has no value").
        bool gotBody = ovrBody.BodyState.HasValue;

        if (gotBody)
        {
            _timeWithoutBody = 0f;
            return;
        }

        _timeWithoutBody += Time.unscaledDeltaTime;

        if (_timeWithoutBody < restartAfterSecondsWithoutBody) return;
        if (_restartAttempts >= maxManualRestartAttempts) return;

        if (logDiagnostic && !_diagnosticLogged)
        {
            _diagnosticLogged = true;
            Debug.LogWarning(
                "[OVRBodyV23LinkWorkaround] No OVR body state received for " +
                $"{_timeWithoutBody:F1}s. This matches the Quest OS v2.3 / Link " +
                "regression in oculus-samples/Unity-Movement#136. Attempting a " +
                "manual restart via OVRPlugin (skipping RequestBodyTrackingFidelity).");
        }

        StartCoroutine(ManualRestartBodyTracking());
        _timeWithoutBody = 0f;
        _restartAttempts++;
    }

    private IEnumerator ManualRestartBodyTracking()
    {
        bool stopped = false;
        try { stopped = OVRPlugin.StopBodyTracking(); } catch { /* ignore */ }
        Debug.Log($"[OVRBodyV23LinkWorkaround] OVRPlugin.StopBodyTracking() -> {stopped}");

        yield return null;
        yield return null;

        bool started = false;
        try
        {
            // -------- ADJUST THIS LINE IF YOUR SDK USES A DIFFERENT ENUM --------
            var jointSet = OVRPlugin.BodyJointSet.FullBody;
            // SDK 65 lacks FullBody; use OVRPlugin.BodyJointSet.UpperBody if needed.
            // ---------------------------------------------------------------------
            started = OVRPlugin.StartBodyTracking2(jointSet);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[OVRBodyV23LinkWorkaround] Manual StartBodyTracking2 threw: {e.Message}");
            yield break;
        }

        Debug.Log($"[OVRBodyV23LinkWorkaround] Manual StartBodyTracking2 -> {started} " +
                  "(skipping RequestBodyTrackingFidelity).");
        // Intentionally NOT calling OVRPlugin.RequestBodyTrackingFidelity(...).
    }
}
