using System.Text.RegularExpressions;
using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using DubbedUp.Godot.LocalSession;
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
        public string SlotId { get; set; } = "slot-1";
        public string CharacterId { get; set; } = "char-1";
        public string CharacterName { get; set; } = "Karakter";
        public string Prompt { get; set; } = "Replik metni";
        public double StartSeconds { get; set; } = 0.0;
        public double EndSeconds { get; set; } = 4.0;
    }

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);
        LoadSceneData();
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

        if (Coordinator is not null)
        {
            LoadSceneData();
        }
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

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"🎬 '{doc.Title}' yüklendi ({_editableSlots.Count} replik). Değişiklik yapıp 'Değişiklikleri Kaydet'e basabilirsiniz.";
        }
    }

    private void LoadVideo()
    {
        if (_videoPlayer is null || Coordinator?.CurrentScene is null) return;

        var videoAsset = Coordinator.CurrentScene.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
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
                slot.CharacterName = string.IsNullOrWhiteSpace(newText) ? $"Karakter {slotIndex + 1}" : newText.Trim();
                slot.CharacterId = ToKebabCaseId(slot.CharacterName, $"char-{slotIndex + 1}");
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
            startSpin.ValueChanged += (newVal) =>
            {
                slot.StartSeconds = newVal;
                if (slot.EndSeconds <= slot.StartSeconds)
                {
                    slot.EndSeconds = slot.StartSeconds + 1.0;
                }
            };
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
            endSpin.ValueChanged += (newVal) =>
            {
                slot.EndSeconds = newVal;
                if (_durationInput is not null && slot.EndSeconds > _durationInput.Value)
                {
                    _durationInput.Value = Math.Ceiling(slot.EndSeconds + 1.0);
                }
            };
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
            promptInput.TextChanged += (newText) => slot.Prompt = string.IsNullOrWhiteSpace(newText) ? "Replik metni..." : newText.Trim();
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

        _videoPlayer.Bus = "Master";
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
            _videoPlayer.Bus = "Master";
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
        var num = _editableSlots.Count + 1;

        _editableSlots.Add(new EditableVoiceSlot
        {
            SlotId = $"slot-{num}",
            CharacterId = $"char-{num}",
            CharacterName = $"Karakter {num}",
            Prompt = "Yeni replik metni...",
            StartSeconds = start,
            EndSeconds = end
        });

        if (_durationInput is not null && end > _durationInput.Value)
        {
            _durationInput.Value = Math.Ceiling(end + 1.0);
        }

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

    private static string ToKebabCaseId(string input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;

        // Convert Turkish special characters to ASCII equivalents for strict kebab-case ID compliance
        var s = input.ToLowerInvariant()
            .Replace("ç", "c")
            .Replace("ğ", "g")
            .Replace("ı", "i")
            .Replace("ö", "o")
            .Replace("ş", "s")
            .Replace("ü", "u");

        // Keep only alphanumeric characters and hyphens
        var clean = Regex.Replace(s, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(clean) ? fallback : clean;
    }

    private void OnSavePressed()
    {
        try
        {
            var title = string.IsNullOrWhiteSpace(_titleInput?.Text) ? "Düzenlenmiş Sahne" : _titleInput.Text.Trim();

            // Ensure at least 1 slot
            if (_editableSlots.Count == 0)
            {
                _editableSlots.Add(new EditableVoiceSlot
                {
                    SlotId = "slot-1",
                    CharacterId = "char-1",
                    CharacterName = "Oyuncu",
                    Prompt = "Replik metni...",
                    StartSeconds = 0.0,
                    EndSeconds = 4.0
                });
            }

            // Calculate max slot end time and ensure total duration is strictly larger
            var maxSlotEndMs = _editableSlots.Max(s => (long)(Math.Max(s.StartSeconds + 0.5, s.EndSeconds) * 1000));
            var inputDurationMs = (long)((_durationInput?.Value ?? 10.0) * 1000);
            var durationMs = Math.Max(inputDurationMs, maxSlotEndMs + 500);

            var existingDoc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
            var sceneId = existingDoc is not null ? ToKebabCaseId(existingDoc.SceneId, "custom-scene") : "custom-scene";

            var distinctChars = new List<CharacterDefinition>();
            var charMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _editableSlots.Count; i++)
            {
                var s = _editableSlots[i];
                var rawCharName = string.IsNullOrWhiteSpace(s.CharacterName) ? $"Karakter {i + 1}" : s.CharacterName.Trim();
                var charId = ToKebabCaseId(rawCharName, $"char-{i + 1}");

                if (!charMap.ContainsKey(charId))
                {
                    charMap[charId] = rawCharName;
                    distinctChars.Add(new CharacterDefinition
                    {
                        CharacterId = charId,
                        DisplayName = rawCharName
                    });
                }
                s.CharacterId = charId;
            }

            var voiceSlots = new List<VoiceSlotDefinition>();
            var timelineEntries = new List<TimelineEntry>();

            for (int i = 0; i < _editableSlots.Count; i++)
            {
                var s = _editableSlots[i];
                var slotId = $"slot-{i + 1}";
                var startMs = (long)(Math.Max(0.0, s.StartSeconds) * 1000);
                var endMs = (long)(Math.Max(s.StartSeconds + 0.5, s.EndSeconds) * 1000);
                var promptText = string.IsNullOrWhiteSpace(s.Prompt) ? "Replik metni..." : s.Prompt.Trim();

                voiceSlots.Add(new VoiceSlotDefinition
                {
                    VoiceSlotId = slotId,
                    CharacterId = s.CharacterId,
                    Prompt = promptText
                });

                timelineEntries.Add(new TimelineEntry
                {
                    TimelineEntryId = $"entry-{i + 1}",
                    VoiceSlotId = slotId,
                    StartMilliseconds = startMs,
                    EndMilliseconds = endMs
                });
            }

            var sourceMedia = existingDoc?.SourceMedia;
            if (sourceMedia is null || sourceMedia.Count == 0 || sourceMedia.All(m => m.Role != SourceMediaRole.SceneVideo))
            {
                sourceMedia = [new SourceMediaAsset { MediaId = "video-main", Role = SourceMediaRole.SceneVideo, RelativePath = "media/speed_homeless.ogv" }];
            }

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

            // Validate and serialize using ProjectJsonSerializer
            var json = ProjectJsonSerializer.SerializeScene(updatedDoc);

            // 1. Save to package directory
            var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
            if (!string.IsNullOrEmpty(folderPath) && System.IO.Directory.Exists(folderPath))
            {
                var jsonPath = System.IO.Path.Combine(folderPath, "scene.json");
                System.IO.File.WriteAllText(jsonPath, json);
                GD.Print($"[SceneEditor] Saved changes to package directory '{jsonPath}'");
            }

            // 2. Sync to res://Content/OfficialScenes/<sceneId>/scene.json
            var resFolder = ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{sceneId}");
            if (System.IO.Directory.Exists(resFolder))
            {
                var resSceneJson = System.IO.Path.Combine(resFolder, "scene.json");
                System.IO.File.WriteAllText(resSceneJson, json);
                GD.Print($"[SceneEditor] Synced to official content '{resSceneJson}'");
            }

            // 3. Sync to root scenes/<sceneId>/scene.json
            var rootFolder = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "..", "scenes", sceneId));
            if (System.IO.Directory.Exists(rootFolder))
            {
                var rootSceneJson = System.IO.Path.Combine(rootFolder, "scene.json");
                System.IO.File.WriteAllText(rootSceneJson, json);
                GD.Print($"[SceneEditor] Synced to repo scenes folder '{rootSceneJson}'");
            }

            // Update Coordinator state
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
                _statusLabel.Text = "✅ Sahne başarıyla doğrulandı ve kaydedildi!";
            }
        }
        catch (ProjectValidationException ex)
        {
            var errStr = string.Join("; ", ex.Errors);
            GD.PrintErr($"[SceneEditor] Validation error: {errStr}");
            if (_statusLabel is not null) _statusLabel.Text = $"❌ Sahne kuralları hatası: {errStr}";
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
