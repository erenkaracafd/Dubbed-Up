using DubbedUp.Core.Game;
using DubbedUp.Godot.LocalSession;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SetupScreen : BaseScreen
{
    private Label? _sceneTitleLabel;
    private Label? _characterPreviewLabel;
    private LineEdit? _player1Input;
    private LineEdit? _player2Input;
    private Label? _player2Label;
    private OptionButton? _gameModeOption;
    private Label? _errorLabel;
    private Button? _editSceneButton;

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);
        UpdateSetupInfo();
    }

    public override void _Ready()
    {
        _sceneTitleLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SceneTitleLabel");
        _characterPreviewLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/CharacterPreviewLabel");
        _player1Input = GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/Player1Input");
        _player2Label = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/Player2Label");
        _player2Input = GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/Player2Input");
        _gameModeOption = GetNodeOrNull<OptionButton>("CenterContainer/VBoxContainer/GameModeOption");
        _errorLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");
        _editSceneButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/EditSceneButton");

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

        var startButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/StartRoundButton");
        if (startButton is not null)
        {
            startButton.Pressed += OnStartRoundPressed;
        }

        if (_editSceneButton is not null)
        {
            _editSceneButton.Pressed += () => Navigator?.NavigateTo(AppScreen.SceneEditor);
        }

        var backButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/BackButton");
        if (backButton is not null)
        {
            backButton.Pressed += OnBackPressed;
        }
    }

    private void UpdateSetupInfo()
    {
        var selectedScene = Coordinator?.SelectedScenePackage;
        if (_sceneTitleLabel is not null)
        {
            _sceneTitleLabel.Text = selectedScene is not null
                ? $"🎬 {selectedScene.Title}  ({selectedScene.DurationMilliseconds / 1000.0:F1}s  |  {selectedScene.Document.VoiceSlots.Count} Dialogue Lines)"
                : "🎬 Scene: Default (Museum Mix-up)";
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
