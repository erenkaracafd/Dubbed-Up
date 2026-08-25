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
    private double _durationSeconds = 10.0;
    private bool _isPlaying = false;
    private bool _hasFinished = false;
    private string? _sceneFolderPath;

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
                Bus = "RecordSink",
                VolumeDb = -80.0f,
            };
            AddChild(_videoPlayer);
        }
        else
        {
            _videoPlayer.Bus = "RecordSink";
            _videoPlayer.VolumeDb = -80.0f;
            _videoPlayer.Expand = true;
        }
    }

    public void LoadScene(OfficialSceneDocument scene, DubProjectDocument? project, VoiceTakeStore? takeStore, string? sceneFolderPath = null)
    {
        ClearTakePlayers();

        _durationSeconds = scene.DurationMilliseconds / 1000.0;
        _masterTimeSeconds = 0.0;
        _hasFinished = false;
        _sceneFolderPath = sceneFolderPath;

        // Try to load video
        var videoAsset = scene.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
        if (videoAsset is not null)
        {
            TryLoadVideo(videoAsset.RelativePath, sceneFolderPath);
        }

        // Schedule take audio players
        foreach (var entry in scene.Timeline)
        {
            var voiceSlotId = entry.VoiceSlotId;
            var takeId = project?.SelectedTakes?.FirstOrDefault(t => t.VoiceSlotId == voiceSlotId)?.TakeId;
            var take = takeId is not null
                ? takeStore?.GetTake(takeId)
                : takeStore?.GetLatestTakeForSlot(voiceSlotId);

            var audioPath = take?.AudioRelativePath ?? string.Empty;
            var startSec = entry.StartMilliseconds / 1000.0;
            var endSec = entry.EndMilliseconds / 1000.0;

            var player = new VoiceTakeAudioPlayer(voiceSlotId, take?.TakeId ?? "missing", startSec, endSec, audioPath, this);
            _takePlayers.Add(player);
        }
    }

    private void TryLoadVideo(string relativePath, string? sceneFolderPath)
    {
        if (_videoPlayer is null) return;

        // 1. Try res:// path directly
        if (ResourceLoader.Exists(relativePath))
        {
            _videoPlayer.Stream = GD.Load<VideoStream>(relativePath);
            GD.Print($"[VideoPlayer] Loaded via res://: {relativePath}");
            return;
        }

        // 2. Try absolute path from scene folder
        if (!string.IsNullOrEmpty(sceneFolderPath))
        {
            var absolutePath = System.IO.Path.Combine(sceneFolderPath, relativePath);
            absolutePath = System.IO.Path.GetFullPath(absolutePath);

            if (System.IO.File.Exists(absolutePath))
            {
                try
                {
                    // Godot 4 supports file:// absolute paths for VideoStreamPlayer in some builds
                    // Use a GodotFileAccess load approach
                    var fileUri = absolutePath.Replace("\\", "/");

                    // Try with res:// via ProjectSettings globalize approach
                    var resPath = ProjectSettings.LocalizePath(absolutePath);
                    if (!string.IsNullOrEmpty(resPath) && ResourceLoader.Exists(resPath))
                    {
                        _videoPlayer.Stream = GD.Load<VideoStream>(resPath);
                        GD.Print($"[VideoPlayer] Loaded via localized path: {resPath}");
                        return;
                    }

                    var theora = new VideoStreamTheora();
                    theora.File = !string.IsNullOrEmpty(resPath) ? resPath : absolutePath.Replace("\\", "/");
                    _videoPlayer.Stream = theora;
                    GD.Print($"[VideoPlayer] Loaded via VideoStreamTheora: {theora.File}");
                    return;
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[VideoPlayer] Failed to load external video: {ex.Message}");
                }
                return;
            }
        }

        // 3. Try GlobalizePath fallback
        var globalPath = ProjectSettings.GlobalizePath(relativePath);
        if (!string.IsNullOrEmpty(globalPath) && System.IO.File.Exists(globalPath))
        {
            var localized = ProjectSettings.LocalizePath(globalPath);
            if (!string.IsNullOrEmpty(localized) && ResourceLoader.Exists(localized))
            {
                _videoPlayer.Stream = GD.Load<VideoStream>(localized);
                GD.Print($"[VideoPlayer] Loaded via globalized path: {localized}");
                return;
            }

            var theora = new VideoStreamTheora();
            theora.File = globalPath.Replace("\\", "/");
            _videoPlayer.Stream = theora;
            GD.Print($"[VideoPlayer] Loaded via globalized VideoStreamTheora: {globalPath}");
            return;
        }

        GD.Print($"[VideoPlayer] No video stream found for path: '{relativePath}'. Playing audio-only mode.");
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
            _videoPlayer.Bus = "RecordSink";
            _videoPlayer.VolumeDb = -80.0f;
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

        if (_videoPlayer is not null && _videoPlayer.Stream is not null && _videoPlayer.IsPlaying())
        {
            _masterTimeSeconds = _videoPlayer.GetStreamPosition();
        }
        else
        {
            _masterTimeSeconds += delta;
        }

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
