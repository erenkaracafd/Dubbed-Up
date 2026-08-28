using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.VoiceTakes;
using DubbedUp.Godot.AudioPlayback;
using Godot;

namespace DubbedUp.Godot.VideoPlayback;

public partial class SynchronizedScenePlayer : Control
{
    public delegate void PlaybackProgressEventHandler(double currentTimeSeconds, double totalDurationSeconds);
    public event PlaybackProgressEventHandler? PlaybackProgress;

    public delegate void PlaybackFinishedEventHandler();
    public event PlaybackFinishedEventHandler? PlaybackFinished;

    private VideoStreamPlayer? _videoPlayer;
    private AudioStreamPlayer? _backgroundAudioPlayer;
    private AudioStreamPlayer? _originalAudioPlayer;
    private readonly List<VoiceTakeAudioPlayer> _takePlayers = [];
    private readonly List<(double StartSec, double EndSec)> _dubbedRanges = [];

    private double _masterTimeSeconds = 0.0;
    private double _durationSeconds = 1.0;
    private bool _isPlaying = false;
    private bool _hasFinished = false;
    private string? _sceneFolderPath;

    public double CurrentTimeSeconds => _masterTimeSeconds;
    public double TotalDurationSeconds => _durationSeconds;
    public bool IsPlaying => _isPlaying;
    public Texture2D? GetVideoTexture() => _videoPlayer?.GetVideoTexture();

    public override void _Ready()
    {
        _videoPlayer = GetNodeOrNull<VideoStreamPlayer>("VideoStreamPlayer");
        if (_videoPlayer is null)
        {
            _videoPlayer = new VideoStreamPlayer
            {
                Name = "VideoStreamPlayer",
                Expand = true,
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                Bus = "RecordSink", // Default muted
                VolumeDb = -80.0f,
            };
            AddChild(_videoPlayer);
        }

        _backgroundAudioPlayer = new AudioStreamPlayer
        {
            Name = "BackgroundAudioPlayer",
            Bus = "Master",
            VolumeDb = 0.0f,
        };
        AddChild(_backgroundAudioPlayer);

        _originalAudioPlayer = new AudioStreamPlayer
        {
            Name = "OriginalAudioPlayer",
            Bus = "Master",
            VolumeDb = 0.0f,
        };
        AddChild(_originalAudioPlayer);
    }

    private bool _hasIsolatedBackgroundStem = false;

    public void LoadScene(OfficialSceneDocument scene, DubProjectDocument? project, VoiceTakeStore? takeStore, string? sceneFolderPath = null)
    {
        ClearTakePlayers();
        _dubbedRanges.Clear();

        _durationSeconds = scene.DurationMilliseconds / 1000.0;
        _masterTimeSeconds = 0.0;
        _hasFinished = false;
        _sceneFolderPath = sceneFolderPath;
        _hasIsolatedBackgroundStem = false;

        // Try to load video
        var videoAsset = scene.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
        if (videoAsset is not null)
        {
            TryLoadVideo(videoAsset.RelativePath, sceneFolderPath);
        }

        // Try to load original mixed audio track (audio.wav)
        var origAudioPath = TryLoadOriginalAudio(scene, sceneFolderPath);

        // Try to load separated ambient background audio track (background.wav)
        TryLoadBackgroundAudio(scene, sceneFolderPath, origAudioPath);

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

            _dubbedRanges.Add((startSec, endSec));

            var player = new VoiceTakeAudioPlayer(voiceSlotId, take?.TakeId ?? "missing", startSec, endSec, audioPath, this);
            _takePlayers.Add(player);
        }
    }

    private string? TryLoadOriginalAudio(OfficialSceneDocument scene, string? sceneFolderPath)
    {
        if (_originalAudioPlayer is null) return null;
        _originalAudioPlayer.Stream = null;

        string? resolvedPath = null;
        var candidates = new List<string>();

        if (!string.IsNullOrEmpty(sceneFolderPath))
        {
            candidates.Add(System.IO.Path.Combine(sceneFolderPath, "media", "audio.wav"));
            candidates.Add(System.IO.Path.Combine(sceneFolderPath, "audio.wav"));
        }

        candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{scene.SceneId}/media/audio.wav"));
        candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{scene.SceneId}/media/audio.wav"));

        foreach (var c in candidates)
        {
            if (System.IO.File.Exists(c))
            {
                resolvedPath = c;
                break;
            }
        }

        if (resolvedPath is not null)
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(resolvedPath);
                var wav = VoiceTakeAudioPlayer.ParseWavBytes(bytes);
                if (wav is not null)
                {
                    _originalAudioPlayer.Stream = wav;
                    GD.Print($"[ScenePlayer] Loaded original audio mix: '{resolvedPath}'");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ScenePlayer] Failed to load original audio mix: {ex.Message}");
            }
        }

        return resolvedPath;
    }

    private void TryLoadBackgroundAudio(OfficialSceneDocument scene, string? sceneFolderPath, string? origAudioPath)
    {
        if (_backgroundAudioPlayer is null) return;
        _backgroundAudioPlayer.Stream = null;

        string? bgRelPath = null;
        var bgAsset = scene.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.BackgroundAudio);
        if (bgAsset is not null)
        {
            bgRelPath = bgAsset.RelativePath;
        }

        string? resolvedPath = null;

        // 1. Check relative path from package folder
        if (!string.IsNullOrEmpty(sceneFolderPath))
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(bgRelPath)) candidates.Add(System.IO.Path.Combine(sceneFolderPath, bgRelPath));
            candidates.Add(System.IO.Path.Combine(sceneFolderPath, "media", "background.wav"));
            candidates.Add(System.IO.Path.Combine(sceneFolderPath, "background.wav"));

            foreach (var c in candidates)
            {
                if (System.IO.File.Exists(c))
                {
                    resolvedPath = c;
                    break;
                }
            }
        }

        // 2. Check official content folder
        if (resolvedPath is null)
        {
            var candidates = new List<string>
            {
                ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{scene.SceneId}/media/background.wav"),
                ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{scene.SceneId}/background.wav"),
                ProjectSettings.GlobalizePath($"res://scenes/{scene.SceneId}/media/background.wav")
            };

            foreach (var c in candidates)
            {
                if (System.IO.File.Exists(c))
                {
                    resolvedPath = c;
                    break;
                }
            }
        }

        if (resolvedPath is not null)
        {
            try
            {
                var bgInfo = new System.IO.FileInfo(resolvedPath);
                if (origAudioPath is not null && System.IO.File.Exists(origAudioPath))
                {
                    var origInfo = new System.IO.FileInfo(origAudioPath);
                    // If background.wav differs in size from audio.wav, it is a genuine isolated stem without speech
                    _hasIsolatedBackgroundStem = Math.Abs(bgInfo.Length - origInfo.Length) > 500;
                }
                else
                {
                    _hasIsolatedBackgroundStem = false;
                }

                var bytes = System.IO.File.ReadAllBytes(resolvedPath);
                var wav = VoiceTakeAudioPlayer.ParseWavBytes(bytes);
                if (wav is not null)
                {
                    _backgroundAudioPlayer.Stream = wav;
                    GD.Print($"[ScenePlayer] Loaded background stem (isIsolated={_hasIsolatedBackgroundStem}): '{resolvedPath}'");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ScenePlayer] Failed to load background stem: {ex.Message}");
            }
        }
        else
        {
            GD.Print($"[ScenePlayer] No ambient background stem found.");
        }
    }

    private void TryLoadVideo(string relativePath, string? sceneFolderPath)
    {
        if (_videoPlayer is null) return;
        _videoPlayer.Stream = MediaTranscoder.LoadVideoStream(sceneFolderPath, relativePath);
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

        if (_originalAudioPlayer is not null && _originalAudioPlayer.Stream is not null)
        {
            if (_originalAudioPlayer.StreamPaused) _originalAudioPlayer.StreamPaused = false;
            else if (!_originalAudioPlayer.Playing) _originalAudioPlayer.Play((float)_masterTimeSeconds);
        }

        if (_backgroundAudioPlayer is not null && _backgroundAudioPlayer.Stream is not null)
        {
            if (_backgroundAudioPlayer.StreamPaused) _backgroundAudioPlayer.StreamPaused = false;
            else if (!_backgroundAudioPlayer.Playing) _backgroundAudioPlayer.Play((float)_masterTimeSeconds);
        }

        UpdateAudioDucking();

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

        if (_originalAudioPlayer is not null && _originalAudioPlayer.Playing)
        {
            _originalAudioPlayer.StreamPaused = true;
        }

        if (_backgroundAudioPlayer is not null && _backgroundAudioPlayer.Playing)
        {
            _backgroundAudioPlayer.StreamPaused = true;
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
            _videoPlayer.Paused = false;
        }

        if (_originalAudioPlayer is not null)
        {
            _originalAudioPlayer.Stop();
            _originalAudioPlayer.StreamPaused = false;
        }

        if (_backgroundAudioPlayer is not null)
        {
            _backgroundAudioPlayer.Stop();
            _backgroundAudioPlayer.StreamPaused = false;
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

        if (_originalAudioPlayer is not null && _originalAudioPlayer.Playing)
        {
            _originalAudioPlayer.Seek((float)_masterTimeSeconds);
        }

        if (_backgroundAudioPlayer is not null && _backgroundAudioPlayer.Playing)
        {
            _backgroundAudioPlayer.Seek((float)_masterTimeSeconds);
        }

        foreach (var player in _takePlayers)
        {
            player.OnManualSeek(_masterTimeSeconds);
        }

        UpdateAudioDucking();

        PlaybackProgress?.Invoke(_masterTimeSeconds, _durationSeconds);
    }

    public override void _Process(double delta)
    {
        if (!_isPlaying || _hasFinished)
        {
            return;
        }

        _masterTimeSeconds += delta;

        UpdateAudioDucking();

        foreach (var player in _takePlayers)
        {
            player.SyncWithMasterTime(_masterTimeSeconds);
        }

        PlaybackProgress?.Invoke(_masterTimeSeconds, _durationSeconds);

        if (_masterTimeSeconds >= _durationSeconds)
        {
            _hasFinished = true;
            _isPlaying = false;

            if (_videoPlayer is not null)
            {
                _videoPlayer.Stop();
                _videoPlayer.Paused = true;
            }

            if (_originalAudioPlayer is not null)
            {
                _originalAudioPlayer.Stop();
                _originalAudioPlayer.StreamPaused = true;
            }

            if (_backgroundAudioPlayer is not null)
            {
                _backgroundAudioPlayer.Stop();
                _backgroundAudioPlayer.StreamPaused = true;
            }

            foreach (var player in _takePlayers)
            {
                player.Stop();
            }

            PlaybackFinished?.Invoke();
        }
    }

    private void UpdateAudioDucking()
    {
        // Dynamic Audio Mixing:
        // Inside a dubbed speech box: Mute original dialogue and original video completely.
        // If an isolated instrumental background stem exists, play it. Otherwise, mute background stem too so original voice is 100% cut.
        // Player's voice take is played cleanly over it.
        // Outside speech boxes: Play original video audio with original actor dialogue.
        bool isInsideDubbedSlot = _dubbedRanges.Any(r => _masterTimeSeconds >= (r.StartSec - 0.05) && _masterTimeSeconds <= (r.EndSec + 0.25));

        if (isInsideDubbedSlot)
        {
            if (_originalAudioPlayer is not null) _originalAudioPlayer.VolumeDb = -80.0f;
            if (_videoPlayer is not null) _videoPlayer.VolumeDb = -80.0f;
            if (_backgroundAudioPlayer is not null) _backgroundAudioPlayer.VolumeDb = 0.0f;
        }
        else
        {
            if (_originalAudioPlayer is not null && _originalAudioPlayer.Stream is not null)
            {
                _originalAudioPlayer.VolumeDb = 0.0f;
                if (_videoPlayer is not null) _videoPlayer.VolumeDb = -80.0f;
            }
            else if (_videoPlayer is not null)
            {
                _videoPlayer.Bus = "Master";
                _videoPlayer.VolumeDb = 0.0f;
            }

            if (_backgroundAudioPlayer is not null) _backgroundAudioPlayer.VolumeDb = -80.0f;
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

    public override void _ExitTree()
    {
        ClearTakePlayers();
    }
}
