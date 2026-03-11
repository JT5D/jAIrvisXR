using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jAIrvisXR.Core.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace jAIrvisXR.AI.Agent
{
    /// <summary>
    /// HTTP client for the Jarvis daemon (localhost:7437).
    /// Wraps UnityWebRequest with async/cancellation support.
    /// </summary>
    public class DaemonClient
    {
        private readonly DaemonConfig _config;
        private bool _daemonReachable;

        public bool IsReachable => _daemonReachable;

        public DaemonClient(DaemonConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Check if the daemon is running and reachable.
        /// </summary>
        public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
        {
            try
            {
                string url = $"{_config.BaseUrl}/api/health";
                using var req = UnityWebRequest.Get(url);
                req.timeout = 3;

                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (ct.IsCancellationRequested) { req.Abort(); return false; }
                    await Task.Yield();
                }

                _daemonReachable = req.result == UnityWebRequest.Result.Success;
                return _daemonReachable;
            }
            catch
            {
                _daemonReachable = false;
                return false;
            }
        }

        /// <summary>
        /// Send a command to POST /api/command and return the raw JSON response.
        /// </summary>
        public async Task<DaemonResult> SendCommandAsync(string command, CancellationToken ct = default)
        {
            string url = $"{_config.BaseUrl}/api/command";
            string body = $"{{\"command\":\"{EscapeJson(command)}\"}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.CeilToInt(_config.TimeoutSeconds);

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    req.Abort();
                    return DaemonResult.Failure("Request cancelled.");
                }
                await Task.Yield();
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                string err = req.downloadHandler?.text ?? req.error;
                _daemonReachable = false;
                return DaemonResult.Failure($"HTTP {req.responseCode}: {err}");
            }

            _daemonReachable = true;
            string json = req.downloadHandler.text;
            return DaemonResult.Ok(json);
        }

        /// <summary>
        /// Get daemon status from GET /api/status.
        /// </summary>
        public async Task<DaemonResult> GetStatusAsync(CancellationToken ct = default)
        {
            string url = $"{_config.BaseUrl}/api/status";
            using var req = UnityWebRequest.Get(url);
            req.timeout = 3;

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (ct.IsCancellationRequested) { req.Abort(); return DaemonResult.Failure("Cancelled."); }
                await Task.Yield();
            }

            if (req.result != UnityWebRequest.Result.Success)
                return DaemonResult.Failure(req.error);

            return DaemonResult.Ok(req.downloadHandler.text);
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
    }

    public struct DaemonResult
    {
        public bool IsSuccess;
        public string Json;
        public string ErrorMessage;

        public static DaemonResult Ok(string json) => new() { IsSuccess = true, Json = json };
        public static DaemonResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
    }
}
