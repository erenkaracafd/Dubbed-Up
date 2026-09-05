using System;
using System.Collections.Generic;
using System.Linq;
using DubbedUp.Core.Scenes;
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

    private OptionButton? _sceneOptionButton;
    private Label? _guestSceneLabel;
    private Label? _sceneNoticeLabel;
    private IReadOnlyList<ScenePackage> _availableScenes = [];

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
            _lobbyManager.SelectedSceneChanged += OnSelectedSceneChanged;
            _lobbyManager.SceneCompatibilityUpdated += OnSceneCompatibilityUpdated;

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

        _sceneOptionButton = GetNodeOrNull<OptionButton>("CenterArea/VBoxContainer/LobbyPanel/Margin/VBoxContainer/SceneSection/SceneOptionButton");
        _guestSceneLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/LobbyPanel/Margin/VBoxContainer/SceneSection/GuestSceneLabel");
        _sceneNoticeLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/LobbyPanel/Margin/VBoxContainer/SceneSection/SceneNoticeLabel");

        if (_sceneOptionButton is not null)
        {
            _sceneOptionButton.ItemSelected += OnSceneOptionSelected;
            StyleOptionButton(_sceneOptionButton);
        }

        PopulateSceneDropdown();
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
            _lobbyManager.SelectedSceneChanged -= OnSelectedSceneChanged;
            _lobbyManager.SceneCompatibilityUpdated -= OnSceneCompatibilityUpdated;
        }
    }

    private void UpdatePanelsVisibility(bool inLobby)
    {
        if (_connectionPanel is not null) _connectionPanel.Visible = !inLobby;
        if (_lobbyPanel is not null) _lobbyPanel.Visible = inLobby;

        if (inLobby)
        {
            UpdateSceneNotice();
        }
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
                var selectedScene = GetCurrentSelectedScene();
                if (selectedScene is not null)
                {
                    _lobbyManager.SetSelectedScene(selectedScene.SceneId, selectedScene.Title, selectedScene.Checksum);
                    if (Coordinator is not null)
                    {
                        Coordinator.SelectedScenePackage = selectedScene;
                    }
                }

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

        var sceneId = _lobbyManager.SelectedSceneId;
        var sceneTitle = _lobbyManager.SelectedSceneTitle;

        // Check if any peer is missing this scene or has checksum mismatch
        var incompatiblePeers = _lobbyManager.Players.Values
            .Where(p => !p.IsHost && !_lobbyManager.PeerHasScene(p.PeerId))
            .ToList();

        if (incompatiblePeers.Count > 0)
        {
            var details = incompatiblePeers.Select(p =>
            {
                var reason = _lobbyManager.GetPeerMismatchReason(p.PeerId);
                return string.IsNullOrEmpty(reason) ? p.PlayerName : $"{p.PlayerName} ({reason})";
            });
            var detailText = string.Join(", ", details);

            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"Cannot start: {detailText}.";
            }
            if (_sceneNoticeLabel is not null)
            {
                _sceneNoticeLabel.Text = $"Cannot start: {detailText}. Please pick a matching scene.";
                _sceneNoticeLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
                _sceneNoticeLabel.Visible = true;
            }
            return;
        }

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

    private void PopulateSceneDropdown()
    {
        if (_sceneOptionButton is null) return;
        _sceneOptionButton.Clear();

        var workshop = new SteamWorkshopService();
        _availableScenes = workshop.GetAvailableScenes();

        if (_availableScenes.Count == 0)
        {
            _sceneOptionButton.AddItem("No scenes found", 0);
            return;
        }

        var currentSelectedId = Coordinator?.SelectedScenePackage?.SceneId ?? _availableScenes[0].SceneId;
        var selectIndex = 0;

        for (var i = 0; i < _availableScenes.Count; i++)
        {
            var sc = _availableScenes[i];
            var charCount = sc.Document.Characters.Count;
            var durSec = sc.DurationMilliseconds / 1000f;
            var text = $"{sc.Title} ({charCount} Chars, {durSec:F0}s)";
            _sceneOptionButton.AddItem(text, i);

            if (sc.SceneId.Equals(currentSelectedId, StringComparison.OrdinalIgnoreCase))
            {
                selectIndex = i;
            }
        }

        _sceneOptionButton.Selected = selectIndex;
        var initialScene = _availableScenes[selectIndex];
        if (Coordinator is not null && Coordinator.SelectedScenePackage is null)
        {
            Coordinator.SelectedScenePackage = initialScene;
        }
    }

    private ScenePackage? GetCurrentSelectedScene()
    {
        if (_availableScenes.Count == 0) return null;
        var index = _sceneOptionButton?.Selected ?? 0;
        if (index >= 0 && index < _availableScenes.Count)
        {
            return _availableScenes[index];
        }
        return _availableScenes[0];
    }

    private void OnSceneOptionSelected(long index)
    {
        if (_availableScenes.Count == 0 || index < 0 || index >= _availableScenes.Count) return;
        var scene = _availableScenes[(int)index];

        if (Coordinator is not null)
        {
            Coordinator.SelectedScenePackage = scene;
        }

        if (_lobbyManager is not null && _lobbyManager.IsHost)
        {
            _lobbyManager.SetSelectedScene(scene.SceneId, scene.Title, scene.Checksum);
        }

        UpdateSceneNotice();
        OnPlayerListUpdated();
    }

    private void OnSelectedSceneChanged(string sceneId, string sceneTitle, string checksum)
    {
        if (_lobbyManager is null) return;

        if (!_lobbyManager.IsHost)
        {
            var workshop = new SteamWorkshopService();
            var all = workshop.GetAvailableScenes();
            var match = all.FirstOrDefault(p => p.SceneId.Equals(sceneId, StringComparison.OrdinalIgnoreCase));
            if (match is not null && Coordinator is not null)
            {
                Coordinator.SelectedScenePackage = match;
            }
        }

        UpdateSceneNotice();
        OnPlayerListUpdated();
    }

    private void OnSceneCompatibilityUpdated()
    {
        UpdateSceneNotice();
        OnPlayerListUpdated();
    }

    private void UpdateSceneNotice()
    {
        if (_lobbyManager is null) return;

        var isHost = _lobbyManager.IsHost;
        var sceneTitle = _lobbyManager.SelectedSceneTitle;
        var sceneId = _lobbyManager.SelectedSceneId;
        var checksum = _lobbyManager.SelectedSceneChecksum;

        if (_sceneOptionButton is not null)
        {
            _sceneOptionButton.Visible = isHost;
        }

        if (_guestSceneLabel is not null)
        {
            _guestSceneLabel.Visible = !isHost;
        }

        if (isHost)
        {
            var incompatiblePeers = _lobbyManager.Players.Values
                .Where(p => !p.IsHost && !_lobbyManager.PeerHasScene(p.PeerId))
                .ToList();

            if (incompatiblePeers.Count > 0)
            {
                var details = incompatiblePeers.Select(p =>
                {
                    var reason = _lobbyManager.GetPeerMismatchReason(p.PeerId);
                    return string.IsNullOrEmpty(reason) ? p.PlayerName : $"{p.PlayerName} ({reason})";
                });
                var detailText = string.Join(", ", details);

                if (_sceneNoticeLabel is not null)
                {
                    _sceneNoticeLabel.Text = $"Cannot start: {detailText}. Please pick a matching scene.";
                    _sceneNoticeLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
                    _sceneNoticeLabel.Visible = true;
                }
            }
            else
            {
                if (_sceneNoticeLabel is not null)
                {
                    _sceneNoticeLabel.Visible = false;
                }
            }
        }
        else
        {
            var workshop = new SteamWorkshopService();
            var localScene = workshop.GetAvailableScenes().FirstOrDefault(s => s.SceneId.Equals(sceneId, StringComparison.OrdinalIgnoreCase));

            var isInstalled = localScene is not null;
            var isMatchingVersion = isInstalled && (string.IsNullOrEmpty(checksum) || string.IsNullOrEmpty(localScene!.Checksum) || localScene.Checksum.Equals(checksum, StringComparison.OrdinalIgnoreCase));

            if (_guestSceneLabel is not null)
            {
                if (isInstalled && isMatchingVersion)
                {
                    _guestSceneLabel.Text = $"Selected Scene: {sceneTitle} (Synchronized)";
                    _guestSceneLabel.AddThemeColorOverride("font_color", new Color(0.18f, 0.65f, 0.35f));
                }
                else if (isInstalled && !isMatchingVersion)
                {
                    _guestSceneLabel.Text = $"Selected Scene: {sceneTitle} (VERSION MISMATCH)";
                    _guestSceneLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
                }
                else
                {
                    _guestSceneLabel.Text = $"Selected Scene: {sceneTitle} (NOT INSTALLED)";
                    _guestSceneLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
                }
            }

            if (_sceneNoticeLabel is not null)
            {
                if (!isInstalled)
                {
                    _sceneNoticeLabel.Text = $"You do not have '{sceneTitle}' installed! Host cannot start until you install this scene or the host picks a shared scene.";
                    _sceneNoticeLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
                    _sceneNoticeLabel.Visible = true;
                }
                else if (!isMatchingVersion)
                {
                    _sceneNoticeLabel.Text = $"Scene '{sceneTitle}' version mismatch! Your local scene files differ from the host's version. Both players must have identical scene files to start.";
                    _sceneNoticeLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
                    _sceneNoticeLabel.Visible = true;
                }
                else
                {
                    _sceneNoticeLabel.Visible = false;
                }
            }
        }
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
        var incompatiblePeersCount = 0;

        foreach (var p in players)
        {
            var item = new HBoxContainer();
            item.AddThemeConstantOverride("separation", 14);

            var hostBadge = p.IsHost ? "[HOST] " : "";
            var readyBadge = p.IsReady ? "[READY]" : "[WAITING]";
            var charText = string.IsNullOrEmpty(p.AssignedCharacterId) ? "No character" : p.AssignedCharacterId;

            var sceneBadge = "";
            var hasIssue = false;
            if (!p.IsHost)
            {
                var hasScene = _lobbyManager.PeerHasScene(p.PeerId);
                if (!hasScene)
                {
                    var reason = _lobbyManager.GetPeerMismatchReason(p.PeerId);
                    sceneBadge = string.IsNullOrEmpty(reason) ? " - [MISSING SCENE]" : $" - [{reason.ToUpperInvariant()}]";
                    incompatiblePeersCount++;
                    hasIssue = true;
                }
            }

            var label = new Label
            {
                Text = $"{hostBadge}{p.PlayerName} {readyBadge}{sceneBadge} - Character: {charText}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };

            if (hasIssue)
            {
                label.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
            }
            else
            {
                label.AddThemeColorOverride("font_color", new Color(0.18f, 0.16f, 0.38f));
            }

            item.AddChild(label);
            _playersListContainer.AddChild(item);
        }

        if (_startButton is not null)
        {
            _startButton.Visible = _lobbyManager.IsHost;
            var allReady = players.Count >= 2 && players.All(p => p.IsReady);
            _startButton.Disabled = !allReady || incompatiblePeersCount > 0;
        }
    }

    private void OnGameStarted(string sceneId, string checksum)
    {
        var workshop = new SteamWorkshopService();
        var all = workshop.GetAvailableScenes();
        var match = all.FirstOrDefault(p => p.SceneId.Equals(sceneId, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"Cannot start game: Scene '{sceneId}' is not installed on your system.";
            }
            if (_sceneNoticeLabel is not null)
            {
                _sceneNoticeLabel.Text = $"Cannot start: Scene '{sceneId}' is not installed on your system.";
                _sceneNoticeLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
                _sceneNoticeLabel.Visible = true;
            }
            return;
        }

        if (!string.IsNullOrEmpty(checksum) && !string.IsNullOrEmpty(match.Checksum) && !match.Checksum.Equals(checksum, StringComparison.OrdinalIgnoreCase))
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"Cannot start game: Scene '{sceneId}' checksum mismatch with host.";
            }
            if (_sceneNoticeLabel is not null)
            {
                _sceneNoticeLabel.Text = $"Cannot start: Scene '{sceneId}' files differ from the host version.";
                _sceneNoticeLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
                _sceneNoticeLabel.Visible = true;
            }
            return;
        }

        if (Coordinator is not null)
        {
            Coordinator.SelectedScenePackage = match;
        }

        var playerNames = _lobbyManager?.Players.Values.Select(p => p.PlayerName) ?? ["Host", "Guest"];
        var sceneDoc = match.Document;

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

    private static void StyleOptionButton(OptionButton btn)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.970f, 0.980f, 0.995f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.820f, 0.860f, 0.920f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14,
            ContentMarginRight = 14
        };
        var hover = new StyleBoxFlat
        {
            BgColor = Colors.White,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.460f, 0.380f, 0.920f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14,
            ContentMarginRight = 14
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", new Color(0.118f, 0.106f, 0.294f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.118f, 0.106f, 0.294f));
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }
}

