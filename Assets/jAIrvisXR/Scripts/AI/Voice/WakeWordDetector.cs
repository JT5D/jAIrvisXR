using System;
using jAIrvisXR.Core.Config;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    public class WakeWordDetector : MonoBehaviour
    {
        [SerializeField] private VoicePipelineConfig _config;
        [SerializeField] private MicrophoneCapture _microphoneCapture;

        private bool _isListening;
        private float _speechStartTime;
        private bool _speechDetected;

        public event Action OnWakeWordDetected;

        public void StartListening()
        {
            _isListening = true;
            _speechDetected = false;
            Debug.Log("[WakeWordDetector] Listening for wake word...");
        }

        public void StopListening()
        {
            _isListening = false;
            _speechDetected = false;
        }

        private void Update()
        {
            if (!_isListening || !_config.UseWakeWord) return;

            float audioLevel = _microphoneCapture.GetCurrentAudioLevel();

            if (!_speechDetected && audioLevel > _config.SilenceThreshold)
            {
                _speechDetected = true;
                _speechStartTime = Time.time;
            }
            else if (_speechDetected && audioLevel < _config.SilenceThreshold)
            {
                float duration = Time.time - _speechStartTime;
                _speechDetected = false;

                if (duration > 0.3f && duration < 3f)
                {
                    // Placeholder: fires on any short speech detection.
                    // Replace with Picovoice Porcupine or custom keyword model.
                    Debug.Log($"[WakeWordDetector] Speech detected ({duration:F1}s). " +
                        "Placeholder: firing wake word event.");
                    OnWakeWordDetected?.Invoke();
                }
            }
        }
    }
}
