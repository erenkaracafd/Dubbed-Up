using System.Text.RegularExpressions;
using DubbedUp.Core.Ai;
using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using DubbedUp.Godot.Ai;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SceneCreatorScreen : BaseScreen
{
    private Button? _openFolderButton;
    private Button? _refreshMediaButton;
    private OptionButton? _mediaOption;

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

    public override void _Ready()
    {
        _openFolderButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/FolderButtonsHBox/OpenFolderButton");
        _refreshMediaButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/FolderButtonsHBox/RefreshMediaButton");
        _mediaOption = GetNodeOrNull<OptionButton>("ScrollContainer/CenterContainer/VBoxContainer/FolderInfoContainer/MediaOption");

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
        if (_mediaOption is not null) _mediaOption.ItemSelected += OnMediaFileSelected;

        if (_titleInput is not null) _titleInput.TextChanged += OnTitleChanged;
        if (_autoExtractButton is not null) _autoExtractButton.Pressed += OnAutoExtractPressed;
        if (_saveButton is not null) _saveButton.Pressed += OnSavePressed;
        if (_cancelButton is not null) _cancelButton.Pressed += OnCancelPressed;

        EnsureCustomScenesDirectoryExists();
        ScanMediaFiles();
    }

    private string GetCustomScenesDirectory()
    {
        var userPath = ProjectSettings.GlobalizePath("user://workshop_scenes");
        return userPath;
    }

    private void EnsureCustomScenesDirectoryExists()
    {
        try
        {
            var dir = GetCustomScenesDirectory();
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
        var dir = GetCustomScenesDirectory();
        EnsureCustomScenesDirectoryExists();
        OS.ShellOpen(dir);
    }

    private void ScanMediaFiles()
    {
        _discoveredMediaFiles.Clear();
        if (_mediaOption is null) return;
        _mediaOption.Clear();

        var searchDirs = new List<string>
        {
            GetCustomScenesDirectory(),
            ProjectSettings.GlobalizePath("res://scenes"),
            ProjectSettings.GlobalizePath("res://Content/OfficialScenes")
        };

        var rootScenes = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "scenes"));
        if (System.IO.Directory.Exists(rootScenes)) searchDirs.Add(rootScenes);

        var validExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ogv", ".mp4", ".webm", ".wav", ".ogg" };
        var foundPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in searchDirs)
        {
            if (!System.IO.Directory.Exists(dir)) continue;

            try
            {
                var files = System.IO.Directory.GetFiles(dir, "*.*", System.IO.SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var ext = System.IO.Path.GetExtension(file);
                    if (validExts.Contains(ext))
                    {
                        var filename = System.IO.Path.GetFileName(file);
                        if (!foundPaths.Contains(file))
                        {
                            foundPaths.Add(file);
                            _discoveredMediaFiles.Add(file);
                        }
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
            _mediaOption.AddItem("⚠️ No media files found. Click 'Open Folder' and drop .ogv, .mp4, or .wav files!", 0);
        }
        else
        {
            for (int i = 0; i < _discoveredMediaFiles.Count; i++)
            {
                var fullPath = _discoveredMediaFiles[i];
                var fileName = System.IO.Path.GetFileName(fullPath);
                var parentDir = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(fullPath) ?? "");
                var ext = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();
                var icon = ext is ".wav" or ".ogg" ? "🎵" : "🎬";

                _mediaOption.AddItem($"{icon} {fileName}  ({parentDir})", i);
            }

            _mediaOption.Select(0);
            OnMediaFileSelected(0);
        }
    }

    private void OnMediaFileSelected(long index)
    {
        if (index < 0 || index >= _discoveredMediaFiles.Count) return;

        var selectedPath = _discoveredMediaFiles[(int)index];
        var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(selectedPath);
        var ext = System.IO.Path.GetExtension(selectedPath).ToLowerInvariant();

        var cleanTitle = fileNameWithoutExt.Replace('_', ' ').Replace('-', ' ');
        cleanTitle = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanTitle);

        if (_titleInput is not null) _titleInput.Text = cleanTitle;
        if (_sceneIdInput is not null) _sceneIdInput.Text = ToKebabCase(fileNameWithoutExt);

        var dur = AudioWaveformLoader.GetAudioDurationSeconds(selectedPath);
        if (dur > 0 && _durationSpinBox is not null)
        {
            _durationSpinBox.Value = Math.Round(dur, 1);
        }
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

        try
        {
            var segments = LocalAiSceneExtractor.ParseSrtText(rawText);
            if (segments.Count == 0)
            {
                if (_aiStatusLabel is not null) _aiStatusLabel.Text = "⚠️ No dialogue lines could be parsed from the text.";
                return;
            }

            if (segments.Count >= 1)
            {
                if (_char1NameInput is not null) _char1NameInput.Text = segments[0].SpeakerDisplayName;
                if (_prompt1Input is not null) _prompt1Input.Text = segments[0].Prompt;
                if (_slot1StartSpin is not null) _slot1StartSpin.Value = segments[0].StartMilliseconds / 1000.0;
                if (_slot1EndSpin is not null) _slot1EndSpin.Value = segments[0].EndMilliseconds / 1000.0;
            }

            if (segments.Count >= 2)
            {
                if (_char2NameInput is not null) _char2NameInput.Text = segments[1].SpeakerDisplayName;
                if (_prompt2Input is not null) _prompt2Input.Text = segments[1].Prompt;
                if (_slot2StartSpin is not null) _slot2StartSpin.Value = segments[1].StartMilliseconds / 1000.0;
                if (_slot2EndSpin is not null) _slot2EndSpin.Value = segments[1].EndMilliseconds / 1000.0;
            }

            var maxEnd = segments.Max(s => s.EndMilliseconds) / 1000.0;
            if (_durationSpinBox is not null) _durationSpinBox.Value = Math.Max(_durationSpinBox.Value, maxEnd + 1.0);

            if (_aiStatusLabel is not null)
            {
                _aiStatusLabel.Text = $"✅ Successfully parsed {segments.Count} dialogue lines from subtitles!";
            }
        }
        catch (Exception ex)
        {
            if (_aiStatusLabel is not null)
            {
                _aiStatusLabel.Text = $"❌ AI Extraction failed: {ex.Message}";
            }
        }
    }

    private static bool RunStemSeparation(string mediaFilePath, string outputMediaFolder)
    {
        try
        {
            var scriptPath = ProjectSettings.GlobalizePath("res://scripts/separate_stems.py");
            if (!System.IO.File.Exists(scriptPath))
            {
                var repoScript = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.Environment.CurrentDirectory, "scripts", "separate_stems.py"));
                if (System.IO.File.Exists(repoScript)) scriptPath = repoScript;
            }

            if (System.IO.File.Exists(scriptPath))
            {
                return VideoPlayback.MediaTranscoder.RunProcess("python", $"\"{scriptPath}\" \"{mediaFilePath}\" \"{outputMediaFolder}\"", 60000);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SceneCreator] Stem separation execution error: {ex.Message}");
        }

        return false;
    }

    private async void OnSavePressed()
    {
        if (_errorLabel is not null) _errorLabel.Visible = false;

        var title = string.IsNullOrWhiteSpace(_titleInput?.Text) ? "Custom Scene" : _titleInput.Text.Trim();
        var sceneId = string.IsNullOrWhiteSpace(_sceneIdInput?.Text) ? ToKebabCase(title) : ToKebabCase(_sceneIdInput.Text);

        var durationSec = _durationSpinBox?.Value ?? 10.0;
        var durationMs = (long)(durationSec * 1000.0);

        var char1Name = string.IsNullOrWhiteSpace(_char1NameInput?.Text) ? "Character 1" : _char1NameInput.Text.Trim();
        var char2Name = string.IsNullOrWhiteSpace(_char2NameInput?.Text) ? "Character 2" : _char2NameInput.Text.Trim();

        var prompt1 = string.IsNullOrWhiteSpace(_prompt1Input?.Text) ? "Speak your line!" : _prompt1Input.Text.Trim();
        var slot1StartMs = (long)((_slot1StartSpin?.Value ?? 1.0) * 1000.0);
        var slot1EndMs = (long)((_slot1EndSpin?.Value ?? 4.0) * 1000.0);

        var prompt2 = string.IsNullOrWhiteSpace(_prompt2Input?.Text) ? "Respond with emotion!" : _prompt2Input.Text.Trim();
        var slot2StartMs = (long)((_slot2StartSpin?.Value ?? 4.5) * 1000.0);
        var slot2EndMs = (long)((_slot2EndSpin?.Value ?? 8.5) * 1000.0);

        var selectedMediaIndex = _mediaOption?.Selected ?? -1;
        var hasDiscoveredMedia = _discoveredMediaFiles.Count > 0 && selectedMediaIndex >= 0 && selectedMediaIndex < _discoveredMediaFiles.Count;
        var sourceMediaFile = hasDiscoveredMedia ? _discoveredMediaFiles[selectedMediaIndex] : null;

        // UI Feedback & prevent duplicate clicks
        if (_saveButton is not null)
        {
            _saveButton.Disabled = true;
            _saveButton.Text = "⏳ Processing Video...";
        }
        if (_aiStatusLabel is not null)
        {
            _aiStatusLabel.Text = "⏳ Transcoding video & preparing audio waveform... Please wait.";
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

    private void OnCancelPressed()
    {
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }
}
