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

    public void LoadScene(OfficialSceneDocument scene, DubProjectDocument? project, VoiceTakeStore? takeStore, string? sceneFolderPath = null)
    {
        ClearTakePlayers();
        _dubbedRanges.Clear();

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

        // Try to load separated ambient background audio track (background.wav)
        TryLoadBackgroundAudio(scene, sceneFolderPath);

        // Try to load original mixed audio track (audio.wav)
        TryLoadOriginalAudio(scene, sceneFolderPath);

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

    private void TryLoadOriginalAudio(OfficialSceneDocument scene, string? sceneFolderPath)
    {
        if (_originalAudioPlayer is null) return;
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
    }

    private void TryLoadBackgroundAudio(OfficialSceneDocument scene, string? sceneFolderPath)
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
                var bytes = System.IO.File.ReadAllBytes(resolvedPath);
                var wav = VoiceTakeAudioPlayer.ParseWavBytes(bytes);
                if (wav is not null)
                {
                    _backgroundAudioPlayer.Stream = wav;
                    GD.Print($"[ScenePlayer] Loaded AI ambient background stem: '{resolvedPath}'");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ScenePlayer] Failed to load background audio stem: {ex.Message}");
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
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(relativePath)) candidates.Add(System.IO.Path.Combine(sceneFolderPath, relativePath));
            candidates.Add(System.IO.Path.Combine(sceneFolderPath, "media", "video.ogv"));
            candidates.Add(System.IO.Path.Combine(sceneFolderPath, "video.ogv"));

            foreach (var cand in candidates)
            {
                var absolutePath = System.IO.Path.GetFullPath(cand);
                if (System.IO.File.Exists(absolutePath))
                {
                    try
                    {
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
                        GD.PrintErr($"[VideoPlayer] Failed to load external video '{absolutePath}': {ex.Message}");
                    }
                }
            }
        }

        // 4. On-the-fly transcoding fallback
        if (!string.IsNullOrEmpty(sceneFolderPath))
        {
            var ogv = MediaTranscoder.EnsureTranscoded(sceneFolderPath);
            if (ogv is not null && System.IO.File.Exists(ogv))
            {
                var theora = new VideoStreamTheora();
                theora.File = ogv.Replace("\\", "/");
                _videoPlayer.Stream = theora;
                GD.Print($"[VideoPlayer] Auto-transcoded and loaded via VideoStreamTheora: {theora.File}");
                return;
            }
        }

        GD.PrintErr($"[VideoPlayer] Video stream not found: {relativePath}");
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

        // Dynamic Audio Mixing:
        // Inside a dubbed speech box: Mute original dialogue, play clean ambient stem + player voice take
        // Outside speech boxes: Play original video audio with original actor dialogue
        bool isInsideDubbedSlot = _dubbedRanges.Any(r => _masterTimeSeconds >= (r.StartSec - 0.05) && _masterTimeSeconds <= (r.EndSec + 0.35));

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
