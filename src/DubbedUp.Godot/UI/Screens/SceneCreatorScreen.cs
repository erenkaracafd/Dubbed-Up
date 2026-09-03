using System.Text.RegularExpressions;
using DubbedUp.Core.Ai;
using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using DubbedUp.Godot.Ai;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.VideoPlayback;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SceneCreatorScreen : BaseScreen
{
    private PanelContainer? _topBar;
    private Button? _backButton;
    private PanelContainer? _creatorCard;

    private Button? _openFolderButton;
    private Button? _refreshMediaButton;
    private Label? _noVideosLabel;
    private HFlowContainer? _videoCardsGrid;

    private LineEdit? _titleInput;
    private LineEdit? _sceneIdInput;

    private Label? _statusInfoLabel;
    private Label? _errorLabel;
    private Button? _saveButton;
    private Button? _cancelButton;

    private readonly List<string> _discoveredMediaFiles = [];
    private readonly List<PanelContainer> _cardNodes = [];
    private string? _selectedSourceMediaFile;
    private PanelContainer? _selectedCardPanel;

    public override void _Ready()
    {
        _topBar = GetNodeOrNull<PanelContainer>("TopBar");
        _backButton = GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/BackButton");
        _creatorCard = GetNodeOrNull<PanelContainer>("ScrollContainer/CenterContainer/CardPadding/CreatorCard");

        _openFolderButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/FolderInfoContainer/FolderButtonsHBox/OpenFolderButton")
            ?? GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/FolderButtonsHBox/OpenFolderButton");
        _refreshMediaButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/FolderInfoContainer/FolderButtonsHBox/RefreshMediaButton")
            ?? GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/FolderButtonsHBox/RefreshMediaButton");
        _noVideosLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/FolderInfoContainer/NoVideosLabel")
            ?? GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/NoVideosLabel");
        _videoCardsGrid = GetNodeOrNull<HFlowContainer>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/FolderInfoContainer/VideoCardsScroll/VideoCardsGrid")
            ?? GetNodeOrNull<HFlowContainer>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/VideoCardsScroll/VideoCardsGrid");

        _titleInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/FormContainer/TitleInput")
            ?? GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/TitleInput");
        _sceneIdInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/FormContainer/SceneIdInput")
            ?? GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/SceneIdInput");
        _statusInfoLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/StatusInfoLabel")
            ?? GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/StatusInfoLabel");

        _errorLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/ErrorLabel")
            ?? GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/ErrorLabel");
        _saveButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/ButtonsHBox/SaveButton")
            ?? GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ButtonsHBox/SaveButton");
        _cancelButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/CardPadding/CreatorCard/CardMargin/VBoxContainer/ButtonsHBox/CancelButton")
            ?? GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ButtonsHBox/CancelButton");

        ApplyStyling();

        if (_backButton is not null) SetupButton(_backButton, OnCancelPressed);
        if (_openFolderButton is not null) SetupButton(_openFolderButton, OnOpenFolderPressed);
        if (_refreshMediaButton is not null) SetupButton(_refreshMediaButton, ScanMediaFiles);

        if (_titleInput is not null) _titleInput.TextChanged += OnTitleChanged;
        if (_saveButton is not null) SetupButton(_saveButton, OnSavePressed);
        if (_cancelButton is not null) SetupButton(_cancelButton, OnCancelPressed);

        EnsureCustomVideosDirectoryExists();
        ScanMediaFiles();
    }

    private string GetCustomVideosDirectory()
    {
        return ProjectSettings.GlobalizePath("user://custom_videos");
    }

    private void EnsureCustomVideosDirectoryExists()
    {
        try
        {
            var dir = GetCustomVideosDirectory();
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
        }
        catch
        {
            // Ignore
        }
    }

    private void OnOpenFolderPressed()
    {
        var dir = GetCustomVideosDirectory();
        EnsureCustomVideosDirectoryExists();
        OS.ShellOpen(dir);
    }

    private void ScanMediaFiles()
    {
        _discoveredMediaFiles.Clear();
        _cardNodes.Clear();
        _selectedCardPanel = null;
        _selectedSourceMediaFile = null;

        if (_videoCardsGrid is null) return;

        foreach (var child in _videoCardsGrid.GetChildren())
        {
            child.QueueFree();
        }

        var searchDirs = new List<string>
        {
            GetCustomVideosDirectory(),
            System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "custom_videos")),
            ProjectSettings.GlobalizePath("res://scenes")
        };

        var validExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mov", ".mkv", ".ogv", ".avi" };
        var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in searchDirs)
        {
            if (!System.IO.Directory.Exists(dir)) continue;

            try
            {
                // Non-recursive scan of the clean videos folder
                var files = System.IO.Directory.GetFiles(dir, "*.*", System.IO.SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var ext = System.IO.Path.GetExtension(file);
                    if (validExts.Contains(ext) && !foundPaths.Contains(file))
                    {
                        foundPaths.Add(file);
                        _discoveredMediaFiles.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SceneCreator] Error scanning directory '{dir}': {ex.Message}");
            }
        }

        if (_discoveredMediaFiles.Count == 0)
        {
            if (_noVideosLabel is not null) _noVideosLabel.Visible = true;
            return;
        }

        if (_noVideosLabel is not null) _noVideosLabel.Visible = false;

        for (int i = 0; i < _discoveredMediaFiles.Count; i++)
        {
            var filePath = _discoveredMediaFiles[i];
            var card = CreateVideoCard(filePath);
            _videoCardsGrid.AddChild(card);
            _cardNodes.Add(card);

            if (i == 0)
            {
                SelectVideoCard(filePath, card);
            }
        }
    }

    private PanelContainer CreateVideoCard(string videoPath)
    {
        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(215, 175);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var defaultStyle = CreateCardStyleBox(false);
        card.AddThemeStyleboxOverride("panel", defaultStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        card.AddChild(vbox);

        // Aspect Ratio Thumbnail Container
        var aspect = new AspectRatioContainer
        {
            Ratio = 16.0f / 9.0f,
            CustomMinimumSize = new Vector2(200, 112),
            StretchMode = AspectRatioContainer.StretchModeEnum.Fit,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        vbox.AddChild(aspect);

        var texRect = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            CustomMinimumSize = new Vector2(200, 112),
        };

        var thumb = VideoThumbnailHelper.GetOrExtractThumbnail(videoPath);
        if (thumb is not null)
        {
            texRect.Texture = thumb;
        }
        else
        {
            texRect.Texture = null;
        }
        aspect.AddChild(texRect);

        // Title Label
        var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(videoPath);
        var cleanTitle = fileNameWithoutExt.Replace('_', ' ').Replace('-', ' ');
        cleanTitle = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanTitle);

        var titleLabel = new Label
        {
            Text = cleanTitle,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 13);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.118f, 0.106f, 0.294f));
        vbox.AddChild(titleLabel);

        // Details HBox (Duration & File Size)
        var detailsHBox = new HBoxContainer();
        detailsHBox.Alignment = BoxContainer.AlignmentMode.Center;
        detailsHBox.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(detailsHBox);

        var durationSec = AudioWaveformLoader.GetAudioDurationSeconds(videoPath);
        var durText = durationSec > 0 ? $"{durationSec:F1}s" : "Video";
        var durLabel = new Label
        {
            Text = durText,
        };
        durLabel.AddThemeFontSizeOverride("font_size", 11);
        durLabel.AddThemeColorOverride("font_color", new Color(0.38f, 0.32f, 0.85f));
        detailsHBox.AddChild(durLabel);

        try
        {
            var fileInfo = new System.IO.FileInfo(videoPath);
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
            var sizeLabel = new Label
            {
                Text = $"{sizeMb:F1} MB",
            };
            sizeLabel.AddThemeFontSizeOverride("font_size", 11);
            sizeLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.48f, 0.60f));
            detailsHBox.AddChild(sizeLabel);
        }
        catch
        {
            // Ignore file size errors
        }

        // Handle Click
        card.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                SelectVideoCard(videoPath, card);
            }
        };

        // Hover scale feedback
        card.MouseEntered += () =>
        {
            if (card != _selectedCardPanel)
            {
                var hoverStyle = CreateCardStyleBox(false);
                hoverStyle.BorderColor = new Color(0.55f, 0.46f, 0.98f);
                hoverStyle.ShadowSize = 8;
                card.AddThemeStyleboxOverride("panel", hoverStyle);
            }
        };
        card.MouseExited += () =>
        {
            if (card != _selectedCardPanel)
            {
                card.AddThemeStyleboxOverride("panel", CreateCardStyleBox(false));
            }
        };

        return card;
    }

    private void SelectVideoCard(string videoPath, PanelContainer card)
    {
        // Deselect previous
        if (_selectedCardPanel is not null)
        {
            _selectedCardPanel.AddThemeStyleboxOverride("panel", CreateCardStyleBox(false));
        }

        _selectedCardPanel = card;
        _selectedSourceMediaFile = videoPath;
        _selectedCardPanel.AddThemeStyleboxOverride("panel", CreateCardStyleBox(true));

        var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(videoPath);
        var cleanTitle = fileNameWithoutExt.Replace('_', ' ').Replace('-', ' ');
        cleanTitle = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanTitle);

        if (_titleInput is not null) _titleInput.Text = cleanTitle;
        if (_sceneIdInput is not null) _sceneIdInput.Text = ToKebabCase(fileNameWithoutExt);
        if (_errorLabel is not null) _errorLabel.Visible = false;
    }

    private static StyleBoxFlat CreateCardStyleBox(bool isSelected)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = isSelected ? new Color(0.940f, 0.930f, 1.000f) : new Color(0.975f, 0.985f, 1.000f),
            BorderColor = isSelected ? new Color(0.420f, 0.360f, 0.920f) : new Color(0.820f, 0.860f, 0.930f),
            BorderWidthLeft = isSelected ? 2 : 1,
            BorderWidthRight = isSelected ? 2 : 1,
            BorderWidthTop = isSelected ? 2 : 1,
            BorderWidthBottom = isSelected ? 2 : 1,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ShadowSize = isSelected ? 12 : 4,
            ShadowColor = isSelected ? new Color(0.42f, 0.36f, 0.92f, 0.25f) : new Color(0.1f, 0.15f, 0.3f, 0.05f),
            ShadowOffset = isSelected ? new Vector2(0, 3) : new Vector2(0, 1),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        return sb;
    }

    private void OnTitleChanged(string newTitle)
    {
        if (_sceneIdInput is not null)
        {
            _sceneIdInput.Text = ToKebabCase(newTitle);
        }
    }

    private static string ToKebabCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "my-custom-scene";
        var sanitized = Regex.Replace(input, @"[^a-zA-Z0-9\s-]", "");
        sanitized = Regex.Replace(sanitized, @"\s+", "-").Trim('-').ToLowerInvariant();
        sanitized = Regex.Replace(sanitized, @"-+", "-");
        return string.IsNullOrEmpty(sanitized) ? "my-custom-scene" : sanitized;
    }

    private async void OnSavePressed()
    {
        var title = _titleInput?.Text?.Trim() ?? string.Empty;
        var sceneId = _sceneIdInput?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("Please enter a scene title.");
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneId))
        {
            ShowError("Please enter a valid scene ID.");
            return;
        }

        var sourceMediaFile = _selectedSourceMediaFile;
        if (string.IsNullOrWhiteSpace(sourceMediaFile) || !System.IO.File.Exists(sourceMediaFile))
        {
            ShowError("Please select a video card from above before creating the scene.");
            return;
        }

        // UI loading state
        if (_saveButton is not null)
        {
            _saveButton.Disabled = true;
            _saveButton.Text = "Transcoding Video & Separating Stems...";
        }
        if (_statusInfoLabel is not null)
        {
            _statusInfoLabel.Text = "Transcoding video & separating vocals... Please wait.";
        }

        try
        {
            long durationMs = 20000;
            var segments = new List<DetectedSpeechSegment>
            {
                new("char-1", "Character 1", "Line 1 dialogue prompt", 1000, 4000),
                new("char-2", "Character 2", "Line 2 dialogue prompt", 4500, 8500)
            };

            // Destination folders
            var targetFolder = ProjectSettings.GlobalizePath($"user://workshop_scenes/{sceneId}");
            var mediaFolder = System.IO.Path.Combine(targetFolder, "media");
            if (!System.IO.Directory.Exists(mediaFolder))
            {
                System.IO.Directory.CreateDirectory(mediaFolder);
            }

            string videoRelPath = "media/video.ogv";

            // Run heavy file copying and transcoding in a background task
            await System.Threading.Tasks.Task.Run(() =>
            {
                if (sourceMediaFile is not null && System.IO.File.Exists(sourceMediaFile))
                {
                    var safeSourceMedia = System.IO.Path.Combine(mediaFolder, "source_input" + System.IO.Path.GetExtension(sourceMediaFile));
                    try
                    {
                        System.IO.File.Copy(sourceMediaFile, safeSourceMedia, true);
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[SceneCreator] Failed to copy source media: {ex.Message}");
                    }

                    // Extract thumbnail into package folder as well
                    var thumbFile = VideoThumbnailHelper.ExtractVideoThumbnailToFile(safeSourceMedia);
                    if (thumbFile is not null && System.IO.File.Exists(thumbFile))
                    {
                        try { System.IO.File.Copy(thumbFile, System.IO.Path.Combine(targetFolder, "thumbnail.png"), true); } catch { }
                    }

                    // Execute fast multithreaded OGV & WAV transcoding
                    VideoPlayback.MediaTranscoder.EnsureTranscoded(targetFolder);

                    var vocalsDst = System.IO.Path.Combine(mediaFolder, "vocals.wav");
                    var audioDst = System.IO.Path.Combine(mediaFolder, "audio.wav");

                    // Detect exact duration if vocals WAV or audio WAV exists
                    var wavForDur = System.IO.File.Exists(vocalsDst) ? vocalsDst : (System.IO.File.Exists(audioDst) ? audioDst : null);
                    if (wavForDur is not null)
                    {
                        var audioDur = AudioWaveformLoader.GetAudioDurationSeconds(wavForDur);
                        if (audioDur > 0)
                        {
                            durationMs = (long)(audioDur * 1000.0);
                        }
                    }
                }

                var doc = AiSceneBuilder.BuildScene(title, sceneId, durationMs, videoRelPath, segments);
                ProjectValidator.Validate(doc);

                var json = ProjectJsonSerializer.SerializeScene(doc);
                var jsonFilePath = System.IO.Path.Combine(targetFolder, "scene.json");
                System.IO.File.WriteAllText(jsonFilePath, json);

                GD.Print($"[SceneCreator] Custom scene package created: {jsonFilePath}");
            });

            // Load new package into session coordinator
            if (Coordinator is not null)
            {
                Coordinator.SelectedScenePackage = ScenePackageLoader.LoadPackageFromDirectory(targetFolder);
                Coordinator.CurrentScene = Coordinator.SelectedScenePackage.Document;
            }

            // Immediately navigate to Scene Editor so user can customize slots on waveform
            Navigator?.NavigateTo(AppScreen.SceneEditor);
        }
        catch (Exception ex)
        {
            if (_errorLabel is not null)
            {
                _errorLabel.Text = $"Creation failed: {ex.Message}";
                _errorLabel.Visible = true;
            }
            if (_saveButton is not null)
            {
                _saveButton.Disabled = false;
                _saveButton.Text = "Save & Open in Editor";
            }
        }
    }

    private void ShowError(string message)
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Text = message;
            _errorLabel.Visible = true;
        }
    }

    private void SetupButton(Button btn, Action action)
    {
        btn.Pressed += action;
        UiSoundManager.Attach(btn);
    }

    private void ApplyStyling()
    {
        // TopBar styling
        if (_topBar is not null)
        {
            var barStyle = new StyleBoxFlat
            {
                BgColor = new Color(1.0f, 1.0f, 1.0f, 0.92f),
                BorderWidthBottom = 1,
                BorderColor = new Color(0.886f, 0.902f, 0.941f, 0.8f),
                ShadowColor = new Color(0.1f, 0.1f, 0.2f, 0.04f),
                ShadowSize = 6
            };
            _topBar.AddThemeStyleboxOverride("panel", barStyle);
        }

        // CreatorCard styling (elevated tangible white surface)
        if (_creatorCard is not null)
        {
            var cardStyle = new StyleBoxFlat
            {
                BgColor = Colors.White,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.886f, 0.902f, 0.941f, 1.0f),
                CornerRadiusTopLeft = 24,
                CornerRadiusTopRight = 24,
                CornerRadiusBottomLeft = 24,
                CornerRadiusBottomRight = 24,
                ShadowColor = new Color(0.12f, 0.15f, 0.30f, 0.10f),
                ShadowSize = 24,
                ShadowOffset = new Vector2(0, 4)
            };
            _creatorCard.AddThemeStyleboxOverride("panel", cardStyle);
        }

        // Input Fields Styling
        StyleInput(_titleInput);
        StyleInput(_sceneIdInput);

        // Buttons Styling
        if (_openFolderButton is not null) StyleOutline(_openFolderButton, 12);
        if (_refreshMediaButton is not null) StyleOutline(_refreshMediaButton, 12);
        if (_cancelButton is not null) StyleOutline(_cancelButton, 14);
        if (_backButton is not null) StyleOutline(_backButton, 14);

        if (_saveButton is not null)
        {
            StyleSaveButton(_saveButton);
        }
    }

    private static void StyleInput(LineEdit? input)
    {
        if (input is null) return;
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.970f, 0.980f, 0.995f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.820f, 0.860f, 0.920f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14,
            ContentMarginRight = 14
        };
        var focus = new StyleBoxFlat
        {
            BgColor = Colors.White,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.460f, 0.380f, 0.920f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14,
            ContentMarginRight = 14
        };
        input.AddThemeStyleboxOverride("normal", normal);
        input.AddThemeStyleboxOverride("focus", focus);
        input.AddThemeColorOverride("font_color", new Color(0.118f, 0.106f, 0.294f));
        input.AddThemeColorOverride("font_placeholder_color", new Color(0.58f, 0.62f, 0.72f));
    }

    private static void StyleOutline(Button btn, int radius)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.955f, 0.975f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.780f, 0.850f, 0.950f),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius
        };
        var hover = new StyleBoxFlat
        {
            BgColor = new Color(0.910f, 0.945f, 0.990f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.38f, 0.71f, 1.0f),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.42f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.118f, 0.106f, 0.294f));
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }

    private static void StyleSaveButton(Button btn)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.460f, 0.380f, 0.920f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.760f, 0.700f, 1.000f),
            CornerRadiusTopLeft = 21,
            CornerRadiusTopRight = 21,
            CornerRadiusBottomLeft = 21,
            CornerRadiusBottomRight = 21,
            ShadowSize = 8,
            ShadowColor = new Color(0.35f, 0.25f, 0.75f, 0.35f),
            ShadowOffset = new Vector2(0, 3),
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        var hover = new StyleBoxFlat
        {
            BgColor = new Color(0.550f, 0.460f, 0.980f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = Colors.White,
            CornerRadiusTopLeft = 21,
            CornerRadiusTopRight = 21,
            CornerRadiusBottomLeft = 21,
            CornerRadiusBottomRight = 21,
            ShadowSize = 14,
            ShadowColor = new Color(0.45f, 0.35f, 0.95f, 0.50f),
            ShadowOffset = new Vector2(0, 4),
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        var pressed = new StyleBoxFlat
        {
            BgColor = new Color(0.360f, 0.280f, 0.800f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.650f, 0.580f, 0.950f),
            CornerRadiusTopLeft = 21,
            CornerRadiusTopRight = 21,
            CornerRadiusBottomLeft = 21,
            CornerRadiusBottomRight = 21,
            ShadowSize = 2,
            ShadowColor = new Color(0.25f, 0.15f, 0.60f, 0.30f),
            ShadowOffset = new Vector2(0, 1),
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 5,
            ContentMarginBottom = 3
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", new Color(0.95f, 0.95f, 1.0f));
        btn.AddThemeColorOverride("font_shadow_color", new Color(0.15f, 0.10f, 0.40f, 0.45f));
        btn.AddThemeConstantOverride("shadow_offset_y", 1);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        btn.PivotOffset = new Vector2(130, 23);

        btn.MouseEntered += () =>
        {
            var tween = btn.CreateTween();
            tween.TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.12)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        };
        btn.MouseExited += () =>
        {
            var tween = btn.CreateTween();
            tween.TweenProperty(btn, "scale", Vector2.One, 0.10)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        };
    }

    private void OnCancelPressed()
    {
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }
}
