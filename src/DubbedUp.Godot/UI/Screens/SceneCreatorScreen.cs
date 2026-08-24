using DubbedUp.Core.Ai;
using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using DubbedUp.Godot.Ai;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SceneCreatorScreen : BaseScreen
{
    private LineEdit? _titleInput;
    private LineEdit? _sceneIdInput;
    private LineEdit? _videoPathInput;
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

    public override void _Ready()
    {
        _titleInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/TitleInput");
        _sceneIdInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/SceneIdInput");
        _videoPathInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/VideoPathInput");
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

        if (_titleInput is not null)
        {
            _titleInput.TextChanged += OnTitleChanged;
        }

        if (_autoExtractButton is not null)
        {
            _autoExtractButton.Pressed += OnAutoExtractPressed;
        }

        if (_saveButton is not null)
        {
            _saveButton.Pressed += OnSavePressed;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Pressed += OnCancelPressed;
        }
    }

    private void OnTitleChanged(string newTitle)
    {
        if (_sceneIdInput is not null && (string.IsNullOrWhiteSpace(_sceneIdInput.Text) || _sceneIdInput.Text == "my-custom-scene"))
        {
            var slug = newTitle.ToLowerInvariant().Replace(' ', '-').Replace(".", "");
            _sceneIdInput.Text = slug;
        }
    }

    private void OnAutoExtractPressed()
    {
        var rawText = _srtInputText?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            if (_aiStatusLabel is not null)
            {
                _aiStatusLabel.Text = "⚠️ Lütfen yukarıdaki kutuya bir SRT altyazı veya zaman damgalı metin yapıştırın.";
            }
            return;
        }

        try
        {
            var segments = LocalAiSceneExtractor.ParseSrtText(rawText);
            if (segments.Count == 0)
            {
                if (_aiStatusLabel is not null)
                {
                    _aiStatusLabel.Text = "⚠️ Metinde geçerli zaman damgası bulunamadı. Örn: 00:00:01,000 --> 00:00:04,500";
                }
                return;
            }

            // Populate detected fields
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

            var maxEndSec = segments.Max(s => s.EndMilliseconds) / 1000.0 + 1.0;
            if (_durationSpinBox is not null)
            {
                _durationSpinBox.Value = Math.Max(_durationSpinBox.Value, maxEndSec);
            }

            if (_aiStatusLabel is not null)
            {
                _aiStatusLabel.Text = $"✅ Yerel AI: {segments.Count} replik başarıyla ayrıştırıldı ve alanlara dolduruldu!";
            }
        }
        catch (Exception ex)
        {
            if (_aiStatusLabel is not null)
            {
                _aiStatusLabel.Text = $"Ayrıştırma hatası: {ex.Message}";
            }
        }
    }

    private void OnSavePressed()
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Visible = false;
        }

        var title = string.IsNullOrWhiteSpace(_titleInput?.Text) ? "Custom Scene" : _titleInput.Text.Trim();
        var sceneId = string.IsNullOrWhiteSpace(_sceneIdInput?.Text)
            ? title.ToLowerInvariant().Replace(' ', '-').Replace(".", "")
            : _sceneIdInput.Text.Trim().ToLowerInvariant().Replace(' ', '-');

        var videoRelPath = string.IsNullOrWhiteSpace(_videoPathInput?.Text) ? "media/video.mp4" : _videoPathInput.Text.Trim();
        var durationSec = _durationSpinBox?.Value ?? 10.0;
        var durationMs = (long)(durationSec * 1000.0);

        var char1Name = string.IsNullOrWhiteSpace(_char1NameInput?.Text) ? "Character 1" : _char1NameInput.Text.Trim();
        var char2Name = string.IsNullOrWhiteSpace(_char2NameInput?.Text) ? "Character 2" : _char2NameInput.Text.Trim();

        var prompt1 = string.IsNullOrWhiteSpace(_prompt1Input?.Text) ? "Say your line!" : _prompt1Input.Text.Trim();
        var slot1StartMs = (long)((_slot1StartSpin?.Value ?? 1.0) * 1000.0);
        var slot1EndMs = (long)((_slot1EndSpin?.Value ?? 4.0) * 1000.0);

        var prompt2 = string.IsNullOrWhiteSpace(_prompt2Input?.Text) ? "Respond with emotion!" : _prompt2Input.Text.Trim();
        var slot2StartMs = (long)((_slot2StartSpin?.Value ?? 4.5) * 1000.0);
        var slot2EndMs = (long)((_slot2EndSpin?.Value ?? 8.5) * 1000.0);

        try
        {
            var segments = new List<DetectedSpeechSegment>
            {
                new("char-1", char1Name, prompt1, slot1StartMs, slot1EndMs),
                new("char-2", char2Name, prompt2, slot2StartMs, slot2EndMs)
            };

            var doc = AiSceneBuilder.BuildScene(title, sceneId, durationMs, videoRelPath, segments);

            // 1. Serialize JSON
            var json = ProjectJsonSerializer.SerializeScene(doc);

            // 2. Write to user workshop folder
            var targetFolder = ProjectSettings.GlobalizePath($"user://workshop_scenes/{sceneId}");
            var mediaFolder = System.IO.Path.Combine(targetFolder, "media");

            if (!System.IO.Directory.Exists(mediaFolder))
            {
                System.IO.Directory.CreateDirectory(mediaFolder);
            }

            var jsonFilePath = System.IO.Path.Combine(targetFolder, "scene.json");
            System.IO.File.WriteAllText(jsonFilePath, json);

            // 3. Return to ScenePicker
            Navigator?.NavigateTo(AppScreen.ScenePicker);
        }
        catch (Exception ex)
        {
            if (_errorLabel is not null)
            {
                _errorLabel.Text = $"Creation failed: {ex.Message}";
                _errorLabel.Visible = true;
            }
        }
    }

    private void OnCancelPressed()
    {
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }
}
