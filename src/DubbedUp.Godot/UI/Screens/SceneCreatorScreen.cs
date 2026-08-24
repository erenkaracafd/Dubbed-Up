using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
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
    private Label? _errorLabel;
    private Button? _saveButton;
    private Button? _cancelButton;

    public override void _Ready()
    {
        _titleInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/TitleInput");
        _sceneIdInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/SceneIdInput");
        _videoPathInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/VideoPathInput");
        _durationSpinBox = GetNodeOrNull<SpinBox>("ScrollContainer/CenterContainer/VBoxContainer/FormContainer/DurationSpinBox");

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
        if (_sceneIdInput is not null && string.IsNullOrWhiteSpace(_sceneIdInput.Text))
        {
            var slug = newTitle.ToLowerInvariant().Replace(' ', '-');
            _sceneIdInput.PlaceholderText = slug;
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

        var videoRelPath = string.IsNullOrWhiteSpace(_videoPathInput?.Text) ? "video.mp4" : _videoPathInput.Text.Trim();
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
            var doc = new OfficialSceneDocument
            {
                SchemaVersion = ProjectSchema.CurrentVersion,
                SceneId = sceneId,
                Title = title,
                DurationMilliseconds = durationMs,
                SourceMedia =
                [
                    new SourceMediaAsset
                    {
                        MediaId = "scene-video",
                        Role = SourceMediaRole.SceneVideo,
                        RelativePath = videoRelPath,
                    }
                ],
                Characters =
                [
                    new CharacterDefinition { CharacterId = "char-1", DisplayName = char1Name },
                    new CharacterDefinition { CharacterId = "char-2", DisplayName = char2Name },
                ],
                VoiceSlots =
                [
                    new VoiceSlotDefinition { VoiceSlotId = "slot-1", CharacterId = "char-1", Prompt = prompt1 },
                    new VoiceSlotDefinition { VoiceSlotId = "slot-2", CharacterId = "char-2", Prompt = prompt2 },
                ],
                Timeline =
                [
                    new TimelineEntry { TimelineEntryId = "entry-1", VoiceSlotId = "slot-1", StartMilliseconds = slot1StartMs, EndMilliseconds = slot1EndMs },
                    new TimelineEntry { TimelineEntryId = "entry-2", VoiceSlotId = "slot-2", StartMilliseconds = slot2StartMs, EndMilliseconds = slot2EndMs },
                ]
            };

            // 1. Validate scene
            ProjectValidator.Validate(doc);

            // 2. Serialize JSON
            var json = ProjectJsonSerializer.SerializeScene(doc);

            // 3. Write to user workshop folder
            var targetFolder = ProjectSettings.GlobalizePath($"user://workshop_scenes/{sceneId}");
            if (!System.IO.Directory.Exists(targetFolder))
            {
                System.IO.Directory.CreateDirectory(targetFolder);
            }

            var jsonFilePath = System.IO.Path.Combine(targetFolder, "scene.json");
            System.IO.File.WriteAllText(jsonFilePath, json);

            // 4. Return to ScenePicker
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
