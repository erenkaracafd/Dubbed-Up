using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class MainMenuScreen : BaseScreen
{
    public override void _Ready()
    {
        var playButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/PlayButton");
        if (playButton is not null)
        {
            playButton.Pressed += OnPlayButtonPressed;
        }

        var onlineButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/OnlinePlayButton");
        if (onlineButton is not null)
        {
            onlineButton.Pressed += OnOnlinePlayPressed;
        }

        var quitButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/QuitButton");
        if (quitButton is not null)
        {
            quitButton.Pressed += OnQuitButtonPressed;
        }
    }

    private void OnPlayButtonPressed()
    {
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }

    private void OnOnlinePlayPressed()
    {
        Navigator?.NavigateTo(AppScreen.Lobby);
    }

    private void OnQuitButtonPressed()
    {
        GetTree().Quit();
    }
}
