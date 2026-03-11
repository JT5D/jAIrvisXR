using System;
using System.Threading;
using System.Threading.Tasks;
using jAIrvisXR.Core.Config;
using UnityEngine;

namespace jAIrvisXR.AI.Agent
{
    /// <summary>
    /// IAgentService implementation that routes through the Jarvis daemon
    /// (localhost:7437) instead of calling LLM APIs directly.
    /// Supports automatic parsing of scene actions from daemon responses.
    /// </summary>
    public class DaemonAgentService : MonoBehaviour, IAgentService
    {
        [SerializeField] private DaemonConfig _config;

        private DaemonClient _client;
        private Coroutine _healthCheckCoroutine;

        /// <summary>Fired when the daemon returns structured scene actions.</summary>
        public event Action<SemanticAction[]> OnSceneActions;

        /// <summary>Fired when daemon reachability changes.</summary>
        public event Action<bool> OnDaemonReachabilityChanged;

        public string ServiceName => "Jarvis Daemon (local)";
        public bool IsReady { get; private set; }
        public bool DaemonReachable => _client?.IsReachable ?? false;
        public string LastProvider { get; private set; }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_config == null)
            {
                Debug.LogError("[DaemonAgentService] DaemonConfig is null. Assign it in the Inspector.");
                return;
            }

            _client = new DaemonClient(_config);

            bool healthy = await _client.CheckHealthAsync(cancellationToken);
            if (healthy)
            {
                IsReady = true;
                Debug.Log($"[DaemonAgentService] Connected to daemon at {_config.BaseUrl}");
            }
            else
            {
                Debug.LogWarning($"[DaemonAgentService] Daemon not reachable at {_config.BaseUrl}. " +
                    "Will retry on next request.");
                // Don't fail — daemon might start later (launchd auto-start)
                IsReady = true;
            }

            StartHealthChecks();
        }

        public async Task<AgentResponse> SendMessageAsync(AgentRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_client == null)
                return AgentResponse.Failure("Daemon client not initialized.");

            DaemonResult result = await _client.SendCommandAsync(request.UserMessage, cancellationToken);

            if (!result.IsSuccess)
            {
                Debug.LogWarning($"[DaemonAgentService] Command failed: {result.ErrorMessage}");
                return AgentResponse.Failure(result.ErrorMessage);
            }

            // Parse the daemon response
            string json = result.Json;
            string responseText = ExtractField(json, "response");
            string action = ExtractField(json, "action");
            string provider = ExtractField(json, "provider");
            LastProvider = provider;

            // If this is a scene action, try to parse structured actions
            if (action == "scene" && !string.IsNullOrEmpty(responseText))
            {
                if (SemanticActionParser.TryParseActions(responseText, out var actions))
                {
                    Debug.Log($"[DaemonAgentService] Parsed {actions.Length} scene action(s) via {provider}");
                    OnSceneActions?.Invoke(actions);

                    // Return a human-readable summary for TTS
                    string summary = BuildActionSummary(actions);
                    return AgentResponse.Success(summary, "scene_action");
                }
            }

            // Regular text response
            if (string.IsNullOrEmpty(responseText))
                return AgentResponse.Failure("Empty response from daemon.");

            return AgentResponse.Success(responseText);
        }

        public void ResetConversation()
        {
            Debug.Log("[DaemonAgentService] Conversation reset (daemon manages its own history).");
        }

        public void Dispose()
        {
            IsReady = false;
            if (_healthCheckCoroutine != null)
                StopCoroutine(_healthCheckCoroutine);
        }

        private void OnDestroy() => Dispose();

        private void StartHealthChecks()
        {
            if (_config.HealthCheckIntervalSeconds <= 0) return;
            _healthCheckCoroutine = StartCoroutine(HealthCheckLoop());
        }

        private System.Collections.IEnumerator HealthCheckLoop()
        {
            bool lastReachable = _client.IsReachable;
            while (true)
            {
                yield return new WaitForSeconds(_config.HealthCheckIntervalSeconds);

                var task = _client.CheckHealthAsync();
                while (!task.IsCompleted)
                    yield return null;

                bool nowReachable = _client.IsReachable;
                if (nowReachable != lastReachable)
                {
                    Debug.Log($"[DaemonAgentService] Daemon reachability: {lastReachable} -> {nowReachable}");
                    OnDaemonReachabilityChanged?.Invoke(nowReachable);
                    lastReachable = nowReachable;
                }
            }
        }

        private static string BuildActionSummary(SemanticAction[] actions)
        {
            if (actions.Length == 1)
            {
                var a = actions[0];
                return a.Type switch
                {
                    SemanticActionType.ADD_OBJECT => FormatAddSummary(a),
                    SemanticActionType.REMOVE_OBJECT => $"Removing {a.Params.Shape ?? "object"}.",
                    SemanticActionType.CLEAR_SCENE => "Clearing the scene.",
                    SemanticActionType.MODIFY_OBJECTS => $"Modifying objects.",
                    SemanticActionType.TRANSFORM_OBJECT => $"Transforming {a.Params.TargetObject ?? "object"}.",
                    _ => $"Executing {a.Type}."
                };
            }

            return $"Executing {actions.Length} scene actions.";
        }

        private static string FormatAddSummary(SemanticAction a)
        {
            string shape = a.Params.Shape ?? "object";
            string color = a.Params.Color;
            int count = Mathf.Max(1, a.Params.Count);

            if (count > 1)
                return string.IsNullOrEmpty(color)
                    ? $"Adding {count} {shape}s."
                    : $"Adding {count} {color} {shape}s.";

            return string.IsNullOrEmpty(color)
                ? $"Adding a {shape}."
                : $"Adding a {color} {shape}.";
        }

        private static string ExtractField(string json, string field)
        {
            string search = $"\"{field}\":\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + search.Length;
            // Handle escaped quotes
            int end = start;
            while (end < json.Length)
            {
                if (json[end] == '"' && (end == 0 || json[end - 1] != '\\'))
                    break;
                end++;
            }
            if (end >= json.Length) return null;
            return json.Substring(start, end - start)
                .Replace("\\n", "\n")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
