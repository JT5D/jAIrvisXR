using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Config/STT Config")]
    public class STTConfig : ScriptableObject
    {
        [SerializeField] private string _providerName = "Mock";
        [SerializeField] private string _language = "en-US";
        [SerializeField] private string _apiKey;
        [SerializeField] private string _endpoint;

        public string ProviderName => _providerName;
        public string Language => _language;
        public string ApiKey => string.IsNullOrEmpty(_apiKey)
            ? System.Environment.GetEnvironmentVariable("STT_API_KEY")
            : _apiKey;
        public string Endpoint => _endpoint;
    }
}
