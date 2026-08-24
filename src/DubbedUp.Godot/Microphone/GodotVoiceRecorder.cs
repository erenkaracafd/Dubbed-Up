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
            GodotLiveMicrophoneService.Instance.StartRecording();
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
        var relativePath = $"user://recordings/{_currentTakeId}.wav";
        var globalPath = ProjectSettings.GlobalizePath(relativePath);

        try
        {
            var sample = GodotLiveMicrophoneService.Instance.StopRecording();
            if (sample is not null)
            {
                sample.SaveToWav(globalPath);
            }
            else if (!System.IO.File.Exists(globalPath))
            {
                // Fallback: write a minimal valid WAV header so playback never fails
                WriteMinimalWavFile(globalPath);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[VoiceRecorder] Failed to save WAV file: {ex.Message}");
            if (!System.IO.File.Exists(globalPath))
            {
                WriteMinimalWavFile(globalPath);
            }
        }

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
        try
        {
            GodotLiveMicrophoneService.Instance.StopRecording();
        }
        catch
        {
            // Ignored on cancel
        }
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
        var targetDir = ProjectSettings.GlobalizePath("user://recordings");
        if (!System.IO.Directory.Exists(targetDir))
        {
            System.IO.Directory.CreateDirectory(targetDir);
        }
    }

    private static void WriteMinimalWavFile(string filePath)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            // Minimal 44-byte WAV header (44.1kHz, 16-bit mono, silence)
            byte[] header = [
                0x52, 0x49, 0x46, 0x46, // "RIFF"
                0x24, 0x00, 0x00, 0x00, // Chunk size (36)
                0x57, 0x41, 0x56, 0x45, // "WAVE"
                0x66, 0x6d, 0x74, 0x20, // "fmt "
                0x10, 0x00, 0x00, 0x00, // Subchunk1Size (16)
                0x01, 0x00,             // AudioFormat (PCM)
                0x01, 0x00,             // NumChannels (1)
                0x44, 0xac, 0x00, 0x00, // SampleRate (44100)
                0x88, 0x58, 0x01, 0x00, // ByteRate (88200)
                0x02, 0x00,             // BlockAlign (2)
                0x10, 0x00,             // BitsPerSample (16)
                0x64, 0x61, 0x74, 0x61, // "data"
                0x00, 0x00, 0x00, 0x00  // Subchunk2Size (0)
            ];
            System.IO.File.WriteAllBytes(filePath, header);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to create fallback WAV: {ex.Message}");
        }
    }
}
