using DubbedUp.Godot.UI;
using Godot;

namespace DubbedUp.Godot.LocalSession;

public partial class LocalNavigationController : Node, IScreenNavigator
{
    private static readonly Dictionary<AppScreen, string> ScreenScenePaths = new()
    {
        { AppScreen.MainMenu, "res://UI/Screens/MainMenuScreen.tscn" },
        { AppScreen.ScenePicker, "res://UI/Screens/ScenePickerScreen.tscn" },
        { AppScreen.SceneCreator, "res://UI/Screens/SceneCreatorScreen.tscn" },
        { AppScreen.Setup, "res://UI/Screens/SetupScreen.tscn" },
        { AppScreen.Lobby, "res://UI/Screens/LobbyScreen.tscn" },
        { AppScreen.Recording, "res://UI/Screens/RecordingScreen.tscn" },
        { AppScreen.Playback, "res://UI/Screens/PlaybackScreen.tscn" },
        { AppScreen.Voting, "res://UI/Screens/VotingScreen.tscn" },
        { AppScreen.Results, "res://UI/Screens/ResultsScreen.tscn" },
        { AppScreen.Settings, "res://UI/Screens/SettingsScreen.tscn" },
    };

    [Signal]
    public delegate void ScreenChangedEventHandler(int newScreen);

    private Control? _screenContainer;
    private BaseScreen? _currentScreenInstance;

    public LocalSessionCoordinator Coordinator { get; } = new();

    public Network.NetworkLobbyManager LobbyManager { get; } = new();

    public AppScreen CurrentScreen { get; private set; } = AppScreen.MainMenu;

    public void Initialize(Control screenContainer)
    {
        _screenContainer = screenContainer;
        if (LobbyManager.GetParent() is null)
        {
            AddChild(LobbyManager);
        }

        // Initialize microphone service early so it's ready when Recording/Settings screens open
        Microphone.GodotLiveMicrophoneService.Instance.Initialize(screenContainer);

        NavigateTo(AppScreen.MainMenu);
    }

    public void NavigateTo(AppScreen screen)
    {
        if (_screenContainer is null)
        {
            GD.PrintErr("LocalNavigationController: Screen container is not set.");
            return;
        }

        if (!ScreenScenePaths.TryGetValue(screen, out var scenePath))
        {
            GD.PrintErr($"LocalNavigationController: No scene path registered for screen '{screen}'.");
            return;
        }

        var scene = GD.Load<PackedScene>(scenePath);
        if (scene is null)
        {
            GD.PrintErr($"LocalNavigationController: Failed to load scene '{scenePath}'.");
            return;
        }

        if (_currentScreenInstance is not null)
        {
            _currentScreenInstance.QueueFree();
            _currentScreenInstance = null;
        }

        var instance = scene.Instantiate();
        if (instance is not BaseScreen screenNode)
        {
            GD.PrintErr($"LocalNavigationController: Scene '{scenePath}' does not inherit BaseScreen.");
            instance.QueueFree();
            return;
        }

        _screenContainer.AddChild(screenNode);
        screenNode.Initialize(this, Coordinator);
        _currentScreenInstance = screenNode;
        CurrentScreen = screen;

        // Update Steam Rich Presence status
        var sceneTitle = Coordinator.CurrentScene?.Title;
        Steam.SteamRichPresenceService.Instance.SetStatus(
            screen switch
            {
                AppScreen.MainMenu => "In Main Menu",
                AppScreen.ScenePicker => "Selecting a Scene",
                AppScreen.SceneCreator => "Creating a Scene",
                AppScreen.Setup => "Setting up Session",
                AppScreen.Lobby => "In Multiplayer Lobby",
                AppScreen.Recording => "Recording Dialogue",
                AppScreen.Playback => "Watching Synchronized Dub",
                AppScreen.Voting => "Voting for Best Dub",
                AppScreen.Results => "Viewing Round Results",
                AppScreen.Settings => "Configuring Audio Settings",
                _ => "Playing Dubbed Up"
            },
            sceneTitle);

        EmitSignal(SignalName.ScreenChanged, (int)screen);
    }
}
