using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.VoiceTakes;
using DubbedUp.Godot.AudioPlayback;
using Godot;

namespace DubbedUp.Godot.VideoPlayback;

public partial class SynchronizedScenePlayer : Control, IMediaPlayer
{
    private readonly List<VoiceTakeAudioPlayer> _takePlayers = [];
    private VideoStreamPlayer? _videoPlayer;
    private double _masterTimeSeconds = 0.0;
    private double _durationSeconds = 10.0; // default fallback 10s
    private bool _isPlaying = false;
    private bool _hasFinished = false;

    public bool IsPlaying => _isPlaying;

    public double CurrentTimeSeconds => _masterTimeSeconds;

    public double DurationSeconds => _durationSeconds;

    public event Action? PlaybackFinished;

    public event Action<double, double>? PlaybackProgress;

    public override void _Ready()
    {
        _videoPlayer = GetNodeOrNull<VideoStreamPlayer>("VideoStreamPlayer");
        if (_videoPlayer is null)
        {
            _videoPlayer = new VideoStreamPlayer
            {
                Expand = true,
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
            };
            AddChild(_videoPlayer);
        }
    }

    public void LoadScene(OfficialSceneDocument scene, DubProjectDocument? project, VoiceTakeStore? takeStore)
    {
        ClearTakePlayers();

        _durationSeconds = scene.DurationMilliseconds / 1000.0;
        _masterTimeSeconds = 0.0;
        _hasFinished = false;

        // Find scene video asset if available
        var videoAsset = scene.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
        if (videoAsset is not null && ResourceLoader.Exists(videoAsset.RelativePath))
        {
            var stream = GD.Load<VideoStream>(videoAsset.RelativePath);
            if (_videoPlayer is not null)
            {
                _videoPlayer.Stream = stream;
            }
        }

        // Schedule take audio players from timeline entries
        foreach (var entry in scene.Timeline)
        {
            var voiceSlotId = entry.VoiceSlotId;
            var takeId = project?.SelectedTakes?.FirstOrDefault(t => t.VoiceSlotId == voiceSlotId)?.TakeId;
            var take = takeId is not null ? takeStore?.GetTake(takeId) : takeStore?.GetLatestTakeForSlot(voiceSlotId);

            var audioPath = take?.AudioRelativePath ?? string.Empty;
            var startSec = entry.StartMilliseconds / 1000.0;
            var endSec = entry.EndMilliseconds / 1000.0;

            var player = new VoiceTakeAudioPlayer(voiceSlotId, take?.TakeId ?? "missing", startSec, endSec, audioPath, this);
            _takePlayers.Add(player);
        }
    }

    public void ScheduleTakes(IEnumerable<(string slotId, string takeId, double startSec, double endSec, string path)> takes)
    {
        ClearTakePlayers();
        foreach (var take in takes)
        {
            var player = new VoiceTakeAudioPlayer(take.slotId, take.takeId, take.startSec, take.endSec, take.path, this);
            _takePlayers.Add(player);
        }
    }

    public void SetDuration(double durationSeconds)
    {
        _durationSeconds = Math.Max(1.0, durationSeconds);
    }

    public void Play()
    {
        if (_hasFinished)
        {
            Restart();
            return;
        }

        _isPlaying = true;
        if (_videoPlayer is not null && _videoPlayer.Stream is not null && !_videoPlayer.IsPlaying())
        {
            _videoPlayer.Play();
            _videoPlayer.StreamPosition = _masterTimeSeconds;
        }

        foreach (var player in _takePlayers)
        {
            player.Resume();
        }
    }

    public void Pause()
    {
        _isPlaying = false;
        if (_videoPlayer is not null && _videoPlayer.IsPlaying())
        {
            _videoPlayer.Paused = true;
        }

        foreach (var player in _takePlayers)
        {
            player.Pause();
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        _masterTimeSeconds = 0.0;
        _hasFinished = false;

        if (_videoPlayer is not null)
        {
            _videoPlayer.Stop();
        }

        foreach (var player in _takePlayers)
        {
            player.Stop();
        }

        PlaybackProgress?.Invoke(0.0, _durationSeconds);
    }

    public void Restart()
    {
        Stop();
        Play();
    }

    public void Seek(double positionSeconds)
    {
        _masterTimeSeconds = Math.Clamp(positionSeconds, 0.0, _durationSeconds);
        _hasFinished = _masterTimeSeconds >= _durationSeconds;

        if (_videoPlayer is not null && _videoPlayer.Stream is not null)
        {
            _videoPlayer.StreamPosition = _masterTimeSeconds;
        }

        foreach (var player in _takePlayers)
        {
            player.SyncWithMasterTime(_masterTimeSeconds);
        }

        PlaybackProgress?.Invoke(_masterTimeSeconds, _durationSeconds);
    }

    public override void _Process(double delta)
    {
        if (!_isPlaying || _hasFinished)
        {
            return;
        }

        // Master clock synchronization
        if (_videoPlayer is not null && _videoPlayer.Stream is not null && _videoPlayer.IsPlaying())
        {
            _masterTimeSeconds = _videoPlayer.GetStreamPosition();
        }
        else
        {
            _masterTimeSeconds += delta;
        }

        // Sync all take audio players to avoid drift
        foreach (var player in _takePlayers)
        {
            player.SyncWithMasterTime(_masterTimeSeconds);
        }

        PlaybackProgress?.Invoke(_masterTimeSeconds, _durationSeconds);

        if (_masterTimeSeconds >= _durationSeconds)
        {
            _masterTimeSeconds = _durationSeconds;
            _isPlaying = false;
            _hasFinished = true;

            foreach (var player in _takePlayers)
            {
                player.Stop();
            }

            PlaybackFinished?.Invoke();
        }
    }

    private void ClearTakePlayers()
    {
        foreach (var player in _takePlayers)
        {
            player.Dispose();
        }
        _takePlayers.Clear();
    }
}
