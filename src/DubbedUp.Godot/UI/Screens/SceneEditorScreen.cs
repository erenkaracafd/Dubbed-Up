using System.Text.Json;
using DubbedUp.Core.Characters;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SceneEditorScreen : BaseScreen
{
    private LineEdit? _titleInput;
    private SpinBox? _durationInput;
    private VideoStreamPlayer? _videoPlayer;
    private Button? _playVideoButton;
    private Label? _videoTimeLabel;
    private VBoxContainer? _slotsContainer;
    private Button? _addSlotButton;
    private Button? _saveButton;
    private Button? _backButton;
    private Label? _statusLabel;

    private readonly List<EditableVoiceSlot> _editableSlots = [];
    private bool _isPreviewingSlot = false;
    private double _previewEndSec = 0.0;

    private sealed class EditableVoiceSlot
    {
        public string SlotId { get; set; } = $"slot-{Guid.NewGuid():N}";
        public string CharacterId { get; set; } = "char-1";
        public string CharacterName { get; set; } = "Karakter";
        public string Prompt { get; set; } = "";
        public double StartSeconds { get; set; } = 0.0;
        public double EndSeconds { get; set; } = 4.0;
    }

    public override void _Ready()
    {
        _titleInput = GetNodeOrNull<LineEdit>("ScrollContainer/CenterContainer/VBoxContainer/HeaderGrid/TitleInput");
        _durationInput = GetNodeOrNull<SpinBox>("ScrollContainer/CenterContainer/VBoxContainer/HeaderGrid/DurationInput");
        _videoPlayer = GetNodeOrNull<VideoStreamPlayer>("ScrollContainer/CenterContainer/VBoxContainer/VideoPanel/VideoPlayer");
        _playVideoButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/PlayVideoButton");
        _videoTimeLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/VideoTimeLabel");
        _slotsContainer = GetNodeOrNull<VBoxContainer>("ScrollContainer/CenterContainer/VBoxContainer/SlotsContainer");
        _addSlotButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/AddSlotButton");
        _saveButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/SaveButton");
        _backButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/BackButton");
        _statusLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/StatusLabel");

        if (_playVideoButton is not null) _playVideoButton.Pressed += OnPlayVideoPressed;
        if (_addSlotButton is not null) _addSlotButton.Pressed += OnAddSlotPressed;
        if (_saveButton is not null) _saveButton.Pressed += OnSavePressed;
        if (_backButton is not null) _backButton.Pressed += OnBackPressed;

        LoadSceneData();
    }

    private void LoadSceneData()
    {
        var doc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
        if (doc is null)
        {
            if (_statusLabel is not null) _statusLabel.Text = "❌ Düzenlenecek sahne bulunamadı.";
            return;
        }

        if (_titleInput is not null) _titleInput.Text = doc.Title;
        if (_durationInput is not null) _durationInput.Value = doc.DurationMilliseconds / 1000.0;

        _editableSlots.Clear();
        foreach (var slot in doc.VoiceSlots)
        {
            var timeline = doc.Timeline.FirstOrDefault(t => t.VoiceSlotId == slot.VoiceSlotId);
            var charDef = doc.Characters.FirstOrDefault(c => c.CharacterId == slot.CharacterId);

            _editableSlots.Add(new EditableVoiceSlot
            {
                SlotId = slot.VoiceSlotId,
                CharacterId = slot.CharacterId,
                CharacterName = charDef?.DisplayName ?? slot.CharacterId,
                Prompt = slot.Prompt,
                StartSeconds = (timeline?.StartMilliseconds ?? 0) / 1000.0,
                EndSeconds = (timeline?.EndMilliseconds ?? 4000) / 1000.0
            });
        }

        LoadVideo();
        RebuildSlotsUi();
    }

    private void LoadVideo()
    {
        if (_videoPlayer is null || Coordinator?.CurrentScene is null) return;

        var videoAsset = Coordinator.CurrentScene.SourceMedia.FirstOrDefault(m => m.Role == Core.Scenes.SourceMediaRole.SceneVideo);
        var relPath = videoAsset?.RelativePath ?? "media/speed_homeless.ogv";
        var folderPath = Coordinator.SelectedScenePackage?.PackageDirectory;

        string? resolvedFilePath = null;
        if (!string.IsNullOrEmpty(folderPath))
        {
            var candidate = System.IO.Path.Combine(folderPath, relPath);
            if (System.IO.File.Exists(candidate)) resolvedFilePath = candidate;
        }

        if (resolvedFilePath is null)
        {
            var sceneId = Coordinator.CurrentScene.SceneId;
            var candidate = ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{sceneId}/{relPath}");
            if (System.IO.File.Exists(candidate)) resolvedFilePath = candidate;
        }

        if (resolvedFilePath is not null)
        {
            try
            {
                var localized = ProjectSettings.LocalizePath(resolvedFilePath);
                var stream = new VideoStreamTheora();
                stream.File = !string.IsNullOrEmpty(localized) ? localized : resolvedFilePath.Replace("\\", "/");
                _videoPlayer.Stream = stream;
                _videoPlayer.Expand = true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SceneEditor] Video error: {ex.Message}");
            }
        }
    }

    private void RebuildSlotsUi()
    {
        if (_slotsContainer is null) return;

        foreach (var child in _slotsContainer.GetChildren())
        {
            child.QueueFree();
        }

        for (int i = 0; i < _editableSlots.Count; i++)
        {
            var slotIndex = i;
            var slot = _editableSlots[i];

            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(760, 95);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            panel.AddChild(vbox);

            // Row 1: Header + Character + Times + Delete
            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 10);
            vbox.AddChild(topRow);

            var numLabel = new Label
            {
                Text = $"#{slotIndex + 1}",
                CustomMinimumSize = new Vector2(30, 0),
            };
            numLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
            topRow.AddChild(numLabel);

            var charInput = new LineEdit
            {
                Text = slot.CharacterName,
                PlaceholderText = "Karakter Adı",
                CustomMinimumSize = new Vector2(160, 32),
            };
            charInput.TextChanged += (newText) =>
            {
                slot.CharacterName = newText.Trim();
                slot.CharacterId = newText.Trim().ToLowerInvariant().Replace(" ", "-");
            };
            topRow.AddChild(charInput);

            var startLabel = new Label { Text = "Başlangıç (sn):" };
            topRow.AddChild(startLabel);

            var startSpin = new SpinBox
            {
                MinValue = 0.0,
                MaxValue = 600.0,
                Step = 0.1,
                Value = slot.StartSeconds,
                CustomMinimumSize = new Vector2(90, 32),
            };
            startSpin.ValueChanged += (newVal) => slot.StartSeconds = newVal;
            topRow.AddChild(startSpin);

            var endLabel = new Label { Text = "Bitiş (sn):" };
            topRow.AddChild(endLabel);

            var endSpin = new SpinBox
            {
                MinValue = 0.0,
                MaxValue = 600.0,
                Step = 0.1,
                Value = slot.EndSeconds,
                CustomMinimumSize = new Vector2(90, 32),
            };
            endSpin.ValueChanged += (newVal) => slot.EndSeconds = newVal;
            topRow.AddChild(endSpin);

            var playRangeBtn = new Button
            {
                Text = "🎧 Önizle",
                CustomMinimumSize = new Vector2(85, 32),
            };
            playRangeBtn.Pressed += () => PlaySlotRange(slot.StartSeconds, slot.EndSeconds);
            topRow.AddChild(playRangeBtn);

            var deleteBtn = new Button
            {
                Text = "🗑️ Sil",
                CustomMinimumSize = new Vector2(65, 32),
            };
            deleteBtn.Pressed += () => DeleteSlot(slotIndex);
            topRow.AddChild(deleteBtn);

            // Row 2: Prompt / Subtitle Text
            var promptInput = new LineEdit
            {
                Text = slot.Prompt,
                PlaceholderText = "Repliğin / Altyazının metni...",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 34),
            };
            promptInput.TextChanged += (newText) => slot.Prompt = newText;
            vbox.AddChild(promptInput);

            _slotsContainer.AddChild(panel);
        }
    }

    private void PlaySlotRange(double startSec, double endSec)
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null)
        {
            LoadVideo();
        }

        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        _videoPlayer.VolumeDb = 0.0f; // Enable audio for review
        _videoPlayer.Play();
        _videoPlayer.StreamPosition = startSec;
        _previewEndSec = endSec;
        _isPreviewingSlot = true;

        if (_statusLabel is not null) _statusLabel.Text = $"▶ Replik aralığı oynatılıyor: {startSec:F1}s - {endSec:F1}s";
    }

    public override void _Process(double delta)
    {
        if (_videoPlayer is not null && _videoPlayer.IsPlaying())
        {
            var pos = _videoPlayer.GetStreamPosition();
            if (_videoTimeLabel is not null)
            {
                _videoTimeLabel.Text = $"⏱ Süre: {pos:F1}s";
            }

            if (_isPreviewingSlot && pos >= _previewEndSec)
            {
                _videoPlayer.Stop();
                _isPreviewingSlot = false;
                if (_statusLabel is not null) _statusLabel.Text = "Önizleme tamamlandı.";
            }
        }
    }

    private void OnPlayVideoPressed()
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null)
        {
            LoadVideo();
        }

        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        if (_videoPlayer.IsPlaying())
        {
            _videoPlayer.Stop();
            _isPreviewingSlot = false;
            if (_playVideoButton is not null) _playVideoButton.Text = "▶ Videoyu Baştan Oynat";
        }
        else
        {
            _videoPlayer.VolumeDb = 0.0f;
            _videoPlayer.Play();
            _videoPlayer.StreamPosition = 0.0;
            _isPreviewingSlot = false;
            if (_playVideoButton is not null) _playVideoButton.Text = "⏹ Videoyu Durdur";
        }
    }

    private void OnAddSlotPressed()
    {
        var lastSlot = _editableSlots.LastOrDefault();
        var start = lastSlot is not null ? lastSlot.EndSeconds + 0.5 : 0.0;
        var end = start + 3.0;

        _editableSlots.Add(new EditableVoiceSlot
        {
            SlotId = $"slot-{_editableSlots.Count + 1}",
            CharacterId = "char-new",
            CharacterName = "Yeni Karakter",
            Prompt = "Yeni replik metni...",
            StartSeconds = start,
            EndSeconds = end
        });

        RebuildSlotsUi();
    }

    private void DeleteSlot(int index)
    {
        if (index >= 0 && index < _editableSlots.Count)
        {
            _editableSlots.RemoveAt(index);
            RebuildSlotsUi();
        }
    }

    private void OnSavePressed()
    {
        try
        {
            var title = string.IsNullOrWhiteSpace(_titleInput?.Text) ? "Düzenlenmiş Sahne" : _titleInput.Text.Trim();
            var durationMs = (long)((_durationInput?.Value ?? 10.0) * 1000);

            var distinctChars = _editableSlots
                .GroupBy(s => s.CharacterId)
                .Select(g => new CharacterDefinition
                {
                    CharacterId = g.Key,
                    DisplayName = g.First().CharacterName
                })
                .ToList();

            if (distinctChars.Count == 0)
            {
                distinctChars.Add(new CharacterDefinition { CharacterId = "default-char", DisplayName = "Oyuncu" });
            }

            var voiceSlots = new List<VoiceSlotDefinition>();
            var timelineEntries = new List<TimelineEntry>();

            for (int i = 0; i < _editableSlots.Count; i++)
            {
                var s = _editableSlots[i];
                var slotId = string.IsNullOrWhiteSpace(s.SlotId) ? $"slot-{i + 1}" : s.SlotId;

                voiceSlots.Add(new VoiceSlotDefinition
                {
                    VoiceSlotId = slotId,
                    CharacterId = s.CharacterId,
                    Prompt = s.Prompt
                });

                timelineEntries.Add(new TimelineEntry
                {
                    TimelineEntryId = $"entry-{i + 1}",
                    VoiceSlotId = slotId,
                    StartMilliseconds = (long)(s.StartSeconds * 1000),
                    EndMilliseconds = (long)(s.EndSeconds * 1000)
                });
            }

            var existingDoc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
            var sourceMedia = existingDoc?.SourceMedia ?? [new SourceMediaAsset { MediaId = "video-main", Role = SourceMediaRole.SceneVideo, RelativePath = "media/speed_homeless.ogv" }];
            var sceneId = existingDoc?.SceneId ?? $"custom-scene-{Guid.NewGuid():N}";

            var updatedDoc = new OfficialSceneDocument
            {
                SchemaVersion = 1,
                SceneId = sceneId,
                Title = title,
                DurationMilliseconds = durationMs,
                SourceMedia = sourceMedia,
                Characters = distinctChars,
                VoiceSlots = voiceSlots,
                Timeline = timelineEntries
            };

            // Serialize using engine-standard ProjectJsonSerializer
            var json = DubbedUp.Core.ProjectFormat.ProjectJsonSerializer.SerializeScene(updatedDoc);

            // Save back to JSON file on disk across all discovery paths
            var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
            if (!string.IsNullOrEmpty(folderPath) && System.IO.Directory.Exists(folderPath))
            {
                var jsonPath = System.IO.Path.Combine(folderPath, "scene.json");
                System.IO.File.WriteAllText(jsonPath, json);
                GD.Print($"[SceneEditor] Saved changes to package directory '{jsonPath}'");
            }

            // Sync with res://Content/OfficialScenes and repo scenes folder
            var resSceneJson = ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{sceneId}/scene.json");
            if (System.IO.File.Exists(resSceneJson))
            {
                System.IO.File.WriteAllText(resSceneJson, json);
                GD.Print($"[SceneEditor] Synced to official content '{resSceneJson}'");
            }

            var rootSceneJson = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "..", "scenes", sceneId, "scene.json"));
            if (System.IO.File.Exists(rootSceneJson))
            {
                System.IO.File.WriteAllText(rootSceneJson, json);
                GD.Print($"[SceneEditor] Synced to repo scenes folder '{rootSceneJson}'");
            }

            if (Coordinator is not null)
            {
                Coordinator.CurrentScene = updatedDoc;
                if (Coordinator.SelectedScenePackage is not null)
                {
                    Coordinator.SelectedScenePackage = new ScenePackage(
                        updatedDoc,
                        Coordinator.SelectedScenePackage.PackageDirectory,
                        Coordinator.SelectedScenePackage.VideoFilePath,
                        Coordinator.SelectedScenePackage.ThumbnailFilePath
                    );
                }
            }

            if (_statusLabel is not null)
            {
                _statusLabel.Text = "✅ Sahne başarıyla kaydedildi ve güncellendi!";
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SceneEditor] Save error: {ex.Message}");
            if (_statusLabel is not null) _statusLabel.Text = $"❌ Kaydetme hatası: {ex.Message}";
        }
    }

    private void OnBackPressed()
    {
        if (_videoPlayer is not null && _videoPlayer.IsPlaying())
        {
            _videoPlayer.Stop();
        }
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }
}

