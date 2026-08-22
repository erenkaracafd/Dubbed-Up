using System.Diagnostics;
using DubbedUp.Core.VoiceTakes;
using Godot;

namespace DubbedUp.Godot.Microphone;

public sealed class GodotVoiceRecorder : IVoiceRecorder
{
    private readonly Stopwatch _stopwatch = new();
    private string? _currentVoiceSlotId;
    private string? _currentPlayerId;
    private string? _currentCharacterId;
    private string? _currentRoundId;
    private string? _currentTakeId;

    public bool IsRecording { get; private set; }

    public void StartRecording(string voiceSlotId, string playerId, string characterId, string roundId)
    {
        if (IsRecording)
        {
            throw new RecordingException("A recording is already in progress.");
        }

        if (string.IsNullOrWhiteSpace(voiceSlotId))
        {
            throw new RecordingException("voiceSlotId cannot be empty.");
        }

        _currentVoiceSlotId = voiceSlotId;
        _currentPlayerId = playerId;
        _currentCharacterId = characterId;
        _currentRoundId = roundId;
        _currentTakeId = $"take-{Guid.NewGuid():N}";

        try
        {
            EnsureRecordingsDirectory();
            _stopwatch.Restart();
            IsRecording = true;
        }
        catch (Exception ex) when (ex is not RecordingException)
        {
            Reset();
            throw new RecordingException($"Failed to start microphone recording: {ex.Message}", ex);
        }
    }

    public VoiceTake StopRecording()
    {
        if (!IsRecording || _currentTakeId is null || _currentVoiceSlotId is null ||
            _currentPlayerId is null || _currentCharacterId is null || _currentRoundId is null)
        {
            throw new RecordingException("No active recording to stop.");
        }

        _stopwatch.Stop();
        var elapsedMs = (int)_stopwatch.ElapsedMilliseconds;
        var relativePath = $"recordings/{_currentTakeId}.wav";

        var take = new VoiceTake(
            takeId: _currentTakeId,
            voiceSlotId: _currentVoiceSlotId,
            playerId: _currentPlayerId,
            characterId: _currentCharacterId,
            roundId: _currentRoundId,
            audioRelativePath: relativePath,
            durationMilliseconds: elapsedMs,
            recordedAtUtc: DateTimeOffset.UtcNow);

        Reset();
        return take;
    }

    public void CancelRecording()
    {
        Reset();
    }

    private void Reset()
    {
        _stopwatch.Reset();
        IsRecording = false;
        _currentVoiceSlotId = null;
        _currentPlayerId = null;
        _currentCharacterId = null;
        _currentRoundId = null;
        _currentTakeId = null;
    }

    private static void EnsureRecordingsDirectory()
    {
        var dir = DirAccess.Open("user://");
        if (dir is not null && !dir.DirExists("recordings"))
        {
            dir.MakeDir("recordings");
        }
    }
}

