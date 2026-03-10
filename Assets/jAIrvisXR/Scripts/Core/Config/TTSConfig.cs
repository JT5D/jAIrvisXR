using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Config/TTS Config")]
    public class TTSConfig : ScriptableObject
    {
        [SerializeField] private string _providerName = "ElevenLabs";
        [SerializeField] private string _voiceId = "21m00Tcm4TlvDq8ikWAM";
        [SerializeField, Range(0.5f, 2f)] private float _speed = 1f;
        [SerializeField] private string _apiKey;
        [SerializeField] private string _endpoint = "https://api.elevenlabs.io/v1/text-to-speech";
        [SerializeField] private string _envVarName = "ELEVENLABS_API_KEY";

        public string ProviderName => _providerName;
        public string VoiceId => _voiceId;
        public float Speed => _speed;
        public string ApiKey => string.IsNullOrEmpty(_apiKey)
            ? System.Environment.GetEnvironmentVariable(_envVarName)
            : _apiKey;
        public string Endpoint => _endpoint;
    }
}
