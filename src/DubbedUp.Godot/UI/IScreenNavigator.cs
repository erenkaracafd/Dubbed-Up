namespace DubbedUp.Godot.UI;

public interface IScreenNavigator
{
    AppScreen CurrentScreen { get; }

    void NavigateTo(AppScreen screen);
}

