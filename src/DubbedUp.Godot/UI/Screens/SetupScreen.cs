using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SetupScreen : BaseScreen
{
    public override void _Ready()
    {
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
        Navigator?.NavigateTo(AppScreen.Recording);
    }

    private void OnBackPressed()
    {
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
