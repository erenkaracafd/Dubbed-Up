using Godot;

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
    public delegate void GameStartedEventHandler(string sceneId);

    [Signal]
    public delegate void AudioTakeReceivedEventHandler(string voiceSlotId, string senderPlayerId, byte[] audioData);

    private readonly Dictionary<long, NetworkPlayerInfo> _players = [];
    private ENetMultiplayerPeer? _peer;
    private string _localPlayerName = "Host";

    public IReadOnlyDictionary<long, NetworkPlayerInfo> Players => _players;

    public bool IsHost => Multiplayer.IsServer();

    public bool IsConnectedToLobby => _peer is not null && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

    public long LocalPeerId => Multiplayer.GetUniqueId();

    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
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

        EmitSignal(SignalName.ConnectionStateChanged, true, $"Server hosting on port {port}");
        EmitSignal(SignalName.PlayerListUpdated);
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

        _players.Clear();
        EmitSignal(SignalName.ConnectionStateChanged, false, "Disconnected from lobby.");
        EmitSignal(SignalName.PlayerListUpdated);
    }

    public void SelectCharacter(string characterId)
    {
        var localId = LocalPeerId;
        Rpc(nameof(SyncPlayerCharacter), localId, characterId);
    }

    public void SetReadyState(bool isReady)
    {
        var localId = LocalPeerId;
        Rpc(nameof(SyncPlayerReady), localId, isReady);
    }

    public void StartGame(string sceneId)
    {
        if (!IsHost)
        {
            return;
        }

        Rpc(nameof(RpcStartGame), sceneId);
    }

    public void BroadcastAudioTake(string voiceSlotId, byte[] audioData)
    {
        Rpc(nameof(RpcReceiveAudioTake), voiceSlotId, _localPlayerName, audioData);
    }

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
            };

            // Broadcast full updated player roster to all clients
            foreach (var (id, p) in _players)
            {
                Rpc(nameof(SyncPlayerInfo), id, p.PlayerName, p.IsHost, p.IsReady, p.AssignedCharacterId ?? string.Empty);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SyncPlayerInfo(long peerId, string name, bool isHost, bool isReady, string characterId)
    {
        _players[peerId] = new NetworkPlayerInfo
        {
            PeerId = peerId,
            PlayerName = name,
            IsHost = isHost,
            IsReady = isReady,
            AssignedCharacterId = string.IsNullOrEmpty(characterId) ? null : characterId,
        };

        EmitSignal(SignalName.PlayerListUpdated);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void SyncPlayerCharacter(long peerId, string characterId)
    {
        if (_players.TryGetValue(peerId, out var player))
        {
            _players[peerId] = player with
            {
                AssignedCharacterId = string.IsNullOrEmpty(characterId) ? null : characterId
            };
            EmitSignal(SignalName.PlayerListUpdated);
        }
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
    private void RpcStartGame(string sceneId)
    {
        EmitSignal(SignalName.GameStarted, sceneId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcReceiveAudioTake(string voiceSlotId, string senderName, byte[] audioData)
    {
        EmitSignal(SignalName.AudioTakeReceived, voiceSlotId, senderName, audioData);
    }

    private void OnPeerConnected(long id)
    {
        GD.Print($"Multiplayer peer connected: {id}");
    }

    private void OnPeerDisconnected(long id)
    {
        GD.Print($"Multiplayer peer disconnected: {id}");
        _players.Remove(id);
        EmitSignal(SignalName.PlayerListUpdated);
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
}

