using System;
using jAIrvisXR.Core.Config;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    /// <summary>
    /// Placeholder wake word detection using voice activity (energy threshold).
    /// Replace with Picovoice Porcupine or custom keyword-spotting model for production.
    /// </summary>
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
                    Debug.Log($"[WakeWordDetector] Speech detected ({duration:F1}s). Firing wake word event.");
                    OnWakeWordDetected?.Invoke();
                }
            }
        }
    }
}
