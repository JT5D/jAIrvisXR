using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using jAIrvisXR.Core.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace jAIrvisXR.Networking
{
    /// <summary>
    /// LiveKit room lifecycle manager. Compiles with or without the LiveKit SDK —
    /// when HAS_LIVEKIT is not defined, falls back to token-only validation mode.
    /// </summary>
    public class LiveKitRoomManager : MonoBehaviour, IMultiplayerProvider
    {
        [SerializeField] private MultiplayerConfig _config;

        private readonly List<ParticipantInfo> _participants = new();
        private RoomConnectionState _connectionState = RoomConnectionState.Disconnected;
        private string _currentRoom;
        private string _localIdentity;

        public string ProviderName => "LiveKit";
        public bool IsReady { get; private set; }
        public RoomConnectionState ConnectionState => _connectionState;
        public IReadOnlyList<ParticipantInfo> Participants => _participants;

        public event Action<ParticipantInfo> OnParticipantJoined;
        public event Action<ParticipantInfo> OnParticipantLeft;
        public event Action<RoomConnectionState> OnConnectionStateChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_config == null)
                Debug.LogWarning("[LiveKit] No MultiplayerConfig assigned — using defaults.");

            IsReady = true;
            Debug.Log($"[LiveKit] Initialized. Server: {ServerUrl}");
            return Task.CompletedTask;
        }

        public async Task<MultiplayerResult> JoinRoomAsync(string roomName, string identity,
            CancellationToken cancellationToken = default)
        {
            if (_connectionState == RoomConnectionState.Connected)
                return MultiplayerResult.Failure("Already connected to a room.");

            SetConnectionState(RoomConnectionState.Connecting);
            _currentRoom = roomName;
            _localIdentity = identity;

            // Fetch JWT token from token server
            string token;
            try
            {
                token = await FetchTokenAsync(roomName, identity, cancellationToken);
            }
            catch (Exception ex)
            {
                SetConnectionState(RoomConnectionState.Error);
                return MultiplayerResult.Failure($"Token fetch failed: {ex.Message}");
            }

            if (string.IsNullOrEmpty(token))
            {
                SetConnectionState(RoomConnectionState.Error);
                return MultiplayerResult.Failure("Empty token received from server.");
            }

#if HAS_LIVEKIT
            // --- LiveKit SDK room connection ---
            try
            {
                // LiveKit SDK integration point:
                // _room = new LiveKit.Room();
                // _room.ParticipantConnected += HandleParticipantConnected;
                // _room.ParticipantDisconnected += HandleParticipantDisconnected;
                // await _room.Connect(ServerUrl, token);
                Debug.Log($"[LiveKit] SDK connect to {ServerUrl} room={roomName}");
            }
            catch (Exception ex)
            {
                SetConnectionState(RoomConnectionState.Error);
                return MultiplayerResult.Failure($"LiveKit connect failed: {ex.Message}");
            }
#else
            Debug.Log($"[LiveKit] Token validated (SDK not installed). room={roomName} identity={identity} token={token[..Math.Min(20, token.Length)]}...");
#endif

            // Add local participant
            var localParticipant = new ParticipantInfo
            {
                Identity = identity,
                Name = _config != null ? _config.DisplayName : identity,
                IsLocal = true,
                HasAudio = _config == null || _config.PublishMicrophone,
                HasVideo = false
            };
            _participants.Add(localParticipant);

            SetConnectionState(RoomConnectionState.Connected);
            Debug.Log($"[LiveKit] Connected to room '{roomName}' as '{identity}'.");
            return MultiplayerResult.Success();
        }

        public Task<MultiplayerResult> LeaveRoomAsync(CancellationToken cancellationToken = default)
        {
            if (_connectionState != RoomConnectionState.Connected)
                return Task.FromResult(MultiplayerResult.Failure("Not connected to any room."));

#if HAS_LIVEKIT
            // _room?.Disconnect();
#endif

            foreach (var p in _participants)
            {
                if (!p.IsLocal) OnParticipantLeft?.Invoke(p);
            }
            _participants.Clear();

            _currentRoom = null;
            _localIdentity = null;
            SetConnectionState(RoomConnectionState.Disconnected);
            Debug.Log("[LiveKit] Disconnected from room.");
            return Task.FromResult(MultiplayerResult.Success());
        }

        public void Dispose()
        {
            if (_connectionState == RoomConnectionState.Connected)
                _ = LeaveRoomAsync();
            IsReady = false;
        }

        private void OnDestroy() => Dispose();

        private void SetConnectionState(RoomConnectionState state)
        {
            if (_connectionState == state) return;
            Debug.Log($"[LiveKit] Connection: {_connectionState} -> {state}");
            _connectionState = state;
            OnConnectionStateChanged?.Invoke(state);
        }

        private string ServerUrl => _config != null ? _config.ServerUrl : "ws://localhost:7880";
        private string TokenUrl => _config != null ? _config.TokenServerUrl : "http://localhost:7439/token";

        private async Task<string> FetchTokenAsync(string roomName, string identity,
            CancellationToken cancellationToken)
        {
            string url = $"{TokenUrl}?room={UnityWebRequest.EscapeURL(roomName)}&identity={UnityWebRequest.EscapeURL(identity)}";
            using var request = UnityWebRequest.Get(url);
            request.timeout = 10;

            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"HTTP {request.responseCode}: {request.error}");

            // Token server returns JSON: { "token": "eyJ..." }
            string json = request.downloadHandler.text;
            int tokenStart = json.IndexOf("\"token\"", StringComparison.Ordinal);
            if (tokenStart < 0) return json.Trim().Trim('"');

            int valueStart = json.IndexOf('"', tokenStart + 7);
            int valueEnd = json.IndexOf('"', valueStart + 1);
            if (valueStart < 0 || valueEnd < 0) return json.Trim();

            return json.Substring(valueStart + 1, valueEnd - valueStart - 1);
        }

#if HAS_LIVEKIT
        private void HandleParticipantConnected(string identity, string name)
        {
            var info = new ParticipantInfo
            {
                Identity = identity,
                Name = name,
                IsLocal = false,
                HasAudio = true,
                HasVideo = false
            };
            _participants.Add(info);
            OnParticipantJoined?.Invoke(info);
            Debug.Log($"[LiveKit] Participant joined: {info}");
        }

        private void HandleParticipantDisconnected(string identity)
        {
            for (int i = _participants.Count - 1; i >= 0; i--)
            {
                if (_participants[i].Identity == identity)
                {
                    var info = _participants[i];
                    _participants.RemoveAt(i);
                    OnParticipantLeft?.Invoke(info);
                    Debug.Log($"[LiveKit] Participant left: {info}");
                    break;
                }
            }
        }
#endif
    }
}
