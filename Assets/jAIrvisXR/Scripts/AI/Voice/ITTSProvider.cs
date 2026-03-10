using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    public struct TTSResult
    {
        public AudioClip AudioClip;
        public bool IsSuccess;
        public string ErrorMessage;

        public static TTSResult Success(AudioClip clip)
            => new() { AudioClip = clip, IsSuccess = true };

        public static TTSResult Failure(string error)
            => new() { IsSuccess = false, ErrorMessage = error };
    }

    public interface ITTSProvider
    {
        string ProviderName { get; }
        bool IsReady { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<TTSResult> SynthesizeAsync(string text, CancellationToken cancellationToken = default);
        void Dispose();
    }
}
