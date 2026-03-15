using UnityEngine;
using UnityEngine.Events;

// Place anywhere. Assign objectA (needs a Rigidbody) and objectB.
// Detects when A and B physically touch each other.

public class CollisionTrigger : MonoBehaviour
{
    public GameObject objectA;
    public GameObject objectB;

    public UnityEvent OnEnter;
    public UnityEvent OnExit;
    public UnityEvent OnEvaluate;

    private bool              _touching = false;
    private CollisionListener _listener;

    private void Start()
    {
        if (objectA == null || objectB == null)
        {
            Debug.LogError("[CollisionTrigger] objectA and objectB must both be assigned.");
            return;
        }

        if (objectA.GetComponent<Rigidbody>() == null)
        {
            Debug.LogError($"[CollisionTrigger] objectA '{objectA.name}' needs a Rigidbody.");
            return;
        }

        _listener = objectA.AddComponent<CollisionListener>();
        _listener.Init(objectB, () => { _touching = true;  OnEnter?.Invoke(); },
                                () => { _touching = false; OnExit?.Invoke();  });
    }

    private void OnDestroy()
    {
        if (_listener != null) Destroy(_listener);
    }

    public void Evaluate()
    {
        if (_touching) OnEvaluate?.Invoke();
    }
}

[AddComponentMenu("")]
public class CollisionListener : MonoBehaviour
{
    private GameObject    _target;
    private System.Action _onEnter;
    private System.Action _onExit;

    public void Init(GameObject target, System.Action onEnter, System.Action onExit)
    {
        _target  = target;
        _onEnter = onEnter;
        _onExit  = onExit;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsTargetOrChild(collision.transform)) _onEnter?.Invoke();
    }

    private void OnCollisionExit(Collision collision)
    {
        if (IsTargetOrChild(collision.transform)) _onExit?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsTargetOrChild(other.transform)) _onEnter?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsTargetOrChild(other.transform)) _onExit?.Invoke();
    }

    private bool IsTargetOrChild(Transform t) =>
        t.gameObject == _target || t.IsChildOf(_target.transform);
}