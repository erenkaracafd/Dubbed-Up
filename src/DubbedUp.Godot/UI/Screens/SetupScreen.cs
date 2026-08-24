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
                _sceneTitleLabel.Text = $"Scene: {selectedScene.Title} ({charNames})";
            }
            else
            {
                _sceneTitleLabel.Text = "Scene: Default (Museum Mix-up)";
            }
        }

        if (_gameModeOption is not null)
        {
            _gameModeOption.Clear();
            _gameModeOption.AddItem("Co-op Dubbing (Direct Cinema Playback)", (int)GameMode.CoopDubbing);
            _gameModeOption.AddItem("Competitive Voting (Party Scoring)", (int)GameMode.CompetitiveVoting);
            _gameModeOption.Select(0);
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

    private void OnStartRoundPressed()
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Visible = false;
        }

        var p1 = string.IsNullOrWhiteSpace(_player1Input?.Text) ? "Player 1" : _player1Input.Text.Trim();
        var p2 = string.IsNullOrWhiteSpace(_player2Input?.Text) ? "Player 2" : _player2Input.Text.Trim();

        var selectedMode = _gameModeOption is not null && _gameModeOption.Selected == 1
            ? GameMode.CompetitiveVoting
            : GameMode.CoopDubbing;

        var sceneDoc = Coordinator?.SelectedScenePackage?.Document;

        try
        {
            Coordinator?.StartSession([p1, p2], sceneDoc, selectedMode);
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
