using DubbedUp.Godot.VideoPlayback;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class PlaybackScreen : BaseScreen
{
    private SynchronizedScenePlayer? _scenePlayer;
    private ProgressBar? _progressBar;
    private Label? _timeLabel;
    private Label? _statusLabel;
    private Label? _subtitleLabel;
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
        _subtitleLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/SubtitleLabel");
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

        if (Coordinator is not null)
        {
            Coordinator.StartPlayback();

            if (_scenePlayer is not null)
            {
                _scenePlayer.PlaybackProgress += OnPlaybackProgress;
                _scenePlayer.PlaybackFinished += OnPlaybackFinished;

                if (Coordinator.CurrentScene is not null)
                {
                    _scenePlayer.LoadScene(Coordinator.CurrentScene, null, Coordinator.TakeStore);
                }
                else
                {
                    _scenePlayer.SetDuration(8.0);
                }

                _scenePlayer.Play();
            }
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

    private void OnMenuPressed()
    {
        _scenePlayer?.Stop();
        Coordinator?.ResetSession();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
