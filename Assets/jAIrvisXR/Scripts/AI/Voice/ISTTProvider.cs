using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    public struct STTResult
    {
        public string Transcript;
        public float Confidence;
        public bool IsSuccess;
        public string ErrorMessage;

        public static STTResult Success(string transcript, float confidence = 1f)
            => new() { Transcript = transcript, Confidence = confidence, IsSuccess = true };

        public static STTResult Failure(string error)
            => new() { IsSuccess = false, ErrorMessage = error };
    }

    public interface ISTTProvider
    {
        string ProviderName { get; }
        bool IsReady { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<STTResult> TranscribeAsync(AudioClip audioClip, CancellationToken cancellationToken = default);
        Task<STTResult> TranscribeAsync(float[] samples, int sampleRate, int channels,
            CancellationToken cancellationToken = default);
        void Dispose();
    }
}
