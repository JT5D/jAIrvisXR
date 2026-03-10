using jAIrvisXR.Core.Config;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    public class MicrophoneCapture : MonoBehaviour
    {
        [SerializeField] private VoicePipelineConfig _config;

        private AudioClip _recordingClip;
        private bool _isRecording;
        private int _recordingStartSample;

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            if (_isRecording)
            {
                Debug.LogWarning("[MicrophoneCapture] Already recording.");
                return;
            }

            string device = string.IsNullOrEmpty(_config.MicrophoneDeviceName)
                ? null : _config.MicrophoneDeviceName;

            _recordingClip = Microphone.Start(
                device,
                loop: true,
                lengthSec: _config.RecordingLengthSeconds,
                frequency: _config.SampleRate
            );

            int waitCount = 0;
            while (Microphone.GetPosition(device) <= 0 && waitCount < 100)
            {
                waitCount++;
            }

            _recordingStartSample = Microphone.GetPosition(device);
            _isRecording = true;
            Debug.Log("[MicrophoneCapture] Recording started.");
        }

        public AudioClip StopRecording()
        {
            if (!_isRecording)
            {
                Debug.LogWarning("[MicrophoneCapture] Not recording.");
                return null;
            }

            string device = string.IsNullOrEmpty(_config.MicrophoneDeviceName)
                ? null : _config.MicrophoneDeviceName;

            int endPosition = Microphone.GetPosition(device);
            Microphone.End(device);
            _isRecording = false;

            if (_recordingClip == null || endPosition <= 0)
            {
                Debug.LogWarning("[MicrophoneCapture] No audio captured.");
                return null;
            }

            int sampleCount = endPosition - _recordingStartSample;
            if (sampleCount <= 0)
            {
                sampleCount = _recordingClip.samples - _recordingStartSample + endPosition;
            }

            float[] samples = new float[sampleCount * _recordingClip.channels];
            _recordingClip.GetData(samples, _recordingStartSample);

            var trimmedClip = AudioClip.Create(
                "RecordedAudio",
                sampleCount,
                _recordingClip.channels,
                _config.SampleRate,
                false
            );
            trimmedClip.SetData(samples, 0);

            Debug.Log($"[MicrophoneCapture] Stopped. Captured {(float)sampleCount / _config.SampleRate:F2}s.");
            return trimmedClip;
        }

        public float GetCurrentAudioLevel()
        {
            if (!_isRecording || _recordingClip == null) return 0f;

            string device = string.IsNullOrEmpty(_config.MicrophoneDeviceName)
                ? null : _config.MicrophoneDeviceName;

            int position = Microphone.GetPosition(device);
            if (position <= 0) return 0f;

            int sampleWindow = Mathf.Min(256, position);
            float[] samples = new float[sampleWindow];
            _recordingClip.GetData(samples, position - sampleWindow);

            float sum = 0f;
            for (int i = 0; i < sampleWindow; i++)
            {
                sum += samples[i] * samples[i];
            }
            return Mathf.Sqrt(sum / sampleWindow);
        }

        private void OnDestroy()
        {
            if (_isRecording)
            {
                string device = string.IsNullOrEmpty(_config?.MicrophoneDeviceName)
                    ? null : _config.MicrophoneDeviceName;
                Microphone.End(device);
            }
        }
    }
}
