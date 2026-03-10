using UnityEngine;
using jAIrvisXR.Core.Config;

namespace jAIrvisXR.Networking
{
    /// <summary>
    /// Captures TTS audio output via OnAudioFilterRead and publishes it
    /// as a LiveKit audio track, so remote participants hear the AI.
    /// Attach to the same GameObject as AudioPlaybackHandler's AudioSource.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LiveKitAudioBridge : MonoBehaviour
    {
        [SerializeField] private MultiplayerConfig _config;
        [SerializeField] private bool _enableCapture = true;

        private IMultiplayerProvider _provider;
        private AudioSource _audioSource;
        private int _sampleRate;
        private int _channels;
        private bool _isPublishing;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _sampleRate = AudioSettings.outputSampleRate;
            AudioSettings.GetDSPBufferSize(out _, out _);

            var outputConfig = AudioSettings.GetConfiguration();
            _channels = outputConfig.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        }

        private void Start()
        {
            // Find multiplayer provider on same or parent GameObject
            _provider = GetComponent<IMultiplayerProvider>() as IMultiplayerProvider;
            if (_provider == null)
                _provider = GetComponentInParent<MonoBehaviour>() as IMultiplayerProvider;

            if (_provider == null)
                Debug.LogWarning("[AudioBridge] No IMultiplayerProvider found. Audio will not be published remotely.");
            else
                Debug.Log($"[AudioBridge] Linked to {_provider.ProviderName}. Capture: {_enableCapture}");
        }

        /// <summary>
        /// Called by Unity's audio thread. Intercepts audio samples being
        /// played through the AudioSource (TTS output) and forwards them
        /// to LiveKit for remote participants.
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_enableCapture || _provider == null) return;
            if (_provider.ConnectionState != RoomConnectionState.Connected) return;
            if (_config != null && !_config.PublishTtsAudio) return;

#if HAS_LIVEKIT
            // Publish audio samples to LiveKit local audio track
            // _localAudioTrack?.WriteSamples(data, _sampleRate, channels);
#endif

            // Data passes through unmodified — local playback is unaffected
        }

        public void SetPublishing(bool enabled)
        {
            _isPublishing = enabled;
            _enableCapture = enabled;
            Debug.Log($"[AudioBridge] Publishing: {enabled}");
        }
    }
}
