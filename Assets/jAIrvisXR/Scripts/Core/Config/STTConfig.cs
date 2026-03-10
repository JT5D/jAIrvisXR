using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Config/STT Config")]
    public class STTConfig : ScriptableObject
    {
        [SerializeField] private string _providerName = "GroqWhisper";
        [SerializeField] private string _language = "en";
        [SerializeField] private string _apiKey;
        [SerializeField] private string _endpoint = "https://api.groq.com/openai/v1/audio/transcriptions";
        [SerializeField] private string _envVarName = "GROQ_API_KEY";

        public string ProviderName => _providerName;
        public string Language => _language;
        public string ApiKey => string.IsNullOrEmpty(_apiKey)
            ? System.Environment.GetEnvironmentVariable(_envVarName)
            : _apiKey;
        public string Endpoint => _endpoint;
    }
}
