using Godot;

namespace DubbedUp.Godot.UI;

public abstract partial class BaseScreen : Control
{
    public IScreenNavigator? Navigator { get; private set; }

    public virtual void Initialize(IScreenNavigator navigator)
    {
        Navigator = navigator;
    }
}
