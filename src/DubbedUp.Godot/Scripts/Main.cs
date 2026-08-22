using Godot;
using DubbedUp.Godot.LocalSession;

namespace DubbedUp.Godot;

public partial class Main : Control
{
    private LocalNavigationController? _navigationController;

    public override void _Ready()
    {
        var screenContainer = GetNodeOrNull<Control>("ScreenContainer") ?? this;
        _navigationController = new LocalNavigationController();
        AddChild(_navigationController);
        _navigationController.Initialize(screenContainer);
    }
}
