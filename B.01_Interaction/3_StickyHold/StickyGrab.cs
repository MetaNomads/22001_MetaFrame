using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaNomads.Interaction
{
#if UNITY_EDITOR
    [CustomEditor(typeof(StickyGrab))]
    public class StickyGrabEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var sticky = (StickyGrab)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

            GUI.enabled = Application.isPlaying;

            if (GUILayout.Button("Force Release"))  sticky.ForceRelease();
            if (GUILayout.Button("Allow Release"))  sticky.AllowRelease();
            if (GUILayout.Button("Revoke Release")) sticky.RevokeRelease();

            GUI.enabled = true;

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode to use debug buttons.", MessageType.Info);
        }
    }
#endif

    [RequireComponent(typeof(HandGrabInteractable))]
    public class StickyGrab : MonoBehaviour
    {
        public static StickyGrab ActiveGrab { get; private set; }

        [Header("Release Detection")]
        [Tooltip("Collider used to detect ReleaseZone. If left empty, uses the collider on the root parent.")]
        [SerializeField] private Collider releaseCollider;

        private HandGrabInteractable _interactable;
        private HandGrabInteractor   _activeInteractor;
        private bool                 _releaseAllowed = false;

        private void Awake()
        {
            _interactable = GetComponent<HandGrabInteractable>();

            if (releaseCollider == null)
                releaseCollider = transform.root.GetComponent<Collider>();

            if (releaseCollider == null)
                Debug.LogWarning("[StickyGrab] No release collider found or assigned.", this);
        }

        private void Start()
        {
            _interactable.WhenSelectingInteractorAdded.Action   += OnGrabbed;
            _interactable.WhenSelectingInteractorRemoved.Action += OnReleased;
        }

        private void OnDestroy()
        {
            _interactable.WhenSelectingInteractorAdded.Action   -= OnGrabbed;
            _interactable.WhenSelectingInteractorRemoved.Action -= OnReleased;

            if (ActiveGrab == this) ActiveGrab = null;
        }

        // ── Static lookup — called by ReleaseZone ─────────────────────────────

        /// <summary>
        /// Returns the StickyGrab whose releaseCollider matches the given collider,
        /// but only if it is the current ActiveGrab.
        /// </summary>
        public static StickyGrab GetStickyGrabForCollider(Collider col)
        {
            if (ActiveGrab != null && ActiveGrab.releaseCollider == col)
                return ActiveGrab;
            return null;
        }

        // ── Grab / Release callbacks ──────────────────────────────────────────

        private void OnGrabbed(HandGrabInteractor interactor)
        {
            if (ActiveGrab != null && ActiveGrab != this)
            {
                Debug.Log($"[StickyGrab] Hand switched — demoting {ActiveGrab.gameObject.name}", this);
                ActiveGrab._releaseAllowed = false;
            }

            _activeInteractor = interactor;
            _releaseAllowed   = false;
            ActiveGrab        = this;

            Debug.Log($"[StickyGrab] Grabbed — {gameObject.name} is ActiveGrab", this);
        }

        private void OnReleased(HandGrabInteractor interactor)
        {
            Debug.Log($"[StickyGrab] Released — releaseAllowed: {_releaseAllowed}", this);

            if (_releaseAllowed)
            {
                Debug.Log($"[StickyGrab] Inside release zone — releasing", this);
                _releaseAllowed   = false;
                _activeInteractor = null;
                if (ActiveGrab == this) ActiveGrab = null;
            }
            else
            {
                Debug.Log($"[StickyGrab] Outside release zone — re-grabbing", this);
                _activeInteractor = interactor;
                interactor.ForceSelect(_interactable, false);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void AllowRelease()
        {
            Debug.Log($"[StickyGrab] AllowRelease — ActiveGrab is me: {ActiveGrab == this}", this);
            _releaseAllowed = true;
        }

        public void RevokeRelease()
        {
            Debug.Log($"[StickyGrab] RevokeRelease", this);
            _releaseAllowed = false;
        }

        public void ForceRelease()
        {
            Debug.Log($"[StickyGrab] ForceRelease", this);
            _releaseAllowed = true;
            _activeInteractor?.ForceRelease();
        }
    }
}