using System.Text.RegularExpressions;
using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using DubbedUp.Godot.Ai;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.UI.Controls;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SceneEditorScreen : BaseScreen
{
    private LineEdit? _titleInput;
    private SpinBox? _durationInput;
    private AspectRatioContainer? _videoContainer;
    private VideoStreamPlayer? _videoPlayer;
    private Button? _seekBackButton;
    private Button? _playPauseButton;
    private Button? _seekForwardButton;
    private Button? _stopButton;
    private Label? _videoTimeLabel;
    private HSlider? _timeSlider;
    private PanelContainer? _timelineContainer;
    private Button? _autoDetectButton;
    private Button? _addBoxButton;
    private Button? _cutBoxButton;
    private Button? _deleteBoxButton;
    private VBoxContainer? _slotsContainer;
    private Label? _statusLabel;
    private Button? _saveAndProceedButton;
    private Button? _saveButton;
    private Button? _backButton;

    private TimelineWaveformEditor? _timelineEditor;
    private readonly List<EditableVoiceSlot> _editableSlots = [];
    private int _selectedSlotIndex = -1;
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
        _videoContainer = GetNodeOrNull<AspectRatioContainer>("ScrollContainer/CenterContainer/VBoxContainer/VideoContainer");
        _videoPlayer = GetNodeOrNull<VideoStreamPlayer>("ScrollContainer/CenterContainer/VBoxContainer/VideoContainer/VideoPanel/VideoPlayer") ?? GetNodeOrNull<VideoStreamPlayer>("ScrollContainer/CenterContainer/VBoxContainer/VideoPanel/VideoPlayer");
        _seekBackButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/SeekBackButton");
        _playPauseButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/PlayPauseButton");
        _seekForwardButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/SeekForwardButton");
        _stopButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/StopButton");
        _videoTimeLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/VideoControls/VideoTimeLabel");
        _timeSlider = GetNodeOrNull<HSlider>("ScrollContainer/CenterContainer/VBoxContainer/TimeSlider");
        _timelineContainer = GetNodeOrNull<PanelContainer>("ScrollContainer/CenterContainer/VBoxContainer/TimelineContainer");
        _autoDetectButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/TimelineButtons/AutoDetectButton");
        _addBoxButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/TimelineButtons/AddBoxButton");
        _cutBoxButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/TimelineButtons/CutBoxButton");
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
            _timelineEditor.SlotSplitRequested += SplitSlotAtIndex;
        }

        // Video Controls
        if (_seekBackButton is not null)
        {
            _seekBackButton.Text = "-5s";
            _seekBackButton.Pressed += () => SeekRelative(-5.0);
            StyleOutlinePill(_seekBackButton, new Color(0.280f, 0.650f, 0.950f), 18);
            UiSoundManager.Attach(_seekBackButton);
        }
        if (_playPauseButton is not null)
        {
            _playPauseButton.Text = "Play";
            _playPauseButton.Pressed += OnPlayPausePressed;
            StyleActionPill(_playPauseButton, new Color(0.280f, 0.650f, 0.950f), 18);
            UiSoundManager.Attach(_playPauseButton);
        }
        if (_seekForwardButton is not null)
        {
            _seekForwardButton.Text = "+5s";
            _seekForwardButton.Pressed += () => SeekRelative(5.0);
            StyleOutlinePill(_seekForwardButton, new Color(0.280f, 0.650f, 0.950f), 18);
            UiSoundManager.Attach(_seekForwardButton);
        }
        if (_stopButton is not null)
        {
            _stopButton.Text = "Reset";
            _stopButton.Pressed += OnStopPressed;
            StyleOutlinePill(_stopButton, new Color(0.450f, 0.500f, 0.650f), 18);
            UiSoundManager.Attach(_stopButton);
        }

        // Timeline box buttons
        if (_autoDetectButton is not null)
        {
            _autoDetectButton.Text = "Auto-Detect Lines";
            _autoDetectButton.Pressed += OnAutoDetectPressed;
            StyleActionPill(_autoDetectButton, new Color(0.600f, 0.480f, 0.950f), 20);
            UiSoundManager.Attach(_autoDetectButton);
        }
        if (_addBoxButton is not null)
        {
            _addBoxButton.Text = "Add Box";
            _addBoxButton.Pressed += OnAddBoxPressed;
            StyleActionPill(_addBoxButton, new Color(0.20f, 0.72f, 0.50f), 20);
            UiSoundManager.Attach(_addBoxButton);
        }
        if (_cutBoxButton is not null)
        {
            _cutBoxButton.Text = "Split (C)";
            _cutBoxButton.Pressed += OnCutSelectedBoxPressed;
            StyleActionPill(_cutBoxButton, new Color(1.0f, 0.650f, 0.250f), 20);
            UiSoundManager.Attach(_cutBoxButton);
        }
        if (_deleteBoxButton is not null)
        {
            _deleteBoxButton.Text = "Delete";
            _deleteBoxButton.Pressed += OnDeleteSelectedBoxPressed;
            StyleOutlinePill(_deleteBoxButton, new Color(0.95f, 0.35f, 0.35f), 20);
            UiSoundManager.Attach(_deleteBoxButton);
        }

        // Bottom Screen Actions
        if (_saveAndProceedButton is not null)
        {
            _saveAndProceedButton.Text = "Save & Exit";
            _saveAndProceedButton.Pressed += OnSaveAndProceedPressed;
            StyleActionPill(_saveAndProceedButton, new Color(1.0f, 0.540f, 0.680f), 22);
            UiSoundManager.Attach(_saveAndProceedButton);
        }
        if (_saveButton is not null)
        {
            _saveButton.Text = "Save";
            _saveButton.Pressed += OnSavePressed;
            StyleActionPill(_saveButton, new Color(0.280f, 0.650f, 0.950f), 22);
            UiSoundManager.Attach(_saveButton);
        }
        if (_backButton is not null)
        {
            _backButton.Text = "Back";
            _backButton.Pressed += OnBackPressed;
            StyleOutlinePill(_backButton, new Color(0.350f, 0.400f, 0.550f), 20);
            UiSoundManager.Attach(_backButton);
        }

        UiIcons.Apply(_playPauseButton, "play");
        UiIcons.Apply(_stopButton, "rotate-ccw");
        UiIcons.Apply(_autoDetectButton, "audio-waveform");
        UiIcons.Apply(_addBoxButton, "plus");
        UiIcons.Apply(_cutBoxButton, "scissors");
        UiIcons.Apply(_deleteBoxButton, "trash-2");
        UiIcons.Apply(_saveAndProceedButton, "check");
        UiIcons.Apply(_saveButton, "save");
        UiIcons.Apply(_backButton, "arrow-left");

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
            if (_statusLabel is not null) _statusLabel.Text = "No scene loaded.";
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
            _statusLabel.Text = string.Empty;
        }
    }

    private void LoadVideoAndAudioWaveform()
    {
        if (_videoPlayer is null) return;

        var doc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
        var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
        if (doc is null) return;

        var videoAsset = doc.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
        var relPath = videoAsset?.RelativePath;

        _videoPlayer.Stream = VideoPlayback.MediaTranscoder.LoadVideoStream(folderPath, relPath);

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

        if (wavPath is null && !string.IsNullOrEmpty(folderPath))
        {
            wavPath = VideoPlayback.MediaTranscoder.EnsureAudioExtracted(folderPath);
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
            _statusLabel.Text = string.Empty;
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
        _selectedSlotIndex = slotIndex;
        UpdateSlotCardsHighlight();
        if (slotIndex >= 0 && slotIndex < _editableSlots.Count)
        {
            var slot = _editableSlots[slotIndex];
            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"#{slotIndex + 1} {slot.CharacterName} · {slot.StartSeconds:F1}–{slot.EndSeconds:F1}s";
            }
        }
    }

    private void SelectSlotIndex(int index)
    {
        _selectedSlotIndex = (index >= 0 && index < _editableSlots.Count) ? index : -1;
        _timelineEditor?.SelectSlot(_selectedSlotIndex);
        UpdateSlotCardsHighlight();
    }

    private void OnTimelineSlotChanged(int slotIndex)
    {
        if (_timelineEditor is not null && slotIndex >= 0 && slotIndex < _editableSlots.Count)
        {
            var editorSlot = _timelineEditor.GetSlot(slotIndex);
            if (editorSlot is not null)
            {
                _editableSlots[slotIndex].StartSeconds = editorSlot.StartSeconds;
                _editableSlots[slotIndex].EndSeconds = editorSlot.EndSeconds;
            }
        }
        _selectedSlotIndex = slotIndex;
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

        if (_playPauseButton is not null) _playPauseButton.Text = "Pause";
        UiIcons.Swap(_playPauseButton, "pause");
    }

    private void PauseVideo()
    {
        if (_videoPlayer is null) return;

        _isPlaying = false;
        _videoPlayer.Paused = true;
        if (_playPauseButton is not null) _playPauseButton.Text = "Play";
        UiIcons.Swap(_playPauseButton, "play");
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
            _videoTimeLabel.Text = $"{clamped:F1}s / {_totalDuration:F1}s";
        }

        _timelineEditor?.SetPlayhead(clamped);
    }

    private void OnAutoDetectPressed()
    {
        if (string.IsNullOrEmpty(_currentWavPath) || !System.IO.File.Exists(_currentWavPath))
        {
            var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
            if (!string.IsNullOrEmpty(folderPath))
            {
                _currentWavPath = VideoPlayback.MediaTranscoder.EnsureAudioExtracted(folderPath);
            }
        }

        if (string.IsNullOrEmpty(_currentWavPath) || !System.IO.File.Exists(_currentWavPath))
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "No audio found.";
            }
            return;
        }

        // Prioritize isolated vocals.wav stem if available for clean speech recognition
        var packageDir = Coordinator?.SelectedScenePackage?.PackageDirectory;
        if (!string.IsNullOrEmpty(packageDir))
        {
            var vocalsWav = System.IO.Path.Combine(packageDir, "media", "vocals.wav");
            if (System.IO.File.Exists(vocalsWav))
            {
                _currentWavPath = vocalsWav;
            }
        }

        var detected = LocalAiSceneExtractor.DetectSpeechFromWavFile(_currentWavPath, maxSlots: 30);
        if (detected.Count == 0)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "No speech detected.";
            }
            return;
        }

        _editableSlots.Clear();
        for (int i = 0; i < detected.Count; i++)
        {
            var seg = detected[i];
            _editableSlots.Add(new EditableVoiceSlot
            {
                SlotId = $"slot-{i + 1}",
                CharacterId = seg.CharacterId,
                CharacterName = seg.SpeakerDisplayName,
                Prompt = seg.Prompt,
                StartSeconds = Math.Round(seg.StartMilliseconds / 1000.0, 2),
                EndSeconds = Math.Round(seg.EndMilliseconds / 1000.0, 2)
            });
        }

        RebuildSlotsUi();
        SyncTimelineData();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"{detected.Count} lines detected";
        }
    }

    private void OnCutSelectedBoxPressed()
    {
        var selectedIdx = _timelineEditor?.GetSelectedSlotIndex() ?? -1;
        if (selectedIdx < 0 || selectedIdx >= _editableSlots.Count)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "Select a box to split.";
            }
            return;
        }

        var currentPos = _videoPlayer is not null && _videoPlayer.IsPlaying() ? _videoPlayer.GetStreamPosition() : (_timeSlider?.Value ?? 0.0);
        SplitSlotAtIndex(selectedIdx, currentPos);
    }

    private void SplitSlotAtIndex(int slotIndex, double splitSeconds)
    {
        if (slotIndex < 0 || slotIndex >= _editableSlots.Count) return;

        var slot = _editableSlots[slotIndex];
        var splitSec = Math.Round(splitSeconds, 2);

        if (splitSec <= slot.StartSeconds + 0.3 || splitSec >= slot.EndSeconds - 0.3)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "Move the playhead inside the box to split it.";
            }
            return;
        }

        var oldEnd = slot.EndSeconds;
        slot.EndSeconds = splitSec;

        var nextIdx = _editableSlots.Count + 1;
        var newSlot = new EditableVoiceSlot
        {
            SlotId = $"slot-{nextIdx}",
            CharacterId = slot.CharacterId,
            CharacterName = slot.CharacterName,
            Prompt = slot.Prompt,
            StartSeconds = splitSec,
            EndSeconds = oldEnd
        };

        _editableSlots.Insert(slotIndex + 1, newSlot);
        _selectedSlotIndex = slotIndex + 1;
        RebuildSlotsUi();
        SyncTimelineData();
        _timelineEditor?.SelectSlot(slotIndex + 1);

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"Split at {splitSec:F1}s";
        }
    }

    private void OnAddBoxPressed()
    {
        var currentPos = _videoPlayer is not null && _videoPlayer.IsPlaying() ? _videoPlayer.GetStreamPosition() : (_timeSlider?.Value ?? 0.0);
        var start = Math.Round(currentPos, 1);
        var end = Math.Round(Math.Min(_totalDuration, start + 3.0), 1);
        if (end <= start) end = Math.Min(_totalDuration, start + 1.0);

        var nextIdx = _editableSlots.Count + 1;
        var speakerNum = ((_editableSlots.Count) % 2) + 1;
        var newSlot = new EditableVoiceSlot
        {
            SlotId = $"slot-{nextIdx}",
            CharacterId = $"char-{speakerNum}",
            CharacterName = $"Character {speakerNum}",
            Prompt = $"Line {nextIdx} dialogue prompt",
            StartSeconds = start,
            EndSeconds = end
        };

        _editableSlots.Add(newSlot);
        _selectedSlotIndex = _editableSlots.Count - 1;
        RebuildSlotsUi();
        SyncTimelineData();
        _timelineEditor?.SelectSlot(_editableSlots.Count - 1);

        if (_statusLabel is not null)
        {
            _statusLabel.Text = string.Empty;
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
            _selectedSlotIndex = Math.Min(index, _editableSlots.Count - 1);
            RebuildSlotsUi();
            SyncTimelineData();
            _timelineEditor?.SelectSlot(_selectedSlotIndex);
            if (_statusLabel is not null)
            {
                _statusLabel.Text = string.Empty;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_videoPlayer is not null && _videoContainer is not null)
        {
            var tex = _videoPlayer.GetVideoTexture();
            if (tex is not null && tex.GetHeight() > 0)
            {
                float r = (float)tex.GetWidth() / tex.GetHeight();
                if (Math.Abs(_videoContainer.Ratio - r) > 0.01f)
                {
                    _videoContainer.Ratio = r;
                }
            }
        }

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
                _videoTimeLabel.Text = $"{pos:F1}s / {_totalDuration:F1}s";
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

        if (_editableSlots.Count == 0)
        {
            var emptyPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(840, 70)
            };
            var emptyStyle = new StyleBoxFlat
            {
                BgColor = Colors.White,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.886f, 0.902f, 0.941f),
                CornerRadiusTopLeft = 14,
                CornerRadiusTopRight = 14,
                CornerRadiusBottomLeft = 14,
                CornerRadiusBottomRight = 14
            };
            emptyPanel.AddThemeStyleboxOverride("panel", emptyStyle);

            var emptyLabel = new Label
            {
                Text = "No lines yet.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.50f, 0.65f));
            emptyLabel.AddThemeFontSizeOverride("font_size", 14);
            emptyPanel.AddChild(emptyLabel);
            _slotsContainer.AddChild(emptyPanel);
            return;
        }

        for (int i = 0; i < _editableSlots.Count; i++)
        {
            var index = i;
            var slot = _editableSlots[i];
            var slotColor = TimelineWaveformEditor.GetPresetColor(index);
            bool isSelected = (index == _selectedSlotIndex);

            var panel = new PanelContainer
            {
                Name = $"SlotCard_{index}",
                CustomMinimumSize = new Vector2(840, 0),
                MouseFilter = Control.MouseFilterEnum.Pass
            };

            var cardStyle = new StyleBoxFlat
            {
                BgColor = Colors.White,
                CornerRadiusTopLeft = 14,
                CornerRadiusTopRight = 14,
                CornerRadiusBottomLeft = 14,
                CornerRadiusBottomRight = 14,
                BorderWidthLeft = isSelected ? 8 : 6,
                BorderWidthTop = isSelected ? 2 : 1,
                BorderWidthRight = isSelected ? 2 : 1,
                BorderWidthBottom = isSelected ? 2 : 1,
                BorderColor = isSelected ? slotColor : new Color(0.886f, 0.902f, 0.941f),
                ShadowColor = isSelected
                    ? new Color(slotColor.R, slotColor.G, slotColor.B, 0.28f)
                    : new Color(0.12f, 0.18f, 0.35f, 0.08f),
                ShadowSize = isSelected ? 12 : 6,
                ShadowOffset = new Vector2(0, 3)
            };
            panel.AddThemeStyleboxOverride("panel", cardStyle);

            panel.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                {
                    SelectSlotIndex(index);
                }
            };

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 18);
            margin.AddThemeConstantOverride("margin_right", 18);
            margin.AddThemeConstantOverride("margin_top", 14);
            margin.AddThemeConstantOverride("margin_bottom", 14);
            panel.AddChild(margin);

            var mainVBox = new VBoxContainer();
            mainVBox.AddThemeConstantOverride("separation", 10);
            margin.AddChild(mainVBox);

            // Row 1: Header / Controls Row
            var row1 = new HBoxContainer();
            row1.AddThemeConstantOverride("separation", 12);
            mainVBox.AddChild(row1);

            // 1. Number Badge Pill
            var badgePanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(42, 32),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            var badgeStyle = new StyleBoxFlat
            {
                BgColor = slotColor,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            };
            badgePanel.AddThemeStyleboxOverride("panel", badgeStyle);

            var numLabel = new Label
            {
                Text = $"#{index + 1}",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            numLabel.AddThemeColorOverride("font_color", Colors.White);
            numLabel.AddThemeFontSizeOverride("font_size", 13);
            badgePanel.AddChild(numLabel);
            row1.AddChild(badgePanel);

            // 2. Character Name
            var charLabel = new Label
            {
                Text = "Character",
                VerticalAlignment = VerticalAlignment.Center
            };
            charLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.40f));
            charLabel.AddThemeFontSizeOverride("font_size", 13);
            row1.AddChild(charLabel);

            var charNameInput = new LineEdit
            {
                Text = slot.CharacterName,
                PlaceholderText = "Name",
                CustomMinimumSize = new Vector2(170, 34),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            StyleCardInput(charNameInput);
            charNameInput.FocusEntered += () => SelectSlotIndex(index);
            charNameInput.TextChanged += text =>
            {
                slot.CharacterName = text;
                slot.CharacterId = ToKebabCaseId(text);
                SyncTimelineData();
            };
            row1.AddChild(charNameInput);

            // 3. Timing Section
            var startLabel = new Label
            {
                Text = "Start",
                VerticalAlignment = VerticalAlignment.Center
            };
            startLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.40f));
            startLabel.AddThemeFontSizeOverride("font_size", 13);
            row1.AddChild(startLabel);

            SpinBox? endInput = null;

            var startInput = new SpinBox
            {
                MinValue = 0.0,
                MaxValue = 600.0,
                Step = 0.1,
                Value = slot.StartSeconds,
                CustomMinimumSize = new Vector2(85, 34),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                Suffix = "s"
            };
            StyleCardInput(startInput.GetLineEdit());
            startInput.GetLineEdit().Alignment = HorizontalAlignment.Center;
            startInput.FocusEntered += () => SelectSlotIndex(index);
            row1.AddChild(startInput);

            var endLabel = new Label
            {
                Text = "End",
                VerticalAlignment = VerticalAlignment.Center
            };
            endLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.40f));
            endLabel.AddThemeFontSizeOverride("font_size", 13);
            row1.AddChild(endLabel);

            endInput = new SpinBox
            {
                MinValue = 0.1,
                MaxValue = 600.0,
                Step = 0.1,
                Value = slot.EndSeconds,
                CustomMinimumSize = new Vector2(85, 34),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                Suffix = "s"
            };
            StyleCardInput(endInput.GetLineEdit());
            endInput.GetLineEdit().Alignment = HorizontalAlignment.Center;
            endInput.FocusEntered += () => SelectSlotIndex(index);
            row1.AddChild(endInput);

            var durationChip = new Label
            {
                Name = "DurationChip",
                Text = $"({Math.Max(0, slot.EndSeconds - slot.StartSeconds):F1}s)",
                VerticalAlignment = VerticalAlignment.Center
            };
            durationChip.AddThemeColorOverride("font_color", new Color(0.48f, 0.52f, 0.64f));
            durationChip.AddThemeFontSizeOverride("font_size", 12);
            row1.AddChild(durationChip);

            startInput.ValueChanged += val =>
            {
                slot.StartSeconds = Math.Round(val, 1);
                if (slot.EndSeconds <= slot.StartSeconds)
                {
                    slot.EndSeconds = Math.Round(slot.StartSeconds + 0.5, 1);
                    if (endInput is not null) endInput.Value = slot.EndSeconds;
                }
                SyncTimelineData();
                durationChip.Text = $"({Math.Max(0, slot.EndSeconds - slot.StartSeconds):F1}s)";
            };

            endInput.ValueChanged += val =>
            {
                slot.EndSeconds = Math.Round(val, 1);
                if (slot.EndSeconds <= slot.StartSeconds)
                {
                    slot.StartSeconds = Math.Round(Math.Max(0, slot.EndSeconds - 0.5), 1);
                    startInput.Value = slot.StartSeconds;
                }
                SyncTimelineData();
                durationChip.Text = $"({Math.Max(0, slot.EndSeconds - slot.StartSeconds):F1}s)";
            };

            // Spacer
            var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row1.AddChild(spacer);

            // 4. Preview Button
            var previewBtn = new Button
            {
                Text = "Preview",
                CustomMinimumSize = new Vector2(105, 34),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            StyleActionPill(previewBtn, new Color(0.280f, 0.650f, 0.950f), 10);
            UiSoundManager.Attach(previewBtn);
            previewBtn.Pressed += () =>
            {
                SelectSlotIndex(index);
                PreviewSlot(slot);
            };
            row1.AddChild(previewBtn);

            // 5. Delete Button
            var deleteBtn = new Button
            {
                Text = "Delete",
                CustomMinimumSize = new Vector2(88, 34),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            StyleOutlinePill(deleteBtn, new Color(0.95f, 0.35f, 0.35f), 10);
            UiSoundManager.Attach(deleteBtn);
            deleteBtn.Pressed += () => DeleteSlotAtIndex(index);
            row1.AddChild(deleteBtn);

            // Row 2: Prompt / Subtitle
            var row2 = new HBoxContainer();
            row2.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(row2);

            var promptLabel = new Label
            {
                Text = "Line",
                VerticalAlignment = VerticalAlignment.Center
            };
            promptLabel.AddThemeColorOverride("font_color", new Color(0.118f, 0.106f, 0.294f));
            promptLabel.AddThemeFontSizeOverride("font_size", 13);
            row2.AddChild(promptLabel);

            var promptInput = new LineEdit
            {
                Text = slot.Prompt,
                PlaceholderText = "What the character says",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 36)
            };
            StyleCardInput(promptInput);
            promptInput.FocusEntered += () => SelectSlotIndex(index);
            promptInput.TextChanged += text =>
            {
                slot.Prompt = text;
                SyncTimelineData();
            };
            row2.AddChild(promptInput);

            _slotsContainer.AddChild(panel);
        }
    }

    private void UpdateSlotCardsHighlight()
    {
        if (_slotsContainer is null) return;

        var children = _slotsContainer.GetChildren();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is PanelContainer panel)
            {
                bool isSelected = (i == _selectedSlotIndex);
                var slotColor = TimelineWaveformEditor.GetPresetColor(i);

                var cardStyle = new StyleBoxFlat
                {
                    BgColor = Colors.White,
                    CornerRadiusTopLeft = 14,
                    CornerRadiusTopRight = 14,
                    CornerRadiusBottomLeft = 14,
                    CornerRadiusBottomRight = 14,
                    BorderWidthLeft = isSelected ? 8 : 6,
                    BorderWidthTop = isSelected ? 2 : 1,
                    BorderWidthRight = isSelected ? 2 : 1,
                    BorderWidthBottom = isSelected ? 2 : 1,
                    BorderColor = isSelected ? slotColor : new Color(0.886f, 0.902f, 0.941f),
                    ShadowColor = isSelected
                        ? new Color(slotColor.R, slotColor.G, slotColor.B, 0.28f)
                        : new Color(0.12f, 0.18f, 0.35f, 0.08f),
                    ShadowSize = isSelected ? 12 : 6,
                    ShadowOffset = new Vector2(0, 3)
                };
                panel.AddThemeStyleboxOverride("panel", cardStyle);
            }
        }
    }

    private static void StyleCardInput(LineEdit input)
    {
        if (input is null) return;

        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.975f, 0.982f, 0.995f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.850f, 0.880f, 0.925f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };

        var focus = new StyleBoxFlat
        {
            BgColor = Colors.White,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.280f, 0.650f, 0.950f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };

        input.AddThemeStyleboxOverride("normal", normal);
        input.AddThemeStyleboxOverride("focus", focus);
        input.AddThemeColorOverride("font_color", new Color(0.118f, 0.106f, 0.294f));
        input.AddThemeColorOverride("font_placeholder_color", new Color(0.550f, 0.580f, 0.680f, 0.85f));
    }

    private static void StyleActionPill(Button btn, Color color, int radius = 20)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            ShadowSize = 4,
            ShadowColor = new Color(color.R, color.G, color.B, 0.25f)
        };
        var hover = new StyleBoxFlat
        {
            BgColor = color.Lightened(0.12f),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            ShadowSize = 8,
            ShadowColor = new Color(color.R, color.G, color.B, 0.35f)
        };
        var pressed = new StyleBoxFlat
        {
            BgColor = color.Darkened(0.12f),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            ShadowSize = 1
        };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", Colors.White);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }

    private static void StyleOutlinePill(Button btn, Color color, int radius = 20)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = Colors.White,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.85f, 0.88f, 0.94f),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        var hover = new StyleBoxFlat
        {
            BgColor = new Color(color.R, color.G, color.B, 0.12f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = color,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        var pressed = new StyleBoxFlat
        {
            BgColor = new Color(color.R, color.G, color.B, 0.22f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = color,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", color);
        btn.AddThemeColorOverride("font_hover_color", color);
        btn.AddThemeColorOverride("font_pressed_color", color);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }

    private void PreviewSlot(EditableVoiceSlot slot)
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        _isPreviewingSlot = true;
        _previewEndSec = slot.EndSeconds;
        var leadInStart = Math.Max(0.0, slot.StartSeconds - 3.0);
        SeekTo(leadInStart);
        PlayVideo();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = string.Empty;
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
            if (_statusLabel is not null) _statusLabel.Text = "No scene loaded.";
            return false;
        }

        if (_editableSlots.Count == 0)
        {
            if (_statusLabel is not null) _statusLabel.Text = "Add at least one line.";
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
            if (_statusLabel is not null) _statusLabel.Text = ex.Message;
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
            _statusLabel.Text = "Saved";
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
