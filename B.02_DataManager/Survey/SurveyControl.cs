using System.Collections.Generic;
using MetaFrame.Data;
using MetaFrame.State;
using UnityEngine;
using UnityEngine.UI;

public class SurveyControl : MonoBehaviour
{
    // =========================================================================
    // Stage tracking — questions are asked sequentially.
    //
    //   Idle         — no panels visible, survey not started
    //   Stage1_Q1Q2  — Q1 + Q2 + Continue (always shown together)
    //   Stage2_Q3    — Q3 + Continue       (always shown)
    //   Stage3_Q4    — Q4 + Continue       (shown only when Q3 == q3ShowQ4Value)
    //
    // Skip rule:
    //   • Q4 is shown only when Q3's answer equals q3ShowQ4Value (default: "2").
    //     Any other Q3 answer commits + steps after Q3.
    // =========================================================================

    private enum SurveyStage
    {
        Idle,
        Stage1_Q1Q2,
        Stage2_Q3,
        Stage3_Q4,
    }

    private SurveyStage _stage = SurveyStage.Idle;

    // =========================================================================
    // Inspector fields — Panels
    // =========================================================================

    [Header("Panels (all disabled at scene start)")]
    [SerializeField] private GameObject q1Panel;
    [SerializeField] private GameObject q2Panel;
    [SerializeField] private GameObject q3Panel;
    [SerializeField] private GameObject q4Panel;
    [SerializeField] private GameObject continueButton;

    // =========================================================================
    // Inspector fields — Toggle Groups
    // =========================================================================

    [Header("Toggle Groups (one per question)")]
    [SerializeField] private ToggleGroup q1Group;
    [SerializeField] private ToggleGroup q2Group;
    [SerializeField] private ToggleGroup q3Group;
    [SerializeField] private ToggleGroup q4Group;

    // =========================================================================
    // Inspector fields — Skip values
    // =========================================================================

    [Header("Skip Values")]
    [Tooltip("Q3 answer that REVEALS Q4. Any other Q3 answer commits + steps.\n" +
             "Must match the ToggleID.value on your Q3 toggle. Default: \"2\".")]
    [SerializeField] private string q3ShowQ4Value = "2";

    // =========================================================================
    // Inspector fields — References
    // =========================================================================

    [Header("References")]
    [SerializeField] private SurveyDataRecorder      surveyDataRecorder;
    [SerializeField] private ExperimentDataRecorder  experimentDataRecorder;
    [SerializeField] private ExperimentController    experimentController;

    // =========================================================================
    // Internals
    // =========================================================================

    private readonly List<Toggle> allToggles = new();

    // =========================================================================
    // Lifecycle
    // =========================================================================

    private void Awake()
    {
        CollectToggles(q1Group);
        CollectToggles(q2Group);
        CollectToggles(q3Group);
        CollectToggles(q4Group);
    }

    private void Start()
    {
        foreach (var t in allToggles)
        {
            if (t != null)
                t.onValueChanged.AddListener(_ => surveyDataRecorder?.StartReport());
        }

        // Defensive — panels should already be disabled in the scene, but make
        // sure we start in a known idle state.
        ClearSelection();
        HideAllPanels();
        _stage = SurveyStage.Idle;
    }

    private void CollectToggles(ToggleGroup group)
    {
        if (group == null) return;

        // includeInactive: true — panels start disabled, so toggles must be
        // reachable even when their parent panel is off.
        var found = group.GetComponentsInChildren<Toggle>(includeInactive: true);
        foreach (var t in found)
        {
            if (t != null && t.group == group && !allToggles.Contains(t))
                allToggles.Add(t);
        }
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Entry point — call this from an event binding (e.g. an onEnter UnityEvent
    /// when the GSM enters a survey state). Shows Q1, Q2, and the Continue button;
    /// hides everything else; clears any stale selection from a previous trial.
    /// </summary>
    public void BeginSurvey()
    {
        ClearSelection();
        ShowStage1();
    }

    /// <summary>
    /// Wire this to the Continue button's onClick event.
    /// Handles gatekeeping, recording, branching, and advancing the experiment.
    /// </summary>
    public void OnContinuePressed()
    {
        switch (_stage)
        {
            case SurveyStage.Stage1_Q1Q2: HandleStage1(); break;
            case SurveyStage.Stage2_Q3:   HandleStage2(); break;
            case SurveyStage.Stage3_Q4:   HandleStage3(); break;
            // SurveyStage.Idle — Continue pressed with no active survey. No-op.
        }
    }

    // =========================================================================
    // Stage Handlers
    //
    // Each handler:
    //   1. Gate-checks the panels currently shown (early-return if incomplete).
    //   2. Records the answers to SurveyDataRecorder.
    //   3. Either advances to the next stage or commits and steps.
    // =========================================================================

    private void HandleStage1()
    {
        // Gate: both Q1 and Q2 must be answered.
        if (!AnyTogglesOnInGroup(q1Group) || !AnyTogglesOnInGroup(q2Group)) return;

        string q1 = GetToggleValue(q1Group);
        string q2 = GetToggleValue(q2Group);

        // Record.
        surveyDataRecorder?.SetQ1(q1);
        surveyDataRecorder?.SetQ2(q2);

        // Always advance to Q3.
        ShowStage2();
    }

    private void HandleStage2()
    {
        // Gate: Q3 must be answered.
        if (!AnyTogglesOnInGroup(q3Group)) return;

        string q3 = GetToggleValue(q3Group);

        // Record.
        surveyDataRecorder?.SetQ3(q3);

        // Skip rule: Q4 is shown only when Q3 matches q3ShowQ4Value.
        if (q3 == q3ShowQ4Value)
            ShowStage3();
        else
            CommitAndStep();
    }

    private void HandleStage3()
    {
        // Gate: Q4 must be answered.
        if (!AnyTogglesOnInGroup(q4Group)) return;

        string q4 = GetToggleValue(q4Group);

        // Record.
        surveyDataRecorder?.SetQ4(q4);

        // End of survey.
        CommitAndStep();
    }

    // =========================================================================
    // Stage Transitions
    // =========================================================================

    private void ShowStage1()
    {
        SetActive(q1Panel,        true);
        SetActive(q2Panel,        true);
        SetActive(q3Panel,        false);
        SetActive(q4Panel,        false);
        SetActive(continueButton, true);
        _stage = SurveyStage.Stage1_Q1Q2;
    }

    private void ShowStage2()
    {
        SetActive(q1Panel,        false);
        SetActive(q2Panel,        false);
        SetActive(q3Panel,        true);
        SetActive(q4Panel,        false);
        SetActive(continueButton, true);
        _stage = SurveyStage.Stage2_Q3;
    }

    private void ShowStage3()
    {
        SetActive(q1Panel,        false);
        SetActive(q2Panel,        false);
        SetActive(q3Panel,        false);
        SetActive(q4Panel,        true);
        SetActive(continueButton, true);
        _stage = SurveyStage.Stage3_Q4;
    }

    private void HideAllPanels()
    {
        SetActive(q1Panel,        false);
        SetActive(q2Panel,        false);
        SetActive(q3Panel,        false);
        SetActive(q4Panel,        false);
        SetActive(continueButton, false);
    }

    // =========================================================================
    // Commit + Step
    //
    // CaptureSurvey() must run BEFORE Step() — Step() fires OnTrialEnded inside
    // sequencer.Advance(), which nulls _currentTrial in ExperimentDataRecorder.
    // After that, CaptureSurvey() silently exits because there's no trial to
    // attach to.
    // =========================================================================

    private void CommitAndStep()
    {
        // 1. Snapshot the staged answers into the current trial record.
        experimentDataRecorder?.CaptureSurvey();

        // 2. Advance the experiment.
        experimentController?.Step();

        // 3. Reset survey UI for the next trial.
        ClearSelection();
        HideAllPanels();
        _stage = SurveyStage.Idle;
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

        surveyDataRecorder?.Reset();
    }

    public void ResetAllGroups() => ClearSelection();

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active) go.SetActive(active);
    }

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
