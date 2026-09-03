using System;
using System.Collections.Generic;
using System.Linq;
using DubbedUp.Core.Game;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SetupScreen : BaseScreen
{
    private PanelContainer? _topBar;
    private Button? _backButton;
    private PanelContainer? _setupCard;
    private PanelContainer? _thumbnailFrame;
    private TextureRect? _thumbnailTexture;
    private Label? _placeholderIcon;
    private Label? _sceneTitleLabel;
    private Label? _characterPreviewLabel;
    private LineEdit? _player1Input;
    private LineEdit? _player2Input;
    private Label? _player2Label;
    private OptionButton? _gameModeOption;
    private Label? _errorLabel;
    private Button? _startRoundButton;
    private Button? _editSceneButton;

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);
        UpdateSetupInfo();
    }

    public override void _Ready()
    {
        _topBar = GetNodeOrNull<PanelContainer>("TopBar");
        _backButton = GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/BackButton");
        _setupCard = GetNodeOrNull<PanelContainer>("ScrollContainer/CenterContainer/SetupCard");
        _thumbnailFrame = GetNodeOrNull<PanelContainer>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/ThumbnailFrame");
        _thumbnailTexture = GetNodeOrNull<TextureRect>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/ThumbnailFrame/ThumbnailTexture");
        _placeholderIcon = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/ThumbnailFrame/PlaceholderIcon");
        _sceneTitleLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/SceneTitleLabel");
        _characterPreviewLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/CharacterPreviewLabel");
        _player1Input = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/Player1Input");
        _player2Label = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/Player2Label");
        _player2Input = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/Player2Input");
        _gameModeOption = GetNodeOrNull<OptionButton>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/GameModeOption");
        _errorLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/ErrorLabel");
        _startRoundButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/StartRoundButton");
        _editSceneButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/SetupCard/Margin/VBoxContainer/EditSceneButton");

        ApplyStyling();
        UpdateSetupInfo();

        if (_gameModeOption is not null)
        {
            _gameModeOption.Clear();
            _gameModeOption.AddItem("👤 Solo Dubbing (1 Player — Voice All Characters)", 0);
            _gameModeOption.AddItem("👥 Co-op Dubbing (2 Players — Watch Together)", 1);
            _gameModeOption.AddItem("🏆 Competitive Voting (2 Players — Party Scoring)", 2);
            _gameModeOption.ItemSelected += OnGameModeSelected;
            _gameModeOption.Select(0);
            OnGameModeSelected(0);
        }

        if (_startRoundButton is not null)
        {
            _startRoundButton.Pressed += OnStartRoundPressed;
            UiSoundManager.Attach(_startRoundButton);
        }

        if (_editSceneButton is not null)
        {
            _editSceneButton.Pressed += () => Navigator?.NavigateTo(AppScreen.SceneEditor);
            UiSoundManager.Attach(_editSceneButton);
        }

        if (_backButton is not null)
        {
            _backButton.Pressed += OnBackPressed;
            UiSoundManager.Attach(_backButton);
        }
    }

    private void ApplyStyling()
    {
        // Top Bar
        if (_topBar is not null)
        {
            var topBarStyle = new StyleBoxFlat
            {
                BgColor = new Color(1.0f, 1.0f, 1.0f, 0.90f),
                BorderWidthBottom = 1,
                BorderColor = new Color(0.886f, 0.902f, 0.941f, 0.8f),
                ShadowColor = new Color(0.1f, 0.1f, 0.2f, 0.04f),
                ShadowSize = 6
            };
            _topBar.AddThemeStyleboxOverride("panel", topBarStyle);
        }

        // Setup Card
        if (_setupCard is not null)
        {
            var cardStyle = new StyleBoxFlat
            {
                BgColor = Colors.White,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.886f, 0.902f, 0.941f),
                CornerRadiusTopLeft = 24,
                CornerRadiusTopRight = 24,
                CornerRadiusBottomLeft = 24,
                CornerRadiusBottomRight = 24,
                ShadowColor = new Color(0.12f, 0.11f, 0.30f, 0.08f),
                ShadowSize = 18,
                ShadowOffset = new Vector2(0, 6)
            };
            _setupCard.AddThemeStyleboxOverride("panel", cardStyle);
        }

        // Thumbnail Frame (Clipped rounded frame)
        if (_thumbnailFrame is not null)
        {
            var frameBox = new StyleBoxFlat
            {
                BgColor = new Color(0.94f, 0.96f, 0.99f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.886f, 0.902f, 0.941f),
                CornerRadiusTopLeft = 16,
                CornerRadiusTopRight = 16,
                CornerRadiusBottomLeft = 16,
                CornerRadiusBottomRight = 16
            };
            _thumbnailFrame.AddThemeStyleboxOverride("panel", frameBox);
        }

        // Buttons
        if (_startRoundButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = new Color(1.0f, 0.243f, 0.514f), CornerRadiusTopLeft = 26, CornerRadiusTopRight = 26, CornerRadiusBottomLeft = 26, CornerRadiusBottomRight = 26, ShadowSize = 8, ShadowColor = new Color(1.0f, 0.243f, 0.514f, 0.35f) };
            var hover = new StyleBoxFlat { BgColor = new Color(1.0f, 0.360f, 0.600f), CornerRadiusTopLeft = 26, CornerRadiusTopRight = 26, CornerRadiusBottomLeft = 26, CornerRadiusBottomRight = 26, ShadowSize = 12, ShadowColor = new Color(1.0f, 0.243f, 0.514f, 0.45f) };
            var pressed = new StyleBoxFlat { BgColor = new Color(0.870f, 0.140f, 0.410f), CornerRadiusTopLeft = 26, CornerRadiusTopRight = 26, CornerRadiusBottomLeft = 26, CornerRadiusBottomRight = 26, ShadowSize = 2 };
            _startRoundButton.AddThemeStyleboxOverride("normal", normal);
            _startRoundButton.AddThemeStyleboxOverride("hover", hover);
            _startRoundButton.AddThemeStyleboxOverride("pressed", pressed);
            _startRoundButton.AddThemeStyleboxOverride("focus", hover);
            _startRoundButton.AddThemeColorOverride("font_color", Colors.White);
        }

        if (_editSceneButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = new Color(0.561f, 0.396f, 0.973f), CornerRadiusTopLeft = 21, CornerRadiusTopRight = 21, CornerRadiusBottomLeft = 21, CornerRadiusBottomRight = 21 };
            var hover = new StyleBoxFlat { BgColor = new Color(0.660f, 0.520f, 1.000f), CornerRadiusTopLeft = 21, CornerRadiusTopRight = 21, CornerRadiusBottomLeft = 21, CornerRadiusBottomRight = 21 };
            _editSceneButton.AddThemeStyleboxOverride("normal", normal);
            _editSceneButton.AddThemeStyleboxOverride("hover", hover);
            _editSceneButton.AddThemeStyleboxOverride("focus", hover);
            _editSceneButton.AddThemeColorOverride("font_color", Colors.White);
        }

        if (_backButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = Colors.White, BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.886f, 0.902f, 0.941f), CornerRadiusTopLeft = 18, CornerRadiusTopRight = 18, CornerRadiusBottomLeft = 18, CornerRadiusBottomRight = 18 };
            var hover = new StyleBoxFlat { BgColor = new Color(0.95f, 0.97f, 1.0f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = 18, CornerRadiusTopRight = 18, CornerRadiusBottomLeft = 18, CornerRadiusBottomRight = 18 };
            _backButton.AddThemeStyleboxOverride("normal", normal);
            _backButton.AddThemeStyleboxOverride("hover", hover);
            _backButton.AddThemeStyleboxOverride("focus", hover);
            _backButton.AddThemeColorOverride("font_color", new Color(0.294f, 0.322f, 0.439f));
        }

        // Inputs
        StyleInput(_player1Input);
        StyleInput(_player2Input);
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

    private void UpdateSetupInfo()
    {
        var selectedScene = Coordinator?.SelectedScenePackage;
        if (_sceneTitleLabel is not null)
        {
            _sceneTitleLabel.Text = selectedScene is not null
                ? $"🎬 {selectedScene.Title}  ({selectedScene.Document.DurationMilliseconds / 1000.0:F1}s  |  {selectedScene.Document.VoiceSlots.Count} Lines)"
                : "🎬 Scene: Default (Museum Mix-up)";
        }

        if (_thumbnailTexture is not null && selectedScene is not null)
        {
            var thumb = VideoPlayback.VideoThumbnailHelper.GetOrExtractThumbnail(selectedScene.PackageDirectory ?? string.Empty);
            _thumbnailTexture.Texture = thumb;
            _thumbnailTexture.Visible = thumb is not null;
            if (_placeholderIcon is not null)
            {
                _placeholderIcon.Visible = thumb is null;
            }
        }

        var isSolo = _gameModeOption?.Selected == 0;
        UpdateCharacterPreview(isSolo);
    }

    private void OnGameModeSelected(long index)
    {
        var isSolo = index == 0;

        if (_player2Input is not null) _player2Input.Visible = !isSolo;
        if (_player2Label is not null) _player2Label.Visible = !isSolo;

        UpdateCharacterPreview(isSolo);
    }

    private void UpdateCharacterPreview(bool isSolo)
    {
        if (_characterPreviewLabel is null) return;

        var scene = Coordinator?.SelectedScenePackage;
        if (scene is null)
        {
            _characterPreviewLabel.Visible = false;
            return;
        }

        _characterPreviewLabel.Visible = true;
        var chars = scene.Document.Characters;

        if (isSolo)
        {
            var lines = chars.Select(c => $"   🎭 {c.DisplayName}").ToList();
            _characterPreviewLabel.Text = $"Characters you will voice (Solo):\n{string.Join("\n", lines)}";
        }
        else
        {
            var p1Char = chars.FirstOrDefault()?.DisplayName ?? "Character 1";
            var p2Char = chars.Skip(1).FirstOrDefault()?.DisplayName ?? "Character 2";
            _characterPreviewLabel.Text = $"Character Assignment:\n   🎭 Player 1 -> {p1Char}\n   🎭 Player 2 -> {p2Char}";
        }
    }

    private void OnStartRoundPressed()
    {
        if (Coordinator is null)
        {
            ShowError("Session coordinator is not initialized.");
            return;
        }

        var p1Name = _player1Input?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(p1Name))
        {
            ShowError("Please enter Player 1's name.");
            return;
        }

        var isSolo = _gameModeOption?.Selected == 0;
        var mode = _gameModeOption?.Selected == 2 ? GameMode.CompetitiveVoting : GameMode.CoopDubbing;

        var playerNames = new List<string> { p1Name };

        if (!isSolo)
        {
            var p2Name = _player2Input?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(p2Name))
            {
                ShowError("Please enter Player 2's name.");
                return;
            }
            playerNames.Add(p2Name);
        }

        var scene = Coordinator.SelectedScenePackage?.Document ?? Coordinator.CurrentScene;
        Coordinator.StartSession(playerNames, scene, mode);
        Navigator?.NavigateTo(AppScreen.Recording);
    }

    private void OnBackPressed()
    {
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }

    private void ShowError(string message)
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Text = message;
            _errorLabel.Visible = true;
        }
    }
}
