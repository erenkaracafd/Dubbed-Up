using Godot;

namespace DubbedUp.Godot.AudioPlayback;

public sealed class VoiceTakeAudioPlayer
{
    private const double DriftThresholdSeconds = 0.05;

    private readonly AudioStreamPlayer _audioPlayer;

    public VoiceTakeAudioPlayer(
        string voiceSlotId,
        string takeId,
        double startSeconds,
        double endSeconds,
        string audioPath,
        Node parentNode)
    {
        VoiceSlotId = voiceSlotId;
        TakeId = takeId;
        StartSeconds = startSeconds;
        EndSeconds = endSeconds;
        AudioPath = audioPath;

        _audioPlayer = new AudioStreamPlayer();
        parentNode.AddChild(_audioPlayer);
        LoadAudioStream(audioPath);
    }

    public string VoiceSlotId { get; }
    public string TakeId { get; }
    public double StartSeconds { get; }
    public double EndSeconds { get; }
    public string AudioPath { get; }
    public bool IsActive { get; private set; }

    public void SyncWithMasterTime(double masterTimeSeconds)
    {
        if (masterTimeSeconds < StartSeconds)
        {
            if (_audioPlayer.Playing) _audioPlayer.Stop();
            IsActive = false;
            return;
        }

        if (masterTimeSeconds >= StartSeconds && masterTimeSeconds <= EndSeconds)
        {
            var latencyOffset = (float)Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds;
            var expectedAudioOffset = (float)(masterTimeSeconds - StartSeconds) + latencyOffset;

            if (!_audioPlayer.Playing)
            {
                if (_audioPlayer.Stream is not null)
                {
                    _audioPlayer.Play(expectedAudioOffset);
                }
                IsActive = true;
            }
            else
            {
                var actualPos = _audioPlayer.GetPlaybackPosition();
                if (Math.Abs(actualPos - expectedAudioOffset) > DriftThresholdSeconds)
                {
                    _audioPlayer.Seek(expectedAudioOffset);
                }
                IsActive = true;
            }
            return;
        }

        if (masterTimeSeconds > EndSeconds)
        {
            if (_audioPlayer.Playing) _audioPlayer.Stop();
            IsActive = false;
        }
    }

    public void Pause()
    {
        if (_audioPlayer.Playing)
        {
            _audioPlayer.StreamPaused = true;
        }
    }

    public void Resume()
    {
        if (_audioPlayer.StreamPaused)
        {
            _audioPlayer.StreamPaused = false;
        }
    }

    public void Stop()
    {
        if (_audioPlayer.Playing)
        {
            _audioPlayer.Stop();
        }
        _audioPlayer.StreamPaused = false;
        IsActive = false;
    }

    public void Dispose()
    {
        Stop();
        if (GodotObject.IsInstanceValid(_audioPlayer))
        {
            _audioPlayer.QueueFree();
        }
    }

    private void LoadAudioStream(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            // Try to load from disk as raw bytes and parse WAV header properly
            byte[]? bytes = null;

            if (global::Godot.FileAccess.FileExists(path))
            {
                bytes = global::Godot.FileAccess.GetFileAsBytes(path);
            }
            else
            {
                var globalPath = ProjectSettings.GlobalizePath(path);
                if (System.IO.File.Exists(globalPath))
                {
                    bytes = System.IO.File.ReadAllBytes(globalPath);
                }
                else if (System.IO.File.Exists(path))
                {
                    bytes = System.IO.File.ReadAllBytes(path);
                }
            }

            if (bytes is not null && bytes.Length > 44)
            {
                var wav = ParseWavBytes(bytes);
                if (wav is not null)
                {
                    _audioPlayer.Stream = wav;
                    return;
                }
            }

            // Fallback: try Godot ResourceLoader (for res:// paths)
            if (ResourceLoader.Exists(path))
            {
                var stream = GD.Load<AudioStream>(path);
                if (stream is not null)
                {
                    _audioPlayer.Stream = stream;
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[AudioPlayer] Failed to load '{path}': {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a WAV byte array by reading the actual header fields (not fixed offset).
    /// Handles standard PCM WAV files (fmt chunk first).
    /// </summary>
    public static AudioStreamWav? ParseWavBytes(byte[] bytes)
    {
        try
        {
            // Verify RIFF header
            if (bytes.Length < 12) return null;
            var riff = System.Text.Encoding.ASCII.GetString(bytes, 0, 4);
            var wave = System.Text.Encoding.ASCII.GetString(bytes, 8, 4);
            if (riff != "RIFF" || wave != "WAVE") return null;

            // Walk chunks
            int pos = 12;
            int sampleRate = 44100;
            short channels = 1;
            short bitsPerSample = 16;
            byte[]? pcmData = null;

            while (pos + 8 <= bytes.Length)
            {
                var chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                var chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                pos += 8;

                if (chunkId == "fmt ")
                {
                    // audioFormat (2), numChannels (2), sampleRate (4), byteRate (4), blockAlign (2), bitsPerSample (2)
                    channels = BitConverter.ToInt16(bytes, pos + 2);
                    sampleRate = BitConverter.ToInt32(bytes, pos + 4);
                    bitsPerSample = BitConverter.ToInt16(bytes, pos + 14);
                }
                else if (chunkId == "data")
                {
                    var dataLen = Math.Min(chunkSize, bytes.Length - pos);
                    pcmData = new byte[dataLen];
                    Array.Copy(bytes, pos, pcmData, 0, dataLen);
                }

                pos += chunkSize;
                if (chunkSize % 2 != 0) pos++; // WAV chunk alignment padding
            }

            if (pcmData is null || pcmData.Length == 0) return null;

            var format = bitsPerSample switch
            {
                8 => AudioStreamWav.FormatEnum.Format8Bits,
                16 => AudioStreamWav.FormatEnum.Format16Bits,
                _ => AudioStreamWav.FormatEnum.Format16Bits,
            };

            return new AudioStreamWav
            {
                Data = pcmData,
                Format = format,
                MixRate = sampleRate,
                Stereo = channels > 1
            };
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[AudioPlayer] WAV parse failed: {ex.Message}");
            return null;
        }
    }
}
