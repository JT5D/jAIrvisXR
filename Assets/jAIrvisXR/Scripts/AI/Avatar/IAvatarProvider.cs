using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace jAIrvisXR.AI.Avatar
{
    public struct AvatarResult
    {
        public GameObject AvatarRoot;
        public bool IsSuccess;
        public string ErrorMessage;

        public static AvatarResult Success(GameObject avatarRoot)
            => new() { AvatarRoot = avatarRoot, IsSuccess = true };

        public static AvatarResult Failure(string error)
            => new() { IsSuccess = false, ErrorMessage = error };
    }

    public interface IAvatarProvider
    {
        string ProviderName { get; }
        bool IsReady { get; }
        GameObject CurrentAvatar { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<AvatarResult> LoadAvatarAsync(string pathOrUrl,
            CancellationToken cancellationToken = default);
        Task<AvatarResult> LoadAvatarAsync(byte[] vrmData,
            CancellationToken cancellationToken = default);
        void UnloadAvatar();
        void Dispose();
    }
}
