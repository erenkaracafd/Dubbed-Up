using DubbedUp.Core.Game;
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

    public override void _Ready()
    {
        _sceneTitleLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SceneTitleLabel");
        _characterPreviewLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/CharacterPreviewLabel");
        _player1Input = GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/Player1Input");
        _player2Label = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/Player2Label");
        _player2Input = GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/Player2Input");
        _gameModeOption = GetNodeOrNull<OptionButton>("CenterContainer/VBoxContainer/GameModeOption");
        _errorLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");

        var selectedScene = Coordinator?.SelectedScenePackage;
        if (_sceneTitleLabel is not null)
        {
            _sceneTitleLabel.Text = selectedScene is not null
                ? $"🎬 {selectedScene.Title}  ({selectedScene.DurationMilliseconds / 1000.0:F0}s)"
                : "🎬 Scene: Default (Museum Mix-up)";
        }

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

        var backButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/BackButton");
        if (backButton is not null)
        {
            backButton.Pressed += OnBackPressed;
        }
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
            _characterPreviewLabel.Text = $"Seslendireceğin karakterler (solo):\n{string.Join("\n", lines)}";
        }
        else
        {
            var sb = new System.Text.StringBuilder("Karakter dağılımı:\n");
            for (int i = 0; i < chars.Count; i++)
            {
                var player = (i % 2 == 0) ? "👤 1. Oyuncu" : "👥 2. Oyuncu";
                sb.AppendLine($"   {player}: 🎭 {chars[i].DisplayName}");
            }
            _characterPreviewLabel.Text = sb.ToString().TrimEnd();
        }
    }

    private void OnStartRoundPressed()
    {
        if (_errorLabel is not null) _errorLabel.Visible = false;

        var p1 = string.IsNullOrWhiteSpace(_player1Input?.Text) ? "Player" : _player1Input.Text.Trim();
        var isSolo = _gameModeOption?.Selected == 0;
        var isVoting = _gameModeOption?.Selected == 2;

        var playerList = new List<string> { p1 };
        if (!isSolo)
        {
            var p2 = string.IsNullOrWhiteSpace(_player2Input?.Text) ? "Player 2" : _player2Input.Text.Trim();
            playerList.Add(p2);
        }

        var selectedMode = isVoting ? GameMode.CompetitiveVoting : GameMode.CoopDubbing;
        var sceneDoc = Coordinator?.SelectedScenePackage?.Document;

        try
        {
            Coordinator?.StartSession(playerList, sceneDoc, selectedMode);
            Navigator?.NavigateTo(AppScreen.Recording);
        }
        catch (Exception ex)
        {
            if (_errorLabel is not null)
            {
                _errorLabel.Text = $"Setup error: {ex.Message}";
                _errorLabel.Visible = true;
            }
        }
    }

    private void OnBackPressed()
    {
        Coordinator?.ResetSession();
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }
}
