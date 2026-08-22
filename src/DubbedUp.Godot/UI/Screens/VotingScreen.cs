using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class VotingScreen : BaseScreen
{
    public override void _Ready()
    {
        var submitButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/SubmitButton");
        if (submitButton is not null)
        {
            submitButton.Pressed += OnSubmitPressed;
        }

        var menuButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/MenuButton");
        if (menuButton is not null)
        {
            menuButton.Pressed += OnMenuPressed;
        }
    }

    private void OnSubmitPressed()
    {
        Navigator?.NavigateTo(AppScreen.Results);
    }

    private void OnMenuPressed()
    {
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}

