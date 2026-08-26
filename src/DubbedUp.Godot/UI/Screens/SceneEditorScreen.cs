using System.Text.RegularExpressions;
using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.UI.Controls;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SceneEditorScreen : BaseScreen
{
    private LineEdit? _titleInput;
    private SpinBox? _durationInput;
    private VideoStreamPlayer? _videoPlayer;
    private Button? _seekBackButton;
    private Button? _playPauseButton;
    private Button? _seekForwardButton;
    private Button? _stopButton;
    private Label? _videoTimeLabel;
    private HSlider? _timeSlider;
    private PanelContainer? _timelineContainer;
    private Button? _addBoxButton;
    private Button? _deleteBoxButton;
    private VBoxContainer? _slotsContainer;
    private Label? _statusLabel;
    private Button? _saveAndProceedButton;
    private Button? _saveButton;
    private Button? _backButton;

    private TimelineWaveformEditor? _timelineEditor;
    private readonly List<EditableVoiceSlot> _editableSlots = [];
    private bool _isPreviewingSlot = false;
    private double _previewEndSec = 0.0;
    private bool _isSliderDragging = false;
    private bool _isPlaying = false;
    private double _totalDuration = 22.0;

    private float[]? _currentWaveform;
    private string? _currentWavPath;
    private double _maxSourceDuration = 0.0;

    private sealed class EditableVoiceSlot
    {
        public string SlotId { get; set; } = "slot-1";
        public string CharacterId { get; set; } = "char-1";
        public string CharacterName { get; set; } = "Character";
        public string Prompt { get; set; } = "Subtitle prompt";
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
        _seekBackButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/SeekBackButton");
        _playPauseButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/PlayPauseButton");
        _seekForwardButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/SeekForwardButton");
        _stopButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/StopButton");
        _videoTimeLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/VideoTimeLabel");
        _timeSlider = GetNodeOrNull<HSlider>("ScrollContainer/CenterContainer/VBoxContainer/TimeSlider");
        _timelineContainer = GetNodeOrNull<PanelContainer>("ScrollContainer/CenterContainer/VBoxContainer/TimelineContainer");
        _addBoxButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/TimelineButtons/AddBoxButton");
        _deleteBoxButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/TimelineButtons/DeleteBoxButton");
        _slotsContainer = GetNodeOrNull<VBoxContainer>("ScrollContainer/CenterContainer/VBoxContainer/SlotsContainer");
        _statusLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/StatusLabel");
        _saveAndProceedButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/SaveAndProceedButton");
        _saveButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/SaveButton");
        _backButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ActionsContainer/BackButton");

        // Create and add TimelineWaveformEditor
        if (_timelineContainer is not null)
        {
            _timelineEditor = new TimelineWaveformEditor();
            _timelineContainer.AddChild(_timelineEditor);
            _timelineEditor.SeekRequested += OnTimelineSeekRequested;
            _timelineEditor.SlotSelected += OnTimelineSlotSelected;
            _timelineEditor.SlotChanged += OnTimelineSlotChanged;
            _timelineEditor.SlotDeleteRequested += DeleteSlotAtIndex;
        }

        if (_playPauseButton is not null) _playPauseButton.Pressed += OnPlayPausePressed;
        if (_seekBackButton is not null) _seekBackButton.Pressed += () => SeekRelative(-5.0);
        if (_seekForwardButton is not null) _seekForwardButton.Pressed += () => SeekRelative(5.0);
        if (_stopButton is not null) _stopButton.Pressed += OnStopPressed;
        if (_addBoxButton is not null) _addBoxButton.Pressed += OnAddBoxPressed;
        if (_deleteBoxButton is not null) _deleteBoxButton.Pressed += OnDeleteSelectedBoxPressed;
        if (_saveAndProceedButton is not null) _saveAndProceedButton.Pressed += OnSaveAndProceedPressed;
        if (_saveButton is not null) _saveButton.Pressed += OnSavePressed;
        if (_backButton is not null) _backButton.Pressed += OnBackPressed;

        if (_durationInput is not null)
        {
            _durationInput.ValueChanged += OnDurationChanged;
        }

        if (_timeSlider is not null)
        {
            _timeSlider.DragStarted += () => _isSliderDragging = true;
            _timeSlider.DragEnded += _ =>
            {
                _isSliderDragging = false;
                if (_timeSlider is not null) SeekTo(_timeSlider.Value);
            };
            _timeSlider.ValueChanged += val =>
            {
                if (_isSliderDragging) SeekTo(val);
            };
        }

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
            if (_statusLabel is not null) _statusLabel.Text = "❌ No scene loaded to edit.";
            return;
        }

        if (_titleInput is not null) _titleInput.Text = doc.Title;

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

        LoadVideoAndAudioWaveform();
        RebuildSlotsUi();
        SyncTimelineData();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"💡 Scene loaded: '{doc.Title}' (Duration: {_totalDuration:F1}s, {_editableSlots.Count} speech slots)";
        }
    }

    private void LoadVideoAndAudioWaveform()
    {
        if (_videoPlayer is null) return;

        var doc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
        var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
        if (doc is null) return;

        var videoAsset = doc.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
        if (videoAsset is not null)
        {
            var relPath = videoAsset.RelativePath;
            if (!string.IsNullOrEmpty(folderPath))
            {
                var fullVideoPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(folderPath, relPath));
                if (System.IO.File.Exists(fullVideoPath))
                {
                    var resPath = ProjectSettings.LocalizePath(fullVideoPath);
                    if (!string.IsNullOrEmpty(resPath) && ResourceLoader.Exists(resPath))
                    {
                        _videoPlayer.Stream = GD.Load<VideoStream>(resPath);
                    }
                    else
                    {
                        var theora = new VideoStreamTheora { File = resPath ?? fullVideoPath.Replace("\\", "/") };
                        _videoPlayer.Stream = theora;
                    }
                }
            }

            if (_videoPlayer.Stream is null && ResourceLoader.Exists(relPath))
            {
                _videoPlayer.Stream = GD.Load<VideoStream>(relPath);
            }
        }

        // Exhaustive audio waveform search
        string? wavPath = null;
        var candidates = new List<string>();

        if (!string.IsNullOrEmpty(folderPath))
        {
            candidates.Add(System.IO.Path.Combine(folderPath, "media", "vocals.wav"));
            candidates.Add(System.IO.Path.Combine(folderPath, "media", "audio.wav"));
            candidates.Add(System.IO.Path.Combine(folderPath, "vocals.wav"));
            candidates.Add(System.IO.Path.Combine(folderPath, "audio.wav"));
            candidates.Add(System.IO.Path.Combine(folderPath, "media", "background.wav"));
        }

        if (doc is not null)
        {
            var id = doc.SceneId;
            var idUnderscore = id.Replace("-", "_");
            var idHyphen = id.Replace("_", "-");

            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{id}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idUnderscore}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idHyphen}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{id}/media/audio.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idUnderscore}/media/audio.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idHyphen}/media/audio.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{id}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{idUnderscore}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{id}/media/audio.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{idUnderscore}/media/audio.wav"));
        }

        foreach (var c in candidates)
        {
            if (System.IO.File.Exists(c))
            {
                wavPath = c;
                break;
            }
        }

        _currentWavPath = wavPath;
        if (wavPath is not null)
        {
            _maxSourceDuration = AudioWaveformLoader.GetAudioDurationSeconds(wavPath);
            GD.Print($"[SceneEditor] Audio source duration detected: {_maxSourceDuration:F2}s from '{wavPath}'");
        }

        var docDurationSec = (doc?.DurationMilliseconds ?? 22000) / 1000.0;
        if (_maxSourceDuration > 0)
        {
            _totalDuration = docDurationSec > 0 ? Math.Min(docDurationSec, _maxSourceDuration) : _maxSourceDuration;
            if (_durationInput is not null)
            {
                _durationInput.MaxValue = Math.Ceiling(_maxSourceDuration);
                _durationInput.MinValue = 1.0;
                _durationInput.Value = _totalDuration;
            }
        }
        else
        {
            _totalDuration = Math.Max(1.0, docDurationSec > 0 ? docDurationSec : 22.0);
            if (_durationInput is not null)
            {
                _durationInput.MaxValue = 600.0;
                _durationInput.MinValue = 1.0;
                _durationInput.Value = _totalDuration;
            }
        }

        if (_timeSlider is not null)
        {
            _timeSlider.MaxValue = _totalDuration;
            _timeSlider.Value = 0.0;
        }

        if (_currentWavPath is not null)
        {
            _currentWaveform = AudioWaveformLoader.ExtractWaveformSegment(_currentWavPath, 0.0, _totalDuration, 350);
            _timelineEditor?.SetWaveform(_currentWaveform);
            GD.Print($"[SceneEditor] Audio waveform loaded for duration [0 - {_totalDuration:F1}s]");
        }
        else
        {
            GD.PrintErr($"[SceneEditor] No audio file found for scene '{doc?.SceneId}'");
        }
    }

    private void OnDurationChanged(double newDuration)
    {
        _totalDuration = Math.Max(1.0, newDuration);
        if (_maxSourceDuration > 0 && _totalDuration > _maxSourceDuration)
        {
            _totalDuration = _maxSourceDuration;
        }

        if (_timeSlider is not null)
        {
            _timeSlider.MaxValue = _totalDuration;
            if (_timeSlider.Value > _totalDuration) _timeSlider.Value = _totalDuration;
        }

        // Re-extract waveform for new clamped time window [0.0 to _totalDuration]
        if (_currentWavPath is not null)
        {
            _currentWaveform = AudioWaveformLoader.ExtractWaveformSegment(_currentWavPath, 0.0, _totalDuration, 350);
        }

        // Clamp any slots that exceed the new duration
        bool slotChanged = false;
        foreach (var s in _editableSlots)
        {
            if (s.EndSeconds > _totalDuration)
            {
                s.EndSeconds = Math.Round(_totalDuration, 1);
                slotChanged = true;
            }
            if (s.StartSeconds >= s.EndSeconds)
            {
                s.StartSeconds = Math.Round(Math.Max(0.0, s.EndSeconds - 0.5), 1);
                slotChanged = true;
            }
        }

        if (slotChanged)
        {
            RebuildSlotsUi();
        }

        SyncTimelineData();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"⏱ Total duration set to {_totalDuration:F1}s. Waveform updated.";
        }
    }

    private void SyncTimelineData()
    {
        if (_timelineEditor is null) return;

        var timelineSlots = _editableSlots.Select(s => new TimelineWaveformEditor.TimelineSlotData
        {
            SlotId = s.SlotId,
            CharacterName = s.CharacterName,
            Prompt = s.Prompt,
            StartSeconds = s.StartSeconds,
            EndSeconds = s.EndSeconds,
        }).ToList();

        _timelineEditor.SetData(_totalDuration, timelineSlots, _currentWaveform);
    }

    private void OnTimelineSeekRequested(double targetSeconds)
    {
        SeekTo(targetSeconds);
    }

    private void OnTimelineSlotSelected(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _editableSlots.Count)
        {
            var slot = _editableSlots[slotIndex];
            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"👉 Selected Line #{slotIndex + 1}: {slot.CharacterName} ({slot.StartSeconds:F1}s - {slot.EndSeconds:F1}s)";
            }
        }
    }

    private void OnTimelineSlotChanged(int slotIndex)
    {
        RebuildSlotsUi();
    }

    private void OnPlayPausePressed()
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        if (_isPlaying)
        {
            PauseVideo();
        }
        else
        {
            PlayVideo();
        }
    }

    private void PlayVideo()
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        _isPreviewingSlot = false;
        _isPlaying = true;
        _videoPlayer.Paused = false;
        if (!_videoPlayer.IsPlaying())
        {
            _videoPlayer.Play();
        }

        if (_playPauseButton is not null) _playPauseButton.Text = "⏸ Pause";
    }

    private void PauseVideo()
    {
        if (_videoPlayer is null) return;

        _isPlaying = false;
        _videoPlayer.Paused = true;
        if (_playPauseButton is not null) _playPauseButton.Text = "▶ Play";
    }

    private void OnStopPressed()
    {
        SeekTo(0.0);
        PauseVideo();
    }

    private void SeekRelative(double deltaSec)
    {
        var current = _videoPlayer is not null && _videoPlayer.IsPlaying() ? _videoPlayer.GetStreamPosition() : (_timeSlider?.Value ?? 0.0);
        SeekTo(current + deltaSec);
    }

    private void SeekTo(double targetSec)
    {
        var clamped = Math.Clamp(targetSec, 0.0, _totalDuration);
        if (_videoPlayer is not null && _videoPlayer.Stream is not null)
        {
            _videoPlayer.StreamPosition = clamped;
        }

        if (_timeSlider is not null && !_isSliderDragging)
        {
            _timeSlider.Value = clamped;
        }

        if (_videoTimeLabel is not null)
        {
            _videoTimeLabel.Text = $"⏱ {clamped:F1}s / {_totalDuration:F1}s";
        }

        _timelineEditor?.SetPlayhead(clamped);
    }

    private void OnAddBoxPressed()
    {
        var currentPos = _videoPlayer is not null && _videoPlayer.IsPlaying() ? _videoPlayer.GetStreamPosition() : (_timeSlider?.Value ?? 0.0);
        var start = Math.Round(currentPos, 1);
        var end = Math.Round(Math.Min(_totalDuration, start + 3.0), 1);
        if (end <= start) end = Math.Min(_totalDuration, start + 1.0);

        var nextIdx = _editableSlots.Count + 1;
        var newSlot = new EditableVoiceSlot
        {
            SlotId = $"slot-{nextIdx}",
            CharacterId = $"char-{nextIdx}",
            CharacterName = $"Character {nextIdx}",
            Prompt = "Enter dialogue prompt here...",
            StartSeconds = start,
            EndSeconds = end
        };

        _editableSlots.Add(newSlot);
        RebuildSlotsUi();
        SyncTimelineData();
        _timelineEditor?.SelectSlot(_editableSlots.Count - 1);

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"✨ Added new speech slot at {start:F1}s!";
        }
    }

    private void OnDeleteSelectedBoxPressed()
    {
        var idx = _timelineEditor?.GetSelectedSlotIndex() ?? -1;
        if (idx >= 0 && idx < _editableSlots.Count)
        {
            DeleteSlotAtIndex(idx);
        }
        else if (_editableSlots.Count > 0)
        {
            DeleteSlotAtIndex(_editableSlots.Count - 1);
        }
    }

    private void DeleteSlotAtIndex(int index)
    {
        if (index >= 0 && index < _editableSlots.Count)
        {
            var charName = _editableSlots[index].CharacterName;
            _editableSlots.RemoveAt(index);
            RebuildSlotsUi();
            SyncTimelineData();
            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"🗑️ Deleted speech slot #{index + 1} ({charName}).";
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_videoPlayer is not null && _videoPlayer.Stream is not null && _videoPlayer.IsPlaying() && !_videoPlayer.Paused)
        {
            var pos = _videoPlayer.GetStreamPosition();
            _isPlaying = true;

            if (_timeSlider is not null && !_isSliderDragging)
            {
                _timeSlider.Value = pos;
            }

            if (_videoTimeLabel is not null)
            {
                _videoTimeLabel.Text = $"⏱ {pos:F1}s / {_totalDuration:F1}s";
            }

            _timelineEditor?.SetPlayhead(pos);

            if (_isPreviewingSlot && pos >= _previewEndSec)
            {
                PauseVideo();
                _isPreviewingSlot = false;
            }

            if (pos >= _totalDuration)
            {
                PauseVideo();
                SeekTo(0.0);
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
            var index = i;
            var slot = _editableSlots[i];

            var panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(820, 0)
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.15f, 0.20f, 0.95f),
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                BorderWidthBottom = 2,
                BorderWidthLeft = 4,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderColor = new Color(0.3f, 0.6f, 0.9f, 0.8f)
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var mainVBox = new VBoxContainer();
            mainVBox.AddThemeConstantOverride("separation", 6);
            panel.AddChild(mainVBox);

            // Row 1: Header (Number, Character, Start/End, Preview, Delete)
            var row1 = new HBoxContainer();
            row1.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(row1);

            var numLabel = new Label
            {
                Text = $"#{index + 1}",
                CustomMinimumSize = new Vector2(32, 0)
            };
            numLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
            row1.AddChild(numLabel);

            var charNameInput = new LineEdit
            {
                Text = slot.CharacterName,
                PlaceholderText = "Character Name...",
                CustomMinimumSize = new Vector2(160, 32)
            };
            charNameInput.TextChanged += text =>
            {
                slot.CharacterName = text;
                slot.CharacterId = ToKebabCaseId(text);
                SyncTimelineData();
            };
            row1.AddChild(charNameInput);

            var startLabel = new Label { Text = "Start:" };
            row1.AddChild(startLabel);

            var startInput = new SpinBox
            {
                MinValue = 0.0,
                MaxValue = 600.0,
                Step = 0.1,
                Value = slot.StartSeconds,
                CustomMinimumSize = new Vector2(80, 32)
            };
            startInput.ValueChanged += val =>
            {
                slot.StartSeconds = val;
                if (slot.EndSeconds <= slot.StartSeconds) slot.EndSeconds = slot.StartSeconds + 0.5;
                SyncTimelineData();
            };
            row1.AddChild(startInput);

            var endLabel = new Label { Text = "End:" };
            row1.AddChild(endLabel);

            var endInput = new SpinBox
            {
                MinValue = 0.1,
                MaxValue = 600.0,
                Step = 0.1,
                Value = slot.EndSeconds,
                CustomMinimumSize = new Vector2(80, 32)
            };
            endInput.ValueChanged += val =>
            {
                slot.EndSeconds = val;
                if (slot.EndSeconds <= slot.StartSeconds) slot.StartSeconds = Math.Max(0, slot.EndSeconds - 0.5);
                SyncTimelineData();
            };
            row1.AddChild(endInput);

            var previewBtn = new Button
            {
                Text = "🎧 Preview Line",
                CustomMinimumSize = new Vector2(120, 32)
            };
            previewBtn.Pressed += () => PreviewSlot(slot);
            row1.AddChild(previewBtn);

            var deleteBtn = new Button
            {
                Text = "🗑️ Delete",
                CustomMinimumSize = new Vector2(70, 32)
            };
            deleteBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.4f));
            deleteBtn.Pressed += () => DeleteSlotAtIndex(index);
            row1.AddChild(deleteBtn);

            // Row 2: Prompt / Subtitle Input
            var row2 = new HBoxContainer();
            row2.AddThemeConstantOverride("separation", 8);
            mainVBox.AddChild(row2);

            var promptLabel = new Label { Text = "💬 Subtitle / Prompt:" };
            row2.AddChild(promptLabel);

            var promptInput = new LineEdit
            {
                Text = slot.Prompt,
                PlaceholderText = "Enter character dialogue prompt / subtitle...",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 32)
            };
            promptInput.TextChanged += text =>
            {
                slot.Prompt = text;
                SyncTimelineData();
            };
            row2.AddChild(promptInput);

            _slotsContainer.AddChild(panel);
        }
    }

    private void PreviewSlot(EditableVoiceSlot slot)
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        _isPreviewingSlot = true;
        _previewEndSec = slot.EndSeconds;
        SeekTo(slot.StartSeconds);
        PlayVideo();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"🎧 Previewing: {slot.CharacterName} ({slot.StartSeconds:F1}s - {slot.EndSeconds:F1}s)";
        }
    }

    private static string ToKebabCaseId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "slot";

        var sanitized = input
            .Replace("ç", "c").Replace("Ç", "c")
            .Replace("ğ", "g").Replace("Ğ", "g")
            .Replace("ı", "i").Replace("I", "i").Replace("İ", "i")
            .Replace("ö", "o").Replace("Ö", "o")
            .Replace("ş", "s").Replace("Ş", "s")
            .Replace("ü", "u").Replace("Ü", "u");

        sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9\s-]", "");
        sanitized = Regex.Replace(sanitized, @"\s+", "-").Trim('-').ToLowerInvariant();
        sanitized = Regex.Replace(sanitized, @"-+", "-");

        return string.IsNullOrEmpty(sanitized) ? "slot" : sanitized;
    }

    private bool SaveSceneData()
    {
        var doc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
        if (doc is null)
        {
            if (_statusLabel is not null) _statusLabel.Text = "❌ No scene loaded to save.";
            return false;
        }

        if (_editableSlots.Count == 0)
        {
            if (_statusLabel is not null) _statusLabel.Text = "❌ At least 1 speech slot is required.";
            return false;
        }

        var newTitle = _titleInput?.Text?.Trim() ?? doc.Title;
        if (string.IsNullOrWhiteSpace(newTitle)) newTitle = doc.Title;

        var inputDurationMs = (int)((_durationInput?.Value ?? 22.0) * 1000);
        var maxSlotEndMs = (int)(_editableSlots.Max(s => s.EndSeconds) * 1000);
        var newDurationMs = Math.Max(inputDurationMs, maxSlotEndMs + 500);

        var newCharacters = new List<CharacterDefinition>();
        var newVoiceSlots = new List<VoiceSlotDefinition>();
        var newTimeline = new List<TimelineEntry>();

        var sortedSlots = _editableSlots.OrderBy(s => s.StartSeconds).ToList();
        var charMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sortedSlots.Count; i++)
        {
            var slot = sortedSlots[i];
            var charKey = string.IsNullOrWhiteSpace(slot.CharacterName) ? $"Character {i + 1}" : slot.CharacterName.Trim();
            if (!charMap.TryGetValue(charKey, out var charId))
            {
                charId = ToKebabCaseId(charKey);
                if (newCharacters.Any(c => c.CharacterId == charId)) charId = $"{charId}-{i + 1}";

                charMap[charKey] = charId;
                newCharacters.Add(new CharacterDefinition
                {
                    CharacterId = charId,
                    DisplayName = charKey
                });
            }

            var slotId = $"slot-{i + 1}-{charId}";
            var prompt = string.IsNullOrWhiteSpace(slot.Prompt) ? "Dialogue line" : slot.Prompt.Trim();

            newVoiceSlots.Add(new VoiceSlotDefinition
            {
                VoiceSlotId = slotId,
                CharacterId = charId,
                Prompt = prompt
            });

            var startMs = (int)(slot.StartSeconds * 1000);
            var endMs = (int)(slot.EndSeconds * 1000);
            if (endMs <= startMs) endMs = startMs + 1000;

            newTimeline.Add(new TimelineEntry
            {
                TimelineEntryId = $"entry-{i + 1}-{slotId}",
                VoiceSlotId = slotId,
                StartMilliseconds = startMs,
                EndMilliseconds = endMs
            });
        }

        var updatedDoc = new OfficialSceneDocument
        {
            SchemaVersion = doc.SchemaVersion,
            SceneId = doc.SceneId,
            Title = newTitle,
            DurationMilliseconds = newDurationMs,
            SourceMedia = doc.SourceMedia,
            Characters = newCharacters,
            VoiceSlots = newVoiceSlots,
            Timeline = newTimeline
        };

        try
        {
            ProjectValidator.Validate(updatedDoc);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SceneEditor] Validation failed: {ex.Message}");
            if (_statusLabel is not null) _statusLabel.Text = $"❌ Validation error: {ex.Message}";
            return false;
        }

        var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
        if (!string.IsNullOrEmpty(folderPath))
        {
            var sceneJsonPath = System.IO.Path.Combine(folderPath, "scene.json");
            var json = ProjectJsonSerializer.SerializeScene(updatedDoc);
            System.IO.File.WriteAllText(sceneJsonPath, json);
            GD.Print($"[SceneEditor] Saved updated scene to: {sceneJsonPath}");

            if (Coordinator is not null)
            {
                try
                {
                    Coordinator.SelectedScenePackage = ScenePackageLoader.LoadPackageFromDirectory(folderPath);
                    Coordinator.CurrentScene = Coordinator.SelectedScenePackage.Document;
                }
                catch
                {
                    Coordinator.CurrentScene = updatedDoc;
                }
            }
        }
        else if (Coordinator is not null)
        {
            Coordinator.CurrentScene = updatedDoc;
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = "✅ Scene, speech timeline, and subtitles saved successfully!";
        }

        return true;
    }

    private void OnSavePressed()
    {
        SaveSceneData();
    }

    private void OnSaveAndProceedPressed()
    {
        if (SaveSceneData())
        {
            PauseVideo();
            Navigator?.NavigateTo(AppScreen.ScenePicker);
        }
    }

    private void OnBackPressed()
    {
        PauseVideo();
        Navigator?.NavigateTo(AppScreen.Setup);
    }
}
