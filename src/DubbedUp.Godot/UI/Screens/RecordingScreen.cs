using DubbedUp.Core.VoiceTakes;
using DubbedUp.Godot.Microphone;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class RecordingScreen : BaseScreen
{
    private readonly IVoiceRecorder _recorder = new GodotVoiceRecorder();
    private readonly VoiceTakeStore _takeStore = new();

    private Label? _statusLabel;
    private Label? _slotInfoLabel;
    private Label? _errorLabel;
    private Button? _recordButton;
    private Button? _reRecordButton;
    private Button? _proceedButton;
    private Button? _cancelButton;

    private int _currentSlotIndex = 0;
    private readonly string[] _mockSlots = ["guard-line-1", "thief-line-1"];
    private readonly string[] _mockCharacters = ["Museum Guard", "Sneaky Thief"];
    private readonly string[] _mockPlayers = ["Player 1", "Player 2"];
    private readonly string[] _mockPrompts = ["React to the suspicious statue!", "Whisper your escape plan."];

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

        if (!_recorder.IsRecording)
        {
            try
            {
                var slotId = _mockSlots[_currentSlotIndex];
                var charId = _mockCharacters[_currentSlotIndex];
                var playerId = _mockPlayers[_currentSlotIndex];

                _recorder.StartRecording(slotId, playerId, charId, "round-1");
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
                var take = _recorder.StopRecording();
                _takeStore.AddTake(take);

                if (_currentSlotIndex < _mockSlots.Length - 1)
                {
                    _currentSlotIndex++;
                }

                UpdateUiState();
            }
            catch (Exception ex)
            {
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
        var slotId = _mockSlots[_currentSlotIndex];
        var charName = _mockCharacters[_currentSlotIndex];
        var playerName = _mockPlayers[_currentSlotIndex];
        var prompt = _mockPrompts[_currentSlotIndex];
        var isRecorded = _takeStore.HasTakeForSlot(slotId);

        if (_slotInfoLabel is not null)
        {
            _slotInfoLabel.Text = $"Slot {_currentSlotIndex + 1}/{_mockSlots.Length}: {charName} ({playerName})\n\"{prompt}\"";
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = isRecorded ? $"Status: Take recorded ({_takeStore.GetLatestTakeForSlot(slotId)?.TakeId})" : "Status: Ready to record";
        }

        if (_recordButton is not null)
        {
            _recordButton.Text = isRecorded ? "Record Again" : "Start Recording";
        }

        var allRecorded = _mockSlots.All(slot => _takeStore.HasTakeForSlot(slot));
        if (_proceedButton is not null)
        {
            _proceedButton.Disabled = !allRecorded;
            _proceedButton.Text = allRecorded
                ? "Complete Recording (Proceed to Playback)"
                : $"Record all slots to proceed ({_takeStore.GetAllTakes().Count}/{_mockSlots.Length})";
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
        Navigator?.NavigateTo(AppScreen.Playback);
    }

    private void OnCancelPressed()
    {
        if (_recorder.IsRecording)
        {
            _recorder.CancelRecording();
        }
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
