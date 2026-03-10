using System;
using jAIrvisXR.Core.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jAIrvisXR.AI.Voice
{
    public class VoiceActivation : MonoBehaviour
    {
        [SerializeField] private VoicePipelineConfig _config;

        [Header("Push-to-Talk")]
        [SerializeField] private InputActionReference _pushToTalkAction;

        [Header("Wake Word")]
        [SerializeField] private WakeWordDetector _wakeWordDetector;

        public event Action OnActivationStarted;
        public event Action OnActivationEnded;

        private bool _isActive;

        public bool IsActive => _isActive;

        private void OnEnable()
        {
            if (_config.UsePushToTalk && _pushToTalkAction != null)
            {
                var action = _pushToTalkAction.action;
                action.Enable();
                action.started += OnPushToTalkStarted;
                action.canceled += OnPushToTalkCanceled;
            }

            if (_config.UseWakeWord && _wakeWordDetector != null)
            {
                _wakeWordDetector.OnWakeWordDetected += OnWakeWordDetected;
            }
        }

        private void OnDisable()
        {
            if (_pushToTalkAction != null)
            {
                var action = _pushToTalkAction.action;
                action.started -= OnPushToTalkStarted;
                action.canceled -= OnPushToTalkCanceled;
            }

            if (_wakeWordDetector != null)
            {
                _wakeWordDetector.OnWakeWordDetected -= OnWakeWordDetected;
            }
        }

        private void OnPushToTalkStarted(InputAction.CallbackContext ctx)
        {
            if (_isActive) return;
            _isActive = true;
            Debug.Log("[VoiceActivation] Push-to-Talk activated.");
            OnActivationStarted?.Invoke();
        }

        private void OnPushToTalkCanceled(InputAction.CallbackContext ctx)
        {
            if (!_isActive) return;
            _isActive = false;
            Debug.Log("[VoiceActivation] Push-to-Talk released.");
            OnActivationEnded?.Invoke();
        }

        private void OnWakeWordDetected()
        {
            if (_isActive) return;
            _isActive = true;
            Debug.Log("[VoiceActivation] Wake word detected!");
            OnActivationStarted?.Invoke();
        }

        public void DeactivateFromSilence()
        {
            if (!_isActive) return;
            _isActive = false;
            Debug.Log("[VoiceActivation] Deactivated (silence timeout).");
            OnActivationEnded?.Invoke();
        }
    }
}
