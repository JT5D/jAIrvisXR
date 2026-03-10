using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    public class MockSTTProvider : MonoBehaviour, ISTTProvider
    {
        [SerializeField] private string _mockTranscript = "Hello Jarvis, what can you do?";
        [SerializeField] private float _simulatedDelaySeconds = 0.5f;

        public string ProviderName => "Mock STT";
        public bool IsReady { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsReady = true;
            Debug.Log($"[{ProviderName}] Initialized (mock).");
            return Task.CompletedTask;
        }

        public async Task<STTResult> TranscribeAsync(AudioClip audioClip,
            CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{ProviderName}] Transcribing {audioClip.length:F1}s of audio (mock)...");
            await Task.Delay((int)(_simulatedDelaySeconds * 1000), cancellationToken);
            return STTResult.Success(_mockTranscript, 0.95f);
        }

        public async Task<STTResult> TranscribeAsync(float[] samples, int sampleRate, int channels,
            CancellationToken cancellationToken = default)
        {
            float duration = (float)samples.Length / sampleRate / channels;
            Debug.Log($"[{ProviderName}] Transcribing {duration:F1}s of raw audio (mock)...");
            await Task.Delay((int)(_simulatedDelaySeconds * 1000), cancellationToken);
            return STTResult.Success(_mockTranscript, 0.95f);
        }

        public void Dispose() { IsReady = false; }
    }
}
