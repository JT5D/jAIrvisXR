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
    /// Requests PCM s16le audio and creates AudioClip directly — no multimedia module needed.
    /// </summary>
    public class ElevenLabsTTSProvider : MonoBehaviour, ITTSProvider
    {
        [SerializeField] private TTSConfig _config;

        private const int PCM_SAMPLE_RATE = 22050;

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

            string baseEndpoint = (_config != null && !string.IsNullOrEmpty(_config.Endpoint))
                ? _config.Endpoint
                : "https://api.elevenlabs.io/v1/text-to-speech";
            string endpoint = $"{baseEndpoint}/{_voiceId}";

            // Add output_format query param for raw PCM
            endpoint += "?output_format=pcm_22050";

            string jsonBody = BuildRequestJson(text);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using var request = new UnityWebRequest(endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", _apiKey);

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

            byte[] pcmData = request.downloadHandler.data;
            if (pcmData == null || pcmData.Length < 2)
                return TTSResult.Failure("Empty audio response.");

            AudioClip clip = PcmToAudioClip(pcmData, PCM_SAMPLE_RATE);
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

        /// <summary>
        /// Convert raw PCM s16le bytes to Unity AudioClip.
        /// </summary>
        private static AudioClip PcmToAudioClip(byte[] pcmData, int sampleRate)
        {
            int sampleCount = pcmData.Length / 2; // 16-bit = 2 bytes per sample
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
                samples[i] = s / 32768f;
            }

            var clip = AudioClip.Create("ElevenLabsTTS", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
