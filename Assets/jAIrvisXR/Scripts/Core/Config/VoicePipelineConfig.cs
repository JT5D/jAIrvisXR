using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Config/Voice Pipeline Config")]
    public class VoicePipelineConfig : ScriptableObject
    {
        [Header("Microphone")]
        [SerializeField] private int _sampleRate = 16000;
        [SerializeField] private int _recordingLengthSeconds = 30;
        [SerializeField, Tooltip("Null/empty = default device")]
        private string _microphoneDeviceName;

        [Header("Activation")]
        [SerializeField] private bool _usePushToTalk = true;
        [SerializeField] private bool _useWakeWord = false;
        [SerializeField] private string _wakeWord = "hey jarvis";
        [SerializeField, Range(0f, 1f)] private float _silenceThreshold = 0.01f;
        [SerializeField] private float _silenceTimeoutSeconds = 1.5f;
        [SerializeField] private float _maxRecordingSeconds = 15f;

        public int SampleRate => _sampleRate;
        public int RecordingLengthSeconds => _recordingLengthSeconds;
        public string MicrophoneDeviceName => _microphoneDeviceName;
        public bool UsePushToTalk => _usePushToTalk;
        public bool UseWakeWord => _useWakeWord;
        public string WakeWord => _wakeWord;
        public float SilenceThreshold => _silenceThreshold;
        public float SilenceTimeoutSeconds => _silenceTimeoutSeconds;
        public float MaxRecordingSeconds => _maxRecordingSeconds;
    }
}
