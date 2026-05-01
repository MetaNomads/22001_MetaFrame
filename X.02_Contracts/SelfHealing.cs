// ── SelfHealing.cs ────────────────────────────────────────────────────────────
// Opt-in pattern for components that can detect and repair drift in their own
// internal state at runtime.
//
// When to use:
//   - Internal counters that should track collection sizes (e.g. _pendingActions
//     vs _activeActions.Count) but that can drift if callbacks are skipped.
//   - Cached references that may go stale (a Transform was destroyed while
//     this component still holds it).
//   - State machines whose external observers may have set a state but failed
//     to reset it.
//
// When NOT to use:
//   - Anything where "incorrect state" should fail loudly so the developer
//     fixes the root cause. Self-heal is the LAST line of defense, not the
//     first. Pair with Contract.Invariant to surface the drift even when
//     it's repaired.
//
// Usage:
//
//   public class MyManager : MonoBehaviour, ISelfHealing
//   {
//       public string SelfHealLabel => name;
//
//       public bool RunSelfHeal()
//       {
//           bool healed = false;
//           healed |= !Contract.Healed(
//               () => _counter == _items.Count,
//               () => _counter = _items.Count,
//               "_counter drifted from _items.Count",
//               this);
//           return healed;
//       }
//   }
//
//   // Add a SelfHealRunner anywhere in the scene to schedule periodic checks.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MetaFrame.Contracts
{
    /// <summary>
    /// Implement on any MonoBehaviour that can validate and repair its own state.
    /// </summary>
    public interface ISelfHealing
    {
        /// <summary>
        /// Short identifier shown in the SelfHealRunner inspector. Typically
        /// `name` for a MonoBehaviour, but can be anything stable.
        /// </summary>
        string SelfHealLabel { get; }

        /// <summary>
        /// Validate this component's internal state. If any inconsistency is
        /// detected, repair it and return true. Returns false if everything
        /// was already consistent.
        ///
        /// Implementations should use <see cref="Contract.Healed"/> for each
        /// individual check so the runner gets a unified log trail.
        /// </summary>
        bool RunSelfHeal();
    }

    /// <summary>
    /// Runs RunSelfHeal() on every active ISelfHealing component in the scene
    /// at a configurable interval. Drop one anywhere; it auto-discovers.
    ///
    /// Designed for research builds: zero ceremony, no scene wiring, surfaces
    /// repair counts in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class SelfHealRunner : MonoBehaviour
    {
        [Tooltip("How often (seconds) to scan all ISelfHealing components and ask " +
                 "them to validate themselves. 0 = on demand only (call ScanNow).")]
        [SerializeField, Range(0f, 60f)] private float intervalSeconds = 5f;

        [Tooltip("Re-discover ISelfHealing components every scan. Slightly more " +
                 "expensive but catches components that spawn at runtime. " +
                 "Disable for static scenes for a small perf win.")]
        [SerializeField] private bool rediscoverEveryScan = true;

        private readonly List<ISelfHealing> _cached = new();
        private Coroutine _loop;

        // ── Inspector diagnostics ────────────────────────────────────────────

        [SerializeField, HideInInspector] private int _scanCount;
        [SerializeField, HideInInspector] private int _totalHeals;
        [SerializeField, HideInInspector] private string _lastHealedLabel;

        public int ScanCount      => _scanCount;
        public int TotalHeals     => _totalHeals;
        public string LastHealedLabel => _lastHealedLabel;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            Discover();
            if (intervalSeconds > 0f)
                _loop = StartCoroutine(LoopForever());
        }

        private void OnDisable()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }
        }

        private IEnumerator LoopForever()
        {
            var wait = new WaitForSeconds(intervalSeconds);
            while (true)
            {
                yield return wait;
                ScanNow();
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Run RunSelfHeal() on every discovered component, now. Safe to call
        /// from a button, a debug HUD, or another system.
        /// </summary>
        public void ScanNow()
        {
            if (rediscoverEveryScan) Discover();
            _scanCount++;

            for (int i = 0; i < _cached.Count; i++)
            {
                var h = _cached[i];
                // The component may have been destroyed since discovery.
                if (h is Object obj && obj == null) continue;

                bool healed = false;
                try { healed = h.RunSelfHeal(); }
                catch (System.Exception e)
                {
                    Debug.LogError(
                        $"[SelfHealRunner] '{h.SelfHealLabel}' threw during RunSelfHeal: " +
                        $"{e.GetType().Name}: {e.Message}",
                        h as Object);
                }

                if (healed)
                {
                    _totalHeals++;
                    _lastHealedLabel = h.SelfHealLabel;
                }
            }
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void Discover()
        {
            _cached.Clear();
#if UNITY_2023_1_OR_NEWER
            var found = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
#else
            var found = FindObjectsOfType<MonoBehaviour>();
#endif
            foreach (var mb in found)
            {
                if (mb is ISelfHealing healing)
                    _cached.Add(healing);
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(SelfHealRunner))]
    public class SelfHealRunnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var t = (SelfHealRunner)target;

            UnityEditor.EditorGUILayout.Space(8);
            UnityEditor.EditorGUILayout.LabelField("Diagnostics", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.LabelField($"Scans:           {t.ScanCount}");
            UnityEditor.EditorGUILayout.LabelField($"Total heals:     {t.TotalHeals}");
            UnityEditor.EditorGUILayout.LabelField($"Last healed:     {t.LastHealedLabel ?? "(none)"}");

            UnityEditor.EditorGUILayout.Space(4);
            GUI.enabled = Application.isPlaying;
            GUI.color = new Color(0.5f, 1f, 0.6f);
            if (GUILayout.Button("Scan Now", GUILayout.Height(28)))
                t.ScanNow();
            GUI.color = Color.white;
            GUI.enabled = true;

            if (!Application.isPlaying)
                UnityEditor.EditorGUILayout.HelpBox(
                    "Enter Play Mode to scan.",
                    UnityEditor.MessageType.None);
        }
    }
#endif
}
