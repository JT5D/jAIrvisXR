using System.Collections;
using UnityEngine;
using jAIrvisXR.Core.Config;

namespace jAIrvisXR.Networking
{
    /// <summary>
    /// Captures TTS audio output and publishes it as a LiveKit audio track,
    /// so remote participants hear the AI assistant.
    /// Attach to the same GameObject as AudioPlaybackHandler's AudioSource.
    /// When HAS_LIVEKIT is defined, uses BasicAudioSource to capture from the
    /// Unity AudioSource and publishes via LocalAudioTrack. Otherwise, no-op.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LiveKitAudioBridge : MonoBehaviour
    {
        [SerializeField] private MultiplayerConfig _config;
        [SerializeField] private bool _enableCapture = true;

        private LiveKitRoomManager _roomManager;
        private AudioSource _audioSource;
        private bool _isPublishing;

#if HAS_LIVEKIT
        private LiveKit.BasicAudioSource _rtcSource;
        private LiveKit.LocalAudioTrack _localTrack;
#endif

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            _roomManager = GetComponent<LiveKitRoomManager>();
            if (_roomManager == null)
                _roomManager = GetComponentInParent<LiveKitRoomManager>();

            if (_roomManager == null)
            {
                Debug.LogWarning("[AudioBridge] No LiveKitRoomManager found. Audio will not be published remotely.");
                return;
            }

            _roomManager.OnConnectionStateChanged += OnConnectionStateChanged;
            Debug.Log($"[AudioBridge] Linked to {_roomManager.ProviderName}. Capture: {_enableCapture}");
        }

        private void OnConnectionStateChanged(RoomConnectionState state)
        {
            if (state == RoomConnectionState.Connected && _enableCapture)
                PublishAudio();
            else if (state == RoomConnectionState.Disconnected)
                UnpublishAudio();
        }

        private void PublishAudio()
        {
            if (_isPublishing) return;
            if (_config != null && !_config.PublishTtsAudio) return;

#if HAS_LIVEKIT
            if (_roomManager.Room == null) return;

            _rtcSource = new LiveKit.BasicAudioSource(_audioSource);
            _localTrack = LiveKit.LocalAudioTrack.CreateAudioTrack(
                "tts-audio", _rtcSource, _roomManager.Room);

            StartCoroutine(PublishCoroutine());
#else
            Debug.Log("[AudioBridge] LiveKit SDK not installed — audio publishing skipped.");
#endif
            _isPublishing = true;
        }

#if HAS_LIVEKIT
        private IEnumerator PublishCoroutine()
        {
            var options = new LiveKit.Proto.TrackPublishOptions
            {
                Source = LiveKit.Proto.TrackSource.SourceMicrophone,
            };

            var publish = _roomManager.Room.LocalParticipant.PublishTrack(_localTrack, options);
            yield return publish;

            if (publish.IsError)
            {
                Debug.LogError("[AudioBridge] Failed to publish TTS audio track.");
                yield break;
            }

            _rtcSource.Start();
            Debug.Log("[AudioBridge] TTS audio track published to LiveKit room.");
        }
#endif

        private void UnpublishAudio()
        {
            if (!_isPublishing) return;

#if HAS_LIVEKIT
            _rtcSource?.Stop();
            _rtcSource?.Dispose();
            _rtcSource = null;
            _localTrack = null;
#endif
            _isPublishing = false;
            Debug.Log("[AudioBridge] Audio track unpublished.");
        }

        public void SetPublishing(bool enabled)
        {
            _enableCapture = enabled;
            if (enabled && _roomManager?.ConnectionState == RoomConnectionState.Connected)
                PublishAudio();
            else if (!enabled)
                UnpublishAudio();
            Debug.Log($"[AudioBridge] Publishing: {enabled}");
        }

        private void OnDestroy()
        {
            UnpublishAudio();
            if (_roomManager != null)
                _roomManager.OnConnectionStateChanged -= OnConnectionStateChanged;
        }
    }
}
