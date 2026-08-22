using Godot;

namespace DubbedUp.Godot.AudioPlayback;

public sealed class VoiceTakeAudioPlayer
{
    private const double DriftThresholdSeconds = 0.05; // 50ms drift tolerance

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
            if (_audioPlayer.Playing)
            {
                _audioPlayer.Stop();
            }
            IsActive = false;
            return;
        }

        if (masterTimeSeconds >= StartSeconds && masterTimeSeconds <= EndSeconds)
        {
            var expectedAudioOffset = (float)(masterTimeSeconds - StartSeconds);

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
                // Check and correct for timeline drift
                var actualAudioPos = _audioPlayer.GetPlaybackPosition();
                if (Math.Abs(actualAudioPos - expectedAudioOffset) > DriftThresholdSeconds)
                {
                    _audioPlayer.Seek(expectedAudioOffset);
                }
                IsActive = true;
            }
            return;
        }

        if (masterTimeSeconds > EndSeconds)
        {
            if (_audioPlayer.Playing)
            {
                _audioPlayer.Stop();
            }
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

        if (ResourceLoader.Exists(path))
        {
            var stream = GD.Load<AudioStream>(path);
            if (stream is not null)
            {
                _audioPlayer.Stream = stream;
            }
        }
    }
}
