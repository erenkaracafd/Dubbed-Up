using DubbedUp.Godot.VideoPlayback;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class PlaybackScreen : BaseScreen
{
    private SynchronizedScenePlayer? _scenePlayer;
    private ProgressBar? _progressBar;
    private Label? _timeLabel;
    private Label? _statusLabel;
    private Button? _playPauseButton;
    private Button? _replayButton;
    private Button? _proceedButton;
    private Button? _menuButton;

    public override void _Ready()
    {
        _scenePlayer = GetNodeOrNull<SynchronizedScenePlayer>("CenterContainer/VBoxContainer/PlayerViewport/SynchronizedScenePlayer");
        _progressBar = GetNodeOrNull<ProgressBar>("CenterContainer/VBoxContainer/ProgressBar");
        _timeLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/TimeLabel");
        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StatusLabel");
        _playPauseButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ControlsContainer/PlayPauseButton");
        _replayButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ControlsContainer/ReplayButton");
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

        if (_proceedButton is not null)
        {
            _proceedButton.Pressed += OnProceedPressed;
        }

        if (_menuButton is not null)
        {
            _menuButton.Pressed += OnMenuPressed;
        }

        if (_scenePlayer is not null)
        {
            _scenePlayer.PlaybackProgress += OnPlaybackProgress;
            _scenePlayer.PlaybackFinished += OnPlaybackFinished;
            _scenePlayer.SetDuration(8.0); // 8 second playback placeholder
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
            _proceedButton.Text = "Proceed to Voting";
        }

        UpdateStatusText("Playback complete! Ready to vote.");
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
        Navigator?.NavigateTo(AppScreen.Voting);
    }

    private void OnMenuPressed()
    {
        _scenePlayer?.Stop();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
