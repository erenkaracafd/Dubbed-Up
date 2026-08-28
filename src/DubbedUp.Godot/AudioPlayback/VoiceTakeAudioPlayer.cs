using Godot;

namespace DubbedUp.Godot.AudioPlayback;

public sealed class VoiceTakeAudioPlayer
{
    private readonly AudioStreamPlayer _audioPlayer;
    private bool _hasStartedForCurrentPass = false;

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

        _audioPlayer = new AudioStreamPlayer { Bus = "Master" };
        parentNode.AddChild(_audioPlayer);
        LoadAudioStream(audioPath);
    }

    public string VoiceSlotId { get; }
    public string TakeId { get; }
    public double StartSeconds { get; }
    public double EndSeconds { get; }
    public string AudioPath { get; }
    public bool IsActive => _audioPlayer.Playing;

    public void SyncWithMasterTime(double masterTimeSeconds)
    {
        if (masterTimeSeconds < StartSeconds - 0.1)
        {
            if (_audioPlayer.Playing) _audioPlayer.Stop();
            _hasStartedForCurrentPass = false;
            return;
        }

        // Trigger playback smoothly when master time enters the slot
        if (masterTimeSeconds >= StartSeconds && masterTimeSeconds <= EndSeconds + 0.5)
        {
            if (!_hasStartedForCurrentPass && !_audioPlayer.Playing && _audioPlayer.Stream is not null)
            {
                var latencyOffset = (float)Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds;
                var startOffset = (float)((masterTimeSeconds - StartSeconds) + latencyOffset);
                startOffset = Math.Max(0.0f, startOffset);

                _audioPlayer.Play(startOffset);
                _hasStartedForCurrentPass = true;
            }
            return;
        }

        // After slot duration + 0.6s grace period, if still playing, let it stop cleanly
        if (masterTimeSeconds > EndSeconds + 0.6)
        {
            if (_audioPlayer.Playing)
            {
                _audioPlayer.Stop();
            }
        }
    }

    public void OnManualSeek(double masterTimeSeconds)
    {
        _hasStartedForCurrentPass = false;
        if (_audioPlayer.Playing) _audioPlayer.Stop();

        if (masterTimeSeconds >= StartSeconds && masterTimeSeconds <= EndSeconds + 0.5)
        {
            if (_audioPlayer.Stream is not null)
            {
                var latencyOffset = (float)Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds;
                var startOffset = (float)((masterTimeSeconds - StartSeconds) + latencyOffset);
                startOffset = Math.Max(0.0f, startOffset);

                _audioPlayer.Play(startOffset);
                _hasStartedForCurrentPass = true;
            }
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
        _audioPlayer.Stop();
        _audioPlayer.StreamPaused = false;
        _hasStartedForCurrentPass = false;
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
                var parsedWav = ParseWavBytes(bytes);
                if (parsedWav is not null)
                {
                    _audioPlayer.Stream = parsedWav;
                    return;
                }
            }

            // Fallback to ResourceLoader
            if (ResourceLoader.Exists(path))
            {
                _audioPlayer.Stream = GD.Load<AudioStream>(path);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[VoiceTakeAudioPlayer] Failed to load audio stream from '{path}': {ex.Message}");
        }
    }

    public static AudioStreamWav? ParseWavBytes(byte[] wavBytes)
    {
        if (wavBytes.Length < 44) return null;

        // Check RIFF header
        if (wavBytes[0] != 'R' || wavBytes[1] != 'I' || wavBytes[2] != 'F' || wavBytes[3] != 'F' ||
            wavBytes[8] != 'W' || wavBytes[9] != 'A' || wavBytes[10] != 'V' || wavBytes[11] != 'E')
        {
            return null;
        }

        int pos = 12;
        int channels = 1;
        int sampleRate = 44100;
        int bitsPerSample = 16;
        int audioFormat = 1; // 1 = PCM, 3 = IEEE float
        byte[]? pcmData = null;

        while (pos + 8 <= wavBytes.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(wavBytes, pos, 4);
            var chunkSize = BitConverter.ToInt32(wavBytes, pos + 4);
            pos += 8;

            if (chunkSize < 0 || pos + chunkSize > wavBytes.Length)
            {
                break;
            }

            if (chunkId == "fmt ")
            {
                if (chunkSize >= 16)
                {
                    audioFormat = BitConverter.ToInt16(wavBytes, pos);
                    channels = BitConverter.ToInt16(wavBytes, pos + 2);
                    sampleRate = BitConverter.ToInt32(wavBytes, pos + 4);
                    bitsPerSample = BitConverter.ToInt16(wavBytes, pos + 14);
                }
            }
            else if (chunkId == "data")
            {
                pcmData = new byte[chunkSize];
                Array.Copy(wavBytes, pos, pcmData, 0, chunkSize);
            }

            pos += chunkSize;
            // Chunks must be word-aligned (even byte boundary)
            if ((chunkSize & 1) != 0) pos++;
        }

        if (pcmData is null || pcmData.Length == 0) return null;

        var wav = new AudioStreamWav
        {
            Data = pcmData,
            MixRate = sampleRate,
            Stereo = channels >= 2
        };

        if (bitsPerSample == 8)
        {
            wav.Format = AudioStreamWav.FormatEnum.Format8Bits;
        }
        else if (bitsPerSample == 16)
        {
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
        }
        else if (bitsPerSample == 32 || audioFormat == 3)
        {
            // Convert 32-bit float or 32-bit int to 16-bit PCM for Godot 4 compatibility
            var sampleCount = pcmData.Length / 4;
            var pcm16 = new byte[sampleCount * 2];

            if (audioFormat == 3) // Float32
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    var f = BitConverter.ToSingle(pcmData, i * 4);
                    var s = (short)Math.Clamp(f * 32767.0f, -32768.0f, 32767.0f);
                    pcm16[i * 2] = (byte)(s & 0xFF);
                    pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                }
            }
            else // Int32
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    var val = BitConverter.ToInt32(pcmData, i * 4);
                    var s = (short)(val >> 16);
                    pcm16[i * 2] = (byte)(s & 0xFF);
                    pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                }
            }

            wav.Data = pcm16;
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
        }
        else
        {
            wav.Format = AudioStreamWav.FormatEnum.Format16Bits;
        }

        return wav;
    }
}
