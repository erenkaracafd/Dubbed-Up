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
    private Button? _openFolderButton;
    private Button? _refreshMediaButton;
    private Label? _noVideosLabel;
    private HFlowContainer? _videoCardsGrid;

    private LineEdit? _titleInput;
    private LineEdit? _sceneIdInput;
    private SpinBox? _durationSpinBox;
    private LineEdit? _char1NameInput;
    private LineEdit? _char2NameInput;
    private LineEdit? _prompt1Input;
    private SpinBox? _slot1StartSpin;
    private SpinBox? _slot1EndSpin;
    private LineEdit? _prompt2Input;
    private SpinBox? _slot2StartSpin;
    private SpinBox? _slot2EndSpin;

    // AI Auto-detect fields
    private TextEdit? _srtInputText;
    private Button? _autoExtractButton;
    private Label? _aiStatusLabel;

    private Label? _errorLabel;
    private Button? _saveButton;
    private Button? _cancelButton;

    private readonly List<string> _discoveredMediaFiles = [];
    private readonly List<PanelContainer> _cardNodes = [];
    private string? _selectedSourceMediaFile;
    private PanelContainer? _selectedCardPanel;

    public override void _Ready()
    {
        _openFolderButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/FolderButtonsHBox/OpenFolderButton");
        _refreshMediaButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/FolderButtonsHBox/RefreshMediaButton");
        _noVideosLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/NoVideosLabel");
        _videoCardsGrid = GetNodeOrNull<HFlowContainer>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/VideoCardsScroll/VideoCardsGrid");

        _titleInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/TitleInput");
        _sceneIdInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/SceneIdInput");
        _durationSpinBox = GetNodeOrNull<SpinBox>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/DurationSpinBox");

        _srtInputText = GetNodeOrNull<TextEdit>("ScrollContainer/CenterContainer/VBoxContainer/AiContainer/SrtInputText");
        _autoExtractButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/AiContainer/AutoExtractButton");
        _aiStatusLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/AiContainer/AiStatusLabel");

        _char1NameInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/Char1NameInput");
        _char2NameInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/Char2NameInput");

        _prompt1Input = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/Slot1PromptInput");
        _slot1StartSpin = GetNodeOrNull<SpinBox>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/Slot1HBox/StartSpin");
        _slot1EndSpin = GetNodeOrNull<SpinBox>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/Slot1HBox/EndSpin");

        _prompt2Input = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/Slot2PromptInput");
        _slot2StartSpin = GetNodeOrNull<SpinBox>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/Slot2HBox/StartSpin");
        _slot2EndSpin = GetNodeOrNull<SpinBox>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/Slot2HBox/EndSpin");

        _errorLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/ErrorLabel");
        _saveButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ButtonsHBox/SaveButton");
        _cancelButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ButtonsHBox/CancelButton");

        if (_openFolderButton is not null) _openFolderButton.Pressed += OnOpenFolderPressed;
        if (_refreshMediaButton is not null) _refreshMediaButton.Pressed += ScanMediaFiles;

        if (_titleInput is not null) _titleInput.TextChanged += OnTitleChanged;
        if (_autoExtractButton is not null) _autoExtractButton.Pressed += OnAutoExtractPressed;
        if (_saveButton is not null) _saveButton.Pressed += OnSavePressed;
        if (_cancelButton is not null) _cancelButton.Pressed += OnCancelPressed;

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
        titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 1.0f));
        vbox.AddChild(titleLabel);

        // Details HBox (Duration & File Size)
        var detailsHBox = new HBoxContainer();
        detailsHBox.Alignment = BoxContainer.AlignmentMode.Center;
        detailsHBox.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(detailsHBox);

        var durationSec = AudioWaveformLoader.GetAudioDurationSeconds(videoPath);
        var durText = durationSec > 0 ? $"⏱ {durationSec:F1}s" : "🎬 Video";
        var durLabel = new Label
        {
            Text = durText,
        };
        durLabel.AddThemeFontSizeOverride("font_size", 11);
        durLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 1.0f));
        detailsHBox.AddChild(durLabel);

        try
        {
            var fileInfo = new System.IO.FileInfo(videoPath);
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
            var sizeLabel = new Label
            {
                Text = $"📁 {sizeMb:F1} MB",
            };
            sizeLabel.AddThemeFontSizeOverride("font_size", 11);
            sizeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.85f));
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

        var dur = AudioWaveformLoader.GetAudioDurationSeconds(videoPath);
        if (dur > 0 && _durationSpinBox is not null)
        {
            _durationSpinBox.MaxValue = Math.Max(1800.0, Math.Ceiling(dur) + 10.0);
            _durationSpinBox.Value = Math.Round(dur, 1);
        }

        if (_errorLabel is not null) _errorLabel.Visible = false;
    }

    private static StyleBoxFlat CreateCardStyleBox(bool isSelected)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = isSelected ? new Color(0.12f, 0.18f, 0.28f, 1.0f) : new Color(0.08f, 0.10f, 0.15f, 0.9f),
            BorderColor = isSelected ? new Color(0.0f, 0.9f, 1.0f, 1.0f) : new Color(0.2f, 0.25f, 0.35f, 0.7f),
            BorderWidthLeft = isSelected ? 3 : 1,
            BorderWidthRight = isSelected ? 3 : 1,
            BorderWidthTop = isSelected ? 3 : 1,
            BorderWidthBottom = isSelected ? 3 : 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
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

    private void OnAutoExtractPressed()
    {
        var rawText = _srtInputText?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            if (_aiStatusLabel is not null)
            {
                _aiStatusLabel.Text = "⚠️ Please paste an SRT subtitle or timestamped script above.";
            }
            return;
        }

        var detected = LocalAiSceneExtractor.ParseSrtText(rawText);
        if (detected.Count == 0)
        {
            if (_aiStatusLabel is not null)
            {
                _aiStatusLabel.Text = "❌ No timestamped dialogue found in the text. Check formatting.";
            }
            return;
        }

        if (detected.Count > 0)
        {
            var first = detected[0];
            if (_char1NameInput is not null && !string.IsNullOrWhiteSpace(first.SpeakerDisplayName))
                _char1NameInput.Text = first.SpeakerDisplayName;
            if (_prompt1Input is not null && !string.IsNullOrWhiteSpace(first.Prompt))
                _prompt1Input.Text = first.Prompt;
            if (_slot1StartSpin is not null) _slot1StartSpin.Value = first.StartMilliseconds / 1000.0;
            if (_slot1EndSpin is not null) _slot1EndSpin.Value = first.EndMilliseconds / 1000.0;
        }

        if (detected.Count > 1)
        {
            var second = detected[1];
            if (_char2NameInput is not null && !string.IsNullOrWhiteSpace(second.SpeakerDisplayName))
                _char2NameInput.Text = second.SpeakerDisplayName;
            if (_prompt2Input is not null && !string.IsNullOrWhiteSpace(second.Prompt))
                _prompt2Input.Text = second.Prompt;
            if (_slot2StartSpin is not null) _slot2StartSpin.Value = second.StartMilliseconds / 1000.0;
            if (_slot2EndSpin is not null) _slot2EndSpin.Value = second.EndMilliseconds / 1000.0;
        }

        var maxEndMs = detected.Max(d => d.EndMilliseconds);
        if (_durationSpinBox is not null && maxEndMs > (long)(_durationSpinBox.Value * 1000.0))
        {
            _durationSpinBox.Value = (maxEndMs / 1000.0) + 1.0;
        }

        if (_aiStatusLabel is not null)
        {
            _aiStatusLabel.Text = $"✅ Extracted {detected.Count} speech lines into characters!";
        }
    }

    private async void OnSavePressed()
    {
        var title = _titleInput?.Text?.Trim() ?? string.Empty;
        var sceneId = _sceneIdInput?.Text?.Trim() ?? string.Empty;
        var durationSec = _durationSpinBox?.Value ?? 10.0;
        var durationMs = (long)(durationSec * 1000.0);

        var char1Name = _char1NameInput?.Text?.Trim();
        var char2Name = _char2NameInput?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(char1Name)) char1Name = "Character 1";
        if (string.IsNullOrWhiteSpace(char2Name)) char2Name = "Character 2";

        var prompt1 = _prompt1Input?.Text?.Trim() ?? "Line 1 dialogue prompt";
        var slot1StartMs = (long)((_slot1StartSpin?.Value ?? 1.0) * 1000.0);
        var slot1EndMs = (long)((_slot1EndSpin?.Value ?? 4.0) * 1000.0);

        var prompt2 = _prompt2Input?.Text?.Trim() ?? "Line 2 dialogue prompt";
        var slot2StartMs = (long)((_slot2StartSpin?.Value ?? 5.0) * 1000.0);
        var slot2EndMs = (long)((_slot2EndSpin?.Value ?? 8.0) * 1000.0);

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
            _saveButton.Text = "⏳ Transcoding Video & Separating Stems...";
        }
        if (_aiStatusLabel is not null)
        {
            _aiStatusLabel.Text = "⏳ Transcoding video & separating vocals... Please wait.";
        }

        try
        {
            var segments = new List<DetectedSpeechSegment>
            {
                new("char-1", char1Name, prompt1, slot1StartMs, slot1EndMs),
                new("char-2", char2Name, prompt2, slot2StartMs, slot2EndMs)
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

                    // Execute fast multithreaded OGV & WAV transcoding with dense keyframes
                    VideoPlayback.MediaTranscoder.EnsureTranscoded(targetFolder, force: true);

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

    private void OnCancelPressed()
    {
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }
}
