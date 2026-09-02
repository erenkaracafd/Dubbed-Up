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
        _scenePlayer = GetNodeOrNull<SynchronizedScenePlayer>("CenterContainer/VBoxContainer/VideoContainer/PlayerViewport/SynchronizedScenePlayer") ?? GetNodeOrNull<SynchronizedScenePlayer>("CenterContainer/VBoxContainer/PlayerViewport/SynchronizedScenePlayer");
        _progressBar = GetNodeOrNull<ProgressBar>("CenterContainer/VBoxContainer/ProgressBar");
        _timeLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/TimeLabel");
        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StatusLabel");
        _subtitleLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SubtitleLabel");
        _playPauseButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ControlsContainer/PlayPauseButton");
        _replayButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ControlsContainer/ReplayButton");
        _exportButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ControlsContainer/ExportButton");
        _proceedButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ProceedButton");
        _menuButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/MenuButton");

        if (_playPauseButton is not null)
        {
            _playPauseButton.Pressed += OnPlayPausePressed;
        }

        if (_replayButton is not null)
        {
            _replayButton.Pressed += OnReplayPressed;
        }

        if (_exportButton is not null)
        {
            _exportButton.Pressed += OnExportPressed;
        }

        if (_proceedButton is not null)
        {
            _proceedButton.Pressed += OnProceedPressed;
        }

        if (_menuButton is not null)
        {
            _menuButton.Pressed += OnMenuPressed;
        }

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
            _exportButton.Text = "⏳ Exporting...";
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
            _exportButton.Text = "🎬 Export Video (.mp4)";
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
}
