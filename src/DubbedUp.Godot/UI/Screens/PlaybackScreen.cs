using System;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.VideoPlayback;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class PlaybackScreen : BaseScreen
{
    private AspectRatioContainer? _videoContainer;
    private SynchronizedScenePlayer? _scenePlayer;
    private ProgressBar? _progressBar;
    private Label? _timeLabel;
    private Label? _statusLabel;
    private Label? _subtitleLabel;
    private Button? _playPauseButton;
    private Button? _replayButton;
    private Button? _exportButton;
    private Button? _proceedButton;
    private Button? _menuButton;

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);
        StartPlaybackSession();
    }

    public override void _Ready()
    {
        _videoContainer = GetNodeOrNull<AspectRatioContainer>("CenterContainer/VBoxContainer/VideoContainer");
        _scenePlayer = GetNodeOrNull<SynchronizedScenePlayer>("CenterContainer/VBoxContainer/VideoContainer/PlayerViewport/SynchronizedScenePlayer");
        _progressBar = GetNodeOrNull<ProgressBar>("CenterContainer/VBoxContainer/ProgressBar");
        _timeLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/TimeRow/TimeLabel");
        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/TimeRow/StatusLabel");
        _subtitleLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SubtitleLabel");
        _playPauseButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ControlsContainer/PlayPauseButton");
        _replayButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ControlsContainer/ReplayButton");
        _exportButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ControlsContainer/ExportButton");
        _proceedButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ProceedButton");
        _menuButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/BackButton");

        ApplyStyling();

        if (_playPauseButton is not null) SetupButton(_playPauseButton, OnPlayPausePressed);
        if (_replayButton is not null) SetupButton(_replayButton, OnReplayPressed);
        if (_exportButton is not null) SetupButton(_exportButton, OnExportPressed);
        if (_proceedButton is not null) SetupButton(_proceedButton, OnProceedPressed);
        if (_menuButton is not null) SetupButton(_menuButton, OnMenuPressed);

        if (Coordinator is not null)
        {
            StartPlaybackSession();
        }
    }

    public override void _Process(double delta)
    {
        if (_scenePlayer is not null && _videoContainer is not null)
        {
            var tex = _scenePlayer.GetVideoTexture();
            if (tex is not null && tex.GetHeight() > 0)
            {
                float r = (float)tex.GetWidth() / tex.GetHeight();
                if (Math.Abs(_videoContainer.Ratio - r) > 0.01f)
                {
                    _videoContainer.Ratio = r;
                }
            }
        }
    }

    private void StartPlaybackSession()
    {
        if (Coordinator is null) return;

        Coordinator.StartPlayback();

        if (_scenePlayer is not null)
        {
            _scenePlayer.PlaybackProgress -= OnPlaybackProgress;
            _scenePlayer.PlaybackFinished -= OnPlaybackFinished;
            _scenePlayer.PlaybackProgress += OnPlaybackProgress;
            _scenePlayer.PlaybackFinished += OnPlaybackFinished;

            if (Coordinator.CurrentScene is not null)
            {
                var folderPath = Coordinator.SelectedScenePackage?.PackageDirectory;
                _scenePlayer.LoadScene(Coordinator.CurrentScene, null, Coordinator.TakeStore, folderPath);
            }
            else
            {
                _scenePlayer.SetDuration(8.0);
            }

            _scenePlayer.Play();
        }

        UpdateStatusText("Playing synchronized scene dub...");
    }

    private void OnPlayPausePressed()
    {
        if (_scenePlayer is null)
        {
            return;
        }

        if (_scenePlayer.IsPlaying)
        {
            _scenePlayer.Pause();
            if (_playPauseButton is not null)
            {
                _playPauseButton.Text = "Play";
            }
            UpdateStatusText("Playback paused");
        }
        else
        {
            _scenePlayer.Play();
            if (_playPauseButton is not null)
            {
                _playPauseButton.Text = "Pause";
            }
            UpdateStatusText("Playing synchronized scene dub...");
        }
    }

    private void OnReplayPressed()
    {
        if (_scenePlayer is not null)
        {
            _scenePlayer.Restart();
            if (_playPauseButton is not null)
            {
                _playPauseButton.Text = "Pause";
            }
            if (_proceedButton is not null)
            {
                _proceedButton.Text = "Watching dub...";
            }
            UpdateStatusText("Replaying synchronized scene dub...");
        }
    }

    private void OnPlaybackProgress(double current, double total)
    {
        if (_progressBar is not null && total > 0)
        {
            _progressBar.Value = (current / total) * 100.0;
        }

        if (_timeLabel is not null)
        {
            _timeLabel.Text = $"{FormatTime(current)} / {FormatTime(total)}";
        }

        // Check active timeline slot for karaoke/dialogue subtitle display
        if (_subtitleLabel is not null && Coordinator?.CurrentScene is not null)
        {
            var currentMs = (long)(current * 1000.0);
            var activeEntry = Coordinator.CurrentScene.Timeline
                .FirstOrDefault(e => currentMs >= e.StartMilliseconds && currentMs <= e.EndMilliseconds);

            if (activeEntry is not null)
            {
                var slot = Coordinator.CurrentScene.VoiceSlots.FirstOrDefault(s => s.VoiceSlotId == activeEntry.VoiceSlotId);
                var charDef = Coordinator.CurrentScene.Characters.FirstOrDefault(c => c.CharacterId == slot?.CharacterId);
                var charName = charDef?.DisplayName ?? slot?.CharacterId ?? "Character";
                var prompt = slot?.Prompt ?? "...";

                _subtitleLabel.Text = $"💬 {charName}: \"{prompt}\"";
                _subtitleLabel.Visible = true;
            }
            else
            {
                _subtitleLabel.Visible = false;
            }
        }
    }

    private void OnPlaybackFinished()
    {
        if (_playPauseButton is not null)
        {
            _playPauseButton.Text = "Play";
        }

        if (_proceedButton is not null)
        {
            _proceedButton.Disabled = false;
            _proceedButton.Text = Coordinator?.Mode == DubbedUp.Core.Game.GameMode.CoopDubbing
                ? "Dub Complete (View Celebration)"
                : "Proceed to Voting";
        }

        UpdateStatusText(Coordinator?.Mode == DubbedUp.Core.Game.GameMode.CoopDubbing
            ? "Playback complete! Dubbing success!"
            : "Playback complete! Ready to vote.");
    }

    private void UpdateStatusText(string text)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = text;
        }
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private void OnProceedPressed()
    {
        _scenePlayer?.Stop();
        try
        {
            Coordinator?.FinishPlayback();
            if (Coordinator?.Mode == DubbedUp.Core.Game.GameMode.CoopDubbing)
            {
                Navigator?.NavigateTo(AppScreen.Results);
            }
            else
            {
                Navigator?.NavigateTo(AppScreen.Voting);
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText($"Error: {ex.Message}");
        }
    }

    private async void OnExportPressed()
    {
        if (Coordinator?.CurrentScene is null)
        {
            UpdateStatusText("❌ No active scene to export.");
            return;
        }

        if (_exportButton is not null)
        {
            _exportButton.Disabled = true;
            _exportButton.Text = "Exporting...";
        }

        UpdateStatusText("🎬 Mixing audio tracks and rendering MP4...");

        var folderPath = Coordinator.SelectedScenePackage?.PackageDirectory;
        var exportedFile = await VideoDubExporter.ExportDubbedVideoAsync(
            Coordinator.CurrentScene,
            folderPath,
            Coordinator.TakeStore,
            status =>
            {
                UpdateStatusText(status);
            });

        if (_exportButton is not null)
        {
            _exportButton.Disabled = false;
            _exportButton.Text = "Export Video (.mp4)";
        }

        if (!string.IsNullOrEmpty(exportedFile) && System.IO.File.Exists(exportedFile))
        {
            UpdateStatusText($"✅ Video saved: {System.IO.Path.GetFileName(exportedFile)}");
            OS.ShellOpen(System.IO.Path.GetDirectoryName(exportedFile) ?? exportedFile);
        }
        else
        {
            UpdateStatusText("❌ Video export failed. Check FFmpeg availability.");
        }
    }

    private void OnMenuPressed()
    {
        _scenePlayer?.Stop();
        Coordinator?.ResetSession();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }

    private void SetupButton(Button btn, Action action)
    {
        btn.Pressed += action;
        UiSoundManager.Attach(btn);
    }

    private void ApplyStyling()
    {
        var viewport = GetNodeOrNull<PanelContainer>("CenterContainer/VBoxContainer/VideoContainer/PlayerViewport");
        if (viewport is not null)
        {
            var pBox = new StyleBoxFlat
            {
                BgColor = Colors.Black,
                CornerRadiusTopLeft = 14,
                CornerRadiusTopRight = 14,
                CornerRadiusBottomLeft = 14,
                CornerRadiusBottomRight = 14,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.780f, 0.850f, 0.950f)
            };
            viewport.AddThemeStyleboxOverride("panel", pBox);
        }

        if (_playPauseButton is not null) StyleOutlinePill(_playPauseButton, 16);
        if (_replayButton is not null) StyleActionPill(_replayButton, new Color(0.280f, 0.650f, 0.950f), 16);
        if (_exportButton is not null) StyleActionPill(_exportButton, new Color(0.600f, 0.480f, 0.950f), 16);
        if (_proceedButton is not null) StyleActionPill(_proceedButton, new Color(1.0f, 0.540f, 0.680f), 20);
        if (_menuButton is not null) StyleOutlinePill(_menuButton, 16);
    }

    private static void StyleActionPill(Button btn, Color color, int radius)
    {
        var normal = new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 6, ShadowColor = new Color(color.R, color.G, color.B, 0.3f) };
        var hover = new StyleBoxFlat { BgColor = color.Lightened(0.15f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 10, ShadowColor = new Color(color.R, color.G, color.B, 0.4f) };
        var pressed = new StyleBoxFlat { BgColor = color.Darkened(0.15f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 1 };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
    }

    private static void StyleOutlinePill(Button btn, int radius)
    {
        var normal = new StyleBoxFlat { BgColor = new Color(0.955f, 0.975f, 1.0f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.780f, 0.850f, 0.950f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
        var hover = new StyleBoxFlat { BgColor = new Color(0.910f, 0.945f, 0.990f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.42f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.118f, 0.106f, 0.294f));
    }
}
