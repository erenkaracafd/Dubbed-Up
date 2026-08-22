using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class ResultsScreen : BaseScreen
{
    public override void _Ready()
    {
        var nextRoundButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/NextRoundButton");
        if (nextRoundButton is not null)
        {
            nextRoundButton.Pressed += OnNextRoundPressed;
        }

        var replayButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ReplayRoundButton");
        if (replayButton is not null)
        {
            replayButton.Pressed += OnReplayRoundPressed;
        }

        var menuButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/MenuButton");
        if (menuButton is not null)
        {
            menuButton.Pressed += OnMenuPressed;
        }
    }

    private void OnNextRoundPressed()
    {
        Navigator?.NavigateTo(AppScreen.Setup);
    }

    private void OnReplayRoundPressed()
    {
        Navigator?.NavigateTo(AppScreen.Playback);
    }

    private void OnMenuPressed()
    {
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
