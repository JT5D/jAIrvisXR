using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jAIrvisXR.Core.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace jAIrvisXR.AI.Agent
{
    public class ClaudeAgentService : MonoBehaviour, IAgentService
    {
        [SerializeField] private AgentConfig _config;

        private ConversationHistory _conversationHistory;

        public string ServiceName => "Claude (Anthropic)";
        public bool IsReady { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_config == null)
            {
                Debug.LogError("[ClaudeAgentService] AgentConfig is null. Assign it in the Inspector.");
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                Debug.LogError("[ClaudeAgentService] API key is not set. " +
                    "Set it in AgentConfig or ANTHROPIC_API_KEY environment variable.");
                return Task.CompletedTask;
            }

            _conversationHistory = new ConversationHistory(_config.MaxConversationTurns);
            IsReady = true;
            Debug.Log($"[ClaudeAgentService] Initialized. Model: {_config.Model}");
            return Task.CompletedTask;
        }

        public async Task<AgentResponse> SendMessageAsync(AgentRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!IsReady)
                return AgentResponse.Failure("Agent service not initialized.");

            _conversationHistory.AddUserMessage(request.UserMessage);

            var requestBody = new ClaudeRequestBody
            {
                model = _config.Model,
                max_tokens = _config.MaxTokens,
                temperature = _config.Temperature,
                system = _config.SystemPrompt,
                messages = _conversationHistory.GetMessagesForRequest()
            };

            string jsonBody = BuildRequestJson(requestBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using var webRequest = new UnityWebRequest(_config.ApiEndpoint, "POST");
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("x-api-key", _config.ApiKey);
            webRequest.SetRequestHeader("anthropic-version", _config.ApiVersion);

            var operation = webRequest.SendWebRequest();

            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    webRequest.Abort();
                    return AgentResponse.Failure("Request cancelled.");
                }
                await Task.Yield();
            }

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                string errorDetail = webRequest.downloadHandler?.text ?? webRequest.error;
                Debug.LogError($"[ClaudeAgentService] API error: {errorDetail}");
                return AgentResponse.Failure($"HTTP {webRequest.responseCode}: {errorDetail}");
            }

            string responseJson = webRequest.downloadHandler.text;
            string responseText = ExtractResponseText(responseJson);
            string stopReason = ExtractJsonStringField(responseJson, "stop_reason");

            if (string.IsNullOrEmpty(responseText))
            {
                return AgentResponse.Failure("Failed to parse response text from API.");
            }

            _conversationHistory.AddAssistantMessage(responseText);
            return AgentResponse.Success(responseText, stopReason);
        }

        public void ResetConversation()
        {
            _conversationHistory?.Clear();
            Debug.Log("[ClaudeAgentService] Conversation history cleared.");
        }

        public void Dispose()
        {
            IsReady = false;
            _conversationHistory?.Clear();
        }

        private string BuildRequestJson(ClaudeRequestBody body)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\":\"{EscapeJson(body.model)}\",");
            sb.Append($"\"max_tokens\":{body.max_tokens},");
            sb.Append($"\"temperature\":{body.temperature:F2},");
            sb.Append($"\"system\":\"{EscapeJson(body.system)}\",");
            sb.Append("\"messages\":[");
            for (int i = 0; i < body.messages.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"role\":\"{body.messages[i].role}\",");
                sb.Append($"\"content\":\"{EscapeJson(body.messages[i].content)}\"}}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private string ExtractResponseText(string json)
        {
            int contentIdx = json.IndexOf("\"content\"", StringComparison.Ordinal);
            if (contentIdx < 0) return null;

            int textTypeIdx = json.IndexOf("\"type\":\"text\"", contentIdx, StringComparison.Ordinal);
            if (textTypeIdx < 0) return null;

            int textFieldIdx = json.IndexOf("\"text\":\"", textTypeIdx, StringComparison.Ordinal);
            if (textFieldIdx < 0) return null;

            int textStart = textFieldIdx + 8;
            int textEnd = FindUnescapedQuote(json, textStart);
            if (textEnd < 0) return null;

            return UnescapeJson(json.Substring(textStart, textEnd - textStart));
        }

        private int FindUnescapedQuote(string json, int startIndex)
        {
            for (int i = startIndex; i < json.Length; i++)
            {
                if (json[i] == '"' && (i == 0 || json[i - 1] != '\\'))
                    return i;
            }
            return -1;
        }

        private string ExtractJsonStringField(string json, string fieldName)
        {
            string search = $"\"{fieldName}\":\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = FindUnescapedQuote(json, start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
        }
    }
}
