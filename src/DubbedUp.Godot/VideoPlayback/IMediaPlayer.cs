namespace DubbedUp.Godot.VideoPlayback;

public interface IMediaPlayer
{
    bool IsPlaying { get; }

    double CurrentTimeSeconds { get; }

    double DurationSeconds { get; }

    void Play();

    void Pause();

    void Stop();

    void Restart();

    void Seek(double positionSeconds);

    event Action? PlaybackFinished;

    event Action<double, double>? PlaybackProgress;
}
