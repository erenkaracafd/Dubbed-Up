using DubbedUp.Core.Scenes;
using DubbedUp.Godot.Workshop;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class ScenePickerScreen : BaseScreen
{
    private VBoxContainer? _scenesListContainer;
    private Label? _statusLabel;
    private Button? _openFolderButton;
    private Button? _refreshButton;
    private Button? _workshopButton;
    private Button? _createSceneButton;
    private Button? _backButton;

    private readonly SteamWorkshopService _workshopService = new();
    private readonly List<ScenePackage> _availableScenes = [];

    public override void _Ready()
    {
        _scenesListContainer = GetNodeOrNull<VBoxContainer>("ScrollContainer/CenterContainer/VBoxContainer/ScenesListContainer");
        _statusLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/StatusLabel");
        _openFolderButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/OpenFolderButton");
        _refreshButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/RefreshButton");
        _workshopButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/WorkshopButton");
        _createSceneButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/CreateSceneButton");
        _backButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/BackButton");

        if (_openFolderButton is not null)
        {
            _openFolderButton.Pressed += OnOpenFolderPressed;
        }

        if (_refreshButton is not null)
        {
            _refreshButton.Pressed += OnRefreshPressed;
        }

        if (_workshopButton is not null)
        {
            _workshopButton.Pressed += OnWorkshopPressed;
        }

        if (_createSceneButton is not null)
        {
            _createSceneButton.Pressed += OnCreateScenePressed;
        }

        if (_backButton is not null)
        {
            _backButton.Pressed += OnBackPressed;
        }

        LoadAvailableScenes();
        PopulateSceneList();
    }

    private void LoadAvailableScenes()
    {
        _availableScenes.Clear();
        _workshopService.Refresh();

        var scenes = _workshopService.GetAvailableScenes();
        _availableScenes.AddRange(scenes);

        // Fallback built-in scene if no folder scenes found
        if (_availableScenes.Count == 0)
        {
            var defaultDoc = LocalSession.LocalSessionCoordinator.CreateDefaultScene();
            var fallbackPackage = new ScenePackage(defaultDoc, "builtin://museum-mixup", null, null);
            _availableScenes.Add(fallbackPackage);
        }
    }

    private void PopulateSceneList()
    {
        if (_scenesListContainer is null)
        {
            return;
        }

        // Clear existing children
        foreach (var child in _scenesListContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"Found {_availableScenes.Count} playable scene(s). Pick one to begin:";
        }

        foreach (var package in _availableScenes)
        {
            var card = CreateSceneCard(package);
            _scenesListContainer.AddChild(card);
        }
    }

    private Control CreateSceneCard(ScenePackage package)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(560, 75);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(hbox);

        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(infoVbox);

        var titleLabel = new Label
        {
            Text = package.Title,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        infoVbox.AddChild(titleLabel);

        var charNames = string.Join(", ", package.Document.Characters.Select(c => c.DisplayName));
        var durationSec = package.DurationMilliseconds / 1000.0;
        var detailsLabel = new Label
        {
            Text = $"{durationSec:F1}s | Characters: {charNames} ({package.Document.VoiceSlots.Count} lines)",
        };
        detailsLabel.AddThemeFontSizeOverride("font_size", 13);
        detailsLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        infoVbox.AddChild(detailsLabel);

        var selectBtn = new Button
        {
            Text = "Select Scene",
            CustomMinimumSize = new Vector2(130, 44),
        };
        selectBtn.Pressed += () => OnSceneSelected(package);
        hbox.AddChild(selectBtn);

        return panel;
    }

    private void OnSceneSelected(ScenePackage package)
    {
        if (Coordinator is not null)
        {
            Coordinator.SelectedScenePackage = package;
        }

        Navigator?.NavigateTo(AppScreen.Setup);
    }

    private void OnOpenFolderPressed()
    {
        _workshopService.OpenLocalScenesFolder();
    }

    private void OnRefreshPressed()
    {
        LoadAvailableScenes();
        PopulateSceneList();
    }

    private void OnWorkshopPressed()
    {
        _workshopService.OpenWorkshopInBrowser();
    }

    private void OnCreateScenePressed()
    {
        Navigator?.NavigateTo(AppScreen.SceneCreator);
    }

    private void OnBackPressed()
    {
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
