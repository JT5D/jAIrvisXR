using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace jAIrvisXR.Networking
{
    /// <summary>
    /// Mock multiplayer provider for testing without a LiveKit server.
    /// Simulates room join/leave and fake remote participants.
    /// </summary>
    public class MockMultiplayerProvider : MonoBehaviour, IMultiplayerProvider
    {
        [SerializeField] private float _simulatedDelaySeconds = 0.3f;
        [SerializeField] private bool _simulateRemoteParticipant = true;
        [SerializeField] private string _fakeRemoteName = "MockUser";

        private readonly List<ParticipantInfo> _participants = new();
        private RoomConnectionState _connectionState = RoomConnectionState.Disconnected;

        public string ProviderName => "Mock Multiplayer";
        public bool IsReady { get; private set; }
        public RoomConnectionState ConnectionState => _connectionState;
        public IReadOnlyList<ParticipantInfo> Participants => _participants;

        public event Action<ParticipantInfo> OnParticipantJoined;
        public event Action<ParticipantInfo> OnParticipantLeft;
        public event Action<RoomConnectionState> OnConnectionStateChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsReady = true;
            Debug.Log($"[{ProviderName}] Initialized (mock).");
            return Task.CompletedTask;
        }

        public async Task<MultiplayerResult> JoinRoomAsync(string roomName, string identity,
            CancellationToken cancellationToken = default)
        {
            if (_connectionState == RoomConnectionState.Connected)
                return MultiplayerResult.Failure("Already connected.");

            SetConnectionState(RoomConnectionState.Connecting);
            await Task.Delay((int)(_simulatedDelaySeconds * 1000), cancellationToken);

            var localParticipant = new ParticipantInfo
            {
                Identity = identity,
                Name = identity,
                IsLocal = true,
                HasAudio = true,
                HasVideo = false
            };
            _participants.Add(localParticipant);

            SetConnectionState(RoomConnectionState.Connected);
            Debug.Log($"[{ProviderName}] Joined room '{roomName}' as '{identity}' (mock).");

            if (_simulateRemoteParticipant)
            {
                await Task.Delay(500, cancellationToken);
                var remote = new ParticipantInfo
                {
                    Identity = $"{_fakeRemoteName.ToLower()}-{UnityEngine.Random.Range(100, 999)}",
                    Name = _fakeRemoteName,
                    IsLocal = false,
                    HasAudio = true,
                    HasVideo = false
                };
                _participants.Add(remote);
                OnParticipantJoined?.Invoke(remote);
                Debug.Log($"[{ProviderName}] Remote participant joined: {remote} (mock).");
            }

            return MultiplayerResult.Success();
        }

        public async Task<MultiplayerResult> LeaveRoomAsync(CancellationToken cancellationToken = default)
        {
            if (_connectionState != RoomConnectionState.Connected)
                return MultiplayerResult.Failure("Not connected.");

            await Task.Delay((int)(_simulatedDelaySeconds * 1000), cancellationToken);

            foreach (var p in _participants)
            {
                if (!p.IsLocal) OnParticipantLeft?.Invoke(p);
            }
            _participants.Clear();

            SetConnectionState(RoomConnectionState.Disconnected);
            Debug.Log($"[{ProviderName}] Left room (mock).");
            return MultiplayerResult.Success();
        }

        public void Dispose()
        {
            _participants.Clear();
            IsReady = false;
        }

        private void OnDestroy() => Dispose();

        private void SetConnectionState(RoomConnectionState state)
        {
            if (_connectionState == state) return;
            _connectionState = state;
            OnConnectionStateChanged?.Invoke(state);
        }
    }
}
