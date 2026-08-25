using DubbedUp.Core.VoiceTakes;
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

    private VideoStreamPlayer? _videoPlayer;
    private WaveformVisualizer? _waveformVisualizer;

    private Button? _previewOriginalButton;
    private Button? _recordButton;
    private Button? _previewTakeButton;
    private Button? _reRecordButton;
    private Button? _nextSlotButton;
    private Button? _proceedButton;
    private Button? _cancelButton;

    private NetworkLobbyManager? _lobbyManager;
    private AudioStreamPlayer? _previewPlayer;

    private int _currentSlotIndex = 0;
    private bool _isRecordingLocal = false;
    private bool _isCountingDown = false;
    private bool _isPreviewingOriginal = false;
    private bool _isPreviewingTake = false;

    private double _countdownTimer = 0.0;
    private double _recordingDuration = 0.0;
    private double _previewTakeDuration = 0.0;
    private double _slotStartSec = 0.0;
    private double _slotEndSec = 4.0;
    private double _maxSlotDuration = 4.0;
    private string? _currentTakeId;

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);
        LoadSceneVideo();
        UpdateUiState();
    }

    public override void _Ready()
    {
        _statusLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/StatusLabel");
        _slotInfoLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/SlotInfoLabel");
        _promptSubtitleLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/PromptSubtitleLabel");
        _countdownLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/CountdownLabel");
        _syncScoreLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/SyncScoreLabel");
        _errorLabel = GetNodeOrNull<Label>("ScrollContainer/CenterContainer/VBoxContainer/ErrorLabel");

        _videoPlayer = GetNodeOrNull<VideoStreamPlayer>("ScrollContainer/CenterContainer/VBoxContainer/VideoPanel/VideoPlayer");
        _waveformVisualizer = GetNodeOrNull<WaveformVisualizer>("ScrollContainer/CenterContainer/VBoxContainer/WaveformVisualizer");

        _previewOriginalButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/StudioActions/PreviewOriginalButton");
        _recordButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/StudioActions/RecordButton");
        _previewTakeButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ReviewActions/PreviewTakeButton");
        _reRecordButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ReviewActions/ReRecordButton");
        _nextSlotButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ReviewActions/NextSlotButton");
        _proceedButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/ProceedButton");
        _cancelButton = GetNodeOrNull<Button>("ScrollContainer/CenterContainer/VBoxContainer/CancelButton");

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

        if (_previewOriginalButton is not null) _previewOriginalButton.Pressed += OnPreviewOriginalPressed;
        if (_recordButton is not null) _recordButton.Pressed += OnRecordButtonPressed;
        if (_previewTakeButton is not null) _previewTakeButton.Pressed += OnPreviewTakePressed;
        if (_reRecordButton is not null) _reRecordButton.Pressed += OnReRecordPressed;
        if (_nextSlotButton is not null) _nextSlotButton.Pressed += OnNextSlotPressed;
        if (_proceedButton is not null) _proceedButton.Pressed += OnProceedPressed;
        if (_cancelButton is not null) _cancelButton.Pressed += OnCancelPressed;

        Microphone.GodotLiveMicrophoneService.Instance.Initialize(this);
        LoadSceneVideo();
        UpdateUiState();
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
        string? resolvedFilePath = null;

        // 1. Direct package folder check
        if (!string.IsNullOrEmpty(folderPath))
        {
            var candidate = System.IO.Path.Combine(folderPath, relPath);
            if (System.IO.File.Exists(candidate)) resolvedFilePath = candidate;
        }

        // 2. Official scenes folder check
        if (resolvedFilePath is null)
        {
            var sceneId = Coordinator.CurrentScene.SceneId;
            var candidate = ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{sceneId}/{relPath}");
            if (System.IO.File.Exists(candidate)) resolvedFilePath = candidate;
        }

        // 3. Fallback to direct res:// path
        if (resolvedFilePath is null)
        {
            var glob = ProjectSettings.GlobalizePath(relPath);
            if (System.IO.File.Exists(glob)) resolvedFilePath = glob;
            else if (System.IO.File.Exists(relPath)) resolvedFilePath = relPath;
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
                _videoPlayer.Bus = "RecordSink"; // Default muted so no unprompted audio leaks
                _videoPlayer.VolumeDb = -80.0f;
                GD.Print($"[RecordingScreen] Video loaded successfully: '{stream.File}'");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[RecordingScreen] Failed to load video: {ex.Message}");
            }
        }
        else
        {
            GD.Print($"[RecordingScreen] No video file found for '{relPath}'.");
        }
    }

    public override void _Process(double delta)
    {
        // 1. Countdown Logic
        if (_isCountingDown)
        {
            _countdownTimer -= delta;
            if (_countdownLabel is not null)
            {
                var count = (int)Math.Ceiling(_countdownTimer);
                _countdownLabel.Text = count > 0 ? $"🎙 Kayıt Başlıyor: {count}..." : "🔴 ŞİMDİ KONUŞ!";
            }

            if (_countdownTimer <= 0.0)
            {
                _isCountingDown = false;
                if (_countdownLabel is not null) _countdownLabel.Visible = false;
                StartLiveRecording();
            }
            return;
        }

        // 2. Live Recording & Waveform Draw
        if (_isRecordingLocal)
        {
            _recordingDuration += delta;

            var peak = Microphone.GodotLiveMicrophoneService.Instance.GetLivePeakLevel();
            _waveformVisualizer?.AddLiveVoiceSample(_recordingDuration, peak);

            // Keep video playing in sync with recording (STRICTLY MUTED)
            if (_videoPlayer is not null && _videoPlayer.Stream is not null)
            {
                if (!_videoPlayer.IsPlaying())
                {
                    _videoPlayer.Bus = "RecordSink";
                    _videoPlayer.VolumeDb = -80.0f;
                    _videoPlayer.Play();
                    _videoPlayer.StreamPosition = _slotStartSec + _recordingDuration;
                }
            }

            // Auto-stop when max slot duration + 0.5s grace is exceeded
            if (_recordingDuration >= _maxSlotDuration + 0.5)
            {
                StopLiveRecording();
            }
            return;
        }

        // 3. Previewing Original Clip (WITH original sound)
        if (_isPreviewingOriginal && _videoPlayer is not null && _videoPlayer.IsPlaying())
        {
            var currentPos = _videoPlayer.GetStreamPosition();
            var relativePos = currentPos - _slotStartSec;
            _waveformVisualizer?.SetPlayhead(relativePos, false);

            if (currentPos >= _slotEndSec)
            {
                _videoPlayer.Stop();
                _isPreviewingOriginal = false;
                _waveformVisualizer?.SetPlayhead(0, false);
                if (_previewOriginalButton is not null) _previewOriginalButton.Text = "🎧 Orijinal Sahneyi Dinle & İzle";
            }
            return;
        }

        // 4. Previewing User Recorded Take (Synchronized Video + Timeline Playhead + User Voice)
        if (_isPreviewingTake)
        {
            _previewTakeDuration += delta;
            _waveformVisualizer?.SetPlayhead(_previewTakeDuration, false);

            if (_previewTakeDuration >= _maxSlotDuration || (_previewPlayer is not null && !_previewPlayer.Playing && _previewTakeDuration > 0.3))
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
        if (_previewTakeButton is not null) _previewTakeButton.Text = "▶ Kendi Kaydımı Dinle";
        if (_statusLabel is not null) _statusLabel.Text = "Önizleme tamamlandı.";
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
            _slotInfoLabel.Text = $"Replik {_currentSlotIndex + 1} / {voiceSlots.Count}  |  Karakter: 🎭 {charName}  ({playerName})  |  Süre: {_maxSlotDuration:F1}s";
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
        if (_previewOriginalButton is not null) _previewOriginalButton.Visible = true;
        if (_recordButton is not null)
        {
            _recordButton.Text = isRecorded ? "🎙️ Yeniden Kaydet (Üstüne Yaz)" : "🎙️ Kayda Başla (3-2-1)";
        }
        if (_previewTakeButton is not null) _previewTakeButton.Visible = isRecorded;
        if (_reRecordButton is not null) _reRecordButton.Visible = isRecorded;
        if (_nextSlotButton is not null)
        {
            _nextSlotButton.Visible = isRecorded && _currentSlotIndex < voiceSlots.Count - 1;
        }

        var allDone = voiceSlots.All(s => Coordinator.TakeStore.HasTakeForSlot(s.VoiceSlotId));
        if (_proceedButton is not null)
        {
            _proceedButton.Visible = allDone;
            _proceedButton.Text = "🎬 Tüm Replikler Tamam — Playback'i İzle!";
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = isRecorded
                ? $"✅ Replik kaydedildi! Kendi kaydını dinleyebilir veya sonraki repliğe geçebilirsin."
                : "Hazır. 'Orijinal Sahneyi Dinle' ile klibi dinleyebilir veya 'Kayda Başla'ya basabilirsin.";
        }
    }

    private void LoadWaveformForCurrentSlot()
    {
        float[]? realWaveform = null;
        var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
        string? wavPath = null;

        if (!string.IsNullOrEmpty(folderPath))
        {
            var candidate = System.IO.Path.Combine(folderPath, "media", "audio.wav");
            if (System.IO.File.Exists(candidate)) wavPath = candidate;
        }

        if (wavPath is null && Coordinator?.CurrentScene is not null)
        {
            var sceneId = Coordinator.CurrentScene.SceneId;
            var candidate = ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{sceneId}/media/audio.wav");
            if (System.IO.File.Exists(candidate)) wavPath = candidate;
        }

        if (wavPath is not null)
        {
            realWaveform = AudioPlayback.AudioWaveformLoader.ExtractWaveformSegment(wavPath, _slotStartSec, _slotEndSec);
        }

        _waveformVisualizer?.Reset(_maxSlotDuration, realWaveform);
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
            if (_statusLabel is not null) _statusLabel.Text = "ℹ️ Bu sahne için video akışı bulunamadı.";
            return;
        }

        if (_isPreviewingOriginal)
        {
            _videoPlayer.Stop();
            _isPreviewingOriginal = false;
            if (_previewOriginalButton is not null) _previewOriginalButton.Text = "🎧 Orijinal Sahneyi Dinle & İzle";
            return;
        }

        // Preview original: UNMUTE video and send to Master bus so original voice is audible
        _videoPlayer.Bus = "Master";
        _videoPlayer.VolumeDb = 0.0f;
        _isPreviewingOriginal = true;
        _videoPlayer.Play();
        _videoPlayer.StreamPosition = _slotStartSec;

        if (_previewOriginalButton is not null) _previewOriginalButton.Text = "⏹ Oynatmayı Durdur";
        if (_statusLabel is not null) _statusLabel.Text = "🎧 Orijinal sahne klibi oynatılıyor...";
    }

    private void OnRecordButtonPressed()
    {
        if (_isPreviewingTake) StopTakePreview();
        if (_isPreviewingOriginal && _videoPlayer is not null) { _videoPlayer.Stop(); _isPreviewingOriginal = false; }

        if (_isCountingDown)
        {
            _isCountingDown = false;
            if (_countdownLabel is not null) _countdownLabel.Visible = false;
            if (_recordButton is not null) _recordButton.Text = "🎙️ Kayda Başla (3-2-1)";
            return;
        }

        if (!_isRecordingLocal)
        {
            _isCountingDown = true;
            _countdownTimer = 2.0; // 2 seconds countdown
            if (_countdownLabel is not null)
            {
                _countdownLabel.Text = "🎙 Hazır ol... 2";
                _countdownLabel.Visible = true;
            }
            if (_recordButton is not null) _recordButton.Text = "İptal Et";
            if (_statusLabel is not null) _statusLabel.Text = "Geri sayım başladı... Karakterin dudak hareketine odaklan!";
        }
        else
        {
            StopLiveRecording();
        }
    }

    private void StartLiveRecording()
    {
        if (Coordinator?.CurrentScene is null) return;

        var voiceSlots = Coordinator.CurrentScene.VoiceSlots;
        var currentSlot = voiceSlots[_currentSlotIndex];

        try
        {
            Coordinator.StartRecording(currentSlot.VoiceSlotId);
            _isRecordingLocal = true;
            _recordingDuration = 0.0;

            LoadWaveformForCurrentSlot();
            _waveformVisualizer?.SetPlayhead(0.0, true);

            // Start video playback from slot start STRICTLY MUTED to RecordSink so original voice NEVER plays
            if (_videoPlayer is null || _videoPlayer.Stream is null)
            {
                LoadSceneVideo();
            }

            if (_videoPlayer is not null && _videoPlayer.Stream is not null)
            {
                _videoPlayer.Bus = "RecordSink"; // Route to muted sink
                _videoPlayer.VolumeDb = -80.0f; // MUTE original video audio completely
                _videoPlayer.Play();
                _videoPlayer.StreamPosition = _slotStartSec;
            }

            if (_recordButton is not null) _recordButton.Text = "⏹ Kaydı Bitir (Tamam)";
            if (_statusLabel is not null) _statusLabel.Text = "🔴 CANLI KAYIT ALINIYOR — Konuşun! (Video oynuyor, ses tamamen sessizde)";
        }
        catch (Exception ex)
        {
            ShowError($"Kayıt başlatılamadı: {ex.Message}");
        }
    }

    private void StopLiveRecording()
    {
        if (!_isRecordingLocal || Coordinator is null || Coordinator.CurrentScene is null) return;

        try
        {
            var take = Coordinator.StopRecording();
            _isRecordingLocal = false;
            _currentTakeId = take.TakeId;

            // Stop video
            if (_videoPlayer is not null && _videoPlayer.IsPlaying())
            {
                _videoPlayer.Stop();
            }

            // Calculate sync score
            var matchPercent = _waveformVisualizer?.CalculateSyncMatchPercentage() ?? 90.0f;
            if (_syncScoreLabel is not null)
            {
                _syncScoreLabel.Text = $"⭐ Zamanlama & Ritim Uyumu: %{matchPercent:F0}";
                _syncScoreLabel.Visible = true;
            }

            // Broadcast take if in lobby
            if (_lobbyManager is not null && _lobbyManager.IsConnectedToLobby && !string.IsNullOrEmpty(take.AudioRelativePath))
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
            _isRecordingLocal = false;
            ShowError($"Kayıt durdurulamadı: {ex.Message}");
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
                    _previewPlayer.Play();

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

                    if (_previewTakeButton is not null) _previewTakeButton.Text = "⏹ Dinlemeyi Durdur";
                    if (_statusLabel is not null) _statusLabel.Text = "▶ Kendi kaydın ve video eşzamanlı oynatılıyor...";
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"Dinleme hatası: {ex.Message}");
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
