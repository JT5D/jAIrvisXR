using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    public class MockTTSProvider : MonoBehaviour, ITTSProvider
    {
        [SerializeField] private float _simulatedDelaySeconds = 0.3f;
        [SerializeField] private float _mockClipDurationSeconds = 2f;

        public string ProviderName => "Mock TTS";
        public bool IsReady { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsReady = true;
            Debug.Log($"[{ProviderName}] Initialized (mock).");
            return Task.CompletedTask;
        }

        public async Task<TTSResult> SynthesizeAsync(string text,
            CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{ProviderName}] Synthesizing: \"{text}\" (mock)...");
            await Task.Delay((int)(_simulatedDelaySeconds * 1000), cancellationToken);

            int sampleRate = 16000;
            int sampleCount = (int)(sampleRate * _mockClipDurationSeconds);
            var clip = AudioClip.Create("MockTTS", sampleCount, 1, sampleRate, false);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / sampleRate) * 0.3f;
            }
            clip.SetData(samples, 0);

            return TTSResult.Success(clip);
        }

        public void Dispose() { IsReady = false; }
    }
}
