using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

// ── CollisionTrigger ──────────────────────────────────────────────────────────
// OnEnter        — fires when a collider object enters a zone.
// OnExit         — fires when a collider object leaves a zone.
// OnEvaluate     — fires when a collision is detected while evaluating.
// OnFinishAction — fires after OnEvaluate once all registered async actions complete.
//
// Each event has its own "Once" toggle. When true, that event fires once
// then goes silent until ResetTrigger() is called.

public class CollisionTrigger : MonoBehaviour
{
    [Header("Objects")]
    [Tooltip("GameObjects whose colliders act as trigger zones.")]
    public List<GameObject> colliderZones = new();

    [Tooltip("GameObjects that must be touched to fire events.")]
    public List<GameObject> colliderObjects = new();

    [Header("On Enter")]
    public UnityEvent OnEnter;
    [Tooltip("If true, OnEnter fires once then goes silent until ResetTrigger().")]
    public bool OnEnterOnce;

    [Header("On Exit")]
    public UnityEvent OnExit;
    [Tooltip("If true, OnExit fires once then goes silent until ResetTrigger().")]
    public bool OnExitOnce;

    [Header("On Evaluate")]
    public UnityEvent OnEvaluate;
    [Tooltip("If true, OnEvaluate fires once then goes silent until ResetTrigger().")]
    public bool OnEvaluateOnce;

    [Header("On Finish Action")]
    public UnityEvent OnFinishAction;
    [Tooltip("If true, OnFinishAction fires once then goes silent until ResetTrigger().")]
    public bool OnFinishActionOnce;

    // Runtime spent flags — not serialized
    private bool _enterSpent;
    private bool _exitSpent;
    private bool _evaluateSpent;
    private bool _finishSpent;

    private enum EvalMode { None, Once, Continuous }
    private EvalMode _evalMode = EvalMode.None;
    private int _pendingActions = 0;

    private readonly List<CollisionListener> _listeners = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void FireEnter()
    {
        if (_enterSpent) return;
        if (OnEnterOnce) _enterSpent = true;
        OnEnter?.Invoke();
    }

    private void FireExit()
    {
        if (_exitSpent) return;
        if (OnExitOnce) _exitSpent = true;
        OnExit?.Invoke();
    }

    private void FireEvaluate()
    {
        if (_evaluateSpent) return;
        if (OnEvaluateOnce) _evaluateSpent = true;
        OnEvaluate?.Invoke();
    }

    private void FireFinishAction()
    {
        if (_finishSpent) return;
        if (OnFinishActionOnce) _finishSpent = true;
        OnFinishAction?.Invoke();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (colliderZones.Count == 0 || colliderObjects.Count == 0) return;

        foreach (var zone in colliderZones)
        {
            if (zone == null) continue;

            foreach (var col in zone.GetComponentsInChildren<Collider>(true))
            {
                var listener = col.gameObject.AddComponent<CollisionListener>();
                listener.Init(colliderObjects, OnContactEnter, OnContactStay, OnContactExit);
                _listeners.Add(listener);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (var l in _listeners)
            if (l != null) Destroy(l);
        _listeners.Clear();
    }

    // ── Evaluate API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Arm for a single evaluation. Fires OnEvaluate on the next contact then disarms.
    /// </summary>
    public void EvaluateOnce()
    {
        _evalMode = EvalMode.Once;
    }

    /// <summary>
    /// Keep evaluating on every contact and stay event until StopEvaluating() is called.
    /// </summary>
    public void EvaluateContinuous()
    {
        _evalMode = EvalMode.Continuous;
    }

    /// <summary>Stop evaluating without firing.</summary>
    public void StopEvaluating()
    {
        _evalMode = EvalMode.None;
    }

    /// <summary>Reset all events so they can fire again.</summary>
    public void ResetTrigger()
    {
        _enterSpent = false;
        _exitSpent = false;
        _evaluateSpent = false;
        _finishSpent = false;
        _pendingActions = 0;
        _evalMode = EvalMode.None;
    }

    /// <summary>Spend all events so nothing fires until ResetTrigger().</summary>
    public void SpendTrigger()
    {
        _enterSpent = true;
        _exitSpent = true;
        _evaluateSpent = true;
        _finishSpent = true;
    }

    // ── Pending Action API ────────────────────────────────────────────────────

    /// <summary>
    /// Wire to OnEvaluate. Starts a tracked async action — OnFinishAction
    /// waits until all registered actions call CompleteAction().
    /// </summary>
    public void RunTracked(MonoBehaviour source)
    {
        if (source is not ITrackableAction action) return;
        RegisterPendingAction();
        action.Run(CompleteAction);
    }

    /// <summary>Manually register an async action before it starts.</summary>
    public void RegisterPendingAction() => _pendingActions++;

    /// <summary>Signal that a registered async action finished.</summary>
    public void CompleteAction()
    {
        if (_pendingActions <= 0) return;

        _pendingActions--;

        if (_pendingActions == 0)
            FireFinishAction();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void OnContactEnter()
    {
        FireEnter();
        TryEvaluate();
    }

    private void OnContactStay()
    {
        if (_evalMode == EvalMode.None) return;
        TryEvaluate();
    }

    private void OnContactExit()
    {
        FireExit();
    }

    private void TryEvaluate()
    {
        if (_evalMode == EvalMode.None) return;
        if (_evaluateSpent) return;

        if (_evalMode == EvalMode.Once)
            _evalMode = EvalMode.None;

        // FIX: Reset _pendingActions to -1 as a sentinel so that any
        // RegisterPendingAction() calls made synchronously inside FireEvaluate()
        // land correctly. After FireEvaluate() returns we check whether anything
        // was registered — if not, fire OnFinishAction immediately.
        _pendingActions = 0;
        FireEvaluate();

        // Only auto-fire OnFinishAction when no async actions were registered
        // during the FireEvaluate() call. If actions were registered, they will
        // call CompleteAction() themselves and OnFinishAction fires from there.
        if (_pendingActions == 0)
            FireFinishAction();
    }
}

// ── CollisionListener ─────────────────────────────────────────────────────────

[AddComponentMenu("")]
public class CollisionListener : MonoBehaviour
{
    // FIX: Use a HashSet built at Init() time (including all children of each
    // target root) so IsTarget() is an O(1) lookup instead of an O(n*depth)
    // walk on every Stay physics callback.
    private HashSet<GameObject> _targetSet;

    private System.Action _onEnter;
    private System.Action _onStay;
    private System.Action _onExit;

    public void Init(List<GameObject> targets,
                     System.Action onEnter,
                     System.Action onStay,
                     System.Action onExit)
    {
        // Build the flat HashSet once, including every child of every target root.
        _targetSet = new HashSet<GameObject>();
        foreach (var root in targets)
        {
            if (root == null) continue;
            _targetSet.Add(root);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                _targetSet.Add(child.gameObject);
        }

        _onEnter = onEnter;
        _onStay = onStay;
        _onExit = onExit;
    }

    private void OnCollisionEnter(Collision c)
    {
        if (!IsTarget(c.transform)) return;
        _onEnter?.Invoke();
    }

    private void OnCollisionStay(Collision c)
    {
        if (IsTarget(c.transform)) _onStay?.Invoke();
    }

    private void OnCollisionExit(Collision c)
    {
        if (!IsTarget(c.transform)) return;
        _onExit?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsTarget(other.transform)) return;
        _onEnter?.Invoke();
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsTarget(other.transform)) _onStay?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsTarget(other.transform)) return;
        _onExit?.Invoke();
    }

    // O(1) — single HashSet lookup replacing the old O(n*depth) list walk.
    private bool IsTarget(Transform t) =>
        t != null && _targetSet != null && _targetSet.Contains(t.gameObject);
}