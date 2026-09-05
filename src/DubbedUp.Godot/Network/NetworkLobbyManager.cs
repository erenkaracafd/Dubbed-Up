using System.Collections.Generic;
using System.Linq;
using Godot;
using DubbedUp.Godot.Steam;
using DubbedUp.Godot.Network.VoiceTransport;
using DubbedUp.Godot.Network.Sync;
using DubbedUp.Godot.Workshop;

namespace DubbedUp.Godot.Network;

/// <summary>
/// Manages high-level multiplayer connections, lobby player tracking, character assignment,
/// and synchronization across clients.
/// </summary>
public partial class NetworkLobbyManager : Node
{
    public const int DefaultPort = 7777;

    [Signal]
    public delegate void PlayerListUpdatedEventHandler();

    [Signal]
    public delegate void ConnectionStateChangedEventHandler(bool isConnected, string statusMessage);

    [Signal]
    public delegate void GameStartedEventHandler(string sceneId, string checksum);

    [Signal]
    public delegate void SelectedSceneChangedEventHandler(string sceneId, string sceneTitle, string checksum);

    [Signal]
    public delegate void SceneCompatibilityUpdatedEventHandler();

    [Signal]
    public delegate void AudioTakeReceivedEventHandler(string voiceSlotId, string senderPlayerId, byte[] audioData);

    [Signal]
    public delegate void VoiceTakeTransferProgressEventHandler(string transferId, float progressFraction);

    [Signal]
    public delegate void PlaybackScheduledEventHandler(string sceneId, string idempotencyToken, float delaySeconds);

    [Signal]
    public delegate void PlaybackStartTriggeredEventHandler(string sceneId, string idempotencyToken);

    [Signal]
    public delegate void PlaybackSyncFailedEventHandler(string reason);

    [Signal]
    public delegate void SteamStateChangedEventHandler(bool isAvailable, string statusMessage);

    private readonly Dictionary<long, NetworkPlayerInfo> _players = [];
    private readonly Dictionary<long, bool> _peerSceneCompatibility = [];
    private readonly Dictionary<long, string> _peerMismatchReasons = [];
    private readonly SteamLobbyService _steamLobby = new();
    private readonly VoiceTakeTransportManager _voiceTransport = new();
    private readonly PlaybackSyncCoordinator _syncCoordinator = new();
    private ENetMultiplayerPeer? _peer;
    private string _localPlayerName = "Host";

    public IReadOnlyDictionary<long, NetworkPlayerInfo> Players => _players;

    public string SelectedSceneId { get; private set; } = "museum-mixup";
    public string SelectedSceneTitle { get; private set; } = "Museum Mix-up";
    public string SelectedSceneChecksum { get; private set; } = string.Empty;
    public string SelectedSceneJson { get; private set; } = string.Empty;

    public IReadOnlyDictionary<long, bool> PeerSceneCompatibility => _peerSceneCompatibility;
    public IReadOnlyDictionary<long, string> PeerMismatchReasons => _peerMismatchReasons;

    public bool AllPeersHaveScene => _peerSceneCompatibility.Count == 0 || _peerSceneCompatibility.Values.All(has => has);

    public bool PeerHasScene(long peerId) => !_peerSceneCompatibility.TryGetValue(peerId, out var has) || has;

    public string GetPeerMismatchReason(long peerId) => _peerMismatchReasons.TryGetValue(peerId, out var r) ? r : string.Empty;

    public bool IsHost => Multiplayer.IsServer();

    public bool IsConnectedToLobby => _peer is not null && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

    public long LocalPeerId => Multiplayer.GetUniqueId();

    public bool IsSteamAvailable => _steamLobby.IsAvailable;

    public ulong CurrentSteamLobbyId => _steamLobby.CurrentLobbyId;

    public IReadOnlyList<SteamLobbyMember> SteamLobbyMembers => _steamLobby.Members;

    public event Action<ulong, IReadOnlyList<SteamLobbyMember>>? SteamLobbyChanged;

    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;

        AddChild(_voiceTransport);
        _voiceTransport.TransferCompleted += OnVoiceTakeTransferCompleted;
        _voiceTransport.TransferProgress += OnVoiceTakeTransferProgress;

        AddChild(_syncCoordinator);
        _syncCoordinator.PlaybackScheduled += OnPlaybackScheduled;
        _syncCoordinator.PlaybackStartTriggered += OnPlaybackStartTriggered;
        _syncCoordinator.PlaybackSyncFailed += OnPlaybackSyncFailed;

        _steamLobby.AvailabilityChanged += OnSteamAvailabilityChanged;
        _steamLobby.LobbyChanged += OnSteamLobbyChanged;
        _steamLobby.LobbyJoinRequested += OnSteamLobbyJoinRequested;
        _steamLobby.Initialize();
    }

    public override void _Process(double delta)
    {
        _steamLobby.RunCallbacks();
    }

    public override void _ExitTree()
    {
        _voiceTransport.TransferCompleted -= OnVoiceTakeTransferCompleted;
        _voiceTransport.TransferProgress -= OnVoiceTakeTransferProgress;

        _syncCoordinator.PlaybackScheduled -= OnPlaybackScheduled;
        _syncCoordinator.PlaybackStartTriggered -= OnPlaybackStartTriggered;
        _syncCoordinator.PlaybackSyncFailed -= OnPlaybackSyncFailed;

        _steamLobby.AvailabilityChanged -= OnSteamAvailabilityChanged;
        _steamLobby.LobbyChanged -= OnSteamLobbyChanged;
        _steamLobby.LobbyJoinRequested -= OnSteamLobbyJoinRequested;
        _steamLobby.Dispose();
    }

    public bool HostSteamLobby(int maxPlayers = 8)
    {
        return _steamLobby.CreateLobby(maxPlayers);
    }

    public bool JoinSteamLobby(ulong lobbyId)
    {
        return _steamLobby.JoinLobby(lobbyId);
    }

    public void LeaveSteamLobby()
    {
        _steamLobby.LeaveLobby();
    }

    public bool OpenSteamInviteOverlay()
    {
        return _steamLobby.OpenInviteOverlay();
    }

    public bool SetSteamLobbyMetadata(string key, string value)
    {
        return _steamLobby.SetMetadata(key, value);
    }

    public Error HostGame(int port = DefaultPort, string playerName = "Host")
    {
        LeaveGame();

        _localPlayerName = string.IsNullOrWhiteSpace(playerName) ? "Host" : playerName.Trim();
        _peer = new ENetMultiplayerPeer();
        var error = _peer.CreateServer(port, 8); // Max 8 players
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to create multiplayer server on port {port}: {error}");
            _peer = null;
            EmitSignal(SignalName.ConnectionStateChanged, false, $"Failed to host on port {port}: {error}");
            return error;
        }

        Multiplayer.MultiplayerPeer = _peer;

        // Register host player (Peer ID 1)
        _players[1] = new NetworkPlayerInfo
        {
            PeerId = 1,
            PlayerName = _localPlayerName,
            IsHost = true,
            IsReady = true,
        };

        _peerSceneCompatibility.Clear();
        _peerSceneCompatibility[1] = true;

        EmitSignal(SignalName.ConnectionStateChanged, true, $"Server hosting on port {port}");
        EmitSignal(SignalName.PlayerListUpdated);
        EmitSignal(SignalName.SceneCompatibilityUpdated);
        return Error.Ok;
    }

    public Error JoinGame(string address = "127.0.0.1", int port = DefaultPort, string playerName = "Player")
    {
        LeaveGame();

        _localPlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        _peer = new ENetMultiplayerPeer();
        var targetAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        var error = _peer.CreateClient(targetAddress, port);
        if (error != Error.Ok)
        {
            GD.PrintErr($"Failed to connect to {targetAddress}:{port}: {error}");
            _peer = null;
            EmitSignal(SignalName.ConnectionStateChanged, false, $"Connection failed: {error}");
            return error;
        }

        Multiplayer.MultiplayerPeer = _peer;
        EmitSignal(SignalName.ConnectionStateChanged, false, $"Connecting to {targetAddress}:{port}...");
        return Error.Ok;
    }

    public void LeaveGame()
    {
        if (_peer is not null)
        {
            _peer.Close();
            _peer = null;
            Multiplayer.MultiplayerPeer = null;
        }

        _voiceTransport.Reset();
        _syncCoordinator.Reset();
        _players.Clear();
        _peerSceneCompatibility.Clear();
        _peerMismatchReasons.Clear();
        EmitSignal(SignalName.ConnectionStateChanged, false, "Disconnected from lobby.");
        EmitSignal(SignalName.PlayerListUpdated);
        EmitSignal(SignalName.SceneCompatibilityUpdated);
    }

    public void SelectCharacter(string characterId)
    {
        ToggleCharacterClaim(characterId);
    }

    public void ToggleCharacterClaim(string characterId)
    {
        var localId = LocalPeerId;
        if (IsHost)
        {
            ServerToggleCharacter(localId, characterId);
        }
        else
        {
            RpcId(1, nameof(RequestToggleCharacter), localId, characterId);
        }
    }

    public void SetReadyState(bool isReady)
    {
        var localId = LocalPeerId;
        Rpc(nameof(SyncPlayerReady), localId, isReady);
    }

    public void SetSelectedScene(string sceneId, string sceneTitle, string checksum, string sceneJson)
    {
        SelectedSceneId = sceneId;
        SelectedSceneTitle = sceneTitle;
        SelectedSceneChecksum = checksum;
        SelectedSceneJson = sceneJson;

        if (IsHost)
        {
            // Reset character claims for all players since new scene has different characters
            foreach (var peerId in _players.Keys.ToList())
            {
                _players[peerId] = _players[peerId] with { AssignedCharacterIds = [] };
                Rpc(nameof(SyncPlayerCharacters), peerId, System.Array.Empty<string>());
            }

            _peerSceneCompatibility[1] = true;
            _peerMismatchReasons.Remove(1);
            foreach (var peerId in _players.Keys)
            {
                if (peerId != 1)
                {
                    _peerSceneCompatibility[peerId] = false;
                    _peerMismatchReasons[peerId] = "Verifying scene...";
                }
            }

            Rpc(nameof(SyncSelectedScene), sceneId, sceneTitle, checksum, sceneJson);
            EmitSignal(SignalName.SelectedSceneChanged, sceneId, sceneTitle, checksum);
            EmitSignal(SignalName.SceneCompatibilityUpdated);
            EmitSignal(SignalName.PlayerListUpdated);
        }
    }

    public void StartGame(string sceneId)
    {
        if (!IsHost)
        {
            return;
        }

        if (!AllPeersHaveScene)
        {
            GD.PrintErr("NetworkLobbyManager: Cannot start game because one or more peers do not have the matching scene installed.");
            return;
        }

        Rpc(nameof(RpcStartGame), sceneId, SelectedSceneChecksum);
    }

    public void BroadcastAudioTake(string voiceSlotId, byte[] audioData, float durationSeconds = 0f)
    {
        _voiceTransport.SendVoiceTake(voiceSlotId, _localPlayerName, audioData, durationSeconds);
    }

    public void StartClockSync() => _syncCoordinator.StartClockSync();

    public void ReportSceneReady(string sceneId) => _syncCoordinator.ReportSceneReady(sceneId);

    public void ReportTakesReady() => _syncCoordinator.ReportTakesReady();

    public bool HostSchedulePlayback(string sceneId, int roundNumber = 1, string sessionId = "") =>
        _syncCoordinator.HostSchedulePlayback(sceneId, roundNumber, sessionId);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RegisterPlayer(long peerId, string name)
    {
        if (IsHost)
        {
            _players[peerId] = new NetworkPlayerInfo
            {
                PeerId = peerId,
                PlayerName = name,
                IsHost = peerId == 1,
                IsReady = false,
                AssignedCharacterIds = [],
            };

            if (peerId != 1)
            {
                _peerSceneCompatibility[peerId] = false;
                _peerMismatchReasons[peerId] = "Verifying scene...";
            }

            // Broadcast full updated player roster to all clients
            foreach (var (id, p) in _players)
            {
                Rpc(nameof(SyncPlayerInfo), id, p.PlayerName, p.IsHost, p.IsReady, p.AssignedCharacterIds);
            }

            // Sync currently selected scene, checksum, and host scene edit to the newly connected peer
            RpcId(peerId, nameof(SyncSelectedScene), SelectedSceneId, SelectedSceneTitle, SelectedSceneChecksum, SelectedSceneJson);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SyncSelectedScene(string sceneId, string sceneTitle, string checksum, string sceneJson)
    {
        SelectedSceneId = sceneId;
        SelectedSceneTitle = sceneTitle;
        SelectedSceneChecksum = checksum;
        SelectedSceneJson = sceneJson;
        EmitSignal(SignalName.SelectedSceneChanged, sceneId, sceneTitle, checksum);

        if (!IsHost)
        {
            var workshop = new SteamWorkshopService();
            var localScenes = workshop.GetAvailableScenes();
            var localScene = localScenes.FirstOrDefault(s => s.SceneId.Equals(sceneId, System.StringComparison.OrdinalIgnoreCase));
            var localId = LocalPeerId;

            if (localScene is null || string.IsNullOrEmpty(localScene.VideoFilePath) || !System.IO.File.Exists(localScene.VideoFilePath))
            {
                RpcId(1, nameof(ReportSceneCompatibility), localId, sceneId, false, "Video not installed");
            }
            else
            {
                // Local video file exists! We use Host's sceneJson in memory.
                RpcId(1, nameof(ReportSceneCompatibility), localId, sceneId, true, string.Empty);
            }

            EmitSignal(SignalName.SceneCompatibilityUpdated);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void ReportSceneCompatibility(long peerId, string sceneId, bool hasScene, string reason)
    {
        if (IsHost && sceneId.Equals(SelectedSceneId, System.StringComparison.OrdinalIgnoreCase))
        {
            _peerSceneCompatibility[peerId] = hasScene;
            if (!hasScene && !string.IsNullOrEmpty(reason))
            {
                _peerMismatchReasons[peerId] = reason;
            }
            else
            {
                _peerMismatchReasons.Remove(peerId);
            }
            EmitSignal(SignalName.SceneCompatibilityUpdated);
            EmitSignal(SignalName.PlayerListUpdated);
        }
    }

    private void ServerToggleCharacter(long peerId, string characterId)
    {
        if (!IsHost) return;

        // Check if another player already claimed this character
        var otherOwner = _players.Values.FirstOrDefault(p => p.PeerId != peerId && p.AssignedCharacterIds.Contains(characterId));
        if (otherOwner is not null)
        {
            GD.Print($"Character '{characterId}' is already claimed by {otherOwner.PlayerName}");
            return;
        }

        if (_players.TryGetValue(peerId, out var player))
        {
            var list = player.AssignedCharacterIds.ToList();
            if (list.Contains(characterId))
            {
                list.Remove(characterId);
            }
            else
            {
                list.Add(characterId);
            }

            var updated = list.ToArray();
            _players[peerId] = player with { AssignedCharacterIds = updated };

            Rpc(nameof(SyncPlayerCharacters), peerId, updated);
            EmitSignal(SignalName.PlayerListUpdated);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RequestToggleCharacter(long peerId, string characterId)
    {
        if (IsHost)
        {
            ServerToggleCharacter(peerId, characterId);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SyncPlayerCharacters(long peerId, string[] characterIds)
    {
        if (_players.TryGetValue(peerId, out var player))
        {
            _players[peerId] = player with { AssignedCharacterIds = characterIds ?? [] };
            EmitSignal(SignalName.PlayerListUpdated);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SyncPlayerInfo(long peerId, string name, bool isHost, bool isReady, string[] characterIds)
    {
        _players[peerId] = new NetworkPlayerInfo
        {
            PeerId = peerId,
            PlayerName = name,
            IsHost = isHost,
            IsReady = isReady,
            AssignedCharacterIds = characterIds ?? [],
        };

        EmitSignal(SignalName.PlayerListUpdated);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SyncPlayerReady(long peerId, bool isReady)
    {
        if (_players.TryGetValue(peerId, out var player))
        {
            _players[peerId] = player with { IsReady = isReady };
            EmitSignal(SignalName.PlayerListUpdated);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
    private void RpcStartGame(string sceneId, string checksum)
    {
        EmitSignal(SignalName.GameStarted, sceneId, checksum);
    }

    private void OnVoiceTakeTransferCompleted(string transferId, string voiceSlotId, long senderPeerId, string senderName, byte[] audioData)
    {
        EmitSignal(SignalName.AudioTakeReceived, voiceSlotId, senderName, audioData);
    }

    private void OnVoiceTakeTransferProgress(string transferId, float progressFraction)
    {
        EmitSignal(SignalName.VoiceTakeTransferProgress, transferId, progressFraction);
    }

    private void OnPlaybackScheduled(string sceneId, string idempotencyToken, float delaySeconds)
    {
        EmitSignal(SignalName.PlaybackScheduled, sceneId, idempotencyToken, delaySeconds);
    }

    private void OnPlaybackStartTriggered(string sceneId, string idempotencyToken)
    {
        EmitSignal(SignalName.PlaybackStartTriggered, sceneId, idempotencyToken);
    }

    private void OnPlaybackSyncFailed(string reason)
    {
        EmitSignal(SignalName.PlaybackSyncFailed, reason);
    }

    private void OnPeerConnected(long id)
    {
        GD.Print($"Multiplayer peer connected: {id}");
        _syncCoordinator.ReadyBarrier.RegisterPeer(id);
    }

    private void OnPeerDisconnected(long id)
    {
        GD.Print($"Multiplayer peer disconnected: {id}");
        _syncCoordinator.ReadyBarrier.UnregisterPeer(id);
        _players.Remove(id);
        _peerSceneCompatibility.Remove(id);
        _peerMismatchReasons.Remove(id);
        EmitSignal(SignalName.PlayerListUpdated);
        EmitSignal(SignalName.SceneCompatibilityUpdated);
    }

    private void OnConnectedToServer()
    {
        GD.Print("Successfully connected to server.");
        EmitSignal(SignalName.ConnectionStateChanged, true, "Connected to host lobby!");
        RpcId(1, nameof(RegisterPlayer), LocalPeerId, _localPlayerName);
    }

    private void OnConnectionFailed()
    {
        GD.PrintErr("Connection failed.");
        LeaveGame();
        EmitSignal(SignalName.ConnectionStateChanged, false, "Connection to host failed.");
    }

    private void OnServerDisconnected()
    {
        GD.Print("Server disconnected.");
        LeaveGame();
        EmitSignal(SignalName.ConnectionStateChanged, false, "Host closed the lobby.");
    }

    private void OnSteamAvailabilityChanged(bool isAvailable, string message)
    {
        EmitSignal(SignalName.SteamStateChanged, isAvailable, message);
    }

    private void OnSteamLobbyChanged(ulong lobbyId, IReadOnlyList<SteamLobbyMember> members)
    {
        SteamLobbyChanged?.Invoke(lobbyId, members);
    }

    private void OnSteamLobbyJoinRequested(ulong lobbyId)
    {
        if (!_steamLobby.JoinLobby(lobbyId))
        {
            EmitSignal(SignalName.SteamStateChanged, true, $"Could not join requested Steam lobby {lobbyId}.");
        }
    }
}

