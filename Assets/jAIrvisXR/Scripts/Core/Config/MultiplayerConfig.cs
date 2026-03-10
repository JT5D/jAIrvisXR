using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Config/Multiplayer Config")]
    public class MultiplayerConfig : ScriptableObject
    {
        [Header("LiveKit Server")]
        [SerializeField] private string _serverUrl = "ws://localhost:7880";
        [SerializeField] private string _apiKey;
        [SerializeField] private string _apiSecret;
        [SerializeField] private string _apiKeyEnvVar = "LIVEKIT_API_KEY";
        [SerializeField] private string _apiSecretEnvVar = "LIVEKIT_API_SECRET";

        [Header("Token Server")]
        [SerializeField] private string _tokenServerUrl = "http://localhost:7439/token";

        [Header("Room")]
        [SerializeField] private string _defaultRoomName = "jairvis-main";
        [SerializeField] private string _displayName = "User";
        [SerializeField] private bool _autoJoinOnStart;

        [Header("Audio")]
        [SerializeField] private bool _publishMicrophone = true;
        [SerializeField] private bool _publishTtsAudio = true;
        [SerializeField] private bool _subscribeToRemoteAudio = true;

        public string ServerUrl => _serverUrl;
        public string ApiKey => string.IsNullOrEmpty(_apiKey)
            ? System.Environment.GetEnvironmentVariable(_apiKeyEnvVar)
            : _apiKey;
        public string ApiSecret => string.IsNullOrEmpty(_apiSecret)
            ? System.Environment.GetEnvironmentVariable(_apiSecretEnvVar)
            : _apiSecret;
        public string TokenServerUrl => _tokenServerUrl;
        public string DefaultRoomName => _defaultRoomName;
        public string DisplayName => _displayName;
        public bool AutoJoinOnStart => _autoJoinOnStart;
        public bool PublishMicrophone => _publishMicrophone;
        public bool PublishTtsAudio => _publishTtsAudio;
        public bool SubscribeToRemoteAudio => _subscribeToRemoteAudio;
    }
}
