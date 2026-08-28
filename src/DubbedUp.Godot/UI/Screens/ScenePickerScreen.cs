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

    // Delete Confirmation Modal
    private Control? _deleteConfirmModal;
    private Label? _confirmMessageLabel;
    private Button? _confirmDeleteButton;
    private Button? _cancelDeleteButton;
    private ScenePackage? _pendingDeletePackage;

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

        // Confirmation Modal References
        _deleteConfirmModal = GetNodeOrNull<Control>("DeleteConfirmModal");
        _confirmMessageLabel = GetNodeOrNull<Label>("DeleteConfirmModal/CenterContainer/DialogPanel/MarginContainer/VBoxContainer/ConfirmMessage");
        _confirmDeleteButton = GetNodeOrNull<Button>("DeleteConfirmModal/CenterContainer/DialogPanel/MarginContainer/VBoxContainer/ConfirmButtonsHBox/ConfirmDeleteButton");
        _cancelDeleteButton = GetNodeOrNull<Button>("DeleteConfirmModal/CenterContainer/DialogPanel/MarginContainer/VBoxContainer/ConfirmButtonsHBox/CancelDeleteButton");

        if (_openFolderButton is not null) _openFolderButton.Pressed += OnOpenFolderPressed;
        if (_refreshButton is not null) _refreshButton.Pressed += OnRefreshPressed;
        if (_workshopButton is not null) _workshopButton.Pressed += OnWorkshopPressed;
        if (_createSceneButton is not null) _createSceneButton.Pressed += OnCreateScenePressed;
        if (_backButton is not null) _backButton.Pressed += OnBackPressed;

        if (_confirmDeleteButton is not null) _confirmDeleteButton.Pressed += OnConfirmDeletePressed;
        if (_cancelDeleteButton is not null) _cancelDeleteButton.Pressed += OnCancelDeletePressed;

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
            _statusLabel.Text = $"🎬 Found {_availableScenes.Count} playable scenes. Select a scene to dub, edit, or delete:";
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
        panel.CustomMinimumSize = new Vector2(640, 85);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(hbox);

        var aspect = new AspectRatioContainer
        {
            Ratio = 16.0f / 9.0f,
            CustomMinimumSize = new Vector2(106, 60),
            StretchMode = AspectRatioContainer.StretchModeEnum.Fit,
        };
        hbox.AddChild(aspect);

        var thumb = VideoPlayback.VideoThumbnailHelper.GetOrExtractThumbnail(package.PackageDirectory);
        if (thumb is not null)
        {
            var texRect = new TextureRect
            {
                Texture = thumb,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                CustomMinimumSize = new Vector2(106, 60),
            };
            aspect.AddChild(texRect);
        }
        else
        {
            var iconLabel = new Label
            {
                Text = package.VideoFilePath is not null || package.Document.SourceMedia.Any(m => m.Role == SourceMediaRole.SceneVideo) ? "🎬" : "🎭",
                CustomMinimumSize = new Vector2(50, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            iconLabel.AddThemeFontSizeOverride("font_size", 32);
            aspect.AddChild(iconLabel);
        }

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
            Text = $"⏱ {durationSec:F1}s  |  👥 Characters: {charNames} ({package.Document.VoiceSlots.Count} Lines)",
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
            Text = "✏️ Edit",
            CustomMinimumSize = new Vector2(85, 44),
        };
        editBtn.Pressed += () => OnEditScenePressed(package);
        btnContainer.AddChild(editBtn);

        var deleteBtn = new Button
        {
            Text = "🗑️ Delete",
            CustomMinimumSize = new Vector2(95, 44),
        };
        deleteBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.4f));
        deleteBtn.Pressed += () => OnPromptDeleteScene(package);
        btnContainer.AddChild(deleteBtn);

        var selectBtn = new Button
        {
            Text = "🎮 Select Scene",
            CustomMinimumSize = new Vector2(135, 44),
        };
        selectBtn.Pressed += () => OnSceneSelected(package);
        btnContainer.AddChild(selectBtn);

        return panel;
    }

    private void OnPromptDeleteScene(ScenePackage package)
    {
        _pendingDeletePackage = package;

        if (_confirmMessageLabel is not null)
        {
            _confirmMessageLabel.Text = $"Are you sure you want to permanently delete \"{package.Title}\"?\n\nThis will remove the scene folder, video, audio tracks, and all associated subtitles.";
        }

        if (_deleteConfirmModal is not null)
        {
            _deleteConfirmModal.Visible = true;
        }
    }

    private void OnConfirmDeletePressed()
    {
        if (_pendingDeletePackage is not null)
        {
            var title = _pendingDeletePackage.Title;
            var success = _workshopService.DeleteScene(_pendingDeletePackage);

            if (success)
            {
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"🗑️ Successfully deleted scene: \"{title}\"";
                }
                LoadAvailableScenes();
                PopulateSceneList();
            }
            else
            {
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"❌ Failed to delete scene: \"{title}\" (built-in or read-only package)";
                }
            }
        }

        if (_deleteConfirmModal is not null)
        {
            _deleteConfirmModal.Visible = false;
        }
        _pendingDeletePackage = null;
    }

    private void OnCancelDeletePressed()
    {
        if (_deleteConfirmModal is not null)
        {
            _deleteConfirmModal.Visible = false;
        }
        _pendingDeletePackage = null;
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
