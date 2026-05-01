using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Input;
using MetaFrame.Contracts;

// ── TeleportToHand ───────────────────────────────────────────────────────────
// Teleports an existing grabbable to the specified hand and force-selects it
// using the closest HandGrabInteractable to the palm pose.
//
// The grabbable must have:
//   - Grabbable on the root
//   - One or more HandGrabInteractable on children
//   - Optional HandGrabPose children for authored wrist poses

public class TeleportToHand : MonoBehaviour
{
    [SerializeField] private Grabbable           grabbable;
    [SerializeField] private HandGrabInteractor  leftHandInteractor;
    [SerializeField] private HandGrabInteractor  rightHandInteractor;

    [Tooltip("If true, snaps to the authored hand grab pose.\n" +
             "If false, attaches at the object's current world position.")]
    [SerializeField] private bool useHandPose = true;

    [Header("Body")]
    [Tooltip("Body component used to read palm joint poses.")]
    [SerializeField] private Body body;

    [Header("Events")]
    [Tooltip("Fires at the moment the teleport is initiated.")]
    public UnityEvent OnTeleportStart;

    [Tooltip("Fires after the object has been moved and grabbed by the hand.")]
    public UnityEvent OnTeleportEnd;

    // ── Runtime state ─────────────────────────────────────────────────────────

    // FIX (F-3): track the active Teleport coroutine so we can cancel it if a
    // second teleport request arrives before the first completes. Two parallel
    // coroutines would race on the same Rigidbody and the same interactor's
    // state machine, leaving the interactor in a corrupted Select state and
    // making subsequent grabs fail silently.
    private Coroutine _activeTeleport;

    // FIX (N-1): re-entrancy guard. OnTeleportStart fires synchronously during
    // the coroutine; a UnityEvent listener on it (e.g. AnomalyStateManager
    // .TriggerAnomaly cascading into onEnter UnityEvents) can call back into
    // TeleportToLeftHand/Right while the outer coroutine is mid-flight. Without
    // this guard, the inner StartTeleport would StopCoroutine the currently-
    // executing outer coroutine — terminating it at its next yield, leaving
    // OnTeleportEnd unfired and the rb.position/state transitions in an
    // unpredictable order with the inner coroutine.
    private bool _invokingStart;

    // Settle timeout for waiting on interactor state transitions. Generous
    // enough to cover Unselecting → Hovering → Normal under heavy frame load,
    // but bounded so a stuck interactor cannot hang the coroutine forever.
    private const float InteractorSettleTimeoutSeconds = 0.5f;

    // ── Public API ────────────────────────────────────────────────────────────

    public void TeleportToLeftHand()  => StartTeleport(leftHandInteractor,  BodyJointId.Body_LeftHandPalm);
    public void TeleportToRightHand() => StartTeleport(rightHandInteractor, BodyJointId.Body_RightHandPalm);

    // ── Validation (CONTRACT) ─────────────────────────────────────────────────
    private void OnValidate()
    {
        if (grabbable == null)
            Debug.LogWarning($"[TeleportToHand:{name}] grabbable is unassigned.", this);
        if (leftHandInteractor == null && rightHandInteractor == null)
            Debug.LogWarning(
                $"[TeleportToHand:{name}] both hand interactors are unassigned. " +
                "TeleportToLeft/RightHand will fail at runtime.", this);
    }

    private void StartTeleport(HandGrabInteractor interactor, BodyJointId palmJointId)
    {
        // FIX (N-1): refuse re-entrant calls that originate from inside an
        // OnTeleportStart UnityEvent. Stopping the outer coroutine while it
        // is still synchronously inside its own Invoke() would terminate it
        // at its next yield without ever firing OnTeleportEnd, while the
        // inner coroutine raced ahead with stale state.
        if (_invokingStart)
        {
            Debug.LogWarning(
                "[TeleportToHand] Re-entrant teleport request while OnTeleportStart " +
                "is still firing — ignored. Check what's wired into OnTeleportStart.");
            return;
        }

        // FIX (F-3): cancel any in-flight teleport before kicking off a new one.
        if (_activeTeleport != null)
        {
            StopCoroutine(_activeTeleport);
            _activeTeleport = null;
        }

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("[TeleportToHand] Cannot start — component is disabled.");
            return;
        }

        _activeTeleport = StartCoroutine(Teleport(interactor, palmJointId));
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private IEnumerator Teleport(HandGrabInteractor interactor, BodyJointId palmJointId)
    {
        if (interactor == null)
        {
            Debug.LogError("[TeleportToHand] Interactor is not assigned.");
            _activeTeleport = null;
            yield break;
        }

        if (grabbable == null)
        {
            Debug.LogError("[TeleportToHand] Grabbable is not assigned.");
            _activeTeleport = null;
            yield break;
        }

        // FIX (N-1): set the re-entrancy flag around the synchronous Invoke so
        // any callback that calls back into StartTeleport is rejected with a
        // clear log instead of corrupting this coroutine's lifetime.
        _invokingStart = true;
        try   { OnTeleportStart?.Invoke(); }
        finally { _invokingStart = false; }

        // FIX (F-2): only release the target hand, and only if it actually has
        // a selection. Force-releasing the *other* hand was gratuitous — it
        // would drop whatever the player was holding in their other hand and
        // could leave its interactable orphaned for a frame. Force-releasing
        // an interactor that's only Hovering (not Selecting) can also briefly
        // drop the hover candidate in some SDK versions.
        if (interactor.HasSelectedInteractable)
            interactor.ForceRelease();

        Vector3   palmPosition = GetPalmPosition(palmJointId, interactor.transform.position);
        Rigidbody rb           = grabbable.GetComponentInChildren<Rigidbody>();

        if (rb != null)
        {
            if (rb.isKinematic)
            {
                // For kinematic Rigidbodies, rb.position is immediate — no FixedUpdate needed.
                // MovePosition only interpolates visually and does NOT update rb.position
                // until the next FixedUpdate, which causes ForceSelect to see the old position.
                rb.position        = palmPosition;
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                // For non-kinematic Rigidbodies, zero velocity and teleport via transform.
                // MovePosition is for smooth movement, not instant teleport.
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position        = palmPosition;
            }
        }
        else
        {
            grabbable.transform.position = palmPosition;
        }

        Debug.Log($"[TeleportToHand] Moved to palm position {palmPosition}.");

        // Wait one FixedUpdate so physics registers the new position before ForceSelect.
        yield return new WaitForFixedUpdate();

        // FIX (F-1): the prior code did a single WaitForFixedUpdate after
        // ForceRelease and then immediately called ForceSelect. That is not
        // enough time for a HandGrabInteractor to traverse Unselecting →
        // Hovering → Normal. Calling ForceSelect mid-transition can leave the
        // previously-released interactable's selecting-pointables set stuck
        // with a phantom entry, AND leave the interactor itself in Select
        // state with no real selection — at which point natural hover→select
        // transitions on every other cup are silently blocked.
        //
        // We now poll the interactor state until it leaves Select/Disabled
        // (or a timeout elapses, so the coroutine can never hang).
        float settleStart = Time.realtimeSinceStartup;
        while (interactor.State == InteractorState.Select ||
               interactor.State == InteractorState.Disabled)
        {
            if (Time.realtimeSinceStartup - settleStart > InteractorSettleTimeoutSeconds)
            {
                Debug.LogWarning(
                    $"[TeleportToHand] Interactor '{interactor.gameObject.name}' did not " +
                    $"settle within {InteractorSettleTimeoutSeconds}s (state={interactor.State}). " +
                    "Proceeding with ForceSelect — selection may be unstable.");
                break;
            }
            yield return null;
        }

        // FIX (N-2): the settle loop polls per-frame (yield return null), but
        // Meta XR interactor state machines commit transitions on their own
        // FixedUpdate-driven update tick. A per-frame poll can observe the
        // state-change boundary while the SDK is still mid-commit. One extra
        // WaitForFixedUpdate gives the SDK a guaranteed physics tick to fully
        // commit Unselecting → Hovering / Normal before we ForceSelect. Cheap
        // insurance against the residual race that the per-frame poll cannot
        // close on its own.
        yield return new WaitForFixedUpdate();

        HandGrabInteractable[] interactables =
            grabbable.GetComponentsInChildren<HandGrabInteractable>();

        HandGrabInteractable best = FindClosestInteractable(interactables, interactor, palmJointId);

        if (best == null)
        {
            Debug.LogError($"[TeleportToHand] No HandGrabInteractable found on '{grabbable.name}'.");
            _activeTeleport = null;
            yield break;
        }

        // FIX (F-6): defensive sanity check. If a future refactor reintroduces
        // cross-talk between cups, this assertion surfaces it loudly instead
        // of silently force-selecting an interactable on a different object.
        if (!best.transform.IsChildOf(grabbable.transform))
        {
            Debug.LogError(
                $"[TeleportToHand] Chosen interactable '{best.name}' is NOT a descendant " +
                $"of grabbable '{grabbable.name}'. Aborting ForceSelect to avoid cross-grab corruption.");
            _activeTeleport = null;
            yield break;
        }

        interactor.ForceSelect(best, useHandPose);

        OnTeleportEnd?.Invoke();

        Debug.Log($"[TeleportToHand] Teleported '{grabbable.name}' into {interactor.gameObject.name} " +
                  $"using '{best.gameObject.name}'.");

        _activeTeleport = null;
    }

    // ── Pose Selection ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the best HandGrabInteractable for the given interactor.
    /// Looks for a child container named "HandGrabInteractable_Left" or
    /// "HandGrabInteractable_Right" matching the hand, then scores by
    /// closest authored pose to the palm. Falls back to all interactables
    /// if no named container is found.
    /// </summary>
    private HandGrabInteractable FindClosestInteractable(
        HandGrabInteractable[] interactables,
        HandGrabInteractor     interactor,
        BodyJointId            palmJointId)
    {
        if (interactables.Length == 0) return null;
        if (interactables.Length == 1) return interactables[0];

        // FIX (F-5): null-guard interactor.Hand. If body/hand tracking has
        // not bound yet (or has been lost mid-session), reading .Handedness
        // would throw NRE and silently abort the teleport mid-trial — a
        // determinism violation. Fall back to scoring across all interactables.
        HandGrabInteractable[] candidates = interactables;

        if (interactor.Hand != null)
        {
            // Try to find the hand-specific container by naming convention.
            Handedness hand          = interactor.Hand.Handedness;
            string     containerName = hand == Handedness.Left
                ? "HandGrabInteractable_Left"
                : "HandGrabInteractable_Right";

            // FIX (F-4): the previous code used interactables[0].transform.root,
            // which returns the SCENE-root topmost ancestor (e.g. a "Tableware"
            // parent shared by every cup), not this grabbable's own root. Combined
            // with Transform.Find — which only checks DIRECT children, not
            // descendants — the named-container heuristic almost never matched
            // and silently fell through to the unscoped interactables list.
            // In a hierarchy where multiple grabbables share an ancestor that DID
            // happen to contain a child of the same name, the lookup would resolve
            // to a sibling object's container and ForceSelect would grab the wrong
            // object. Scope to the grabbable's own subtree, and search recursively.
            Transform container = FindDescendant(grabbable.transform, containerName);

            if (container != null)
            {
                var scoped = container.GetComponentsInChildren<HandGrabInteractable>();
                if (scoped.Length > 0) candidates = scoped;
            }
        }
        else
        {
            Debug.LogWarning(
                "[TeleportToHand] interactor.Hand is null — falling back to " +
                "unscoped interactable scoring. Hand tracking may not be bound yet.");
        }

        if (candidates.Length == 0) candidates = interactables;
        if (candidates.Length == 1) return candidates[0];

        // Score by closest authored pose to the palm
        Vector3 gripPos = GetPalmPosition(palmJointId, interactor.transform.position);

        HandGrabInteractable best     = null;
        float                bestDist = float.MaxValue;

        foreach (var interactable in candidates)
        {
            float dist = ClosestPoseDistance(interactable, gripPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best     = interactable;
            }
        }

        return best;
    }

    /// <summary>
    /// Recursive name-based descendant search, scoped to a single subtree.
    /// Used in place of Transform.Find (which only searches direct children)
    /// so the named-container heuristic actually matches when the container
    /// sits more than one level deep under the grabbable.
    /// </summary>
    private static Transform FindDescendant(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
            Transform deeper = FindDescendant(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }

    /// <summary>
    /// Returns the world position of the requested body palm joint.
    /// Falls back to <paramref name="fallback"/> if body tracking is unavailable.
    /// </summary>
    private Vector3 GetPalmPosition(BodyJointId jointId, Vector3 fallback)
    {
        if (body == null || !body.IsConnected || !body.IsTrackedDataValid)
            return fallback;

        if (body.GetJointPose(jointId, out Pose pose))
            return pose.position;

        return fallback;
    }

    /// <summary>
    /// Measures the closest authored HandGrabPose transform to the wrist.
    /// If no poses exist, falls back to the interactable's own transform.
    /// </summary>
    private float ClosestPoseDistance(HandGrabInteractable interactable, Vector3 reference)
    {
        HandGrabPose[] poses = interactable.GetComponentsInChildren<HandGrabPose>();

        if (poses.Length == 0)
            return Vector3.Distance(reference, interactable.transform.position);

        float closest = float.MaxValue;
        foreach (var pose in poses)
            closest = Mathf.Min(closest, Vector3.Distance(reference, pose.transform.position));

        return closest;
    }
}

// ── Editor ────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TeleportToHand))]
public class SpawnInHandEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var  spawner    = (TeleportToHand)target;
        bool inPlayMode = Application.isPlaying;

        UnityEditor.EditorGUILayout.Space(10);
        UnityEditor.EditorGUILayout.LabelField("Debug", UnityEditor.EditorStyles.boldLabel);

        GUI.enabled = inPlayMode;

        UnityEditor.EditorGUILayout.BeginHorizontal();

        GUI.color = new Color(0.4f, 0.75f, 1f);
        if (GUILayout.Button("Teleport to Left Hand", GUILayout.Height(28)))
            spawner.TeleportToLeftHand();

        GUI.color = new Color(0.4f, 1f, 0.6f);
        if (GUILayout.Button("Teleport to Right Hand", GUILayout.Height(28)))
            spawner.TeleportToRightHand();

        GUI.color   = Color.white;
        GUI.enabled = true;

        UnityEditor.EditorGUILayout.EndHorizontal();

        if (!inPlayMode)
            UnityEditor.EditorGUILayout.HelpBox(
                "Enter Play Mode to use debug buttons.", UnityEditor.MessageType.None);
    }
}
#endif