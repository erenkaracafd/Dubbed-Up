using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class RecordingScreen : BaseScreen
{
    public override void _Ready()
    {
        var proceedButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ProceedButton");
        if (proceedButton is not null)
        {
            proceedButton.Pressed += OnProceedPressed;
        }

        var cancelButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/CancelButton");
        if (cancelButton is not null)
        {
            cancelButton.Pressed += OnCancelPressed;
        }
    }

    private void OnProceedPressed()
    {
        Navigator?.NavigateTo(AppScreen.Playback);
    }

    private void OnCancelPressed()
    {
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
