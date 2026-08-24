using DubbedUp.Core.VoiceTakes;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.Network;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class RecordingScreen : BaseScreen
{
    private Label? _statusLabel;
    private Label? _slotInfoLabel;
    private Label? _errorLabel;
    private ProgressBar? _audioMeterBar;
    private Button? _recordButton;
    private Button? _reRecordButton;
    private Button? _proceedButton;
    private Button? _cancelButton;

    private NetworkLobbyManager? _lobbyManager;
    private int _currentSlotIndex = 0;
    private bool _isRecordingLocal = false;
    private double _meterAnimationTime = 0.0;

    public override void _Ready()
    {
        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StatusLabel");
        _slotInfoLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SlotInfoLabel");
        _errorLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");
        _audioMeterBar = GetNodeOrNull<ProgressBar>("CenterContainer/VBoxContainer/AudioMeterBar");
        _recordButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/RecordButton");
        _reRecordButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ReRecordButton");
        _proceedButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ProceedButton");
        _cancelButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/CancelButton");

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
        if (_isRecordingLocal && _audioMeterBar is not null)
        {
            _meterAnimationTime += delta * 6.0;
            // Simulated dynamic VU meter activity
            var level = 45.0 + Math.Sin(_meterAnimationTime) * 35.0 + Math.Cos(_meterAnimationTime * 1.7) * 15.0;
            _audioMeterBar.Value = Math.Clamp(level, 10.0, 95.0);
        }
    }

    private void OnRecordButtonPressed()
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Visible = false;
        }

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

        if (!_isRecordingLocal)
        {
            try
            {
                Coordinator.StartRecording(currentSlot.VoiceSlotId);
                _isRecordingLocal = true;
                _meterAnimationTime = 0.0;
                if (_audioMeterBar is not null)
                {
                    _audioMeterBar.Visible = true;
                }
                if (_recordButton is not null)
                {
                    _recordButton.Text = "⏹ Stop Recording";
                }
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = "🔴 Status: Recording live audio...";
                }
            }
            catch (Exception ex)
            {
                ShowError($"Recording failed: {ex.Message}");
            }
        }
        else
        {
            try
            {
                var take = Coordinator.StopRecording();
                _isRecordingLocal = false;
                if (_audioMeterBar is not null)
                {
                    _audioMeterBar.Visible = false;
                }

                // If in multiplayer, broadcast the recorded audio file to peers
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
                        GD.PrintErr($"Failed to broadcast audio take over network: {ex.Message}");
                    }
                }

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
            GD.PrintErr($"Failed to process incoming remote audio take: {ex.Message}");
        }
    }

    private void OnReRecordButtonPressed()
    {
        if (_currentSlotIndex > 0)
        {
            _currentSlotIndex--;
        }
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        if (Coordinator is null || Coordinator.CurrentScene is null || Coordinator.ActiveRound is null)
        {
            if (_slotInfoLabel is not null)
            {
                _slotInfoLabel.Text = "No active session loaded.";
            }
            return;
        }

        var scene = Coordinator.CurrentScene;
        var voiceSlots = scene.VoiceSlots;
        if (voiceSlots.Count == 0)
        {
            return;
        }

        var currentSlot = voiceSlots[Math.Clamp(_currentSlotIndex, 0, voiceSlots.Count - 1)];
        var charDef = scene.Characters.FirstOrDefault(c => c.CharacterId == currentSlot.CharacterId);
        var charName = charDef?.DisplayName ?? currentSlot.CharacterId;

        var assignment = Coordinator.ActiveRound.GetVoiceSlotAssignments()
            .FirstOrDefault(a => a.VoiceSlotId == currentSlot.VoiceSlotId);
        var player = Coordinator.CurrentSession?.Players.FirstOrDefault(p => p.PlayerId == assignment?.PlayerId);
        var playerName = player?.DisplayName ?? assignment?.PlayerId ?? "Unassigned";

        var isRecorded = Coordinator.TakeStore.HasTakeForSlot(currentSlot.VoiceSlotId);

        if (_slotInfoLabel is not null)
        {
            _slotInfoLabel.Text = $"Slot {_currentSlotIndex + 1}/{voiceSlots.Count}: {charName} ({playerName})\n\"{currentSlot.Prompt}\"";
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = isRecorded
                ? $"Status: Take recorded ({Coordinator.TakeStore.GetLatestTakeForSlot(currentSlot.VoiceSlotId)?.TakeId})"
                : "Status: Ready to record";
        }

        if (_recordButton is not null)
        {
            _recordButton.Text = isRecorded ? "🎙 Record Again" : "🎙 Start Recording";
        }

        var allRecorded = voiceSlots.All(slot => Coordinator.TakeStore.HasTakeForSlot(slot.VoiceSlotId));
        if (_proceedButton is not null)
        {
            _proceedButton.Disabled = !allRecorded;
            _proceedButton.Text = allRecorded
                ? "Complete Recording (Proceed to Playback)"
                : $"Record all slots to proceed ({Coordinator.TakeStore.GetAllTakes().Count}/{voiceSlots.Count})";
        }
    }

    private void ShowError(string message)
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Text = message;
            _errorLabel.Visible = true;
        }
        if (_recordButton is not null)
        {
            _recordButton.Text = "Retry Recording";
        }
    }

    private void OnProceedPressed()
    {
        try
        {
            Coordinator?.FinishRecording();
            Navigator?.NavigateTo(AppScreen.Playback);
        }
        catch (Exception ex)
        {
            ShowError($"Cannot proceed: {ex.Message}");
        }
    }

    private void OnCancelPressed()
    {
        if (_isRecordingLocal)
        {
            Coordinator?.VoiceRecorder.CancelRecording();
            _isRecordingLocal = false;
        }
        Coordinator?.ResetSession();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
