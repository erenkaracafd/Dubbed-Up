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

        if (_openFolderButton is not null) _openFolderButton.Pressed += OnOpenFolderPressed;
        if (_refreshButton is not null) _refreshButton.Pressed += OnRefreshPressed;
        if (_workshopButton is not null) _workshopButton.Pressed += OnWorkshopPressed;
        if (_createSceneButton is not null) _createSceneButton.Pressed += OnCreateScenePressed;
        if (_backButton is not null) _backButton.Pressed += OnBackPressed;

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
        if (_scenesListContainer is null) return;

        // Clear existing children
        foreach (var child in _scenesListContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"🎬 {_availableScenes.Count} oynanabilir sahne bulundu. Seslendirmek için bir sahne seçin:";
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
        panel.CustomMinimumSize = new Vector2(620, 85);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(hbox);

        var iconLabel = new Label
        {
            Text = package.VideoFilePath is not null || package.Document.SourceMedia.Any(m => m.Role == SourceMediaRole.SceneVideo) ? "🎬" : "🎭",
            CustomMinimumSize = new Vector2(50, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        iconLabel.AddThemeFontSizeOverride("font_size", 32);
        hbox.AddChild(iconLabel);

        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        infoVbox.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddChild(infoVbox);

        var titleLabel = new Label
        {
            Text = package.Title,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 1.0f));
        infoVbox.AddChild(titleLabel);

        var charNames = string.Join(", ", package.Document.Characters.Select(c => c.DisplayName));
        var durationSec = package.DurationMilliseconds / 1000.0;
        var detailsLabel = new Label
        {
            Text = $"⏱ {durationSec:F1}s  |  👥 Karakterler: {charNames} ({package.Document.VoiceSlots.Count} Replik)",
        };
        detailsLabel.AddThemeFontSizeOverride("font_size", 13);
        detailsLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1.0f));
        infoVbox.AddChild(detailsLabel);

        var btnContainer = new HBoxContainer();
        btnContainer.AddThemeConstantOverride("separation", 8);
        btnContainer.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddChild(btnContainer);

        var editBtn = new Button
        {
            Text = "✏️ Düzenle",
            CustomMinimumSize = new Vector2(100, 48),
        };
        editBtn.Pressed += () => OnEditScenePressed(package);
        btnContainer.AddChild(editBtn);

        var selectBtn = new Button
        {
            Text = "🎮 Bu Sahneyi Seç",
            CustomMinimumSize = new Vector2(150, 48),
        };
        selectBtn.Pressed += () => OnSceneSelected(package);
        btnContainer.AddChild(selectBtn);

        return panel;
    }

    private void OnEditScenePressed(ScenePackage package)
    {
        if (Coordinator is not null)
        {
            Coordinator.SelectedScenePackage = package;
            Coordinator.CurrentScene = package.Document;
        }

        Navigator?.NavigateTo(AppScreen.SceneEditor);
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
