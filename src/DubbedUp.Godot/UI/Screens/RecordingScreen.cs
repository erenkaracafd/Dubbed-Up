using DubbedUp.Core.VoiceTakes;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.Network;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class RecordingScreen : BaseScreen
{
    private Label? _statusLabel;
    private Label? _slotInfoLabel;
    private Label? _countdownLabel;
    private Label? _errorLabel;
    private VBoxContainer? _recordingProgressHBox;
    private ProgressBar? _recordingProgressBar;
    private Label? _recordingTimeLabel;
    private ProgressBar? _audioMeterBar;
    private Button? _recordButton;
    private Button? _previewTakeButton;
    private Button? _reRecordButton;
    private Button? _proceedButton;
    private Button? _cancelButton;

    private NetworkLobbyManager? _lobbyManager;
    private AudioStreamPlayer? _previewPlayer;
    private int _currentSlotIndex = 0;
    private bool _isRecordingLocal = false;
    private bool _isCountingDown = false;
    private double _countdownTimer = 0.0;
    private double _recordingDuration = 0.0;
    private double _maxSlotDuration = 5.0;

    public override void _Ready()
    {
        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StatusLabel");
        _slotInfoLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SlotInfoLabel");
        _countdownLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/CountdownLabel");
        _errorLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");
        _recordingProgressHBox = GetNodeOrNull<VBoxContainer>("CenterContainer/VBoxContainer/RecordingProgressHBox");
        _recordingProgressBar = GetNodeOrNull<ProgressBar>("CenterContainer/VBoxContainer/RecordingProgressHBox/RecordingProgressBar");
        _recordingTimeLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/RecordingProgressHBox/RecordingTimeLabel");
        _audioMeterBar = GetNodeOrNull<ProgressBar>("CenterContainer/VBoxContainer/AudioMeterBar");
        _recordButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/RecordButton");
        _previewTakeButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/PreviewTakeButton");
        _reRecordButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ReRecordButton");
        _proceedButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ProceedButton");
        _cancelButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/CancelButton");

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

        if (_recordButton is not null)
        {
            _recordButton.Pressed += OnRecordButtonPressed;
        }

        if (_previewTakeButton is not null)
        {
            _previewTakeButton.Pressed += OnPreviewTakePressed;
        }

        if (_reRecordButton is not null)
        {
            _reRecordButton.Pressed += OnReRecordButtonPressed;
        }

        if (_proceedButton is not null)
        {
            _proceedButton.Pressed += OnProceedPressed;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Pressed += OnCancelPressed;
        }

        Microphone.GodotLiveMicrophoneService.Instance.Initialize(this);
        UpdateUiState();
    }

    public override void _ExitTree()
    {
        if (_lobbyManager is not null)
        {
            _lobbyManager.AudioTakeReceived -= OnRemoteAudioTakeReceived;
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
                _countdownLabel.Text = count > 0 ? $"🎙 Starting in: {count}..." : "🔴 RECORD NOW!";
            }

            if (_countdownTimer <= 0.0)
            {
                _isCountingDown = false;
                if (_countdownLabel is not null)
                {
                    _countdownLabel.Visible = false;
                }
                StartLiveRecording();
            }
            return;
        }

        // 2. Recording Logic & Auto-Stop
        if (_isRecordingLocal)
        {
            _recordingDuration += delta;

            if (_recordingProgressBar is not null)
            {
                _recordingProgressBar.Value = Math.Min(_recordingDuration, _maxSlotDuration);
            }

            if (_recordingTimeLabel is not null)
            {
                _recordingTimeLabel.Text = $"{_recordingDuration:F1}s / {_maxSlotDuration:F1}s";
            }

            if (_audioMeterBar is not null)
            {
                var level = Microphone.GodotLiveMicrophoneService.Instance.GetLivePeakLevel();
                _audioMeterBar.Value = Math.Clamp(level, 0.0, 100.0);
            }

            // Auto-stop when duration reaches max slot time + 0.5s grace
            if (_recordingDuration >= _maxSlotDuration + 0.5)
            {
                StopLiveRecording();
            }
        }
    }

    private void OnRecordButtonPressed()
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Visible = false;
        }

        if (_isCountingDown)
        {
            _isCountingDown = false;
            if (_countdownLabel is not null) _countdownLabel.Visible = false;
            if (_recordButton is not null) _recordButton.Text = "🎙 Start Recording";
            return;
        }

        if (!_isRecordingLocal)
        {
            _isCountingDown = true;
            _countdownTimer = 2.0; // 2-second countdown
            if (_countdownLabel is not null)
            {
                _countdownLabel.Text = "🎙 Ready... 2";
                _countdownLabel.Visible = true;
            }
            if (_recordButton is not null)
            {
                _recordButton.Text = "Cancel Countdown";
            }
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "Get ready to speak...";
            }
        }
        else
        {
            StopLiveRecording();
        }
    }

    private void StartLiveRecording()
    {
        if (Coordinator is null || Coordinator.CurrentScene is null || Coordinator.ActiveRound is null)
        {
            ShowError("No active round available.");
            return;
        }

        var voiceSlots = Coordinator.CurrentScene.VoiceSlots;
        if (voiceSlots.Count == 0)
        {
            ShowError("Scene has no voice slots.");
            return;
        }

        var currentSlot = voiceSlots[Math.Clamp(_currentSlotIndex, 0, voiceSlots.Count - 1)];

        var timelineEntry = Coordinator.CurrentScene.Timeline.FirstOrDefault(e => e.VoiceSlotId == currentSlot.VoiceSlotId);
        if (timelineEntry is not null && timelineEntry.EndMilliseconds > timelineEntry.StartMilliseconds)
        {
            _maxSlotDuration = (timelineEntry.EndMilliseconds - timelineEntry.StartMilliseconds) / 1000.0;
        }
        else
        {
            _maxSlotDuration = 4.0;
        }

        try
        {
            Coordinator.StartRecording(currentSlot.VoiceSlotId);
            _isRecordingLocal = true;
            _recordingDuration = 0.0;

            if (_recordingProgressBar is not null)
            {
                _recordingProgressBar.MaxValue = _maxSlotDuration;
                _recordingProgressBar.Value = 0.0;
            }

            if (_recordingProgressHBox is not null)
            {
                _recordingProgressHBox.Visible = true;
            }

            if (_audioMeterBar is not null)
            {
                _audioMeterBar.Visible = true;
            }

            if (_previewTakeButton is not null)
            {
                _previewTakeButton.Visible = false;
            }

            if (_recordButton is not null)
            {
                _recordButton.Text = "⏹ Done Speaking (Stop)";
            }

            if (_statusLabel is not null)
            {
                _statusLabel.Text = "🔴 Status: Recording live microphone audio...";
            }
        }
        catch (Exception ex)
        {
            ShowError($"Recording failed: {ex.Message}");
        }
    }

    private void StopLiveRecording()
    {
        if (!_isRecordingLocal || Coordinator is null || Coordinator.CurrentScene is null)
        {
            return;
        }

        try
        {
            var take = Coordinator.StopRecording();
            _isRecordingLocal = false;

            if (_audioMeterBar is not null)
            {
                _audioMeterBar.Visible = false;
            }

            if (_recordingProgressHBox is not null)
            {
                _recordingProgressHBox.Visible = false;
            }

            if (_previewTakeButton is not null)
            {
                _previewTakeButton.Visible = true;
            }

            if (_lobbyManager is not null && _lobbyManager.IsConnectedToLobby && !string.IsNullOrEmpty(take.AudioRelativePath))
            {
                try
                {
                    var globalPath = ProjectSettings.GlobalizePath(take.AudioRelativePath);
                    if (global::Godot.FileAccess.FileExists(take.AudioRelativePath) || System.IO.File.Exists(globalPath))
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

            var voiceSlots = Coordinator.CurrentScene.VoiceSlots;
            if (_currentSlotIndex < voiceSlots.Count - 1)
            {
                _currentSlotIndex++;
            }

            UpdateUiState();
        }
        catch (Exception ex)
        {
            _isRecordingLocal = false;
            ShowError($"Failed to save take: {ex.Message}");
        }
    }

    private void OnPreviewTakePressed()
    {
        if (Coordinator?.CurrentScene is null || _previewPlayer is null)
        {
            return;
        }

        var voiceSlots = Coordinator.CurrentScene.VoiceSlots;
        var prevSlotIdx = Math.Clamp(_currentSlotIndex > 0 ? _currentSlotIndex - 1 : _currentSlotIndex, 0, voiceSlots.Count - 1);
        var slot = voiceSlots[prevSlotIdx];

        var take = Coordinator.TakeStore.GetLatestTakeForSlot(slot.VoiceSlotId);
        if (take is null || string.IsNullOrWhiteSpace(take.AudioRelativePath))
        {
            ShowError("No take recorded yet for preview.");
            return;
        }

        try
        {
            var globalPath = ProjectSettings.GlobalizePath(take.AudioRelativePath);
            byte[]? bytes = null;

            if (global::Godot.FileAccess.FileExists(take.AudioRelativePath))
            {
                bytes = global::Godot.FileAccess.GetFileAsBytes(take.AudioRelativePath);
            }
            else if (System.IO.File.Exists(globalPath))
            {
                bytes = System.IO.File.ReadAllBytes(globalPath);
            }

            if (bytes is not null && bytes.Length > 44)
            {
                var wav = new AudioStreamWav
                {
                    Data = bytes[44..],
                    Format = AudioStreamWav.FormatEnum.Format16Bits,
                    MixRate = 44100
                };
                _previewPlayer.Stream = wav;
                _previewPlayer.Play();

                if (_statusLabel is not null)
                {
                    _statusLabel.Text = "🔊 Playing back your recorded take...";
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"Preview failed: {ex.Message}");
        }
    }

    private void OnRemoteAudioTakeReceived(string voiceSlotId, string senderName, byte[] audioData)
    {
        if (Coordinator is null || Coordinator.ActiveRound is null || audioData.Length == 0)
        {
            return;
        }

        try
        {
            var fileName = $"user://takes/remote_{voiceSlotId}_{Guid.NewGuid():N}.wav";
            using (var file = global::Godot.FileAccess.Open(fileName, global::Godot.FileAccess.ModeFlags.Write))
            {
                file?.StoreBuffer(audioData);
            }

            var takeId = $"take-net-{voiceSlotId}-{Guid.NewGuid():N}";
            var assignment = Coordinator.ActiveRound.GetVoiceSlotAssignments()
                .FirstOrDefault(a => a.VoiceSlotId == voiceSlotId);

            var playerId = assignment?.PlayerId ?? senderName;
            var characterId = assignment?.CharacterId ?? "unknown";

            var remoteTake = new VoiceTake(
                takeId,
                voiceSlotId,
                playerId,
                characterId,
                Coordinator.ActiveRound.RoundId,
                fileName,
                durationMilliseconds: 3000,
                DateTimeOffset.UtcNow);

            Coordinator.TakeStore.AddTake(remoteTake);

            if (!Coordinator.ActiveRound.RecordedVoiceSlotIds.Contains(voiceSlotId))
            {
                Coordinator.ActiveRound.MarkVoiceSlotRecorded(voiceSlotId);
            }

            UpdateUiState();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to process incoming audio take: {ex.Message}");
        }
    }

    private void OnReRecordButtonPressed()
    {
        if (_currentSlotIndex > 0)
        {
            _currentSlotIndex--;
            UpdateUiState();
        }
    }

    private void OnProceedPressed()
    {
        if (Coordinator?.ActiveRound is null)
        {
            ShowError("No active round.");
            return;
        }

        if (Coordinator.ActiveRound.Phase != DubbedUp.Core.Rounds.RoundPhase.ReadyForPlayback &&
            Coordinator.ActiveRound.Phase != DubbedUp.Core.Rounds.RoundPhase.Playing)
        {
            var scene = Coordinator.CurrentScene;
            if (scene is not null)
            {
                foreach (var slot in scene.VoiceSlots)
                {
                    if (!Coordinator.ActiveRound.RecordedVoiceSlotIds.Contains(slot.VoiceSlotId))
                    {
                        var dummyTake = new VoiceTake(
                            $"take-skip-{slot.VoiceSlotId}-{Guid.NewGuid():N}",
                            slot.VoiceSlotId,
                            "player-1",
                            slot.CharacterId,
                            Coordinator.ActiveRound.RoundId,
                            "user://recordings/silence.wav",
                            1000,
                            DateTimeOffset.UtcNow);
                        Coordinator.TakeStore.AddTake(dummyTake);
                        Coordinator.ActiveRound.MarkVoiceSlotRecorded(slot.VoiceSlotId);
                    }
                }
            }
        }

        Navigator?.NavigateTo(AppScreen.Playback);
    }

    private void OnCancelPressed()
    {
        if (_isRecordingLocal)
        {
            try { Coordinator?.VoiceRecorder.CancelRecording(); } catch { }
            _isRecordingLocal = false;
        }
        Coordinator?.ResetSession();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }

    private void UpdateUiState()
    {
        if (Coordinator?.CurrentScene is null || Coordinator.ActiveRound is null)
        {
            return;
        }

        var voiceSlots = Coordinator.CurrentScene.VoiceSlots;
        var assignments = Coordinator.ActiveRound.GetVoiceSlotAssignments();

        if (_currentSlotIndex >= voiceSlots.Count)
        {
            _currentSlotIndex = voiceSlots.Count - 1;
        }

        var currentSlot = voiceSlots[_currentSlotIndex];
        var assignment = assignments.FirstOrDefault(a => a.VoiceSlotId == currentSlot.VoiceSlotId);

        var playerObj = Coordinator.CurrentSession?.Players.FirstOrDefault(p => p.PlayerId == assignment?.PlayerId);
        var playerName = playerObj?.DisplayName ?? assignment?.PlayerId ?? "Player";

        var charDef = Coordinator.CurrentScene.Characters.FirstOrDefault(c => c.CharacterId == currentSlot.CharacterId);
        var charName = charDef?.DisplayName ?? currentSlot.CharacterId;

        var isRecorded = Coordinator.ActiveRound.RecordedVoiceSlotIds.Contains(currentSlot.VoiceSlotId);

        if (_slotInfoLabel is not null)
        {
            _slotInfoLabel.Text = $"Slot {_currentSlotIndex + 1} of {voiceSlots.Count}:\n🎭 Character: {charName}  (🎙 {playerName})\n💬 Prompt: \"{currentSlot.Prompt}\"";
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = isRecorded
                ? $"✅ Replik kaydedildi! (Dilersen dinleyebilir, tekrar kaydedebilir veya sonraki repliğe geçebilirsin)"
                : $"Hazır! Kaydı başlatmak için butona bas.";
        }

        if (_recordButton is not null)
        {
            _recordButton.Text = isRecorded ? "🔄 Re-record This Slot" : "🎙 Start Recording";
        }

        if (_previewTakeButton is not null)
        {
            _previewTakeButton.Visible = isRecorded;
        }

        if (_reRecordButton is not null)
        {
            _reRecordButton.Disabled = _currentSlotIndex == 0;
        }

        if (_proceedButton is not null)
        {
            var allRecorded = Coordinator.ActiveRound.Phase == DubbedUp.Core.Rounds.RoundPhase.ReadyForPlayback;
            _proceedButton.Text = allRecorded
                ? "🎬 Complete Recording (Watch Dubbed Video!)"
                : "Skip & Proceed to Playback";
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
}
