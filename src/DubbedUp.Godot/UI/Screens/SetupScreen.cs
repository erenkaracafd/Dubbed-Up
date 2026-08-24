using DubbedUp.Core.Game;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SetupScreen : BaseScreen
{
    private Label? _sceneTitleLabel;
    private LineEdit? _player1Input;
    private LineEdit? _player2Input;
    private OptionButton? _gameModeOption;
    private Label? _errorLabel;

    public override void _Ready()
    {
        _sceneTitleLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SceneTitleLabel");
        _player1Input = GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/Player1Input");
        _player2Input = GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/Player2Input");
        _gameModeOption = GetNodeOrNull<OptionButton>("CenterContainer/VBoxContainer/GameModeOption");
        _errorLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");

        if (_sceneTitleLabel is not null)
        {
            var selectedScene = Coordinator?.SelectedScenePackage;
            if (selectedScene is not null)
            {
                var charNames = string.Join(", ", selectedScene.Document.Characters.Select(c => c.DisplayName));
                _sceneTitleLabel.Text = $"Scene: {selectedScene.Title}\nCharacters: {charNames}";
            }
            else
            {
                _sceneTitleLabel.Text = "Scene: Default (Museum Mix-up)";
            }
        }

        if (_gameModeOption is not null)
        {
            _gameModeOption.Clear();
            _gameModeOption.AddItem("👤 Solo Dubbing (1 Player - Voice All Characters)", 0);
            _gameModeOption.AddItem("👥 Co-op Dubbing (2 Players - Team Playback)", 1);
            _gameModeOption.AddItem("🏆 Competitive Voting (2 Players - Party Scoring)", 2);
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
        if (_player2Input is not null)
        {
            _player2Input.Visible = index != 0;
        }
    }

    private void OnStartRoundPressed()
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Visible = false;
        }

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
