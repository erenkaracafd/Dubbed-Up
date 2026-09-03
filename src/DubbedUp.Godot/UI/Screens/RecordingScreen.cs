using DubbedUp.Core.VoiceTakes;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.Network;
using DubbedUp.Godot.UI.Controls;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class RecordingScreen : BaseScreen
{
    private Label? _statusLabel;
    private Label? _slotInfoLabel;
    private Label? _promptSubtitleLabel;
    private Label? _countdownLabel;
    private Label? _syncScoreLabel;
    private Label? _errorLabel;

    private AspectRatioContainer? _videoContainer;
    private VideoStreamPlayer? _videoPlayer;
    private WaveformVisualizer? _waveformVisualizer;

    private Button? _previewOriginalButton;
    private Button? _recordButton;
    private Button? _previewTakeButton;
    private Button? _reRecordButton;
    private Button? _prevSlotButton;
    private Button? _nextSlotButton;
    private Button? _proceedButton;
    private Button? _cancelButton;

    private NetworkLobbyManager? _lobbyManager;
    private AudioStreamPlayer? _previewPlayer;

    private int _currentSlotIndex = 0;
    private bool _isRecordingActive = false;
    private bool _isMicCapturing = false;
    private bool _isCountingDown = false;
    private bool _isPreviewingOriginal = false;
    private bool _isPreviewingTake = false;

    private double _countdownTimer = 0.0;
    private double _settingLeadInSeconds = 3.0;
    private double _settingCountdownSeconds = 0.0;
    private double _recordingElapsed = 0.0;
    private double _leadInSec = 3.0;
    private double _leadInStartSec = 0.0;
    private double _previewOriginalDuration = 0.0;
    private double _previewTakeDuration = 0.0;
    private double _slotStartSec = 0.0;
    private double _slotEndSec = 4.0;
    private double _maxSlotDuration = 4.0;
    private string? _currentTakeId;

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);
        LoadGameplaySettings();
        LoadSceneVideo();
        UpdateUiState();
    }

    private void LoadGameplaySettings()
    {
        try
        {
            var config = new ConfigFile();
            if (config.Load("user://audio_settings.cfg") == Error.Ok)
            {
                var leadInVariant = config.GetValue("Gameplay", "LeadInSeconds", 3.0);
                var countdownVariant = config.GetValue("Gameplay", "CountdownSeconds", 0.0);

                _settingLeadInSeconds = Convert.ToDouble(leadInVariant.Obj ?? 3.0);
                _settingCountdownSeconds = Convert.ToDouble(countdownVariant.Obj ?? 0.0);
                GD.Print($"[RecordingScreen] Loaded gameplay settings: LeadIn={_settingLeadInSeconds:F1}s, Countdown={_settingCountdownSeconds:F0}s");
            }
            else
            {
                _settingLeadInSeconds = 3.0;
                _settingCountdownSeconds = 0.0;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RecordingScreen] Error loading gameplay settings: {ex.Message}");
            _settingLeadInSeconds = 3.0;
            _settingCountdownSeconds = 0.0;
        }
    }

    public override void _Ready()
    {
        _statusLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/StatusLabel");
        _slotInfoLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/TopInfoHBox/SlotInfoLabel");
        _promptSubtitleLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/PromptSubtitleLabel");
        _countdownLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/CountdownLabel");
        _syncScoreLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/SyncScoreLabel");
        _errorLabel = GetNodeOrNull<Label>("CenterArea/VBoxContainer/ErrorLabel");

        _videoContainer = GetNodeOrNull<AspectRatioContainer>("CenterArea/VBoxContainer/VideoContainer");
        _videoPlayer = GetNodeOrNull<VideoStreamPlayer>("CenterArea/VBoxContainer/VideoContainer/VideoPanel/VideoPlayer");
        _waveformVisualizer = GetNodeOrNull<WaveformVisualizer>("CenterArea/VBoxContainer/WaveformVisualizer");

        _previewOriginalButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/StudioActions/PreviewOriginalButton");
        _recordButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/StudioActions/RecordButton");
        _previewTakeButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/ReviewActions/PreviewTakeButton");
        _reRecordButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/ReviewActions/ReRecordButton");
        _prevSlotButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/ReviewActions/PrevSlotButton");
        _nextSlotButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/ReviewActions/NextSlotButton");
        _proceedButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/ReviewActions/FinishRecordingButton");
        _cancelButton = GetNodeOrNull<Button>("CenterArea/VBoxContainer/BottomRow/CancelButton");

        ApplyStyling();

        _previewPlayer = new AudioStreamPlayer();
        AddChild(_previewPlayer);

        if (Navigator is LocalNavigationController navCtrl)
        {
            _lobbyManager = navCtrl.LobbyManager;
            if (_lobbyManager.IsConnectedToLobby)
            {
                _lobbyManager.AudioTakeReceived += OnRemoteAudioTakeReceived;
            }
        }

        if (_previewOriginalButton is not null) SetupButton(_previewOriginalButton, OnPreviewOriginalPressed);
        if (_recordButton is not null) SetupButton(_recordButton, OnRecordButtonPressed);
        if (_previewTakeButton is not null) SetupButton(_previewTakeButton, OnPreviewTakePressed);
        if (_reRecordButton is not null) SetupButton(_reRecordButton, OnReRecordPressed);
        if (_prevSlotButton is not null) SetupButton(_prevSlotButton, OnPrevSlotPressed);
        if (_nextSlotButton is not null) SetupButton(_nextSlotButton, OnNextSlotPressed);
        if (_proceedButton is not null) SetupButton(_proceedButton, OnProceedPressed);
        if (_cancelButton is not null) SetupButton(_cancelButton, OnCancelPressed);

        Microphone.GodotLiveMicrophoneService.Instance.Initialize(this);
        LoadSceneVideo();
        UpdateUiState();
    }

    private void SetupButton(Button btn, Action action)
    {
        btn.Pressed += action;
        UiSoundManager.Attach(btn);
    }

    private void ApplyStyling()
    {
        var panel = GetNodeOrNull<PanelContainer>("CenterArea/VBoxContainer/VideoContainer/VideoPanel");
        if (panel is not null)
        {
            var pBox = new StyleBoxFlat
            {
                BgColor = Colors.Black,
                CornerRadiusTopLeft = 14,
                CornerRadiusTopRight = 14,
                CornerRadiusBottomLeft = 14,
                CornerRadiusBottomRight = 14,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.780f, 0.850f, 0.950f)
            };
            panel.AddThemeStyleboxOverride("panel", pBox);
        }

        if (_previewOriginalButton is not null) StyleActionPill(_previewOriginalButton, new Color(0.280f, 0.650f, 0.950f), 21);
        if (_recordButton is not null) StyleActionPill(_recordButton, new Color(1.0f, 0.540f, 0.680f), 21);
        if (_proceedButton is not null) StyleActionPill(_proceedButton, new Color(1.0f, 0.540f, 0.680f), 20);
        if (_previewTakeButton is not null) StyleOutlinePill(_previewTakeButton, 18);
        if (_reRecordButton is not null) StyleOutlinePill(_reRecordButton, 18);
        if (_prevSlotButton is not null) StyleOutlinePill(_prevSlotButton, 18);
        if (_nextSlotButton is not null) StyleOutlinePill(_nextSlotButton, 18);
        if (_cancelButton is not null) StyleOutlinePill(_cancelButton, 16);
    }

    private static void StyleActionPill(Button btn, Color color, int radius)
    {
        var normal = new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 6, ShadowColor = new Color(color.R, color.G, color.B, 0.3f) };
        var hover = new StyleBoxFlat { BgColor = color.Lightened(0.15f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 10, ShadowColor = new Color(color.R, color.G, color.B, 0.4f) };
        var pressed = new StyleBoxFlat { BgColor = color.Darkened(0.15f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 1 };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
    }

    private static void StyleOutlinePill(Button btn, int radius)
    {
        var normal = new StyleBoxFlat { BgColor = new Color(0.955f, 0.975f, 1.0f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.780f, 0.850f, 0.950f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
        var hover = new StyleBoxFlat { BgColor = new Color(0.910f, 0.945f, 0.990f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.42f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.118f, 0.106f, 0.294f));
    }

    public override void _ExitTree()
    {
        if (_lobbyManager is not null)
        {
            _lobbyManager.AudioTakeReceived -= OnRemoteAudioTakeReceived;
        }
    }

    private void LoadSceneVideo()
    {
        if (_videoPlayer is null || Coordinator?.CurrentScene is null) return;

        var videoAsset = Coordinator.CurrentScene.SourceMedia.FirstOrDefault(m => m.Role == Core.Scenes.SourceMediaRole.SceneVideo);
        var relPath = videoAsset?.RelativePath ?? "media/speed_homeless.ogv";

        var folderPath = Coordinator.SelectedScenePackage?.PackageDirectory;
        _videoPlayer.Stream = VideoPlayback.MediaTranscoder.LoadVideoStream(folderPath, relPath);
        if (_videoPlayer.Stream is not null)
        {
            _videoPlayer.Expand = true;
            _videoPlayer.Bus = "RecordSink";
            _videoPlayer.VolumeDb = -80.0f;
            GD.Print($"[RecordingScreen] Video loaded successfully.");
        }
        else
        {
            GD.Print($"[RecordingScreen] No video file found for '{relPath}'.");
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

        // 1. Countdown Logic (if enabled in settings)
        if (_isCountingDown)
        {
            _countdownTimer -= delta;
            if (_countdownLabel is not null)
            {
                var count = (int)Math.Ceiling(_countdownTimer);
                _countdownLabel.Text = count > 0 ? $"🎙️ COUNTDOWN: {count}" : "🎬 STARTING!";
                _countdownLabel.Visible = true;
            }

            if (_countdownTimer <= 0.0)
            {
                _isCountingDown = false;
                StartLiveRecording();
            }
            return;
        }

        // 2. Pre-Roll + Live Recording Logic
        if (_isRecordingActive)
        {
            _recordingElapsed += delta;
            _waveformVisualizer?.SetPlayhead(_recordingElapsed, true);

            // Stage 1: Pre-roll lead-in (dimmed area with original video sound)
            if (_recordingElapsed < _leadInSec)
            {
                var remaining = _leadInSec - _recordingElapsed;
                if (_countdownLabel is not null)
                {
                    _countdownLabel.Text = remaining > 0.2 ? $"⏳ PRE-ROLL LEAD-IN: {remaining:F1}s" : "🔴 SPEAK NOW!";
                    _countdownLabel.Visible = true;
                }
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"🎬 Video playing ({_leadInSec:F1}s lead-in)... Get ready to speak in {remaining:F1}s!";
                }
            }
            // Stage 2: Speech Box (Live Microphone Capture with original sound muted)
            else
            {
                if (!_isMicCapturing && Coordinator?.CurrentScene is not null)
                {
                    _isMicCapturing = true;
                    var currentSlot = Coordinator.CurrentScene.VoiceSlots[_currentSlotIndex];
                    Coordinator.StartRecording(currentSlot.VoiceSlotId);

                    // Mute original video audio inside speech box
                    if (_videoPlayer is not null)
                    {
                        _videoPlayer.Bus = "RecordSink";
                        _videoPlayer.VolumeDb = -80.0f;
                    }

                    if (_countdownLabel is not null)
                    {
                        _countdownLabel.Text = "🔴 RECORDING LIVE — SPEAK NOW!";
                        _countdownLabel.Visible = true;
                    }
                    if (_statusLabel is not null)
                    {
                        _statusLabel.Text = "🔴 RECORDING LIVE — Speak your line!";
                    }
                }

                // Sample user microphone peak level
                var peak = Microphone.GodotLiveMicrophoneService.Instance.GetLivePeakLevel();
                _waveformVisualizer?.AddLiveVoiceSample(_recordingElapsed, peak);

                // Auto-stop when speech box ends
                if (_recordingElapsed >= _leadInSec + _maxSlotDuration + 0.25)
                {
                    StopLiveRecording();
                }
            }
            return;
        }

        // 3. Previewing Original Clip (WITH original sound + 3s pre-roll)
        if (_isPreviewingOriginal)
        {
            _previewOriginalDuration += delta;
            _waveformVisualizer?.SetPlayhead(_previewOriginalDuration, false);

            if (_previewOriginalDuration >= _leadInSec + _maxSlotDuration)
            {
                if (_videoPlayer is not null && _videoPlayer.IsPlaying()) _videoPlayer.Stop();
                _isPreviewingOriginal = false;
                _previewOriginalDuration = 0.0;
                _waveformVisualizer?.SetPlayhead(0, false);
                if (_previewOriginalButton is not null) _previewOriginalButton.Text = "🎧 Listen to Original Reference";
                if (_statusLabel is not null) _statusLabel.Text = "Ready. Press 'Listen to Original' or 'Start Recording'.";
            }
            return;
        }

        // 4. Previewing User Recorded Take (Synchronized Video + Timeline Playhead + User Voice)
        if (_isPreviewingTake)
        {
            _previewTakeDuration += delta;
            _waveformVisualizer?.SetPlayhead(_leadInSec + _previewTakeDuration, false);

            if (_previewTakeDuration >= _maxSlotDuration + 0.4 || (_previewPlayer is not null && !_previewPlayer.Playing && _previewTakeDuration > 0.3))
            {
                StopTakePreview();
            }
        }
    }

    private void StopTakePreview()
    {
        _isPreviewingTake = false;
        _previewTakeDuration = 0.0;

        if (_previewPlayer is not null && _previewPlayer.Playing) _previewPlayer.Stop();
        if (_videoPlayer is not null && _videoPlayer.IsPlaying()) _videoPlayer.Stop();

        _waveformVisualizer?.SetPlayhead(0.0, false);
        if (_previewTakeButton is not null) _previewTakeButton.Text = "▶ Listen to My Take";
        if (_statusLabel is not null) _statusLabel.Text = "Take preview finished.";
    }

    private void UpdateUiState()
    {
        if (Coordinator?.CurrentScene is null || Coordinator.ActiveRound is null)
        {
            return;
        }

        var voiceSlots = Coordinator.CurrentScene.VoiceSlots;
        if (voiceSlots.Count == 0) return;

        _currentSlotIndex = Math.Clamp(_currentSlotIndex, 0, voiceSlots.Count - 1);
        var currentSlot = voiceSlots[_currentSlotIndex];

        // Resolve timeline entry & durations
        var timelineEntry = Coordinator.CurrentScene.Timeline.FirstOrDefault(e => e.VoiceSlotId == currentSlot.VoiceSlotId);
        if (timelineEntry is not null && timelineEntry.EndMilliseconds > timelineEntry.StartMilliseconds)
        {
            _slotStartSec = timelineEntry.StartMilliseconds / 1000.0;
            _slotEndSec = timelineEntry.EndMilliseconds / 1000.0;
            _maxSlotDuration = _slotEndSec - _slotStartSec;
        }
        else
        {
            _slotStartSec = 0.0;
            _slotEndSec = 4.0;
            _maxSlotDuration = 4.0;
        }

        var charDef = Coordinator.CurrentScene.Characters.FirstOrDefault(c => c.CharacterId == currentSlot.CharacterId);
        var charName = charDef?.DisplayName ?? currentSlot.CharacterId;

        var assignment = Coordinator.ActiveRound.GetVoiceSlotAssignments().FirstOrDefault(a => a.VoiceSlotId == currentSlot.VoiceSlotId);
        var playerName = Coordinator.CurrentSession?.Players.FirstOrDefault(p => p.PlayerId == assignment?.PlayerId)?.DisplayName ?? "Player";

        if (_slotInfoLabel is not null)
        {
            _slotInfoLabel.Text = $"Line {_currentSlotIndex + 1} / {voiceSlots.Count}  |  Character: 🎭 {charName}  ({playerName})  |  Duration: {_maxSlotDuration:F1}s";
        }

        if (_promptSubtitleLabel is not null)
        {
            _promptSubtitleLabel.Text = $"💬 \"{currentSlot.Prompt}\"";
        }

        // Reset Waveform visualizer with REAL audio waveform extracted from video audio
        LoadWaveformForCurrentSlot();

        var isRecorded = Coordinator.TakeStore.HasTakeForSlot(currentSlot.VoiceSlotId);
        var latestTake = Coordinator.TakeStore.GetLatestTakeForSlot(currentSlot.VoiceSlotId);
        _currentTakeId = latestTake?.TakeId;

        // Button visibility
        LoadGameplaySettings();
        if (_previewOriginalButton is not null) _previewOriginalButton.Visible = true;
        if (_recordButton is not null)
        {
            if (isRecorded)
            {
                _recordButton.Text = "🎙️ Re-Record (Overwrite)";
            }
            else
            {
                _recordButton.Text = _settingCountdownSeconds > 0
                    ? $"🎙️ Start Recording ({_settingCountdownSeconds:F0}s Countdown)"
                    : "🎙️ Start Recording";
            }
        }
        if (_previewTakeButton is not null) _previewTakeButton.Visible = isRecorded;
        if (_reRecordButton is not null) _reRecordButton.Visible = isRecorded;
        if (_prevSlotButton is not null)
        {
            _prevSlotButton.Visible = _currentSlotIndex > 0;
        }
        if (_nextSlotButton is not null)
        {
            _nextSlotButton.Visible = isRecorded && _currentSlotIndex < voiceSlots.Count - 1;
        }

        var allDone = voiceSlots.All(s => Coordinator.TakeStore.HasTakeForSlot(s.VoiceSlotId));
        if (_proceedButton is not null)
        {
            _proceedButton.Visible = allDone;
            _proceedButton.Text = "🎬 All Lines Recorded — Watch Full Playback!";
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = isRecorded
                ? "✅ Take recorded! You can preview your take, re-record, or proceed to the next line."
                : "Ready. Press 'Listen to Original' or 'Start Recording'.";
        }
    }

    private string? FindVocalsWavPath()
    {
        var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
        var sceneId = Coordinator?.CurrentScene?.SceneId;

        var vocalCandidates = new List<string>();

        // 1. Direct package directory
        if (!string.IsNullOrEmpty(folderPath))
        {
            vocalCandidates.Add(System.IO.Path.Combine(folderPath, "media", "vocals.wav"));
            vocalCandidates.Add(System.IO.Path.Combine(folderPath, "vocals.wav"));
        }

        // 2. Workshop scenes folder (user://workshop_scenes) & official/scenes paths
        if (!string.IsNullOrEmpty(sceneId))
        {
            var idUnderscore = sceneId.Replace("-", "_");
            var idHyphen = sceneId.Replace("_", "-");

            vocalCandidates.Add(ProjectSettings.GlobalizePath($"user://workshop_scenes/{sceneId}/media/vocals.wav"));
            vocalCandidates.Add(ProjectSettings.GlobalizePath($"user://workshop_scenes/{sceneId}/vocals.wav"));
            vocalCandidates.Add(ProjectSettings.GlobalizePath($"user://workshop_scenes/{idUnderscore}/media/vocals.wav"));
            vocalCandidates.Add(ProjectSettings.GlobalizePath($"user://workshop_scenes/{idHyphen}/media/vocals.wav"));

            vocalCandidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{sceneId}/media/vocals.wav"));
            vocalCandidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idUnderscore}/media/vocals.wav"));
            vocalCandidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idHyphen}/media/vocals.wav"));

            vocalCandidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{sceneId}/media/vocals.wav"));
            vocalCandidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{idUnderscore}/media/vocals.wav"));
            vocalCandidates.Add(ProjectSettings.GlobalizePath($"user://scenes/{sceneId}/media/vocals.wav"));
            vocalCandidates.Add(ProjectSettings.GlobalizePath($"user://scenes/{sceneId}/vocals.wav"));
        }

        // Search for vocals.wav first (EXCLUSIVELY prioritized isolated vocal track)
        foreach (var candidate in vocalCandidates)
        {
            if (System.IO.File.Exists(candidate) || global::Godot.FileAccess.FileExists(candidate))
            {
                GD.Print($"[RecordingScreen] Found isolated vocal track for waveform: '{candidate}'");
                return candidate;
            }
        }

        // Deep search in user://workshop_scenes/ if sceneId matches any directory
        if (!string.IsNullOrEmpty(sceneId))
        {
            try
            {
                var workshopDir = ProjectSettings.GlobalizePath("user://workshop_scenes");
                if (System.IO.Directory.Exists(workshopDir))
                {
                    foreach (var dir in System.IO.Directory.GetDirectories(workshopDir))
                    {
                        var dirName = System.IO.Path.GetFileName(dir);
                        if (dirName.Equals(sceneId, StringComparison.OrdinalIgnoreCase) ||
                            dirName.StartsWith(sceneId, StringComparison.OrdinalIgnoreCase) ||
                            sceneId.StartsWith(dirName, StringComparison.OrdinalIgnoreCase))
                        {
                            var v1 = System.IO.Path.Combine(dir, "media", "vocals.wav");
                            var v2 = System.IO.Path.Combine(dir, "vocals.wav");
                            if (System.IO.File.Exists(v1)) return v1;
                            if (System.IO.File.Exists(v2)) return v2;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[RecordingScreen] Error during deep vocals search: {ex.Message}");
            }
        }

        // Fallback: full audio.wav only if vocals.wav does not exist anywhere
        var audioCandidates = new List<string>();
        if (!string.IsNullOrEmpty(folderPath))
        {
            audioCandidates.Add(System.IO.Path.Combine(folderPath, "media", "audio.wav"));
            audioCandidates.Add(System.IO.Path.Combine(folderPath, "audio.wav"));
        }
        if (!string.IsNullOrEmpty(sceneId))
        {
            audioCandidates.Add(ProjectSettings.GlobalizePath($"user://workshop_scenes/{sceneId}/media/audio.wav"));
            audioCandidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{sceneId}/media/audio.wav"));
            audioCandidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{sceneId}/media/audio.wav"));
        }
        foreach (var candidate in audioCandidates)
        {
            if (System.IO.File.Exists(candidate) || global::Godot.FileAccess.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private void LoadWaveformForCurrentSlot()
    {
        float[]? realWaveform = null;
        string? wavPath = FindVocalsWavPath();

        LoadGameplaySettings();
        _leadInSec = Math.Min(_settingLeadInSeconds, _slotStartSec);
        _leadInStartSec = _slotStartSec - _leadInSec;

        if (wavPath is not null)
        {
            realWaveform = AudioPlayback.AudioWaveformLoader.ExtractWaveformSegment(wavPath, _leadInStartSec, _slotEndSec, 150);
        }

        _waveformVisualizer?.Reset(_leadInSec, _maxSlotDuration, realWaveform);
    }

    private void OnPreviewOriginalPressed()
    {
        if (_isPreviewingTake) StopTakePreview();

        if (_videoPlayer is null || _videoPlayer.Stream is null)
        {
            LoadSceneVideo();
        }

        if (_videoPlayer is null || _videoPlayer.Stream is null)
        {
            if (_statusLabel is not null) _statusLabel.Text = "ℹ️ No video stream found for this scene.";
            return;
        }

        if (_isPreviewingOriginal)
        {
            _videoPlayer.Stop();
            _isPreviewingOriginal = false;
            if (_previewOriginalButton is not null) _previewOriginalButton.Text = "🎧 Listen to Original Reference";
            return;
        }

        // Preview original with configurable context lead-in so player hears the scene build up
        LoadWaveformForCurrentSlot();
        _waveformVisualizer?.SetPlayhead(0.0, false);

        _videoPlayer.Bus = "Master";
        _videoPlayer.VolumeDb = 0.0f;
        _isPreviewingOriginal = true;
        _previewOriginalDuration = 0.0;
        _videoPlayer.Play();
        _videoPlayer.StreamPosition = _leadInStartSec;

        if (_previewOriginalButton is not null) _previewOriginalButton.Text = "⏹ Stop Playing";
        if (_statusLabel is not null) _statusLabel.Text = $"🎧 Playing clip with {_leadInSec:F1}s context lead-in ({_leadInStartSec:F1}s → {_slotEndSec:F1}s)...";
    }

    private void OnRecordButtonPressed()
    {
        if (_isPreviewingTake) StopTakePreview();
        if (_isPreviewingOriginal && _videoPlayer is not null) { _videoPlayer.Stop(); _isPreviewingOriginal = false; }

        if (_isCountingDown)
        {
            _isCountingDown = false;
            if (_countdownLabel is not null) _countdownLabel.Visible = false;
            UpdateUiState();
            return;
        }

        if (_isRecordingActive)
        {
            StopLiveRecording();
            return;
        }

        LoadGameplaySettings();

        if (_settingCountdownSeconds > 0.0)
        {
            _isCountingDown = true;
            _countdownTimer = _settingCountdownSeconds;
            if (_countdownLabel is not null)
            {
                _countdownLabel.Text = $"🎙️ COUNTDOWN: {(int)Math.Ceiling(_countdownTimer)}";
                _countdownLabel.Visible = true;
            }
            if (_recordButton is not null) _recordButton.Text = "Cancel";
            if (_statusLabel is not null) _statusLabel.Text = $"Countdown ({_settingCountdownSeconds:F0}s) started... Get ready!";
        }
        else
        {
            StartLiveRecording();
        }
    }

    private void StartLiveRecording()
    {
        if (Coordinator?.CurrentScene is null) return;

        try
        {
            LoadGameplaySettings();
            _isRecordingActive = true;
            _isMicCapturing = false;
            _recordingElapsed = 0.0;
            _leadInSec = Math.Min(_settingLeadInSeconds, _slotStartSec);
            _leadInStartSec = _slotStartSec - _leadInSec;

            LoadWaveformForCurrentSlot();
            _waveformVisualizer?.SetPlayhead(0.0, true);

            // Start video playback from lead-in with sound so player hears the cue
            if (_videoPlayer is null || _videoPlayer.Stream is null)
            {
                LoadSceneVideo();
            }

            if (_videoPlayer is not null && _videoPlayer.Stream is not null)
            {
                _videoPlayer.Bus = "Master";
                _videoPlayer.VolumeDb = 0.0f;
                _videoPlayer.Play();
                _videoPlayer.StreamPosition = _leadInStartSec;
            }

            if (_recordButton is not null) _recordButton.Text = "⏹ Cancel Recording";
            if (_countdownLabel is not null)
            {
                _countdownLabel.Text = _leadInSec > 0 ? $"⏳ PRE-ROLL LEAD-IN: {_leadInSec:F1}s" : "🔴 RECORDING LIVE — SPEAK NOW!";
                _countdownLabel.Visible = true;
            }
            if (_statusLabel is not null)
            {
                _statusLabel.Text = _leadInSec > 0
                    ? $"🎬 Video starting ({_leadInSec:F1}s lead-in)... Focus on actor timing!"
                    : "🔴 RECORDING LIVE — Speak your line!";
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to start recording: {ex.Message}");
        }
    }

    private void StopLiveRecording()
    {
        if (!_isRecordingActive || Coordinator is null || Coordinator.CurrentScene is null) return;

        try
        {
            _isRecordingActive = false;
            VoiceTake? take = null;

            if (_isMicCapturing)
            {
                take = Coordinator.StopRecording();
                _isMicCapturing = false;
                _currentTakeId = take.TakeId;
            }

            if (_countdownLabel is not null) _countdownLabel.Visible = false;

            // Stop video
            if (_videoPlayer is not null && _videoPlayer.IsPlaying())
            {
                _videoPlayer.Stop();
            }

            // Calculate sync score
            var matchPercent = _waveformVisualizer?.CalculateSyncMatchPercentage() ?? 90.0f;
            if (_syncScoreLabel is not null && take is not null)
            {
                _syncScoreLabel.Text = $"⭐ Timing & Rhythm Match: {matchPercent:F0}%";
                _syncScoreLabel.Visible = true;
            }

            // Broadcast take if in lobby
            if (take is not null && _lobbyManager is not null && _lobbyManager.IsConnectedToLobby && !string.IsNullOrEmpty(take.AudioRelativePath))
            {
                try
                {
                    if (global::Godot.FileAccess.FileExists(take.AudioRelativePath))
                    {
                        var bytes = global::Godot.FileAccess.GetFileAsBytes(take.AudioRelativePath);
                        if (bytes is not null && bytes.Length > 0)
                        {
                            _lobbyManager.BroadcastAudioTake(take.VoiceSlotId, bytes);
                        }
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to broadcast take: {ex.Message}");
                }
            }

            UpdateUiState();
        }
        catch (Exception ex)
        {
            _isRecordingActive = false;
            _isMicCapturing = false;
            ShowError($"Failed to stop recording: {ex.Message}");
        }
    }

    private void OnPreviewTakePressed()
    {
        if (_isPreviewingTake)
        {
            StopTakePreview();
            return;
        }

        if (_isPreviewingOriginal && _videoPlayer is not null)
        {
            _videoPlayer.Stop();
            _isPreviewingOriginal = false;
        }

        if (_currentTakeId is null || _previewPlayer is null || Coordinator is null) return;

        var take = Coordinator.TakeStore.GetTake(_currentTakeId);
        if (take is null || string.IsNullOrEmpty(take.AudioRelativePath)) return;

        try
        {
            byte[]? bytes = null;
            if (global::Godot.FileAccess.FileExists(take.AudioRelativePath))
            {
                bytes = global::Godot.FileAccess.GetFileAsBytes(take.AudioRelativePath);
            }
            else
            {
                var globalPath = ProjectSettings.GlobalizePath(take.AudioRelativePath);
                if (System.IO.File.Exists(globalPath))
                {
                    bytes = System.IO.File.ReadAllBytes(globalPath);
                }
            }

            if (bytes is not null && bytes.Length > 44)
            {
                var wav = AudioPlayback.VoiceTakeAudioPlayer.ParseWavBytes(bytes);
                if (wav is not null)
                {
                    _previewPlayer.Stream = wav;
                    _previewPlayer.Play(0.0f);

                    // Start synchronized MUTED video playback
                    if (_videoPlayer is null || _videoPlayer.Stream is null) LoadSceneVideo();
                    if (_videoPlayer is not null && _videoPlayer.Stream is not null)
                    {
                        _videoPlayer.Bus = "RecordSink";
                        _videoPlayer.VolumeDb = -80.0f;
                        _videoPlayer.Play();
                        _videoPlayer.StreamPosition = _slotStartSec;
                    }

                    _isPreviewingTake = true;
                    _previewTakeDuration = 0.0;
                    _waveformVisualizer?.SetPlayhead(0.0, false);

                    if (_previewTakeButton is not null) _previewTakeButton.Text = "⏹ Stop Preview";
                    if (_statusLabel is not null) _statusLabel.Text = "▶ Playing your take in sync with video...";
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"Take preview error: {ex.Message}");
        }
    }

    private void OnReRecordPressed()
    {
        if (_isPreviewingTake) StopTakePreview();
        if (_previewPlayer is not null && _previewPlayer.Playing) _previewPlayer.Stop();
        if (_videoPlayer is not null && _videoPlayer.IsPlaying()) _videoPlayer.Stop();

        LoadWaveformForCurrentSlot();
        if (_syncScoreLabel is not null) _syncScoreLabel.Visible = false;

        OnRecordButtonPressed();
    }

    private void OnPrevSlotPressed()
    {
        if (_isPreviewingTake) StopTakePreview();
        if (_previewPlayer is not null && _previewPlayer.Playing) _previewPlayer.Stop();
        if (_videoPlayer is not null && _videoPlayer.IsPlaying()) _videoPlayer.Stop();

        if (_currentSlotIndex > 0)
        {
            _currentSlotIndex--;
            if (_syncScoreLabel is not null) _syncScoreLabel.Visible = false;
            UpdateUiState();
        }
    }

    private void OnNextSlotPressed()
    {
        if (_isPreviewingTake) StopTakePreview();
        if (_previewPlayer is not null && _previewPlayer.Playing) _previewPlayer.Stop();
        if (_videoPlayer is not null && _videoPlayer.IsPlaying()) _videoPlayer.Stop();

        if (Coordinator?.CurrentScene is not null && _currentSlotIndex < Coordinator.CurrentScene.VoiceSlots.Count - 1)
        {
            _currentSlotIndex++;
            if (_syncScoreLabel is not null) _syncScoreLabel.Visible = false;
            UpdateUiState();
        }
    }

    private void OnRemoteAudioTakeReceived(string slotId, string senderPlayerId, byte[] audioBytes)
    {
        if (Coordinator is null) return;

        try
        {
            var takeId = $"take-remote-{slotId}-{senderPlayerId}";
            var userDir = ProjectSettings.GlobalizePath("user://recordings");
            if (!System.IO.Directory.Exists(userDir))
            {
                System.IO.Directory.CreateDirectory(userDir);
            }

            var filePath = System.IO.Path.Combine(userDir, $"{takeId}.wav");
            System.IO.File.WriteAllBytes(filePath, audioBytes);

            var relativePath = $"user://recordings/{takeId}.wav";
            var roundId = Coordinator.ActiveRound?.RoundId ?? "round-1";
            var characterId = Coordinator.CurrentScene?.VoiceSlots.FirstOrDefault(s => s.VoiceSlotId == slotId)?.CharacterId ?? "char-1";
            var remoteTake = new VoiceTake(takeId, slotId, senderPlayerId, characterId, roundId, relativePath, 3000, DateTimeOffset.UtcNow);

            Coordinator.TakeStore.AddTake(remoteTake);
            Coordinator.ActiveRound?.MarkVoiceSlotRecorded(slotId);

            CallDeferred(nameof(UpdateUiState));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to process remote take: {ex.Message}");
        }
    }

    private void OnProceedPressed()
    {
        try
        {
            if (_isPreviewingTake) StopTakePreview();
            if (_videoPlayer is not null && _videoPlayer.IsPlaying()) _videoPlayer.Stop();
            if (_previewPlayer is not null && _previewPlayer.Playing) _previewPlayer.Stop();

            Coordinator?.FinishRecording();
            Navigator?.NavigateTo(AppScreen.Playback);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnCancelPressed()
    {
        if (_isPreviewingTake) StopTakePreview();
        if (_videoPlayer is not null && _videoPlayer.IsPlaying()) _videoPlayer.Stop();
        if (_previewPlayer is not null && _previewPlayer.Playing) _previewPlayer.Stop();

        Coordinator?.ResetSession();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }

    private void ShowError(string message)
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Text = message;
            _errorLabel.Visible = true;
        }
    }
}
