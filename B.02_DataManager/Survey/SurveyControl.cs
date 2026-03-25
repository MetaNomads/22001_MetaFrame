using System.Collections.Generic;
using MetaFrame.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SurveyControl : MonoBehaviour
{
    // =========================================================================
    // Gate types
    //
    //   None         — no requirement, always proceeds          (end panel)
    //   Confirmation — a toggle group must have a selection     (tutorial, break panels)
    //   Survey       — all required survey questions answered   (trial panel)
    // =========================================================================

    public enum GateType { None, Confirmation, Survey }

    // =========================================================================
    // Inspector fields
    // =========================================================================

    [Header("Panels")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private GameObject breakPanel;
    [SerializeField] private GameObject surveyPanel;
    [SerializeField] private GameObject experimentEndPanel;

    [Header("Plausibility Sub-Panel")]
    [Tooltip("Shown inside surveyPanel when detection = Yes and confidence is answered.")]
    [SerializeField] private GameObject plausibilityPanel;

    [Header("Confirmation Toggle Groups")]
    [SerializeField] private ToggleGroup tutorialGroup;
    [SerializeField] private ToggleGroup breakGroup;
    [SerializeField] private ToggleGroup endGroup;

    [Header("Survey Toggle Groups")]
    [SerializeField] private Toggle toggle_y;
    [SerializeField] private Toggle toggle_n;
    [SerializeField] private ToggleGroup detectionGroup;
    [SerializeField] private ToggleGroup confidenceGroup;
    [SerializeField] private ToggleGroup plausibilityGroup;

    [Header("All Toggles (drag every toggle here)")]
    [Tooltip("Drag every Toggle in the experiment into this list. " +
             "ClearSelection uses direct references so inactive toggles " +
             "(panels currently hidden) are always reached.")]
    [SerializeField] private List<Toggle> allToggles = new();

    [Header("Survey Data")]
    [SerializeField] private SurveyDataRecorder surveyDataRecorder;

    [Header("Setup Events")]
    [SerializeField] private UnityEvent onTutorialSetup;
    [SerializeField] private UnityEvent onBreakSetup;
    [SerializeField] private UnityEvent onSurveySetup;
    [SerializeField] private UnityEvent onExperimentEndSetup;

    // =========================================================================
    // Runtime state
    // =========================================================================

    private GateType    _gate               = GateType.None;
    private ToggleGroup _activeConfirmGroup = null;

    public GateType CurrentGate => _gate;

    // =========================================================================
    // Lifecycle
    // =========================================================================

    private void Start()
    {
        // Subscribe to all survey toggles so the first interaction stamps reportStart.
        // StartReport() is idempotent — only the first call per trial sets the time.
        // Reset() in ClearSelection() nulls _reportStart, re-arming it each trial.
        foreach (var t in allToggles)
        {
            if (t != null)
                t.onValueChanged.AddListener(_ => surveyDataRecorder.StartReport());
        }

        ClearSelection();
    }

    // =========================================================================
    // Gate
    // =========================================================================

    public bool CanProceed()
    {
        switch (_gate)
        {
            case GateType.None:
                return true;

            case GateType.Confirmation:
                if (_activeConfirmGroup == null)
                {
                    Debug.LogWarning("[SurveyControl] Cannot proceed — no confirmation group assigned.");
                    return false;
                }
                bool anyOn = AnyTogglesOnInGroup(_activeConfirmGroup);
                Debug.Log($"[SurveyControl] Confirmation check — group: '{_activeConfirmGroup.name}', AnyOn: {anyOn}");
                if (!anyOn) { Debug.LogWarning("[SurveyControl] Cannot proceed — make a selection."); return false; }
                return true;

            case GateType.Survey:
                return EvaluateSurveyGate();

            default:
                return true;
        }
    }

    private bool EvaluateSurveyGate()
    {
        if (!AnyTogglesOnInGroup(detectionGroup))
            { Debug.LogWarning("[SurveyControl] Detection not answered."); return false; }
        if (!AnyTogglesOnInGroup(confidenceGroup))
            { Debug.LogWarning("[SurveyControl] Confidence not answered."); return false; }
        if (toggle_y.isOn && !AnyTogglesOnInGroup(plausibilityGroup))
            { Debug.LogWarning("[SurveyControl] Plausibility not answered."); return false; }
        return true;
    }

    // =========================================================================
    // Setup methods — wire to GSM state OnEnter events
    //
    // ClearSelection is called from Step() after Advance() completes.
    // Setup methods only activate the correct panel — the panel opens clean
    // because ClearSelection already ran.
    // =========================================================================

    public void TutorialSetup()
    {
        SetActivePanel(tutorialPanel);
        _gate               = GateType.Confirmation;
        _activeConfirmGroup = tutorialGroup;
        onTutorialSetup?.Invoke();
    }

    public void BreakSetup()
    {
        SetActivePanel(breakPanel);
        _gate               = GateType.Confirmation;
        _activeConfirmGroup = breakGroup;
        onBreakSetup?.Invoke();
    }

    public void SurveySetup()
    {
        SetActivePanel(surveyPanel);
        _gate               = GateType.Survey;
        _activeConfirmGroup = null;
        onSurveySetup?.Invoke();
    }

    public void ExperimentEndSetup()
    {
        SetActivePanel(experimentEndPanel);
        _gate               = GateType.None;
        _activeConfirmGroup = null;
        onExperimentEndSetup?.Invoke();
    }

    // =========================================================================
    // Plausibility visibility — mid-trial, does not reset selection
    // =========================================================================

    public void OnDetectionChanged(bool _) => OnDetectionChanged();

    public void OnDetectionChanged()
    {
        if (plausibilityPanel == null) { Debug.LogWarning("[SurveyControl] plausibilityPanel not assigned."); return; }
        bool show = toggle_y != null && toggle_y.isOn;
        plausibilityPanel.SetActive(show);
        Debug.Log($"[SurveyControl] OnDetectionChanged — showing plausibility: {show}");
    }

    // =========================================================================
    // Push / Capture — called separately from Step()
    //
    // Push()    — snapshots toggle values into surveyDataRecorder while the
    //             panel is still visible. Call BEFORE Advance().
    // Capture() — commits the snapshot to the experiment record. Call AFTER
    //             Advance() succeeds so data is only recorded on a valid step.
    //
    // Splitting these means: if Advance() fails (GSM blocked), the snapshot
    // sits in surveyDataRecorder but is never committed. ClearSelection is
    // also skipped, so the UI remains unchanged and the participant can retry.
    // =========================================================================

    public void Push()
    {
        if (_gate != GateType.Survey) return;
        surveyDataRecorder.SetDetection(GetToggleValue(detectionGroup));
        surveyDataRecorder.SetConfidence(GetToggleValue(confidenceGroup));
        surveyDataRecorder.SetPlausibility(toggle_y.isOn ? GetToggleValue(plausibilityGroup) : null);
    }

    public void Capture(ExperimentDataRecorder recorder)
    {
        recorder?.CaptureSurvey();
    }

    // Kept for backwards compatibility if called elsewhere.
    public void PushAndCapture(ExperimentDataRecorder recorder)
    {
        Push();
        Capture(recorder);
    }

    // =========================================================================
    // ClearSelection — the single authoritative reset
    //
    // Called externally at the end of each trial. Resets all toggle state and
    // visuals across all panels, active or inactive.
    //
    // For each toggle in allToggles:
    //   1. group.allowSwitchOff = true  — let the group have nothing selected
    //   2. t.isOn = false               — clear selection state
    //      • Active toggle:   fires onValueChanged → OnToggleChanged(false) →
    //                         StopCustom (clears property block) + re-enables
    //                         oculusColorVisual via the original logic
    //      • Inactive toggle: no event fires (listener removed in OnDisable)
    //   3. ClearPropertyBlock()         — explicitly clears the renderer property
    //                                    block and resyncs currentColor on ALL
    //                                    toggles. For active ones this is a safe
    //                                    redundant cleanup. For inactive ones this
    //                                    is the primary visual reset since no
    //                                    event fired in step 2.
    //
    // After this, when a setup method activates a panel, OnEnable fires on each
    // ToggleOculusColorVisual → OnToggleChanged(false) → oculusColorVisual
    // re-enabled and drives the correct Normal state color (poke interaction
    // ended long before the next setup runs).
    // =========================================================================

    // =========================================================================
    // ClearSelection
    //
    // Uses isOn = false (full Unity Toggle.Set path) so group logic and isOn
    // state are always cleared correctly on both active and inactive toggles.
    //
    // SetClearing(true) is set before isOn = false so OnToggleChanged(false)
    // skips cycling oculusColorVisual — which would trigger Oculus.OnEnable
    // and re-apply the cached OVR Select state from a recent poke.
    // ClearPropertyBlock() then clears the ON color and re-enables
    // oculusColorVisual cleanly after the event has fired.
    // =========================================================================

    public void ClearSelection()
    {
        foreach (var t in allToggles)
        {
            if (t == null) continue;
            if (t.group != null) t.group.allowSwitchOff = true;

            var visual = t.GetComponent<ToggleOculusColorVisual>();
            if (visual != null) visual.SetClearing(true);
            t.isOn = false;
            if (visual != null)
            {
                visual.SetClearing(false);
                visual.ClearPropertyBlock();
            }
        }

        if (plausibilityPanel != null) plausibilityPanel.SetActive(false);
        surveyDataRecorder.Reset();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void SetActivePanel(GameObject target)
    {
        tutorialPanel?.SetActive(tutorialPanel == target);
        breakPanel?.SetActive(breakPanel == target);
        surveyPanel?.SetActive(surveyPanel == target);
        experimentEndPanel?.SetActive(experimentEndPanel == target);
    }

    public void ResetAllGroups() => ClearSelection();

    private static bool AnyTogglesOnInGroup(ToggleGroup group)
    {
        if (group == null) return false;
        return group.AnyTogglesOn();
    }

    private static string GetToggleValue(ToggleGroup group)
    {
        if (group == null) return null;
        foreach (Toggle t in group.ActiveToggles())
        {
            var id = t.GetComponent<ToggleID>();
            if (id != null) return id.value;
        }
        return null;
    }
}