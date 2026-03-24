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

    // ── Confirmation toggle groups ─────────────────────────────────────────
    // Each boundary panel has its own toggle group.
    // The participant must select something before the physical button proceeds.
    // End panel has a group too but it has no gate — it is display only.

    [Header("Confirmation Toggle Groups")]
    [Tooltip("Toggle group on the tutorial panel. Must have a selection to proceed.")]
    [SerializeField] private ToggleGroup tutorialGroup;

    [Tooltip("Toggle group on the break panel. Must have a selection to proceed.")]
    [SerializeField] private ToggleGroup breakGroup;

    [Tooltip("Toggle group on the end panel. Display only — no gate.")]
    [SerializeField] private ToggleGroup endGroup;

    // ── Survey toggle groups ───────────────────────────────────────────────

    [Header("Survey Toggle Groups")]
    [SerializeField] private Toggle      toggle_y;          // detection = Yes
    [SerializeField] private Toggle      toggle_n;          // detection = No
    [SerializeField] private ToggleGroup detectionGroup;
    [SerializeField] private ToggleGroup confidenceGroup;
    [SerializeField] private ToggleGroup plausibilityGroup;

    [Header("All Toggle Groups (for reset)")]
    [SerializeField] private List<ToggleGroup> allGroups = new();

    [Header("Survey Data")]
    [SerializeField] private SurveyDataRecorder surveyDataRecorder;

    [Header("Setup Events — fire after each setup completes")]
    [SerializeField] private UnityEvent onTutorialSetup;
    [SerializeField] private UnityEvent onBreakSetup;
    [SerializeField] private UnityEvent onSurveySetup;
    [SerializeField] private UnityEvent onExperimentEndSetup;

    // =========================================================================
    // Runtime state
    // =========================================================================

    private GateType    _gate               = GateType.None;
    private ToggleGroup _activeConfirmGroup = null;  // which group the current confirmation gate checks

    public GateType CurrentGate => _gate;

    // =========================================================================
    // Single proceed gate — ExperimentController calls only this
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
                    Debug.LogWarning("[SurveyControl] Cannot proceed — no confirmation group assigned for this panel.");
                    return false;
                }
                bool anyOn = AnyTogglesOnInGroup(_activeConfirmGroup);
                Debug.Log($"[SurveyControl] Confirmation check — group: '{_activeConfirmGroup.name}', AnyOn: {anyOn}");
                if (!anyOn)
                {
                    Debug.LogWarning("[SurveyControl] Cannot proceed — please make a selection on the panel.");
                    return false;
                }
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
        {
            Debug.LogWarning("[SurveyControl] Cannot proceed — detection not answered.");
            return false;
        }
        if (!AnyTogglesOnInGroup(confidenceGroup))
        {
            Debug.LogWarning("[SurveyControl] Cannot proceed — confidence not answered.");
            return false;
        }
        if (toggle_y.isOn && !AnyTogglesOnInGroup(plausibilityGroup))
        {
            Debug.LogWarning("[SurveyControl] Cannot proceed — plausibility not answered (required when detection = Yes).");
            return false;
        }
        return true;
    }

    // =========================================================================
    // Setup methods — wire to GSM state OnEnter events
    // =========================================================================

    /// <summary>
    /// Gate: Confirmation via tutorialGroup.
    /// Participant must select a toggle before the physical button proceeds.
    /// Wire to GSM tutorial state OnEnter.
    /// </summary>
    public void TutorialSetup()
    {
        _gate               = GateType.Confirmation;
        _activeConfirmGroup = tutorialGroup;
        SetActivePanel(tutorialPanel);
        onTutorialSetup?.Invoke();
    }

    /// <summary>
    /// Gate: Confirmation via breakGroup.
    /// Participant must select a toggle before the physical button proceeds.
    /// Wire to GSM idle (break) state OnEnter.
    /// </summary>
    public void BreakSetup()
    {
        _gate               = GateType.Confirmation;
        _activeConfirmGroup = breakGroup;
        SetActivePanel(breakPanel);
        onBreakSetup?.Invoke();
    }

    /// <summary>
    /// Gate: Survey — detection + confidence always required;
    ///               plausibility required when detection = Yes.
    /// Wire to GSM trial-start state OnEnter.
    /// </summary>
    public void SurveySetup()
    {
        _gate               = GateType.Survey;
        _activeConfirmGroup = null;
        surveyDataRecorder.StartReport();
        SetActivePanel(surveyPanel);
        onSurveySetup?.Invoke();
    }

    /// <summary>
    /// Gate: None — physical button cannot proceed (experiment is over).
    /// endGroup is shown for display only, no gate check.
    /// Wire to GSM experiment-end state OnEnter.
    /// </summary>
    public void ExperimentEndSetup()
    {
        _gate               = GateType.None;
        _activeConfirmGroup = null;
        SetActivePanel(experimentEndPanel);
        onExperimentEndSetup?.Invoke();
    }

    // =========================================================================
    // Plausibility sub-panel visibility
    // Wire toggle_y and toggle_n OnValueChanged to OnDetectionChanged.
    // In the Inspector, select "Dynamic bool" so the toggle value is passed in.
    // =========================================================================

    /// <summary>Dynamic bool — wire directly to toggle_y and toggle_n OnValueChanged.</summary>
    public void OnDetectionChanged(bool _)
    {
        OnDetectionChanged();
    }

    public void OnDetectionChanged()
    {
        if (plausibilityPanel == null)
        {
            Debug.LogWarning("[SurveyControl] plausibilityPanel is not assigned.");
            return;
        }
        bool show = toggle_y != null && toggle_y.isOn;
        plausibilityPanel.SetActive(show);
        Debug.Log($"[SurveyControl] OnDetectionChanged — toggle_y.isOn={toggle_y?.isOn}, showing plausibility: {show}");
    }

    // =========================================================================
    // Data push — called by ExperimentController just before CaptureSurvey()
    // =========================================================================

    public void PushToRecorder()
    {
        surveyDataRecorder.SetDetection(GetToggleValue(detectionGroup));
        surveyDataRecorder.SetConfidence(GetToggleValue(confidenceGroup));
        surveyDataRecorder.SetPlausibility(toggle_y.isOn ? GetToggleValue(plausibilityGroup) : null);
    }

    // =========================================================================
    // ClearSelection — resets all toggle groups and survey data after proceed.
    // Every referenced group is reset explicitly — no reliance on allGroups list.
    // =========================================================================

    public void ClearSelection()
    {
        ResetGroup(tutorialGroup);
        ResetGroup(breakGroup);
        ResetGroup(endGroup);
        ResetGroup(detectionGroup);
        ResetGroup(confidenceGroup);
        ResetGroup(plausibilityGroup);

        if (plausibilityPanel != null) plausibilityPanel.SetActive(false);
        surveyDataRecorder.Reset();

        Debug.Log("[SurveyControl] All selections cleared.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ResetGroup
    //
    // Two problems solved here:
    //
    // 1. Hierarchy bleed — GetComponentsInChildren finds ALL toggles under the
    //    group's GameObject, including those belonging to sibling groups if the
    //    hierarchy nests them. We guard with t.group == group so we only touch
    //    toggles actually registered to THIS group.
    //
    // 2. allowSwitchOff — when false (Unity default), setting every toggle to
    //    isOn=false causes Unity to snap the last one back to true, leaving a
    //    ghost selection. We temporarily enable allowSwitchOff to permit a
    //    fully-empty state, then restore the original value.
    // ─────────────────────────────────────────────────────────────────────────
    private static void ResetGroup(ToggleGroup group)
    {
        if (group == null) return;
        bool prev = group.allowSwitchOff;
        group.allowSwitchOff = true;
        foreach (Toggle t in group.GetComponentsInChildren<Toggle>())
            if (t != null && t.group == group) t.isOn = false;
        group.allowSwitchOff = prev;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void SetActivePanel(GameObject target)
    {
        tutorialPanel?     .SetActive(tutorialPanel      == target);
        breakPanel?        .SetActive(breakPanel         == target);
        surveyPanel?       .SetActive(surveyPanel        == target);
        experimentEndPanel?.SetActive(experimentEndPanel == target);
    }

    public void ResetAllGroups()
    {
        foreach (var group in allGroups)
            ResetGroup(group);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AnyTogglesOnInGroup / GetToggleValue
    //
    // Previously used GetComponentsInChildren<Toggle>() on the ToggleGroup
    // GameObject, which traverses the entire child hierarchy. When detection
    // and confidence groups live inside the same parent panel, both methods
    // would find each other's toggles — causing a confidence answer of "4"
    // to appear as the detection value as well.
    //
    // Fix: use Unity's ToggleGroup APIs instead:
    //   • group.AnyTogglesOn()   — true only if a toggle registered to THIS
    //                              group is currently on.
    //   • group.ActiveToggles()  — enumerates only on-toggles registered to
    //                              THIS group, regardless of scene hierarchy.
    //
    // These work by group registration (each Toggle's m_Group field), so
    // they are immune to nesting. The only requirement is that each Toggle's
    // Group field is correctly set in the Inspector — which it must be for
    // Unity's mutual-exclusion behaviour to work anyway.
    // ─────────────────────────────────────────────────────────────────────────

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