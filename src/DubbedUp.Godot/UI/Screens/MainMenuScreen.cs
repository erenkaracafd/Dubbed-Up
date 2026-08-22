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

        var quitButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/QuitButton");
        if (quitButton is not null)
        {
            quitButton.Pressed += OnQuitButtonPressed;
        }
    }

    private void OnPlayButtonPressed()
    {
        Navigator?.NavigateTo(AppScreen.Setup);
    }

    private void OnQuitButtonPressed()
    {
        GetTree().Quit();
    }
}
