using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.Network;
using DubbedUp.Godot.Workshop;
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

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);

        if (navigator is LocalNavigationController navCtrl)
        {
            _lobbyManager = navCtrl.LobbyManager;
            _lobbyManager.PlayerListUpdated += OnPlayerListUpdated;
            _lobbyManager.ConnectionStateChanged += OnConnectionStateChanged;
            _lobbyManager.GameStarted += OnGameStarted;

            UpdatePanelsVisibility(_lobbyManager.IsConnectedToLobby);
            OnPlayerListUpdated();
        }
    }

    public override void _Ready()
    {
        _statusLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/StatusLabel");
        _connectionPanel = GetNodeOrNull<Control>("CenterArea/VBoxContainer/ConnectionPanel");
        _lobbyPanel = GetNodeOrNull<Control>("CenterArea/VBoxContainer/LobbyPanel");
        _playersListContainer = GetNodeOrNull<VBoxContainer>("CenterArea/VBoxContainer/LobbyPanel/Margin/VBoxContainer/PlayersListContainer");

        _playerNameInput = GetNodeOrNull<LineEdit>("CenterArea/VBoxContainer/ConnectionPanel/Margin/VBoxContainer/PlayerNameInput");
        _addressInput = GetNodeOrNull<LineEdit>("CenterArea/VBoxContainer/ConnectionPanel/Margin/VBoxContainer/AddressInput");
        _portInput = GetNodeOrNull<LineEdit>("CenterArea/VBoxContainer/ConnectionPanel/Margin/VBoxContainer/PortInput");
        _hostButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/ConnectionPanel/Margin/VBoxContainer/ButtonsHBox/HostButton");
        _joinButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/ConnectionPanel/Margin/VBoxContainer/ButtonsHBox/JoinButton");
        _readyButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/LobbyPanel/Margin/VBoxContainer/ActionsHBox/ReadyButton");
        _startButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/LobbyPanel/Margin/VBoxContainer/ActionsHBox/StartButton");
        _leaveButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/LobbyPanel/Margin/VBoxContainer/ActionsHBox/LeaveButton");
        _backButton = GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/BackButton");

        ApplyStyling();

        if (_hostButton is not null) SetupButton(_hostButton, OnHostPressed);
        if (_joinButton is not null) SetupButton(_joinButton, OnJoinPressed);
        if (_readyButton is not null) SetupButton(_readyButton, OnReadyPressed);
        if (_startButton is not null) SetupButton(_startButton, OnStartPressed);
        if (_leaveButton is not null) SetupButton(_leaveButton, OnLeavePressed);
        if (_backButton is not null) SetupButton(_backButton, OnBackPressed);

        if (_lobbyManager is not null)
        {
            UpdatePanelsVisibility(_lobbyManager.IsConnectedToLobby);
            OnPlayerListUpdated();
        }
        else
        {
            UpdatePanelsVisibility(false);
        }
    }

    private void SetupButton(Button btn, Action action)
    {
        btn.Pressed += action;
        UiSoundManager.Attach(btn);
    }

    private void ApplyStyling()
    {
        // Top bar
        var topBar = GetNodeOrNull<PanelContainer>("TopBar");
        if (topBar is not null)
        {
            var topBarStyle = new StyleBoxFlat
            {
                BgColor = new Color(1.0f, 1.0f, 1.0f, 0.90f),
                BorderWidthBottom = 1,
                BorderColor = new Color(0.886f, 0.902f, 0.941f, 0.8f),
                ShadowColor = new Color(0.1f, 0.1f, 0.2f, 0.04f),
                ShadowSize = 6
            };
            topBar.AddThemeStyleboxOverride("panel", topBarStyle);
        }

        // Panels
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.965f, 0.980f, 1.000f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.780f, 0.850f, 0.950f),
            CornerRadiusTopLeft = 24,
            CornerRadiusTopRight = 24,
            CornerRadiusBottomLeft = 24,
            CornerRadiusBottomRight = 24,
            ShadowColor = new Color(0.12f, 0.18f, 0.35f, 0.10f),
            ShadowSize = 18,
            ShadowOffset = new Vector2(0, 6)
        };

        if (_connectionPanel is PanelContainer cp) cp.AddThemeStyleboxOverride("panel", panelStyle);
        if (_lobbyPanel is PanelContainer lp) lp.AddThemeStyleboxOverride("panel", panelStyle);

        // Buttons
        if (_hostButton is not null) StyleActionPill(_hostButton, new Color(1.0f, 0.540f, 0.680f), 24);
        if (_joinButton is not null) StyleActionPill(_joinButton, new Color(0.280f, 0.650f, 0.950f), 24);
        if (_startButton is not null) StyleActionPill(_startButton, new Color(1.0f, 0.540f, 0.680f), 23);
        if (_readyButton is not null) StyleActionPill(_readyButton, new Color(0.600f, 0.480f, 0.950f), 23);
        if (_leaveButton is not null) StyleOutlinePill(_leaveButton, 23);
        if (_backButton is not null) StyleOutlinePill(_backButton, 18);

        // Inputs
        StyleInput(_playerNameInput);
        StyleInput(_addressInput);
        StyleInput(_portInput);
    }

    private static void StyleInput(LineEdit? input)
    {
        if (input is null) return;
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.97f, 0.98f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.886f, 0.902f, 0.941f),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ContentMarginLeft = 14,
            ContentMarginRight = 14
        };
        input.AddThemeStyleboxOverride("normal", box);
        input.AddThemeStyleboxOverride("focus", box);
        input.AddThemeColorOverride("font_color", new Color(0.118f, 0.106f, 0.294f));
    }

    private static void StyleActionPill(Button btn, Color color, int radius)
    {
        var normal = new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 6, ShadowColor = new Color(color.R, color.G, color.B, 0.3f) };
        var hover = new StyleBoxFlat { BgColor = color.Lightened(0.15f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 10, ShadowColor = new Color(color.R, color.G, color.B, 0.4f) };
        var pressed = new StyleBoxFlat { BgColor = color.Darkened(0.15f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 1 };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        btn.PivotOffset = new Vector2(btn.CustomMinimumSize.X > 0 ? btn.CustomMinimumSize.X / 2f : 60f, 23f);

        btn.MouseEntered += () =>
        {
            var tween = btn.CreateTween();
            tween.TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.12)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        };
        btn.MouseExited += () =>
        {
            var tween = btn.CreateTween();
            tween.TweenProperty(btn, "scale", Vector2.One, 0.10)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        };
    }

    private static void StyleOutlinePill(Button btn, int radius)
    {
        var normal = new StyleBoxFlat { BgColor = Colors.White, BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.886f, 0.902f, 0.941f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
        var hover = new StyleBoxFlat { BgColor = new Color(0.95f, 0.97f, 1.0f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", new Color(0.294f, 0.322f, 0.439f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.118f, 0.106f, 0.294f));
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        btn.PivotOffset = new Vector2(btn.CustomMinimumSize.X > 0 ? btn.CustomMinimumSize.X / 2f : 55f, 18f);

        btn.MouseEntered += () =>
        {
            var tween = btn.CreateTween();
            tween.TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.12)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        };
        btn.MouseExited += () =>
        {
            var tween = btn.CreateTween();
            tween.TweenProperty(btn, "scale", Vector2.One, 0.10)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        };
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

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"Starting server on port {port}...";
        }

        if (_lobbyManager is not null)
        {
            var err = _lobbyManager.HostGame(port, name);
            if (err == Error.Ok)
            {
                UpdatePanelsVisibility(true);
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"Hosting lobby on port {port}. Waiting for players...";
                }
            }
            else
            {
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"Failed to host server on port {port}: {err}";
                }
            }
        }
        else
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "Network manager not available.";
            }
        }
    }

    private void OnJoinPressed()
    {
        var name = string.IsNullOrWhiteSpace(_playerNameInput?.Text) ? "Guest Player" : _playerNameInput.Text.Trim();
        var addr = string.IsNullOrWhiteSpace(_addressInput?.Text) ? "127.0.0.1" : _addressInput.Text.Trim();
        var port = int.TryParse(_portInput?.Text, out var p) ? p : NetworkLobbyManager.DefaultPort;

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"Connecting to host at {addr}:{port}...";
        }

        if (_lobbyManager is not null)
        {
            var err = _lobbyManager.JoinGame(addr, port, name);
            if (err != Error.Ok)
            {
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"Failed to initiate connection: {err}";
                }
            }
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
            _readyButton.Text = _isLocalReady ? "Ready!" : "Set Ready";
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
            var readyBadge = p.IsReady ? "[READY]" : "[WAITING]";
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

