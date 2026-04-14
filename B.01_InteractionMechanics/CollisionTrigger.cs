using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

// ── CollisionTrigger ──────────────────────────────────────────────────────────
// OnEnter        — fires when the FIRST target collider enters any zone.
// OnExit         — fires when the LAST target collider leaves all zones.
// OnEvaluate     — fires based on which evaluate function was called (see below).
// OnFinishAction — fires after OnEvaluate once all registered async actions complete.
//
// ── Evaluate functions ────────────────────────────────────────────────────────
//   EvaluateNow()
//     Immediate snapshot. Fires if currently intersecting, does nothing if not.
//     No arming, no lasting state. Call any time.
//
//   EvaluateFirstContact()
//     Arms and waits. Fires once on the next collision, then auto-disarms.
//     If already intersecting when called: fires immediately.
//     Re-call to arm again after it has fired.
//
//   EvaluateEveryContact()
//     Arms and stays armed. Fires on every new collision (same as OnEnter logic)
//     until StopEvaluating() is called.
//     StopEvaluating() then re-calling EvaluateEveryContact() resets and re-arms.
//
//   StopEvaluating() — disarms EvaluateEveryContact() without firing.
//
// ── OnEnter / OnExit ──────────────────────────────────────────────────────────
// Contact counting: OnEnter fires on 0→1, OnExit fires on n→0.
// Entering with two overlapping colliders = one enter; OnExit fires only when
// both have left.
//
// ── First Contact Only toggles ────────────────────────────────────────────────
// OnEnter and OnExit each have a firstContactOnly toggle:
//   Off — fires every time the condition is met.
//   On  — fires only the first time, then silent until ResetTrigger().
//
// ── Zone / Target inputs ──────────────────────────────────────────────────────
// Both accept direct Collider references and GameObjects.
// GameObjects are expanded once at Start() — all child colliders are registered
// into a HashSet so every runtime lookup is O(1).

public class CollisionTrigger : MonoBehaviour
{
    [Header("Zones")]
    [Tooltip("Specific colliders that act as trigger zones.")]
    public List<Collider> zoneColliders = new();

    [Tooltip("GameObjects whose colliders (incl. children) act as trigger zones.")]
    public List<GameObject> zoneGameObjects = new();

    [Header("Objects")]
    [Tooltip("Specific colliders that must be touched to fire events.")]
    public List<Collider> triggerColliders = new();

    [Tooltip("GameObjects whose colliders (incl. children) must be touched to fire events.")]
    public List<GameObject> triggerGameObjects = new();

    [Header("On Enter")]
    public UnityEvent OnEnter;
    [Tooltip("Off — fires each time targets re-enter after fully leaving.\n" +
             "On  — fires only the first contact, then silent until ResetTrigger().")]
    public bool onEnterFirstContactOnly;

    [Header("On Exit")]
    public UnityEvent OnExit;
    [Tooltip("Off — fires each time all targets leave after entering.\n" +
             "On  — fires only the first time all targets leave, then silent until ResetTrigger().")]
    public bool onExitFirstContactOnly;

    [Header("On Evaluate")]
    public UnityEvent OnEvaluate;

    [Header("On Finish Action")]
    public UnityEvent OnFinishAction;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private readonly HashSet<Collider> _activeContacts = new();

    private bool _enterSpent;
    private bool _exitSpent;

    private enum EvalMode { None, FirstContact, EveryContact }
    private EvalMode _evalMode = EvalMode.None;

    private int _pendingActions = 0;

    private readonly List<CollisionListener> _listeners = new();

    // Stored at Start() for direct physics queries in EvaluateNow().
    private List<Collider> _resolvedZones   = new();
    private List<Collider> _resolvedTargets = new();

    // Reused by GetCurrentOverlaps() to avoid per-call allocation.
    private readonly HashSet<Collider> _overlapScratch = new();

    // ── Fire helpers ──────────────────────────────────────────────────────────

    private void FireEnter()
    {
        if (_enterSpent) return;
        if (onEnterFirstContactOnly) _enterSpent = true;
        OnEnter?.Invoke();
    }

    private void FireExit()
    {
        if (_exitSpent) return;
        if (onExitFirstContactOnly) _exitSpent = true;
        OnExit?.Invoke();
    }

    // Fires OnEvaluate and chains into OnFinishAction.
    // autoDisarm: switches eval mode back to None after firing (used by FirstContact).
    private void RunEvaluate(bool autoDisarm)
    {
        if (autoDisarm) _evalMode = EvalMode.None;

        _pendingActions = 0;
        OnEvaluate?.Invoke();

        if (_pendingActions == 0)
            OnFinishAction?.Invoke();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        var targetSet = new HashSet<Collider>();

        foreach (var col in triggerColliders)
        {
            if (col == null) { Debug.LogWarning($"[CollisionTrigger:{name}] Null entry in triggerColliders — skipped."); continue; }
            targetSet.Add(col);
            _resolvedTargets.Add(col);
        }

        foreach (var go in triggerGameObjects)
        {
            if (go == null) { Debug.LogWarning($"[CollisionTrigger:{name}] Null entry in triggerGameObjects — skipped."); continue; }
            var cols = go.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
                Debug.LogWarning($"[CollisionTrigger:{name}] triggerGameObject '{go.name}' has no colliders in hierarchy.");
            foreach (var col in cols)
            {
                targetSet.Add(col);
                _resolvedTargets.Add(col);
            }
        }

        if (targetSet.Count == 0)
        {
            Debug.LogWarning($"[CollisionTrigger:{name}] No trigger colliders found — component will not fire any events.");
            return;
        }

        foreach (var zone in zoneColliders)
        {
            if (zone == null) { Debug.LogWarning($"[CollisionTrigger:{name}] Null entry in zoneColliders — skipped."); continue; }
            AddListener(zone, targetSet);
        }

        foreach (var go in zoneGameObjects)
        {
            if (go == null) { Debug.LogWarning($"[CollisionTrigger:{name}] Null entry in zoneGameObjects — skipped."); continue; }
            var cols = go.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
                Debug.LogWarning($"[CollisionTrigger:{name}] zoneGameObject '{go.name}' has no colliders in hierarchy.");
            foreach (var col in cols)
                AddListener(col, targetSet);
        }

        if (_listeners.Count == 0)
            Debug.LogWarning($"[CollisionTrigger:{name}] No zone colliders found — component will not detect any contacts.");
    }

    private void AddListener(Collider zone, HashSet<Collider> targetSet)
    {
        _resolvedZones.Add(zone);

        // Destroy any stale CollisionListener left over from a previous run.
        var existing = zone.gameObject.GetComponent<CollisionListener>();
        if (existing != null) Destroy(existing);

        var listener = zone.gameObject.AddComponent<CollisionListener>();
        listener.Init(targetSet, OnContactEnter, OnContactExit);
        _listeners.Add(listener);
    }

    private void OnDestroy()
    {
        foreach (var l in _listeners)
            if (l != null) Destroy(l);
        _listeners.Clear();
    }

    // ── Evaluate API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Immediate snapshot check using a direct physics query.
    /// Fires OnEvaluate if any zone and target collider are currently overlapping,
    /// regardless of whether disable/re-enable events have been processed yet.
    /// Does nothing if no overlap is found. No arming, no lasting state.
    /// </summary>
    public void EvaluateNow()
    {
        if (HasAnyOverlap())
            RunEvaluate(autoDisarm: false);
    }

    /// <summary>
    /// Arms and fires OnEvaluate on the next collision, then auto-disarms.
    /// If already intersecting when called (including after a disable/re-enable):
    /// fires immediately via physics query. Re-call to arm again after it has fired.
    /// </summary>
    public void EvaluateFirstContact()
    {
        _evalMode = EvalMode.FirstContact;

        var overlaps = GetCurrentOverlaps();
        if (overlaps.Count > 0)
        {
            // Sync _activeContacts so the incoming OnTriggerEnter (next physics tick)
            // is seen as a duplicate and does not double-fire.
            foreach (var col in overlaps) _activeContacts.Add(col);
            RunEvaluate(autoDisarm: true);
        }
    }

    /// <summary>
    /// Arms and fires OnEvaluate on every new collision (same as OnEnter logic)
    /// until StopEvaluating() is called.
    /// If already intersecting when called (including after a disable/re-enable):
    /// fires immediately via physics query. Re-calling after StopEvaluating() re-arms cleanly.
    /// </summary>
    public void EvaluateEveryContact()
    {
        _evalMode = EvalMode.EveryContact;

        var overlaps = GetCurrentOverlaps();
        if (overlaps.Count > 0)
        {
            // Sync _activeContacts so the incoming OnTriggerEnter (next physics tick)
            // is seen as a duplicate and does not double-fire.
            foreach (var col in overlaps) _activeContacts.Add(col);
            RunEvaluate(autoDisarm: false);
        }
    }

    /// <summary>Disarms EvaluateEveryContact() without firing.</summary>
    public void StopEvaluating()
    {
        _evalMode = EvalMode.None;
    }

    // Short-circuit bool check — returns as soon as any overlap is found.
    // Used by EvaluateNow which only needs to know if overlap exists, not which ones.
    private bool HasAnyOverlap()
    {
        foreach (var zone in _resolvedZones)
        {
            if (zone == null || !zone.enabled) continue;
            foreach (var target in _resolvedTargets)
            {
                if (target == null || !target.enabled) continue;
                if (Physics.ComputePenetration(
                        zone,   zone.transform.position,   zone.transform.rotation,
                        target, target.transform.position, target.transform.rotation,
                        out _, out _))
                    return true;
            }
        }
        return false;
    }

    // Returns all currently overlapping target colliders into _overlapScratch.
    // Reuses the same HashSet each call — caller must not hold onto the reference.
    private HashSet<Collider> GetCurrentOverlaps()
    {
        _overlapScratch.Clear();
        foreach (var zone in _resolvedZones)
        {
            if (zone == null || !zone.enabled) continue;
            foreach (var target in _resolvedTargets)
            {
                if (target == null || !target.enabled) continue;
                if (Physics.ComputePenetration(
                        zone,   zone.transform.position,   zone.transform.rotation,
                        target, target.transform.position, target.transform.rotation,
                        out _, out _))
                    _overlapScratch.Add(target);
            }
        }
        return _overlapScratch;
    }

    // ── State API ─────────────────────────────────────────────────────────────

    /// <summary>Reset firstContactOnly spent flags so OnEnter and OnExit can fire again.</summary>
    public void ResetTrigger()
    {
        _enterSpent     = false;
        _exitSpent      = false;
        _pendingActions = 0;
        _evalMode       = EvalMode.None;
        _activeContacts.Clear();
    }

    /// <summary>Silence OnEnter and OnExit until ResetTrigger() is called.</summary>
    public void SpendTrigger()
    {
        _enterSpent = true;
        _exitSpent  = true;
    }

    // ── Pending Action API ────────────────────────────────────────────────────

    /// <summary>
    /// Wire to OnEvaluate. Starts a tracked async action — OnFinishAction fires
    /// once all registered actions call CompleteAction().
    /// </summary>
    public void RunTracked(MonoBehaviour source)
    {
        if (source is not ITrackableAction action)
        {
            Debug.LogWarning($"[CollisionTrigger:{name}] RunTracked: '{source?.name}' does not implement ITrackableAction — ignored.");
            return;
        }
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
            OnFinishAction?.Invoke();
    }

    // ── Contact callbacks ─────────────────────────────────────────────────────

    internal void OnContactEnter(Collider col)
    {
        if (!_activeContacts.Add(col)) return;
        if (_activeContacts.Count != 1) return; // not the 0→1 transition

        FireEnter();

        switch (_evalMode)
        {
            case EvalMode.FirstContact:  RunEvaluate(autoDisarm: true);  break;
            case EvalMode.EveryContact:  RunEvaluate(autoDisarm: false); break;
        }
    }

    internal void OnContactExit(Collider col)
    {
        if (!_activeContacts.Remove(col)) return;
        if (_activeContacts.Count == 0)
            FireExit();
    }
}

// ── CollisionListener ─────────────────────────────────────────────────────────

[AddComponentMenu("")]
public class CollisionListener : MonoBehaviour
{
    private HashSet<Collider>        _targets;
    private System.Action<Collider>  _onEnter;
    private System.Action<Collider>  _onExit;

    public void Init(HashSet<Collider> targets,
                     System.Action<Collider> onEnter,
                     System.Action<Collider> onExit)
    {
        _targets = targets;
        _onEnter = onEnter;
        _onExit  = onExit;
    }

    private void OnCollisionEnter(Collision c)
    {
        if (_targets == null || !_targets.Contains(c.collider)) return;
        _onEnter?.Invoke(c.collider);
    }

    private void OnCollisionExit(Collision c)
    {
        if (_targets == null || !_targets.Contains(c.collider)) return;
        _onExit?.Invoke(c.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_targets == null || !_targets.Contains(other)) return;
        _onEnter?.Invoke(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_targets == null || !_targets.Contains(other)) return;
        _onExit?.Invoke(other);
    }
}