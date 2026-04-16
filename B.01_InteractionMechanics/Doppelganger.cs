using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Doppelganger : MonoBehaviour
{
    [SerializeField] private GameObject source;

    [Tooltip("Spawn at a custom transform instead of the source's position.")]
    [SerializeField] private bool useSpawnPoint = false;

    [Tooltip("Custom transform to spawn at.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Names of child GameObjects to disable after spawning.")]
    [SerializeField] private List<string> childrenToDisable = new();

    [Tooltip("Destroy the doppelganger after it separates from the original.")]
    [SerializeField] private bool useLifetime = false;

    [Tooltip("Seconds before the doppelganger is destroyed, counted from the moment it separates from the original.")]
    [SerializeField] private float lifetime = 3f;

    private GameObject _instance;
    private System.Action _onComplete;

    [Space]
    [Tooltip("Fires after the deferred spawn completes — use this to chain actions that depend on the spawn.")]
    [SerializeField] public UnityEvent OnSpawned;

    [Tooltip("Fires when the doppelganger is destroyed.")]
    [SerializeField] public UnityEvent OnDestroyed;

    public void Spawn() { Debug.Log("[Doppelganger] Spawn() called."); StartCoroutine(DoSpawn()); }

    private IEnumerator DoSpawn()
    {
        if (source == null) yield break;
        yield return null;

        Vector3    spawnPos = useSpawnPoint && spawnPoint != null ? spawnPoint.position : source.transform.position;
        Quaternion spawnRot = useSpawnPoint && spawnPoint != null ? spawnPoint.rotation : source.transform.rotation;

        Debug.Log($"[Doppelganger] DoSpawn() resumed — instantiating '{source.name}'.");
        _instance = Instantiate(source, spawnPos, spawnRot);
        Debug.Log($"[Doppelganger] Instantiated '{_instance.name}' (instanceID={_instance.GetInstanceID()}).");

        // Disable children BEFORE SetActive(true) so their Awake/Start never run
        foreach (var childName in childrenToDisable)
        {
            if (string.IsNullOrEmpty(childName)) continue;
            var child = FindDeepChild(_instance.transform, childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
                Debug.Log($"[Doppelganger] Disabled child '{child.name}'.");
            }
            else
                Debug.LogWarning($"[Doppelganger] Child '{childName}' not found in hierarchy.", _instance);
        }

        _instance.SetActive(true);

        var dupRbs  = _instance.GetComponentsInChildren<Rigidbody>();
        var dupCols = _instance.GetComponentsInChildren<Collider>();

        foreach (var rb  in dupRbs)  rb.isKinematic = true;
        foreach (var col in dupCols) col.isTrigger  = true;

        var handler = _instance.AddComponent<DoppelgangerPhysicsHandler>();
        handler.Init(dupRbs, dupCols, source.GetComponentsInChildren<Collider>());

        Debug.Log("[Doppelganger] Firing OnSpawned.");
        OnSpawned?.Invoke();

        if (useLifetime && lifetime > 0f)
        {
            Debug.Log($"[Doppelganger] Starting lifetime coroutine ({lifetime}s).");
            StartCoroutine(DestroyAfterLifetime());
        }
        var cb = _onComplete;
        _onComplete = null;
        cb?.Invoke();
    }

    // Searches all descendants recursively by name
    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    public void Destroy()
    {
        Debug.Log("[Doppelganger] Destroy() called — _instance=" + (_instance == null ? "NULL" : _instance.name + " id=" + _instance.GetInstanceID()));
        if (_instance != null)
        {
            StopAllCoroutines(); // cancel lifetime coroutine if running
            UnityEngine.Object.Destroy(_instance);
            _instance = null;
            Debug.Log("[Doppelganger] Instance destroyed — firing OnDestroyed.");
            OnDestroyed?.Invoke();
        }
        else
        {
            Debug.LogWarning("[Doppelganger] Destroy() called but _instance is null.");
        }
    }

    private IEnumerator DestroyAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        Debug.Log("[Doppelganger] Lifetime elapsed — calling Destroy().");
        Destroy();
    }
}

// ── DoppelgangerPhysicsHandler ────────────────────────────────────────────────

[AddComponentMenu("")]
public class DoppelgangerPhysicsHandler : MonoBehaviour
{
    private Rigidbody[]   _dupRbs;
    private Collider[]    _dupCols;
    private Collider[]    _sourceCols;
    private bool _restored;

    // FIX: maximum seconds to wait for the doppelganger to separate from the source.
    // Without this, complex or concave geometry that never fully separates would keep
    // Physics.ComputePenetration running every FixedUpdate indefinitely, causing a
    // sustained framerate drop for the lifetime of the doppelganger object.
    private const float OverlapTimeoutSeconds = 3f;

    public void Init(
        Rigidbody[]   dupRbs,
        Collider[]    dupCols,
        Collider[]    sourceCols)
    {
        _dupRbs     = dupRbs;
        _dupCols    = dupCols;
        _sourceCols = sourceCols;
    }

    private void Start()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // Wait two fixed frames for physics to fully initialize bounds
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        bool wasOverlapping = CheckPenetration();

        if (wasOverlapping)
        {
            float elapsed = 0f;

            while (CheckPenetration())
            {
                elapsed += Time.fixedDeltaTime;

                if (elapsed >= OverlapTimeoutSeconds)
                {
                    // FIX: geometry never separated — force Restore() rather than
                    // spinning forever and burning physics time every fixed frame.
                    Debug.LogWarning(
                        $"[DoppelgangerPhysicsHandler] '{gameObject.name}' overlap timeout " +
                        $"({OverlapTimeoutSeconds}s) — forcing Restore(). " +
                        "Check source geometry for concave or non-convex colliders.");
                    break;
                }

                yield return new WaitForFixedUpdate();
            }
        }

        Restore();
    }

    // Uses Physics.ComputePenetration for accurate overlap detection
    private bool CheckPenetration()
    {
        foreach (var dc in _dupCols)
        {
            if (dc == null) continue;
            foreach (var sc in _sourceCols)
            {
                if (sc == null) continue;
                if (Physics.ComputePenetration(
                        dc, dc.transform.position, dc.transform.rotation,
                        sc, sc.transform.position, sc.transform.rotation,
                        out _, out float dist) && dist > 0f)
                    return true;
            }
        }
        return false;
    }

    private void Restore()
    {
        if (_restored) return;
        _restored = true;

        Debug.Log($"[DoppelgangerPhysicsHandler] Restore() on '{gameObject.name}' — re-enabling physics.");
        foreach (var rb  in _dupRbs)  if (rb  != null) rb.isKinematic  = false;
        foreach (var col in _dupCols) if (col != null) col.isTrigger   = false;

        UnityEngine.Object.Destroy(this);
    }
}

// ── Custom Editor ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
[CustomEditor(typeof(Doppelganger))]
public class DoppelgangerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var source            = serializedObject.FindProperty("source");
        var useSpawnPoint     = serializedObject.FindProperty("useSpawnPoint");
        var spawnPoint        = serializedObject.FindProperty("spawnPoint");
        var childrenToDisable = serializedObject.FindProperty("childrenToDisable");
        var useLifetime       = serializedObject.FindProperty("useLifetime");
        var lifetime          = serializedObject.FindProperty("lifetime");
        var onSpawned         = serializedObject.FindProperty("OnSpawned");
        var onDestroyed       = serializedObject.FindProperty("OnDestroyed");

        EditorGUILayout.PropertyField(source);
        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(useSpawnPoint, new GUIContent("Spawn At Given Position"));
        if (useSpawnPoint.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(spawnPoint, new GUIContent("Spawn Point"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(childrenToDisable, new GUIContent("Children To Disable"), true);
        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(useLifetime, new GUIContent("Use Lifetime"));
        if (useLifetime.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(lifetime, new GUIContent("Lifetime (s)"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(onSpawned,   new GUIContent("On Spawned"));
        EditorGUILayout.PropertyField(onDestroyed, new GUIContent("On Destroyed"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8);
        var t = (Doppelganger)target;
        if (GUILayout.Button("Spawn"))   t.Spawn();
        if (GUILayout.Button("Destroy")) t.Destroy();
    }
}
#endif
