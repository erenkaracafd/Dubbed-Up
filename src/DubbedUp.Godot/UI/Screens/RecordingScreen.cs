using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class RecordingScreen : BaseScreen
{
    private Label? _statusLabel;
    private Label? _slotInfoLabel;
    private Label? _errorLabel;
    private Button? _recordButton;
    private Button? _reRecordButton;
    private Button? _proceedButton;
    private Button? _cancelButton;

    private int _currentSlotIndex = 0;
    private bool _isRecordingLocal = false;

    public override void _Ready()
    {
        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StatusLabel");
        _slotInfoLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SlotInfoLabel");
        _errorLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");
        _recordButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/RecordButton");
        _reRecordButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ReRecordButton");
        _proceedButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ProceedButton");
        _cancelButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/CancelButton");

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
                if (_recordButton is not null)
                {
                    _recordButton.Text = "Stop Recording";
                }
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = "Status: Recording in progress...";
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
            _recordButton.Text = isRecorded ? "Record Again" : "Start Recording";
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
