namespace DubbedUp.Core.VoiceTakes;

public interface IVoiceRecorder
{
    bool IsRecording { get; }

    void StartRecording(string voiceSlotId, string playerId, string characterId, string roundId);

    VoiceTake StopRecording();

    void CancelRecording();
}

