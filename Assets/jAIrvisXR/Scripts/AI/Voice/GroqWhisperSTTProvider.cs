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
    /// Real STT provider using Groq Whisper API (free tier).
    /// Sends raw audio as WAV via multipart/form-data.
    /// </summary>
    public class GroqWhisperSTTProvider : MonoBehaviour, ISTTProvider
    {
        [SerializeField] private STTConfig _config;

        private string _apiKey;

        public string ProviderName => "Groq Whisper";
        public bool IsReady { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _apiKey = _config != null ? _config.ApiKey : null;
            if (string.IsNullOrEmpty(_apiKey))
                _apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");

            if (string.IsNullOrEmpty(_apiKey))
            {
                Debug.LogError($"[{ProviderName}] No API key. Set in STTConfig or GROQ_API_KEY env var.");
                return Task.CompletedTask;
            }

            IsReady = true;
            Debug.Log($"[{ProviderName}] Initialized.");
            return Task.CompletedTask;
        }

        public async Task<STTResult> TranscribeAsync(AudioClip audioClip,
            CancellationToken cancellationToken = default)
        {
            if (!IsReady) return STTResult.Failure("Not initialized.");

            float[] samples = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(samples, 0);
            return await TranscribeAsync(samples, audioClip.frequency, audioClip.channels, cancellationToken);
        }

        public async Task<STTResult> TranscribeAsync(float[] samples, int sampleRate, int channels,
            CancellationToken cancellationToken = default)
        {
            if (!IsReady) return STTResult.Failure("Not initialized.");

            byte[] wavBytes = EncodeWav(samples, sampleRate, channels);

            // Build multipart form data
            var form = new WWWForm();
            form.AddBinaryData("file", wavBytes, "audio.wav", "audio/wav");
            form.AddField("model", "whisper-large-v3");
            form.AddField("language", _config != null ? _config.Language : "en");
            form.AddField("response_format", "json");

            string endpoint = (_config != null && !string.IsNullOrEmpty(_config.Endpoint))
                ? _config.Endpoint
                : "https://api.groq.com/openai/v1/audio/transcriptions";

            using var request = UnityWebRequest.Post(endpoint, form);
            request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    return STTResult.Failure("Cancelled.");
                }
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = request.downloadHandler?.text ?? request.error;
                Debug.LogError($"[{ProviderName}] API error: {error}");
                return STTResult.Failure($"HTTP {request.responseCode}: {error}");
            }

            string json = request.downloadHandler.text;
            string transcript = ExtractTextField(json, "text");

            if (string.IsNullOrEmpty(transcript))
                return STTResult.Failure("Empty transcript.");

            Debug.Log($"[{ProviderName}] Transcript: \"{transcript}\"");
            return STTResult.Success(transcript);
        }

        public void Dispose() { IsReady = false; }

        /// <summary>
        /// Encode float samples to 16-bit PCM WAV byte array.
        /// </summary>
        private static byte[] EncodeWav(float[] samples, int sampleRate, int channels)
        {
            int sampleCount = samples.Length;
            int byteRate = sampleRate * channels * 2;
            int dataSize = sampleCount * 2;
            int fileSize = 44 + dataSize;

            byte[] wav = new byte[fileSize];
            // RIFF header
            Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            BitConverter.GetBytes(fileSize - 8).CopyTo(wav, 4);
            Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
            // fmt chunk
            Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
            BitConverter.GetBytes(16).CopyTo(wav, 16); // chunk size
            BitConverter.GetBytes((short)1).CopyTo(wav, 20); // PCM
            BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
            BitConverter.GetBytes(byteRate).CopyTo(wav, 28);
            BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32);
            BitConverter.GetBytes((short)16).CopyTo(wav, 34); // bits per sample
            // data chunk
            Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
            BitConverter.GetBytes(dataSize).CopyTo(wav, 40);

            int offset = 44;
            for (int i = 0; i < sampleCount; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                short val = (short)(clamped * 32767f);
                BitConverter.GetBytes(val).CopyTo(wav, offset);
                offset += 2;
            }

            return wav;
        }

        private static string ExtractTextField(string json, string fieldName)
        {
            string search = $"\"{fieldName}\":\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start)
                .Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
