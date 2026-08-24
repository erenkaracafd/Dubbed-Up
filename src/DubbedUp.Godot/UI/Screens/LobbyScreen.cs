using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.Network;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class LobbyScreen : BaseScreen
{
    private LineEdit? _playerNameInput;
    private LineEdit? _addressInput;
    private LineEdit? _portInput;
    private Button? _hostButton;
    private Button? _joinButton;
    private Button? _leaveButton;
    private Button? _readyButton;
    private Button? _startButton;
    private Button? _backButton;
    private VBoxContainer? _playersListContainer;
    private Label? _statusLabel;
    private Control? _connectionPanel;
    private Control? _lobbyPanel;

    private NetworkLobbyManager? _lobbyManager;
    private bool _isLocalReady = false;

    public override void _Ready()
    {
        _playerNameInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/ConnectionPanel/VBoxContainer/PlayerNameInput");
        _addressInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/ConnectionPanel/VBoxContainer/AddressInput");
        _portInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/ConnectionPanel/VBoxContainer/PortInput");
        _hostButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ConnectionPanel/VBoxContainer/ButtonsHBox/HostButton");
        _joinButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ConnectionPanel/VBoxContainer/ButtonsHBox/JoinButton");

        _connectionPanel = GetNodeOrNull<Control>("ScrollContainer/CenterContainer/VBoxContainer/ConnectionPanel");
        _lobbyPanel = GetNodeOrNull<Control>("ScrollContainer/CenterContainer/VBoxContainer/LobbyPanel");
        _playersListContainer = GetNodeOrNull<VBoxContainer>("ScrollContainer/CenterContainer/VBoxContainer/LobbyPanel/VBoxContainer/PlayersListContainer");
        _statusLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/StatusLabel");
        _readyButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/LobbyPanel/VBoxContainer/ActionsHBox/ReadyButton");
        _startButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/LobbyPanel/VBoxContainer/ActionsHBox/StartButton");
        _leaveButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/LobbyPanel/VBoxContainer/ActionsHBox/LeaveButton");
        _backButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/BackButton");

        if (Navigator is LocalNavigationController navCtrl)
        {
            _lobbyManager = navCtrl.LobbyManager;
            _lobbyManager.PlayerListUpdated += OnPlayerListUpdated;
            _lobbyManager.ConnectionStateChanged += OnConnectionStateChanged;
            _lobbyManager.GameStarted += OnGameStarted;
        }

        if (_hostButton is not null) _hostButton.Pressed += OnHostPressed;
        if (_joinButton is not null) _joinButton.Pressed += OnJoinPressed;
        if (_leaveButton is not null) _leaveButton.Pressed += OnLeavePressed;
        if (_readyButton is not null) _readyButton.Pressed += OnReadyPressed;
        if (_startButton is not null) _startButton.Pressed += OnStartPressed;
        if (_backButton is not null) _backButton.Pressed += OnBackPressed;

        UpdatePanelsVisibility(false);
    }

    public override void _ExitTree()
    {
        if (_lobbyManager is not null)
        {
            _lobbyManager.PlayerListUpdated -= OnPlayerListUpdated;
            _lobbyManager.ConnectionStateChanged -= OnConnectionStateChanged;
            _lobbyManager.GameStarted -= OnGameStarted;
        }
    }

    private void UpdatePanelsVisibility(bool inLobby)
    {
        if (_connectionPanel is not null) _connectionPanel.Visible = !inLobby;
        if (_lobbyPanel is not null) _lobbyPanel.Visible = inLobby;
    }

    private void OnHostPressed()
    {
        var name = string.IsNullOrWhiteSpace(_playerNameInput?.Text) ? "Host Player" : _playerNameInput.Text.Trim();
        var port = int.TryParse(_portInput?.Text, out var p) ? p : NetworkLobbyManager.DefaultPort;

        if (_lobbyManager is not null)
        {
            var err = _lobbyManager.HostGame(port, name);
            if (err == Error.Ok)
            {
                UpdatePanelsVisibility(true);
            }
        }
    }

    private void OnJoinPressed()
    {
        var name = string.IsNullOrWhiteSpace(_playerNameInput?.Text) ? "Guest Player" : _playerNameInput.Text.Trim();
        var addr = string.IsNullOrWhiteSpace(_addressInput?.Text) ? "127.0.0.1" : _addressInput.Text.Trim();
        var port = int.TryParse(_portInput?.Text, out var p) ? p : NetworkLobbyManager.DefaultPort;

        if (_lobbyManager is not null)
        {
            _lobbyManager.JoinGame(addr, port, name);
        }
    }

    private void OnLeavePressed()
    {
        _lobbyManager?.LeaveGame();
        _isLocalReady = false;
        UpdatePanelsVisibility(false);
    }

    private void OnReadyPressed()
    {
        _isLocalReady = !_isLocalReady;
        _lobbyManager?.SetReadyState(_isLocalReady);
        if (_readyButton is not null)
        {
            _readyButton.Text = _isLocalReady ? "✅ Ready!" : "Set Ready";
        }
    }

    private void OnStartPressed()
    {
        if (_lobbyManager is null || !_lobbyManager.IsHost)
        {
            return;
        }

        var sceneId = Coordinator?.SelectedScenePackage?.SceneId ?? "museum-mixup";
        _lobbyManager.StartGame(sceneId);
    }

    private void OnBackPressed()
    {
        _lobbyManager?.LeaveGame();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }

    private void OnConnectionStateChanged(bool isConnected, string message)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = message;
        }

        UpdatePanelsVisibility(isConnected);
    }

    private void OnPlayerListUpdated()
    {
        if (_playersListContainer is null || _lobbyManager is null)
        {
            return;
        }

        foreach (var child in _playersListContainer.GetChildren())
        {
            child.QueueFree();
        }

        var players = _lobbyManager.Players.Values.ToList();
        foreach (var p in players)
        {
            var item = new HBoxContainer();
            item.AddThemeConstantOverride("separation", 14);

            var hostBadge = p.IsHost ? "[HOST] " : "";
            var readyBadge = p.IsReady ? "✅" : "⏳";
            var charText = string.IsNullOrEmpty(p.AssignedCharacterId) ? "No character" : p.AssignedCharacterId;

            var label = new Label
            {
                Text = $"{hostBadge}{p.PlayerName} {readyBadge} - Character: {charText}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            item.AddChild(label);
            _playersListContainer.AddChild(item);
        }

        if (_startButton is not null)
        {
            _startButton.Visible = _lobbyManager.IsHost;
            _startButton.Disabled = players.Count < 2 || players.Any(p => !p.IsReady);
        }
    }

    private void OnGameStarted(string sceneId)
    {
        // Transition to Recording screen when host starts the match
        var playerNames = _lobbyManager?.Players.Values.Select(p => p.PlayerName) ?? ["Host", "Guest"];
        var sceneDoc = Coordinator?.SelectedScenePackage?.Document;

        try
        {
            Coordinator?.StartSession(playerNames, sceneDoc, DubbedUp.Core.Game.GameMode.CoopDubbing);
            Navigator?.NavigateTo(AppScreen.Recording);
        }
        catch (Exception ex)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"Launch error: {ex.Message}";
            }
        }
    }
}
