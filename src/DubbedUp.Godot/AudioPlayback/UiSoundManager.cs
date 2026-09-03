using System;
using Godot;

namespace DubbedUp.Godot.AudioPlayback;

/// <summary>
/// Centralized manager for osu!-style responsive UI sound effects (hover ticks, click pops, whooshes).
/// </summary>
public partial class UiSoundManager : Node
{
    private static UiSoundManager? _instance;
    public static UiSoundManager Instance => _instance ??= new UiSoundManager();

    private AudioStream? _hoverStream;
    private AudioStream? _clickStream;
    private AudioStream? _whooshStream;

    private AudioStreamPlayer? _hoverPlayer;
    private AudioStreamPlayer? _clickPlayer;
    private AudioStreamPlayer? _whooshPlayer;

    private double _lastHoverTime = 0.0;
    private const double HoverCooldownSec = 0.04; // prevent machine-gunning audio when sweeping mouse

    public override void _Ready()
    {
        _instance = this;

        _hoverStream = LoadSound("res://Content/Audio/Sfx/ui_hover.wav");
        _clickStream = LoadSound("res://Content/Audio/Sfx/ui_click.wav");
        _whooshStream = LoadSound("res://Content/Audio/Sfx/ui_whoosh.wav");

        _hoverPlayer = CreatePlayer(_hoverStream, -10.0f);
        _clickPlayer = CreatePlayer(_clickStream, -6.0f);
        _whooshPlayer = CreatePlayer(_whooshStream, -12.0f);
    }

    private static AudioStream? LoadSound(string path)
    {
        try
        {
            if (ResourceLoader.Exists(path))
            {
                return GD.Load<AudioStream>(path);
            }
        }
        catch { }
        return null;
    }

    private AudioStreamPlayer CreatePlayer(AudioStream? stream, float volumeDb)
    {
        var player = new AudioStreamPlayer
        {
            Stream = stream,
            VolumeDb = volumeDb,
            Bus = "Master"
        };
        AddChild(player);
        return player;
    }

    public void PlayHover()
    {
        var now = Time.GetTicksMsec() / 1000.0;
        if (now - _lastHoverTime < HoverCooldownSec) return;
        _lastHoverTime = now;

        if (_hoverPlayer is not null && _hoverStream is not null)
        {
            _hoverPlayer.PitchScale = (float)GD.RandRange(0.96, 1.04); // subtle organic variation
            _hoverPlayer.Play();
        }
    }

    public void PlayClick()
    {
        if (_clickPlayer is not null && _clickStream is not null)
        {
            _clickPlayer.PitchScale = (float)GD.RandRange(0.98, 1.02);
            _clickPlayer.Play();
        }
    }

    public void PlayWhoosh()
    {
        if (_whooshPlayer is not null && _whooshStream is not null)
        {
            _whooshPlayer.Play();
        }
    }

    /// <summary>
    /// Attaches hover sound and click sound to any Godot Button automatically.
    /// </summary>
    public static void Attach(Button button)
    {
        if (button is null) return;

        button.MouseEntered += () =>
        {
            if (!button.Disabled) Instance.PlayHover();
        };

        button.Pressed += () =>
        {
            if (!button.Disabled) Instance.PlayClick();
        };
    }
}

