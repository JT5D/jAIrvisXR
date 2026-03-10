using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace jAIrvisXR.Networking
{
    public enum RoomConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Error
    }

    public struct ParticipantInfo
    {
        public string Identity;
        public string Name;
        public bool IsLocal;
        public bool HasAudio;
        public bool HasVideo;

        public override string ToString() => $"{Name} ({Identity})";
    }

    public struct MultiplayerResult
    {
        public bool IsSuccess;
        public string ErrorMessage;

        public static MultiplayerResult Success()
            => new() { IsSuccess = true };

        public static MultiplayerResult Failure(string error)
            => new() { IsSuccess = false, ErrorMessage = error };
    }

    public interface IMultiplayerProvider
    {
        string ProviderName { get; }
        bool IsReady { get; }
        RoomConnectionState ConnectionState { get; }
        IReadOnlyList<ParticipantInfo> Participants { get; }

        event Action<ParticipantInfo> OnParticipantJoined;
        event Action<ParticipantInfo> OnParticipantLeft;
        event Action<RoomConnectionState> OnConnectionStateChanged;

        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<MultiplayerResult> JoinRoomAsync(string roomName, string identity,
            CancellationToken cancellationToken = default);
        Task<MultiplayerResult> LeaveRoomAsync(CancellationToken cancellationToken = default);
        void Dispose();
    }
}
