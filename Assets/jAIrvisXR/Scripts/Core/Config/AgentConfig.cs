using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Config/Agent Config")]
    public class AgentConfig : ScriptableObject
    {
        [Header("Anthropic API")]
        [SerializeField] private string _apiKey;
        [SerializeField] private string _model = "claude-sonnet-4-20250514";
        [SerializeField] private string _apiEndpoint = "https://api.anthropic.com/v1/messages";
        [SerializeField] private string _apiVersion = "2023-06-01";

        [Header("Conversation")]
        [SerializeField, Range(1, 100)] private int _maxConversationTurns = 20;
        [SerializeField, Range(1, 4096)] private int _maxTokens = 1024;
        [SerializeField, Range(0f, 1f)] private float _temperature = 0.7f;

        [Header("System Prompt")]
        [SerializeField, TextArea(3, 10)]
        private string _systemPrompt = "You are Jarvis, an AI assistant in a mixed reality environment. Be concise and helpful. Respond conversationally.";

        public string ApiKey => string.IsNullOrEmpty(_apiKey)
            ? System.Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            : _apiKey;
        public string Model => _model;
        public string ApiEndpoint => _apiEndpoint;
        public string ApiVersion => _apiVersion;
        public int MaxConversationTurns => _maxConversationTurns;
        public int MaxTokens => _maxTokens;
        public float Temperature => _temperature;
        public string SystemPrompt => _systemPrompt;
    }
}
