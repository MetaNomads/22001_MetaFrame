using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Oculus.Interaction;
using UnityEngine.Events;


namespace MetaNomads.Interaction
{
    // a manager to enable interactable from code
    public class InteractableManager : MonoBehaviour
    {
        public string Interactable;
        [Header("State Control")]
        [SerializeField] private bool isEnabledState = true;

        [Header("Events")]
        public UnityEvent OnEnabled;
        public UnityEvent OnDisabled;

        private bool currentState;

        private void Awake()
        {
            currentState = isEnabledState;
            InvokeState();
        }

        private void OnEnable()
        {
            SetState(true);
        }

        private void OnDisable()
        {
            SetState(false);
        }

        public void SetState(bool value)
        {
            if (currentState == value)
                return;

            currentState = value;
            isEnabledState = value;

            InvokeState();
        }

        private void InvokeState()
        {
            if (currentState)
                OnEnabled?.Invoke();
            else
                OnDisabled?.Invoke();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                SetState(isEnabledState);
        }
#endif
    }
}