using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SetupScreen : BaseScreen
{
    private LineEdit? _player1Input;
    private LineEdit? _player2Input;
    private Label? _errorLabel;

    public override void _Ready()
    {
        _player1Input = GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/Player1Input");
        _player2Input = GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/Player2Input");
        _errorLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");

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

        try
        {
            Coordinator?.StartSession([p1, p2]);
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
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
