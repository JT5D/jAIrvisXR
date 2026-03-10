using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Config/TTS Config")]
    public class TTSConfig : ScriptableObject
    {
        [SerializeField] private string _providerName = "Mock";
        [SerializeField] private string _voiceId = "default";
        [SerializeField, Range(0.5f, 2f)] private float _speed = 1f;
        [SerializeField] private string _apiKey;
        [SerializeField] private string _endpoint;

        public string ProviderName => _providerName;
        public string VoiceId => _voiceId;
        public float Speed => _speed;
        public string ApiKey => string.IsNullOrEmpty(_apiKey)
            ? System.Environment.GetEnvironmentVariable("TTS_API_KEY")
            : _apiKey;
        public string Endpoint => _endpoint;
    }
}
