using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class PlaybackScreen : BaseScreen
{
    private Label? _statusLabel;
    private int _playCount = 1;

    public override void _Ready()
    {
        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StatusLabel");

        var proceedButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ProceedButton");
        if (proceedButton is not null)
        {
            proceedButton.Pressed += OnProceedPressed;
        }

        var replayButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ReplayButton");
        if (replayButton is not null)
        {
            replayButton.Pressed += OnReplayPressed;
        }

        var menuButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/MenuButton");
        if (menuButton is not null)
        {
            menuButton.Pressed += OnMenuPressed;
        }
    }

    private void OnProceedPressed()
    {
        Navigator?.NavigateTo(AppScreen.Voting);
    }

    private void OnReplayPressed()
    {
        _playCount++;
        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"Playing synchronized dub (Take #{_playCount})...";
        }
    }

    private void OnMenuPressed()
    {
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}

