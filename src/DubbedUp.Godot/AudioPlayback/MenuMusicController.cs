using System;
using DubbedUp.Godot.UI;
using Godot;

namespace DubbedUp.Godot.AudioPlayback;

/// <summary>
/// Global background music manager for menus and lobby.
/// Provides smooth volume fading, screen-aware ducking (respecting recording/playback invariants),
/// and emits rhythmic beat pulses (~128 BPM) for UI animations.
/// </summary>
public partial class MenuMusicController : Node
{
    public const string DefaultMusicPath = "res://Content/Audio/Music/Dubbed Up.mp3";
    private const double TargetBpm = 128.0;
    private const double BeatInterval = 60.0 / TargetBpm; // ~0.46875s

    [Signal]
    public delegate void BeatPulseEventHandler(int beatIndex);

    private AudioStreamPlayer? _player;
    private Tween? _fadeTween;
    private double _beatTimer = 0.0;
    private int _beatCount = 0;
    private float _userVolumeLinear = 0.8f;
    private bool _isMuted = false;
    private bool _isSilencedForGameplay = false;

    public float UserVolume
    {
        get => _userVolumeLinear;
        set
        {
            _userVolumeLinear = Mathf.Clamp(value, 0.0f, 1.0f);
            if (!_isMuted && !_isSilencedForGameplay && _player is not null)
            {
                _player.VolumeDb = Mathf.LinearToDb(_userVolumeLinear);
            }
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            if (_isMuted)
            {
                FadeToDb(-80.0f, 0.2f);
            }
            else if (!_isSilencedForGameplay)
            {
                FadeToDb(Mathf.LinearToDb(_userVolumeLinear), 0.3f);
            }
        }
    }

    public bool IsPlaying => _player is not null && _player.Playing;

    public override void _Ready()
    {
        _player = new AudioStreamPlayer
        {
            Bus = "Master",
            VolumeDb = Mathf.LinearToDb(_userVolumeLinear),
            Autoplay = false
        };
        AddChild(_player);

        LoadAndStartMusic();
    }

    public void LoadAndStartMusic()
    {
        if (_player is null) return;

        try
        {
            if (ResourceLoader.Exists(DefaultMusicPath))
            {
                var stream = GD.Load<AudioStream>(DefaultMusicPath);
                if (stream is not null)
                {
                    _player.Stream = stream;
                    _player.Play();
                    GD.Print($"[MenuMusicController] Started background music from '{DefaultMusicPath}'");
                }
            }
            else
            {
                GD.Print($"[MenuMusicController] Music file not found at '{DefaultMusicPath}'.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[MenuMusicController] Error loading music: {ex.Message}");
        }
    }

    public override void _Process(double delta)
    {
        if (_player is null || !_player.Playing) return;

        // Loop playback when track finishes
        var streamLen = (float)(_player.Stream?.GetLength() ?? 0.0);
        if (streamLen > 1.0f && _player.GetPlaybackPosition() >= streamLen - 0.1f)
        {
            _player.Seek(0.0f);
        }

        // Beat pulse calculation for UI animations
        _beatTimer += delta;
        if (_beatTimer >= BeatInterval)
        {
            _beatTimer -= BeatInterval;
            _beatCount++;
            EmitSignal(SignalName.BeatPulse, _beatCount);
        }
    }

    /// <summary>
    /// React to screen transitions: automatically duck music during recording and playback
    /// to preserve audio separation invariants (-80 dB dialogue muting / pristine voice recording).
    /// </summary>
    public void OnScreenChanged(AppScreen newScreen)
    {
        if (newScreen == AppScreen.Recording || newScreen == AppScreen.Playback)
        {
            _isSilencedForGameplay = true;
            FadeToDb(-80.0f, 0.25f);
        }
        else
        {
            _isSilencedForGameplay = false;
            if (!_isMuted)
            {
                // Ensure player is running if it was stopped
                if (_player is not null && !_player.Playing && _player.Stream is not null)
                {
                    _player.Play();
                }
                FadeToDb(Mathf.LinearToDb(_userVolumeLinear), 0.4f);
            }
        }
    }

    public void FadeToDb(float targetDb, float durationSeconds)
    {
        if (_player is null) return;

        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        if (_fadeTween is not null)
        {
            _fadeTween.TweenProperty(_player, "volume_db", targetDb, durationSeconds)
                      .SetTrans(Tween.TransitionType.Cubic)
                      .SetEase(Tween.EaseType.Out);
        }
        else
        {
            _player.VolumeDb = targetDb;
        }
    }
}
