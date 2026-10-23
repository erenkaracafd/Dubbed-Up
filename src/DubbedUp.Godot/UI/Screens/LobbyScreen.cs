using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.Network;
using DubbedUp.Godot.Workshop;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

/// <summary>
/// Online lobby: a connect view (name, host or join) and a room view (scene, characters, players).
/// The layout is built in code with <see cref="UiKit"/>; network flow is driven by <see cref="NetworkLobbyManager"/>.
/// </summary>
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
    private VBoxContainer? _playersListContainer;
    private Label? _statusLabel;
    private Label? _roomLabel;
    private Control? _connectionPanel;
    private Control? _lobbyPanel;
    private Control? _nameAvatarSlot;
    private Label? _playerCountLabel;

    private Panel? _sceneArt;
    private Label? _sceneTitleLabel;
    private Label? _sceneMetaLabel;
    private OptionButton? _sceneOptionButton;
    private Label? _guestSceneLabel;
    private Label? _sceneNoticeLabel;
    private HFlowContainer? _characterListContainer;
    private IReadOnlyList<ScenePackage> _availableScenes = [];

    private NetworkLobbyManager? _lobbyManager;
    private bool _isLocalReady = false;
    private bool? _shownInLobby;

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

            // Dev aid alongside --start-screen: `-- --start-screen=Lobby --auto-host` opens a hosted room.
            if (OS.GetCmdlineUserArgs().Contains("--auto-host") && !_lobbyManager.IsConnectedToLobby)
            {
                Callable.From(OnHostPressed).CallDeferred();
            }
        }
    }

    public override void _Ready()
    {
        BuildLayout();
        PopulateSceneDropdown();

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

    // ───────────────────────── Layout ─────────────────────────

    private void BuildLayout()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 22);
        margin.AddChild(column);

        // Header
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        column.AddChild(header);

        var backButton = UiKit.CreateButton("Back", "arrow-left", outline: true, UiKit.Soft, UiKit.Ink, OnBackPressed);
        backButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(backButton);

        var titleBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleBox.AddThemeConstantOverride("separation", 0);
        header.AddChild(titleBox);
        titleBox.AddChild(UiKit.Heading("Play online", 32));
        _roomLabel = UiKit.Body("Not connected", 13);
        titleBox.AddChild(_roomLabel);

        _statusLabel = UiKit.Body(string.Empty, 14);
        _statusLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
        header.AddChild(_statusLabel);

        // Both views share the remaining space; only one is visible at a time.
        var body = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(body);

        _connectionPanel = BuildConnectView();
        body.AddChild(_connectionPanel);

        _lobbyPanel = BuildRoomView();
        body.AddChild(_lobbyPanel);
    }

    private Control BuildConnectView()
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);

        var stack = new VBoxContainer { CustomMinimumSize = new Vector2(860, 0) };
        stack.AddThemeConstantOverride("separation", 20);
        center.AddChild(stack);

        // Identity card: avatar, name, port
        var identity = new PanelContainer();
        identity.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 20));
        stack.AddChild(identity);

        var identityRow = new HBoxContainer();
        identityRow.AddThemeConstantOverride("separation", 18);
        identity.AddChild(identityRow);

        _nameAvatarSlot = new CenterContainer { CustomMinimumSize = new Vector2(64, 64) };
        identityRow.AddChild(_nameAvatarSlot);

        var nameBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        nameBox.AddThemeConstantOverride("separation", 6);
        identityRow.AddChild(nameBox);
        nameBox.AddChild(UiKit.Body("Your name", 13));
        _playerNameInput = new LineEdit { Text = "Player 1", PlaceholderText = "Your name", MaxLength = 24 };
        UiKit.StyleInput(_playerNameInput);
        _playerNameInput.TextChanged += _ => RefreshNameAvatar();
        nameBox.AddChild(_playerNameInput);

        var portBox = new VBoxContainer { CustomMinimumSize = new Vector2(130, 0) };
        portBox.AddThemeConstantOverride("separation", 6);
        identityRow.AddChild(portBox);
        portBox.AddChild(UiKit.Body("Port", 13));
        _portInput = new LineEdit { Text = NetworkLobbyManager.DefaultPort.ToString(), PlaceholderText = "7777", MaxLength = 5 };
        UiKit.StyleInput(_portInput);
        portBox.AddChild(_portInput);

        RefreshNameAvatar();

        // Host / join choice
        var choices = new HBoxContainer();
        choices.AddThemeConstantOverride("separation", 20);
        stack.AddChild(choices);

        var hostCard = BuildChoiceCard("users", UiKit.Pink, "Host a room", "Friends join with your IP address.");
        choices.AddChild(hostCard.Card);
        hostCard.Content.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 46) });
        _hostButton = UiKit.CreateButton("Host", "users", outline: false, UiKit.Pink, Colors.White, OnHostPressed);
        _hostButton.CustomMinimumSize = new Vector2(0, 50);
        hostCard.Content.AddChild(_hostButton);

        var joinCard = BuildChoiceCard("globe", UiKit.Sky, "Join a room", "Enter the host's IP address.");
        choices.AddChild(joinCard.Card);
        _addressInput = new LineEdit { Text = "127.0.0.1", PlaceholderText = "e.g. 192.168.1.20" };
        UiKit.StyleInput(_addressInput);
        joinCard.Content.AddChild(_addressInput);
        _joinButton = UiKit.CreateButton("Join", "globe", outline: false, UiKit.Sky, Colors.White, OnJoinPressed);
        _joinButton.CustomMinimumSize = new Vector2(0, 50);
        joinCard.Content.AddChild(_joinButton);

        return center;
    }

    private static (PanelContainer Card, VBoxContainer Content) BuildChoiceCard(string icon, Color accent, string title, string caption)
    {
        var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 24, border: accent.Lerp(Colors.White, 0.6f)));

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        card.AddChild(content);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 14);
        content.AddChild(top);

        var bubble = new PanelContainer { CustomMinimumSize = new Vector2(52, 52), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var bubbleBox = UiKit.RoundedBox(accent, 26, padding: 13);
        bubbleBox.CornerDetail = 24;
        bubbleBox.ShadowColor = accent with { A = 0.35f };
        bubbleBox.ShadowSize = 10;
        bubble.AddThemeStyleboxOverride("panel", bubbleBox);
        bubble.AddChild(new TextureRect
        {
            Texture = UiIcons.Get(icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(26, 26),
        });
        top.AddChild(bubble);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        text.AddThemeConstantOverride("separation", 0);
        top.AddChild(text);
        text.AddChild(UiKit.Heading(title, 22));
        text.AddChild(UiKit.Body(caption, 13));

        return (card, content);
    }

    private Control BuildRoomView()
    {
        var row = new HBoxContainer();
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", 22);

        // Left: scene + characters
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.35f };
        left.AddThemeConstantOverride("separation", 18);
        row.AddChild(left);

        var sceneCard = new PanelContainer();
        sceneCard.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 18));
        left.AddChild(sceneCard);

        var sceneRow = new HBoxContainer();
        sceneRow.AddThemeConstantOverride("separation", 18);
        sceneCard.AddChild(sceneRow);

        _sceneArt = new Panel { CustomMinimumSize = new Vector2(208, 117), ClipChildren = ClipChildrenMode.Only };
        _sceneArt.AddThemeStyleboxOverride("panel", UiKit.RoundedBox(Colors.White, 16));
        sceneRow.AddChild(_sceneArt);

        var sceneInfo = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        sceneInfo.AddThemeConstantOverride("separation", 4);
        sceneRow.AddChild(sceneInfo);

        sceneInfo.AddChild(UiKit.Body("Scene", 12));
        _sceneTitleLabel = UiKit.Heading("No scene", 24);
        _sceneTitleLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        sceneInfo.AddChild(_sceneTitleLabel);
        _sceneMetaLabel = UiKit.Body(string.Empty, 13);
        _sceneMetaLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        sceneInfo.AddChild(_sceneMetaLabel);

        _sceneOptionButton = new OptionButton { CustomMinimumSize = new Vector2(0, 42), FitToLongestItem = false };
        _sceneOptionButton.AddThemeFontSizeOverride("font_size", 14);
        UiKit.StyleOptionButton(_sceneOptionButton);
        _sceneOptionButton.ItemSelected += OnSceneOptionSelected;
        sceneInfo.AddChild(_sceneOptionButton);

        _guestSceneLabel = UiKit.Body(string.Empty, 13);
        _guestSceneLabel.Visible = false;
        sceneInfo.AddChild(_guestSceneLabel);

        _sceneNoticeLabel = UiKit.Body(string.Empty, 13, UiKit.Danger);
        _sceneNoticeLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _sceneNoticeLabel.Visible = false;
        sceneInfo.AddChild(_sceneNoticeLabel);

        var charactersCard = new PanelContainer();
        charactersCard.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 20));
        left.AddChild(charactersCard);

        var charactersStack = new VBoxContainer();
        charactersStack.AddThemeConstantOverride("separation", 14);
        charactersCard.AddChild(charactersStack);

        var charactersHeader = new HBoxContainer();
        charactersStack.AddChild(charactersHeader);
        var charactersTitle = UiKit.Heading("Characters", 20);
        charactersTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        charactersHeader.AddChild(charactersTitle);
        charactersHeader.AddChild(UiKit.Body("Click to claim", 13));

        _characterListContainer = new HFlowContainer();
        _characterListContainer.AddThemeConstantOverride("h_separation", 12);
        _characterListContainer.AddThemeConstantOverride("v_separation", 12);
        charactersStack.AddChild(_characterListContainer);

        // Right: players + actions
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 18);
        row.AddChild(right);

        var playersCard = new PanelContainer();
        playersCard.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 20));
        right.AddChild(playersCard);
        right.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        var playersStack = new VBoxContainer();
        playersStack.AddThemeConstantOverride("separation", 14);
        playersCard.AddChild(playersStack);

        var playersHeader = new HBoxContainer();
        playersStack.AddChild(playersHeader);
        var playersTitle = UiKit.Heading("Players", 20);
        playersTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        playersHeader.AddChild(playersTitle);
        _playerCountLabel = UiKit.Body(string.Empty, 13);
        playersHeader.AddChild(_playerCountLabel);

        _playersListContainer = new VBoxContainer();
        _playersListContainer.AddThemeConstantOverride("separation", 10);
        playersStack.AddChild(_playersListContainer);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 12);
        right.AddChild(actions);

        _leaveButton = UiKit.CreateButton("Leave", "log-out", outline: true, UiKit.Soft, UiKit.Ink, OnLeavePressed);
        _leaveButton.CustomMinimumSize = new Vector2(0, 52);
        actions.AddChild(_leaveButton);

        _readyButton = UiKit.CreateButton("Ready up", "check", outline: false, UiKit.Lilac, Colors.White, OnReadyPressed);
        _readyButton.CustomMinimumSize = new Vector2(0, 52);
        _readyButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(_readyButton);

        _startButton = UiKit.CreateButton("Start", "play", outline: false, UiKit.Pink, Colors.White, OnStartPressed);
        _startButton.CustomMinimumSize = new Vector2(0, 52);
        _startButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(_startButton);

        return row;
    }

    private void RefreshNameAvatar()
    {
        if (_nameAvatarSlot is null) return;
        foreach (var child in _nameAvatarSlot.GetChildren())
        {
            _nameAvatarSlot.RemoveChild(child);
            child.QueueFree();
        }
        _nameAvatarSlot.AddChild(UiKit.CreateAvatar(_playerNameInput?.Text ?? "?", UiKit.Lilac, 60));
    }

    private void UpdatePanelsVisibility(bool inLobby)
    {
        if (_connectionPanel is not null) _connectionPanel.Visible = !inLobby;
        if (_lobbyPanel is not null) _lobbyPanel.Visible = inLobby;

        if (_shownInLobby != inLobby)
        {
            _shownInLobby = inLobby;
            var shown = inLobby ? _lobbyPanel : _connectionPanel;
            if (shown is not null)
            {
                shown.Modulate = new Color(1, 1, 1, 0);
                shown.CreateTween().TweenProperty(shown, "modulate:a", 1.0f, 0.3f)
                     .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            }
        }

        UpdateRoomLabel();

        if (inLobby)
        {
            UpdateSceneNotice();
        }
    }

    private void UpdateRoomLabel()
    {
        if (_roomLabel is null) return;
        if (_lobbyManager is null || !_lobbyManager.IsConnectedToLobby)
        {
            _roomLabel.Text = "Not connected";
        }
        else
        {
            _roomLabel.Text = _lobbyManager.IsHost ? "You're hosting" : "Connected to host";
        }
    }

    // ───────────────────────── Connect flow ─────────────────────────

    private void OnHostPressed()
    {
        var name = string.IsNullOrWhiteSpace(_playerNameInput?.Text) ? "Host Player" : _playerNameInput.Text.Trim();
        var port = int.TryParse(_portInput?.Text, out var p) ? p : NetworkLobbyManager.DefaultPort;

        if (_statusLabel is not null)
        {
            _statusLabel.Text = "Starting...";
        }

        if (_lobbyManager is not null)
        {
            var err = _lobbyManager.HostGame(port, name);
            if (err == Error.Ok)
            {
                var selectedScene = GetCurrentSelectedScene();
                if (selectedScene is not null)
                {
                    var sceneJson = ProjectJsonSerializer.SerializeScene(selectedScene.Document);
                    _lobbyManager.SetSelectedScene(selectedScene.SceneId, selectedScene.Title, selectedScene.Checksum, sceneJson);
                    if (Coordinator is not null)
                    {
                        Coordinator.SelectedScenePackage = selectedScene;
                    }
                }

                PopulateCharacterList();
                UpdatePanelsVisibility(true);
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"Hosting on port {port}";
                }
            }
            else
            {
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"Couldn't host on port {port}: {err}";
                }
            }
        }
        else
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "Network unavailable.";
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
            _statusLabel.Text = $"Connecting to {addr}:{port}...";
        }

        if (_lobbyManager is not null)
        {
            var err = _lobbyManager.JoinGame(addr, port, name);
            if (err != Error.Ok)
            {
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"Couldn't connect: {err}";
                }
            }
        }
    }

    private void OnLeavePressed()
    {
        _lobbyManager?.LeaveGame();
        _isLocalReady = false;
        UpdateReadyButton();
        if (_statusLabel is not null) _statusLabel.Text = string.Empty;
        UpdatePanelsVisibility(false);
    }

    private void OnReadyPressed()
    {
        _isLocalReady = !_isLocalReady;
        _lobbyManager?.SetReadyState(_isLocalReady);
        UpdateReadyButton();
    }

    private void UpdateReadyButton()
    {
        if (_readyButton is null) return;
        _readyButton.Text = _isLocalReady ? "Ready" : "Ready up";
        UiKit.StylePill(_readyButton, _isLocalReady ? UiKit.Mint : UiKit.Lilac, Colors.White, outline: false);
    }

    private void OnStartPressed()
    {
        if (_lobbyManager is null || !_lobbyManager.IsHost)
        {
            return;
        }

        var sceneId = _lobbyManager.SelectedSceneId;

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
                _statusLabel.Text = $"Can't start: {detailText}";
            }
            ShowSceneNotice($"Missing scene: {detailText}");
            return;
        }

        // Check if all characters with lines in the scene are claimed
        var players = _lobbyManager.Players.Values.ToList();
        var requiredCharIds = Coordinator?.SelectedScenePackage?.Document.VoiceSlots
            .Select(v => v.CharacterId)
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
        var allClaimedCharIds = players.SelectMany(p => p.AssignedCharacterIds).ToHashSet(StringComparer.Ordinal);
        var unassignedChars = requiredCharIds.Where(cId => !allClaimedCharIds.Contains(cId)).ToList();

        if (unassignedChars.Count > 0)
        {
            var sceneChars = Coordinator?.SelectedScenePackage?.Document.Characters ?? [];
            var unassignedNames = unassignedChars.Select(id => sceneChars.FirstOrDefault(c => c.CharacterId == id)?.DisplayName ?? id);
            var notice = $"Unclaimed: {string.Join(", ", unassignedNames)}";

            if (_statusLabel is not null) _statusLabel.Text = notice;
            ShowSceneNotice(notice);
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

    // ───────────────────────── Scene ─────────────────────────

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
            _sceneOptionButton.AddItem(sc.Title, i);

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
            var sceneJson = ProjectJsonSerializer.SerializeScene(scene.Document);
            _lobbyManager.SetSelectedScene(scene.SceneId, scene.Title, scene.Checksum, sceneJson);
        }

        UpdateSceneNotice();
        PopulateCharacterList();
        OnPlayerListUpdated();
    }

    private void OnSelectedSceneChanged(string sceneId, string sceneTitle, string checksum)
    {
        if (_lobbyManager is null) return;

        if (!_lobbyManager.IsHost)
        {
            var workshop = new SteamWorkshopService();
            var all = workshop.GetAvailableScenes();
            var localMatch = all.FirstOrDefault(p => p.SceneId.Equals(sceneId, StringComparison.OrdinalIgnoreCase));
            if (localMatch is not null && !string.IsNullOrEmpty(localMatch.VideoFilePath) && File.Exists(localMatch.VideoFilePath))
            {
                try
                {
                    var hostDoc = ProjectJsonSerializer.DeserializeScene(_lobbyManager.SelectedSceneJson);
                    var transientPackage = new ScenePackage(
                        hostDoc,
                        localMatch.PackageDirectory,
                        localMatch.VideoFilePath,
                        localMatch.ThumbnailFilePath);

                    if (Coordinator is not null)
                    {
                        Coordinator.SelectedScenePackage = transientPackage;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to apply host transient scene edit: {ex.Message}");
                    if (Coordinator is not null)
                    {
                        Coordinator.SelectedScenePackage = localMatch;
                    }
                }
            }
        }

        UpdateSceneNotice();
        PopulateCharacterList();
        OnPlayerListUpdated();
    }

    private void OnSceneCompatibilityUpdated()
    {
        UpdateSceneNotice();
        PopulateCharacterList();
        OnPlayerListUpdated();
    }

    private void UpdateSceneNotice()
    {
        UpdateSceneCard();
        if (_lobbyManager is null) return;

        var isHost = _lobbyManager.IsHost;
        var sceneTitle = _lobbyManager.SelectedSceneTitle;
        var sceneId = _lobbyManager.SelectedSceneId;

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
                ShowSceneNotice($"Missing scene: {string.Join(", ", details)}");
            }
            else if (_sceneNoticeLabel is not null)
            {
                _sceneNoticeLabel.Visible = false;
            }
        }
        else
        {
            var workshop = new SteamWorkshopService();
            var localScene = workshop.GetAvailableScenes().FirstOrDefault(s => s.SceneId.Equals(sceneId, StringComparison.OrdinalIgnoreCase));
            var hasVideo = localScene is not null && !string.IsNullOrEmpty(localScene.VideoFilePath) && File.Exists(localScene.VideoFilePath);

            if (_guestSceneLabel is not null)
            {
                _guestSceneLabel.Text = hasVideo ? "Picked by the host" : "Not installed on this computer";
                _guestSceneLabel.AddThemeColorOverride("font_color", hasVideo ? UiKit.Mint : UiKit.Danger);
            }

            if (!hasVideo)
            {
                ShowSceneNotice($"You don't have \"{sceneTitle}\". The host needs to pick a scene everyone has.");
            }
            else if (_sceneNoticeLabel is not null)
            {
                _sceneNoticeLabel.Visible = false;
            }
        }
    }

    private void ShowSceneNotice(string text)
    {
        if (_sceneNoticeLabel is null) return;
        _sceneNoticeLabel.Text = text;
        _sceneNoticeLabel.Visible = true;
    }

    private void UpdateSceneCard()
    {
        var package = Coordinator?.SelectedScenePackage;
        var title = package?.Title ?? _lobbyManager?.SelectedSceneTitle;

        if (_sceneTitleLabel is not null) _sceneTitleLabel.Text = string.IsNullOrEmpty(title) ? "No scene" : title;
        if (_sceneMetaLabel is not null)
        {
            _sceneMetaLabel.Text = package is null
                ? string.Empty
                : $"{package.Document.VoiceSlots.Count} lines  ·  {package.Document.Characters.Count} characters  ·  {package.Document.DurationMilliseconds / 1000.0:F0}s";
        }

        if (_sceneArt is null) return;
        foreach (var child in _sceneArt.GetChildren())
        {
            _sceneArt.RemoveChild(child);
            child.QueueFree();
        }

        if (package is not null)
        {
            var index = _availableScenes.ToList().FindIndex(s => s.PackageDirectory == package.PackageDirectory);
            UiKit.FillSceneArt(_sceneArt, package, UiKit.PaletteColor(Math.Max(0, index)), initialSize: 48);
        }
    }

    // ───────────────────────── Characters & players ─────────────────────────

    private void PopulateCharacterList()
    {
        if (_characterListContainer is null) return;

        foreach (var child in _characterListContainer.GetChildren())
        {
            _characterListContainer.RemoveChild(child);
            child.QueueFree();
        }

        var scene = Coordinator?.SelectedScenePackage;
        if (scene is null || _lobbyManager is null) return;

        var characters = scene.Document.Characters;
        var localPeerId = _lobbyManager.LocalPeerId;

        for (var i = 0; i < characters.Count; i++)
        {
            var charDef = characters[i];
            var owner = _lobbyManager.Players.Values.FirstOrDefault(p => p.AssignedCharacterIds.Contains(charDef.CharacterId));
            var isMine = owner is not null && owner.PeerId == localPeerId;
            var color = UiKit.PaletteColor(i + 1);
            var charId = charDef.CharacterId;

            var ownerText = owner is null ? "Free" : isMine ? "You" : owner.PlayerName;
            var tile = CreateCharacterTile(charDef.DisplayName, ownerText, color, isMine, takenByOther: owner is not null && !isMine);
            if (owner is null || isMine)
            {
                tile.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    {
                        UiSoundManager.Instance.PlayClick();
                        _lobbyManager?.ToggleCharacterClaim(charId);
                    }
                };
            }

            _characterListContainer.AddChild(tile);
        }
    }

    private static PanelContainer CreateCharacterTile(string name, string ownerText, Color color, bool isMine, bool takenByOther)
    {
        var tile = new PanelContainer
        {
            CustomMinimumSize = new Vector2(196, 0),
            MouseFilter = takenByOther ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop,
            MouseDefaultCursorShape = takenByOther ? CursorShape.Arrow : CursorShape.PointingHand,
            Modulate = new Color(1, 1, 1, takenByOther ? 0.6f : 1f),
        };

        var box = UiKit.RoundedBox(isMine ? color.Lerp(Colors.White, 0.85f) : UiKit.Soft, 20, padding: 10);
        box.BorderWidthLeft = 2;
        box.BorderWidthTop = 2;
        box.BorderWidthRight = 2;
        box.BorderWidthBottom = 2;
        box.BorderColor = isMine ? color : UiKit.Line;
        if (isMine)
        {
            box.ShadowColor = color with { A = 0.3f };
            box.ShadowSize = 12;
            box.ShadowOffset = new Vector2(0, 4);
        }
        tile.AddThemeStyleboxOverride("panel", box);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        tile.AddChild(row);

        row.AddChild(UiKit.CreateAvatar(name, color, 40));

        var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        text.AddThemeConstantOverride("separation", 0);
        row.AddChild(text);

        var title = UiKit.Heading(name, 16);
        title.MouseFilter = MouseFilterEnum.Ignore;
        text.AddChild(title);
        var owner = UiKit.Body(ownerText, 12, isMine ? color.Darkened(0.2f) : UiKit.Muted);
        owner.MouseFilter = MouseFilterEnum.Ignore;
        text.AddChild(owner);

        if (!takenByOther) UiKit.AttachHoverBounce(tile);
        return tile;
    }

    private void OnPlayerListUpdated()
    {
        if (_playersListContainer is null || _lobbyManager is null)
        {
            return;
        }

        foreach (var child in _playersListContainer.GetChildren())
        {
            _playersListContainer.RemoveChild(child);
            child.QueueFree();
        }

        var players = _lobbyManager.Players.Values.ToList();
        var incompatiblePeersCount = 0;
        var sceneChars = Coordinator?.SelectedScenePackage?.Document.Characters ?? [];

        // The manager owns ready state (the host starts ready), so mirror it on the button.
        if (_lobbyManager.IsConnectedToLobby && _lobbyManager.Players.TryGetValue(_lobbyManager.LocalPeerId, out var localPlayer))
        {
            _isLocalReady = localPlayer.IsReady;
            UpdateReadyButton();
        }

        if (_playerCountLabel is not null)
        {
            _playerCountLabel.Text = players.Count == 1 ? "1 player" : $"{players.Count} players";
        }

        for (var i = 0; i < players.Count; i++)
        {
            var p = players[i];
            var charNames = p.AssignedCharacterIds
                .Select(id => sceneChars.FirstOrDefault(c => c.CharacterId == id)?.DisplayName ?? id)
                .ToList();

            string? issue = null;
            if (!p.IsHost && !_lobbyManager.PeerHasScene(p.PeerId))
            {
                var reason = _lobbyManager.GetPeerMismatchReason(p.PeerId);
                issue = string.IsNullOrEmpty(reason) ? "Missing scene" : reason;
                incompatiblePeersCount++;
            }

            var row = CreatePlayerRow(p.PlayerName, charNames, p.IsHost, p.IsReady, issue, UiKit.PaletteColor(i + 2));
            _playersListContainer.AddChild(row);

            row.Modulate = new Color(1, 1, 1, 0);
            row.CreateTween().TweenProperty(row, "modulate:a", 1.0f, 0.25f).SetDelay(i * 0.04);
        }

        PopulateCharacterList();

        if (_startButton is not null)
        {
            _startButton.Visible = _lobbyManager.IsHost;
            var allReady = players.Count >= 2 && players.All(p => p.IsReady);
            var everyPlayerHasChar = players.All(p => p.AssignedCharacterIds.Length > 0);

            var requiredCharIds = Coordinator?.SelectedScenePackage?.Document.VoiceSlots
                .Select(v => v.CharacterId)
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? [];
            var allClaimedCharIds = players.SelectMany(p => p.AssignedCharacterIds).ToHashSet(StringComparer.Ordinal);
            var allRequiredClaimed = requiredCharIds.All(cId => allClaimedCharIds.Contains(cId));

            _startButton.Disabled = !allReady || incompatiblePeersCount > 0 || !everyPlayerHasChar || !allRequiredClaimed;
        }

        UpdateRoomLabel();
    }

    private static PanelContainer CreatePlayerRow(string name, IReadOnlyList<string> characters, bool isHost, bool isReady, string? issue, Color color)
    {
        var row = new PanelContainer();
        var box = UiKit.RoundedBox(UiKit.Soft, 20, padding: 10);
        if (issue is not null)
        {
            box.BorderWidthLeft = 2;
            box.BorderWidthTop = 2;
            box.BorderWidthRight = 2;
            box.BorderWidthBottom = 2;
            box.BorderColor = UiKit.Danger with { A = 0.6f };
        }
        row.AddThemeStyleboxOverride("panel", box);

        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 12);
        row.AddChild(line);

        line.AddChild(UiKit.CreateAvatar(name, color, 42));

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        text.AddThemeConstantOverride("separation", 0);
        line.AddChild(text);

        var title = UiKit.Heading(name, 17);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        text.AddChild(title);

        var detail = issue ?? (characters.Count > 0 ? string.Join(", ", characters) : "No character yet");
        var detailLabel = UiKit.Body(detail, 12, issue is not null ? UiKit.Danger : UiKit.Muted);
        detailLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        text.AddChild(detailLabel);

        if (isHost)
        {
            var hostChip = UiKit.CreateChip("Host", UiKit.Lilac);
            hostChip.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            line.AddChild(hostChip);
        }

        var status = UiKit.CreateChip(isReady ? "Ready" : "Waiting", isReady ? UiKit.Mint : new Color(0.86f, 0.88f, 0.93f), isReady ? Colors.White : UiKit.Muted);
        status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        line.AddChild(status);

        return row;
    }

    // ───────────────────────── Game start ─────────────────────────

    private void OnGameStarted(string sceneId, string checksum)
    {
        var workshop = new SteamWorkshopService();
        var all = workshop.GetAvailableScenes();
        var localMatch = all.FirstOrDefault(p => p.SceneId.Equals(sceneId, StringComparison.OrdinalIgnoreCase));

        if (localMatch is null || string.IsNullOrEmpty(localMatch.VideoFilePath) || !File.Exists(localMatch.VideoFilePath))
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"Scene \"{sceneId}\" is not installed.";
            }
            ShowSceneNotice($"Scene \"{sceneId}\" is not installed.");
            return;
        }

        // Apply Host's transient scene document in RAM if guest
        OfficialSceneDocument sceneDoc;
        if (!_lobbyManager?.IsHost ?? false)
        {
            try
            {
                var hostDoc = ProjectJsonSerializer.DeserializeScene(_lobbyManager!.SelectedSceneJson);
                var transientPackage = new ScenePackage(
                    hostDoc,
                    localMatch.PackageDirectory,
                    localMatch.VideoFilePath,
                    localMatch.ThumbnailFilePath);
                sceneDoc = hostDoc;
                if (Coordinator is not null)
                {
                    Coordinator.SelectedScenePackage = transientPackage;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to parse host scene document at launch: {ex.Message}");
                sceneDoc = localMatch.Document;
            }
        }
        else
        {
            sceneDoc = Coordinator?.SelectedScenePackage?.Document ?? localMatch.Document;
        }

        // Build characterToPlayerNameMap from claimed characters
        var charMap = new Dictionary<string, string>();
        if (_lobbyManager is not null)
        {
            foreach (var player in _lobbyManager.Players.Values)
            {
                foreach (var charId in player.AssignedCharacterIds)
                {
                    charMap[charId] = player.PlayerName;
                }
            }
        }

        var playerNames = _lobbyManager?.Players.Values.Select(p => p.PlayerName) ?? ["Host", "Guest"];

        try
        {
            Coordinator?.StartSession(playerNames, sceneDoc, DubbedUp.Core.Game.GameMode.CoopDubbing, charMap);
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
