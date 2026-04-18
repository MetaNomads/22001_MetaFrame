using System.Collections.Generic;
using MetaFrame.Data;
using UnityEngine;
using UnityEngine.UI;

public class SurveyControl : MonoBehaviour
{
    // =========================================================================
    // Inspector fields
    // =========================================================================

    [Header("Survey Panels")]
    [Tooltip("Detection yes/no panel. Enabled/disabled externally via the event system.")]
    [SerializeField] private GameObject surveySdt;

    [Tooltip("Confidence panel. Enabled/disabled externally via the event system.")]
    [SerializeField] private GameObject surveyConfidence;

    [Tooltip("Plausibility panel. Enabled/disabled externally via the event system.")]
    [SerializeField] private GameObject surveyPlausibility;

    [Tooltip("Explanation panel. Enabled/disabled externally via the event system.")]
    [SerializeField] private GameObject surveyExplanation;

    [Header("Detection Toggles")]
    [SerializeField] private Toggle toggle_y;
    [SerializeField] private Toggle toggle_n;
    [SerializeField] private ToggleGroup detectionGroup;

    [Header("Confidence Toggle Group")]
    [SerializeField] private ToggleGroup confidenceGroup;

    [Header("Plausibility Toggle Group")]
    [SerializeField] private ToggleGroup plausibilityGroup;

    [Header("Explanation Toggle Group")]
    [SerializeField] private ToggleGroup explanationGroup;

    [Header("Survey Data")]
    [SerializeField] private SurveyDataRecorder surveyDataRecorder;

    // Auto-populated from the four groups at Awake — no manual drag needed.
    // Includes inactive toggles so panels disabled at startup are still reached.
    private readonly List<Toggle> allToggles = new();

    // =========================================================================
    // Lifecycle
    // =========================================================================

    private void Awake()
    {
        CollectToggles(detectionGroup);
        CollectToggles(confidenceGroup);
        CollectToggles(plausibilityGroup);
        CollectToggles(explanationGroup);
    }

    private void Start()
    {
        foreach (var t in allToggles)
        {
            if (t != null)
                t.onValueChanged.AddListener(_ => surveyDataRecorder.StartReport());
        }

        ClearSelection();
    }

    private void CollectToggles(ToggleGroup group)
    {
        if (group == null) return;

        // includeInactive: true so toggles on panels disabled at startup are reached
        var found = group.GetComponentsInChildren<Toggle>(includeInactive: true);
        foreach (var t in found)
        {
            if (t != null && t.group == group && !allToggles.Contains(t))
                allToggles.Add(t);
        }
    }

    // =========================================================================
    // Gate
    //
    //   surveySdt active          → detection must be answered
    //   surveyConfidence active   → confidence must be answered
    //   surveyPlausibility active → plausibility must be answered
    //   surveyExplanation active  → explanation must be answered
    // =========================================================================

    public bool CanProceed()
    {
        bool sdtActive = surveySdt != null && surveySdt.activeInHierarchy;
        bool confidenceActive = surveyConfidence != null && surveyConfidence.activeInHierarchy;
        bool plausibilityActive = surveyPlausibility != null && surveyPlausibility.activeInHierarchy;
        bool explanationActive = surveyExplanation != null && surveyExplanation.activeInHierarchy;

        if (sdtActive && !AnyTogglesOnInGroup(detectionGroup))
            return false;

        if (confidenceActive && !AnyTogglesOnInGroup(confidenceGroup))
            return false;

        if (plausibilityActive && !AnyTogglesOnInGroup(plausibilityGroup))
            return false;

        if (explanationActive && !AnyTogglesOnInGroup(explanationGroup))
            return false;

        return true;
    }

    // =========================================================================
    // Push / Capture — called from ExperimentController.Step()
    // =========================================================================

    public void Push()
    {
        surveyDataRecorder.SetDetection(GetToggleValue(detectionGroup));
        surveyDataRecorder.SetConfidence(GetToggleValue(confidenceGroup));
        surveyDataRecorder.SetPlausibility(GetToggleValue(plausibilityGroup));
        surveyDataRecorder.SetExplanation(GetToggleValue(explanationGroup));
    }

    public void Capture(ExperimentDataRecorder recorder)
    {
        recorder?.CaptureSurvey();
    }

    // =========================================================================
    // ClearSelection — resets all toggle state and visuals
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

        surveyDataRecorder.Reset();
    }

    public void ResetAllGroups() => ClearSelection();

    // =========================================================================
    // Helpers
    // =========================================================================

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