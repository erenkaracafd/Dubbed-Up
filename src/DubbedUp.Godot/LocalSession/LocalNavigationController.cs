using DubbedUp.Godot.AudioPlayback;
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
        { AppScreen.SceneEditor, "res://UI/Screens/SceneEditorScreen.tscn" },
    };

    [Signal]
    public delegate void ScreenChangedEventHandler(int newScreen);

    private Control? _screenContainer;
    private BaseScreen? _currentScreenInstance;
    private bool _isTransitioning = false;

    public LocalSessionCoordinator Coordinator { get; } = new();

    public Network.NetworkLobbyManager LobbyManager { get; } = new();

    public AudioPlayback.MenuMusicController MusicManager { get; } = new();

    public AppScreen CurrentScreen { get; private set; } = AppScreen.MainMenu;

    public void Initialize(Control screenContainer)
    {
        _screenContainer = screenContainer;
        if (LobbyManager.GetParent() is null)
        {
            AddChild(LobbyManager);
        }
        if (MusicManager.GetParent() is null)
        {
            AddChild(MusicManager);
        }

        // Initialize microphone service early so it's ready when Recording/Settings screens open
        Microphone.GodotLiveMicrophoneService.Instance.Initialize(screenContainer);

        // Apply saved display settings
        try
        {
            var config = new ConfigFile();
            if (config.Load("user://audio_settings.cfg") == Error.Ok)
            {
                var isFullscreen = (bool)config.GetValue("Display", "Fullscreen", false);
                if (isFullscreen)
                {
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                }
            }
        }
        catch { /* Ignore */ }

        ApplyStartScene();
        NavigateTo(GetStartScreen());
    }

    // Dev aid: `godot --path . -- --start-screen=ScenePicker` opens a screen directly.
    private static AppScreen GetStartScreen()
    {
        var value = GetUserArg("--start-screen=");
        return value is not null && Enum.TryParse<AppScreen>(value, ignoreCase: true, out var screen)
            ? screen
            : AppScreen.MainMenu;
    }

    // Dev aid: `-- --start-scene=<scene id>` preselects a scene (useful with --start-screen=SceneEditor).
    private void ApplyStartScene()
    {
        var sceneId = GetUserArg("--start-scene=");
        if (sceneId is null) return;

        var match = new Workshop.SteamWorkshopService().GetAvailableScenes()
            .FirstOrDefault(s => s.SceneId.Equals(sceneId, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            Coordinator.SelectedScenePackage = match;
        }
    }

    private static string? GetUserArg(string prefix) =>
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    public override void _ExitTree()
    {
        Ai.WhisperWorker.Stop();
    }

    public void NavigateTo(AppScreen screen)
    {
        if (_screenContainer is null)
        {
            GD.PrintErr("LocalNavigationController: Screen container is not set.");
            return;
        }

        if (_isTransitioning) return;

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

        var instance = scene.Instantiate();
        if (instance is not BaseScreen screenNode)
        {
            GD.PrintErr($"LocalNavigationController: Scene '{scenePath}' does not inherit BaseScreen.");
            instance.QueueFree();
            return;
        }

        UiSoundManager.Instance.PlayWhoosh();

        if (_currentScreenInstance is not null)
        {
            _isTransitioning = true;
            var oldScreen = _currentScreenInstance;

            oldScreen.PivotOffset = oldScreen.GetViewportRect().Size * 0.5f;
            var outTween = CreateTween();
            outTween?.SetParallel(true);
            outTween?.TweenProperty(oldScreen, "modulate:a", 0.0f, 0.16f)
                   .SetTrans(Tween.TransitionType.Quad)
                   .SetEase(Tween.EaseType.In);
            outTween?.TweenProperty(oldScreen, "scale", new Vector2(1.015f, 1.015f), 0.16f)
                   .SetTrans(Tween.TransitionType.Quad)
                   .SetEase(Tween.EaseType.In);

            if (outTween is not null)
            {
                outTween.Finished += () =>
                {
                    oldScreen.QueueFree();
                    AttachAndAnimateNewScreen(screenNode, screen, scenePath);
                };
            }
            else
            {
                oldScreen.QueueFree();
                AttachAndAnimateNewScreen(screenNode, screen, scenePath);
            }
        }
        else
        {
            AttachAndAnimateNewScreen(screenNode, screen, scenePath);
        }
    }

    private void AttachAndAnimateNewScreen(BaseScreen screenNode, AppScreen screen, string scenePath)
    {
        _screenContainer?.AddChild(screenNode);
        screenNode.Initialize(this, Coordinator);
        _currentScreenInstance = screenNode;
        CurrentScreen = screen;

        // Start with 0 opacity and slight zoom-in
        screenNode.Modulate = new Color(1, 1, 1, 0);
        screenNode.Scale = new Vector2(0.985f, 0.985f);
        screenNode.PivotOffset = screenNode.GetViewportRect().Size * 0.5f;

        var inTween = screenNode.CreateTween();
        inTween?.SetParallel(true);
        inTween?.TweenProperty(screenNode, "modulate:a", 1.0f, 0.28f)
               .SetTrans(Tween.TransitionType.Cubic)
               .SetEase(Tween.EaseType.Out);
        inTween?.TweenProperty(screenNode, "scale", Vector2.One, 0.36f)
               .SetTrans(Tween.TransitionType.Quint)
               .SetEase(Tween.EaseType.Out);

        if (inTween is not null)
        {
            inTween.Finished += () =>
            {
                _isTransitioning = false;
            };
        }
        else
        {
            _isTransitioning = false;
        }

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

        MusicManager.OnScreenChanged(screen);
        EmitSignal(SignalName.ScreenChanged, (int)screen);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.F11 || (keyEvent.AltPressed && keyEvent.Keycode == Key.Enter))
            {
                var isCurrentlyFullscreen = DisplayServer.WindowGetMode(0) is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
                SetFullscreen(!isCurrentlyFullscreen, this);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public static void SetFullscreen(bool isFullscreen, Node? context = null)
    {
        try
        {
            var targetMode = isFullscreen ? DisplayServer.WindowMode.ExclusiveFullscreen : DisplayServer.WindowMode.Windowed;
            DisplayServer.WindowSetMode(targetMode, 0);

            if (context is not null)
            {
                var win = context.GetWindow();
                if (win is not null)
                {
                    win.Mode = isFullscreen ? Window.ModeEnum.ExclusiveFullscreen : Window.ModeEnum.Windowed;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LocalNavigationController] Error setting fullscreen mode: {ex.Message}");
        }
    }

    public static void ToggleFullscreen(Node? context = null)
    {
        var currentMode = DisplayServer.WindowGetMode(0);
        var isFullscreen = currentMode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        SetFullscreen(!isFullscreen, context);
    }
}
