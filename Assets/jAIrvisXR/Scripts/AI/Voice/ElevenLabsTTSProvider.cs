using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jAIrvisXR.Core.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace jAIrvisXR.AI.Voice
{
    /// <summary>
    /// Real TTS provider using ElevenLabs API.
    /// Returns audio as AudioClip for Unity playback.
    /// </summary>
    public class ElevenLabsTTSProvider : MonoBehaviour, ITTSProvider
    {
        [SerializeField] private TTSConfig _config;

        private string _apiKey;
        private string _voiceId;

        public string ProviderName => "ElevenLabs";
        public bool IsReady { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _apiKey = _config != null ? _config.ApiKey : null;
            if (string.IsNullOrEmpty(_apiKey))
                _apiKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");

            _voiceId = _config != null ? _config.VoiceId : "21m00Tcm4TlvDq8ikWAM";
            if (string.IsNullOrEmpty(_voiceId))
                _voiceId = "21m00Tcm4TlvDq8ikWAM"; // Rachel (default)

            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogError($"[{ProviderName}] No API key. Set in TTSConfig or ELEVENLABS_API_KEY env var.");
                return Task.CompletedTask;
            }

            IsReady = true;
            Debug.Log($"[{ProviderName}] Initialized. Voice: {_voiceId}");
            return Task.CompletedTask;
        }

        public async Task<TTSResult> SynthesizeAsync(string text,
            CancellationToken cancellationToken = default)
        {
            if (!IsReady) return TTSResult.Failure("Not initialized.");
            if (string.IsNullOrEmpty(text)) return TTSResult.Failure("Empty text.");

            string endpoint = (_config != null && !string.IsNullOrEmpty(_config.Endpoint))
                ? _config.Endpoint
                : $"https://api.elevenlabs.io/v1/text-to-speech/{_voiceId}";

            string jsonBody = BuildRequestJson(text);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using var request = new UnityWebRequest(endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", _apiKey);
            // Request PCM format for easier AudioClip creation
            request.SetRequestHeader("Accept", "audio/mpeg");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    return TTSResult.Failure("Cancelled.");
                }
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = request.downloadHandler?.text ?? request.error;
                Debug.LogError($"[{ProviderName}] API error: {error}");
                return TTSResult.Failure($"HTTP {request.responseCode}: {error}");
            }

            byte[] audioData = request.downloadHandler.data;
            if (audioData == null || audioData.Length == 0)
                return TTSResult.Failure("Empty audio response.");

            // ElevenLabs returns MP3 — decode via AudioClip
            // Unity doesn't natively decode MP3 from bytes, so we write to temp file
            // and use UnityWebRequestMultimedia
            AudioClip clip = await DecodeMp3(audioData, cancellationToken);
            if (clip == null)
                return TTSResult.Failure("Failed to decode audio.");

            Debug.Log($"[{ProviderName}] Synthesized {clip.length:F1}s audio.");
            return TTSResult.Success(clip);
        }

        public void Dispose() { IsReady = false; }

        private string BuildRequestJson(string text)
        {
            float speed = _config != null ? _config.Speed : 1f;
            string escaped = text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            return $"{{" +
                $"\"text\":\"{escaped}\"," +
                $"\"model_id\":\"eleven_monolingual_v1\"," +
                $"\"voice_settings\":{{" +
                $"\"stability\":0.5," +
                $"\"similarity_boost\":0.75," +
                $"\"speed\":{speed:F2}" +
                $"}}" +
                $"}}";
        }

        private async Task<AudioClip> DecodeMp3(byte[] mp3Data,
            CancellationToken cancellationToken)
        {
            // Write to temp file, then load via UnityWebRequestMultimedia
            string tempPath = System.IO.Path.Combine(
                Application.temporaryCachePath, $"tts_{DateTime.Now.Ticks}.mp3");

            try
            {
                System.IO.File.WriteAllBytes(tempPath, mp3Data);

                using var audioRequest = UnityWebRequestMultimedia.GetAudioClip(
                    "file://" + tempPath, AudioType.MPEG);

                var op = audioRequest.SendWebRequest();
                while (!op.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        audioRequest.Abort();
                        return null;
                    }
                    await Task.Yield();
                }

                if (audioRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[{ProviderName}] Audio decode error: {audioRequest.error}");
                    return null;
                }

                return DownloadHandlerAudioClip.GetContent(audioRequest);
            }
            finally
            {
                try { System.IO.File.Delete(tempPath); } catch { }
            }
        }
    }
}
