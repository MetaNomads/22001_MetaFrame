using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.Input;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MetaNomads.Interaction
{
#if UNITY_EDITOR
    [CustomEditor(typeof(LockableHandGrab))]
    public class LockableHandGrabEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var grab = (LockableHandGrab)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Force Release"))  grab.ForceRelease();
            if (GUILayout.Button("Allow Release"))  grab.AllowRelease();
            if (GUILayout.Button("Revoke Release")) grab.RevokeRelease();
            GUI.enabled = true;
        }
    }
#endif

    /// <summary>
    /// Place on the parent object. Locks all child HandGrabInteractables so they
    /// cannot be released until AllowRelease() is called (e.g. from an UnlockZone).
    /// </summary>
    public class LockableHandGrab : MonoBehaviour
    {
        public bool IsActive => _activeCount > 0;

        private static readonly GrabbingRule _lockedRule = new GrabbingRule()
        {
            [HandFinger.Thumb]  = FingerRequirement.Ignored,
            [HandFinger.Index]  = FingerRequirement.Ignored,
            [HandFinger.Middle] = FingerRequirement.Ignored,
            [HandFinger.Ring]   = FingerRequirement.Ignored,
            [HandFinger.Pinky]  = FingerRequirement.Ignored,
        };

        private class InteractableState
        {
            public HandGrabInteractable Interactable;
            public HandGrabInteractor   ActiveInteractor;
            public GrabbingRule         OriginalPinchRules;
            public GrabbingRule         OriginalPalmRules;
            public bool                 IsGrabbed;
        }

        private readonly List<InteractableState> _states = new List<InteractableState>();
        private int  _activeCount    = 0;
        private bool _releaseAllowed = false;

        private void Start()
        {
            foreach (var interactable in GetComponentsInChildren<HandGrabInteractable>())
            {
                var state = new InteractableState
                {
                    Interactable       = interactable,
                    OriginalPinchRules = interactable.PinchGrabRules,
                    OriginalPalmRules  = interactable.PalmGrabRules,
                };
                _states.Add(state);

                var s = state;
                interactable.WhenSelectingInteractorAdded.Action   += interactor => OnGrabbed(s, interactor);
                interactable.WhenSelectingInteractorRemoved.Action += interactor => OnReleased(s, interactor);
            }
        }

        private void OnDestroy()
        {
            foreach (var state in _states)
                RestoreRules(state);
        }

        // ── Callbacks ─────────────────────────────────────────────────────────

        private void OnGrabbed(InteractableState state, HandGrabInteractor interactor)
        {
            state.IsGrabbed        = true;
            state.ActiveInteractor = interactor;
            _activeCount++;

            // Restore rules on other grabs and release if hand already open
            foreach (var other in _states)
            {
                if (other == state || !other.IsGrabbed) continue;
                RestoreAndReleaseIfOpen(other);
            }

            UpdateRules();
        }

        private void OnReleased(InteractableState state, HandGrabInteractor interactor)
        {
            state.IsGrabbed        = false;
            state.ActiveInteractor = null;
            _activeCount           = Mathf.Max(0, _activeCount - 1);

            RestoreRules(state);

            if (_activeCount == 0)
                _releaseAllowed = false;

            UpdateRules();
        }

        // ── Rule management ───────────────────────────────────────────────────

        private void UpdateRules()
        {
            bool shouldLock = _activeCount == 1 && !_releaseAllowed;

            foreach (var state in _states)
            {
                if (!state.IsGrabbed) continue;

                if (shouldLock)
                    LockRules(state);
                else
                    RestoreRules(state);
            }
        }

        private void LockRules(InteractableState state)
        {
            state.Interactable.InjectPinchGrabRules(_lockedRule);
            state.Interactable.InjectPalmGrabRules(_lockedRule);
        }

        private void RestoreRules(InteractableState state)
        {
            state.Interactable.InjectPinchGrabRules(state.OriginalPinchRules);
            state.Interactable.InjectPalmGrabRules(state.OriginalPalmRules);
        }

        /// <summary>
        /// Restores natural grab rules on a state. If the hand is already open,
        /// force releases immediately since the SDK needs a close→open transition to detect unselect.
        /// </summary>
        private void RestoreAndReleaseIfOpen(InteractableState state)
        {
            RestoreRules(state);

            if (state.ActiveInteractor == null) return;

            var api = state.ActiveInteractor.HandGrabApi;
            bool isOpen = !api.IsHandPinchGrabbing(state.OriginalPinchRules) &&
                          !api.IsHandPalmGrabbing(state.OriginalPalmRules);

            if (isOpen)
                state.ActiveInteractor.ForceRelease();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void AllowRelease()
        {
            _releaseAllowed = true;

            foreach (var state in _states)
            {
                if (!state.IsGrabbed) continue;
                RestoreAndReleaseIfOpen(state);
            }
        }

        public void RevokeRelease()
        {
            _releaseAllowed = false;
            UpdateRules();
        }

        public void ForceRelease()
        {
            _releaseAllowed = true;

            foreach (var state in _states)
            {
                if (!state.IsGrabbed) continue;
                RestoreRules(state);
                state.ActiveInteractor?.ForceRelease();
            }
        }
    }
}