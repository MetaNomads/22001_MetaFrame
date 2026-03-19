using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Body.Input;
using Oculus.Interaction.Input;

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

    // ── Public API ────────────────────────────────────────────────────────────

    public void TeleportToLeftHand()  => StartCoroutine(Teleport(leftHandInteractor,  BodyJointId.Body_LeftHandPalm));
    public void TeleportToRightHand() => StartCoroutine(Teleport(rightHandInteractor, BodyJointId.Body_RightHandPalm));

    // ── Internal ──────────────────────────────────────────────────────────────

    private IEnumerator Teleport(HandGrabInteractor interactor, BodyJointId palmJointId)
    {
        if (interactor == null)
        {
            Debug.LogError("[TeleportToHand] Interactor is not assigned.");
            yield break;
        }

        if (grabbable == null)
        {
            Debug.LogError("[TeleportToHand] Grabbable is not assigned.");
            yield break;
        }

        OnTeleportStart?.Invoke();

        // Release any existing grab on both hands first
        leftHandInteractor?.ForceRelease();
        rightHandInteractor?.ForceRelease();

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

        // Wait one FixedUpdate so physics registers the new position before ForceSelect
        yield return new WaitForFixedUpdate();

        HandGrabInteractable[] interactables =
            grabbable.GetComponentsInChildren<HandGrabInteractable>();

        HandGrabInteractable best = FindClosestInteractable(interactables, interactor, palmJointId);

        if (best == null)
        {
            Debug.LogError($"[TeleportToHand] No HandGrabInteractable found on '{grabbable.name}'.");
            yield break;
        }

        interactor.ForceSelect(best, useHandPose);

        OnTeleportEnd?.Invoke();

        Debug.Log($"[TeleportToHand] Teleported '{grabbable.name}' into {interactor.gameObject.name} " +
                  $"using '{best.gameObject.name}'.");
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

        // Try to find the hand-specific container by naming convention
        Handedness hand          = interactor.Hand.Handedness;
        string     containerName = hand == Handedness.Left
            ? "HandGrabInteractable_Left"
            : "HandGrabInteractable_Right";

        // Get the root instance (parent of all interactables)
        Transform root = interactables[0].transform.root;
        Transform container = root.Find(containerName);

        HandGrabInteractable[] candidates = container != null
            ? container.GetComponentsInChildren<HandGrabInteractable>()
            : interactables;   // fall back if naming convention not matched

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