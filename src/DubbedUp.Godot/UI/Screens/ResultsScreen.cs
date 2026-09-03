using DubbedUp.Core.Scoring;
using DubbedUp.Core.Voting;
using DubbedUp.Godot.VideoPlayback;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

/// <summary>
/// Results screen: shows voting outcome, winner, and cumulative scoreboard.
/// Allows replaying current dub, starting a new round, or returning to main menu.
/// </summary>
public partial class ResultsScreen : BaseScreen
{
    private Label? _winnerLabel;
    private Label? _tallyLabel;
    private Label? _standingsLabel;
    private Label? _errorLabel;
    private Button? _exportButton;
    private Button? _nextRoundButton;
    private Button? _replayButton;
    private Button? _menuButton;

    public override void _Ready()
    {
        _winnerLabel = GetNodeOrNull<Label>("CenterArea/ResultsCard/CardMargin/VBoxContainer/WinnerLabel");
        _tallyLabel = GetNodeOrNull<Label>("CenterArea/ResultsCard/CardMargin/VBoxContainer/TallyLabel");
        _standingsLabel = GetNodeOrNull<Label>("CenterArea/ResultsCard/CardMargin/VBoxContainer/StandingsLabel");
        _errorLabel = GetNodeOrNull<Label>("CenterArea/ResultsCard/CardMargin/VBoxContainer/ErrorLabel");
        _nextRoundButton = GetNodeOrNull<Button>("CenterArea/ResultsCard/CardMargin/VBoxContainer/ActionsGrid/NextRoundButton");
        _replayButton = GetNodeOrNull<Button>("CenterArea/ResultsCard/CardMargin/VBoxContainer/ActionsGrid/ReplayRoundButton");
        _exportButton = GetNodeOrNull<Button>("CenterArea/ResultsCard/CardMargin/VBoxContainer/BottomRow/ExportButton");
        _menuButton = GetNodeOrNull<Button>("CenterArea/ResultsCard/CardMargin/VBoxContainer/BottomRow/MenuButton");

        ApplyStyling();

        if (_nextRoundButton is not null)
        {
            _nextRoundButton.Pressed += OnNextRoundPressed;
            AudioPlayback.UiSoundManager.Attach(_nextRoundButton);
        }

        if (_replayButton is not null)
        {
            _replayButton.Pressed += OnReplayRoundPressed;
            AudioPlayback.UiSoundManager.Attach(_replayButton);
        }

        if (_exportButton is not null)
        {
            _exportButton.Pressed += OnExportPressed;
            AudioPlayback.UiSoundManager.Attach(_exportButton);
        }

        if (_menuButton is not null)
        {
            _menuButton.Pressed += OnMenuPressed;
            AudioPlayback.UiSoundManager.Attach(_menuButton);
        }

        PopulateResults();
    }

    private void ApplyStyling()
    {
        var card = GetNodeOrNull<PanelContainer>("CenterArea/ResultsCard");
        if (card is not null)
        {
            var cardStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.965f, 0.980f, 1.000f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.780f, 0.850f, 0.950f),
                CornerRadiusTopLeft = 24,
                CornerRadiusTopRight = 24,
                CornerRadiusBottomLeft = 24,
                CornerRadiusBottomRight = 24,
                ShadowColor = new Color(0.12f, 0.18f, 0.35f, 0.10f),
                ShadowSize = 18,
                ShadowOffset = new Vector2(0, 6)
            };
            card.AddThemeStyleboxOverride("panel", cardStyle);
        }

        if (_nextRoundButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = new Color(1.0f, 0.540f, 0.680f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, ShadowSize = 8, ShadowColor = new Color(1.0f, 0.540f, 0.680f, 0.35f) };
            var hover = new StyleBoxFlat { BgColor = new Color(1.0f, 0.660f, 0.780f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, ShadowSize = 12, ShadowColor = new Color(1.0f, 0.540f, 0.680f, 0.45f) };
            _nextRoundButton.AddThemeStyleboxOverride("normal", normal);
            _nextRoundButton.AddThemeStyleboxOverride("hover", hover);
            _nextRoundButton.AddThemeStyleboxOverride("focus", hover);
            _nextRoundButton.AddThemeColorOverride("font_color", Colors.White);
        }

        if (_replayButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = new Color(0.280f, 0.650f, 0.950f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22 };
            var hover = new StyleBoxFlat { BgColor = new Color(0.420f, 0.740f, 0.980f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22 };
            _replayButton.AddThemeStyleboxOverride("normal", normal);
            _replayButton.AddThemeStyleboxOverride("hover", hover);
            _replayButton.AddThemeStyleboxOverride("focus", hover);
            _replayButton.AddThemeColorOverride("font_color", Colors.White);
        }

        if (_exportButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = new Color(0.600f, 0.480f, 0.950f), CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomLeft = 20, CornerRadiusBottomRight = 20 };
            var hover = new StyleBoxFlat { BgColor = new Color(0.700f, 0.600f, 1.000f), CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomLeft = 20, CornerRadiusBottomRight = 20 };
            _exportButton.AddThemeStyleboxOverride("normal", normal);
            _exportButton.AddThemeStyleboxOverride("hover", hover);
            _exportButton.AddThemeStyleboxOverride("focus", hover);
            _exportButton.AddThemeColorOverride("font_color", Colors.White);
        }

        if (_menuButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = new Color(0.955f, 0.975f, 1.0f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.780f, 0.850f, 0.950f), CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomLeft = 20, CornerRadiusBottomRight = 20 };
            var hover = new StyleBoxFlat { BgColor = new Color(0.910f, 0.945f, 0.990f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomLeft = 20, CornerRadiusBottomRight = 20 };
            _menuButton.AddThemeStyleboxOverride("normal", normal);
            _menuButton.AddThemeStyleboxOverride("hover", hover);
            _menuButton.AddThemeStyleboxOverride("focus", hover);
            _menuButton.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.42f));
        }
    }

    private void PopulateResults()
    {
        var session = Coordinator?.CurrentSession;
        var mode = Coordinator?.Mode ?? DubbedUp.Core.Game.GameMode.CoopDubbing;

        if (mode == DubbedUp.Core.Game.GameMode.CoopDubbing)
        {
            SetWinner("🎬 Scene Dubbed Successfully!");
            var playerNames = session?.Players.Select(p => p.DisplayName) ?? [];
            var charNames = Coordinator?.CurrentScene?.Characters.Select(c => c.DisplayName) ?? [];
            SetTally($"Players: {string.Join(" & ", playerNames)}\nCharacters: {string.Join(" & ", charNames)}");
            SetStandings("Mode: Co-op Dubbing (Teamwork Win!)");
            if (_nextRoundButton is not null)
            {
                _nextRoundButton.Text = "Pick Another Scene";
            }
            return;
        }

        var result = Coordinator?.LatestVotingResult;
        var scoreBoard = Coordinator?.ScoreBoard;

        if (result is null)
        {
            SetWinner("No voting result available.");
            return;
        }

        // Winner announcement
        var winnerNames = result.WinningPlayerIds
            .Select(id => session?.Players.FirstOrDefault(p => p.PlayerId == id)?.DisplayName ?? id)
            .ToArray();

        var isTie = winnerNames.Length > 1;
        var winnerText = isTie
            ? $"🤝 Tie! Winners: {string.Join(" & ", winnerNames)}"
            : $"🏆 Winner: {winnerNames[0]}";
        SetWinner(winnerText);

        // Vote tallies
        var tallyLines = result.Tallies.Select(tally =>
        {
            var playerName = session?.Players.FirstOrDefault(p => p.PlayerId == tally.PlayerId)?.DisplayName ?? tally.PlayerId;
            return $"  {playerName} ({tally.PerformanceId}): {tally.VoteCount} vote(s)";
        });
        SetTally("Vote Tallies:\n" + string.Join("\n", tallyLines));

        // Scoreboard standings
        if (scoreBoard is not null)
        {
            var standingLines = scoreBoard.Standings.Select((s, i) =>
            {
                var playerName = session?.Players.FirstOrDefault(p => p.PlayerId == s.PlayerId)?.DisplayName ?? s.PlayerId;
                return $"  #{i + 1} {playerName}: {s.Points} pt(s)";
            });
            SetStandings("Cumulative Standings:\n" + string.Join("\n", standingLines));
        }
    }

    private void OnNextRoundPressed()
    {
        if (Coordinator?.Mode == DubbedUp.Core.Game.GameMode.CoopDubbing)
        {
            Navigator?.NavigateTo(AppScreen.ScenePicker);
            return;
        }

        // Start a new round from Setup — Coordinator retains the session/scoreboard
        try
        {
            Coordinator?.StartNextRound();
            Navigator?.NavigateTo(AppScreen.Recording);
        }
        catch (Exception ex)
        {
            ShowError($"Cannot start next round: {ex.Message}");
        }
    }

    private void OnReplayRoundPressed()
    {
        // Navigate back to Playback to re-watch the dub; no state change needed
        Navigator?.NavigateTo(AppScreen.Playback);
    }

    private async void OnExportPressed()
    {
        if (Coordinator?.CurrentScene is null)
        {
            ShowError("No active scene to export.");
            return;
        }

        if (_exportButton is not null)
        {
            _exportButton.Disabled = true;
            _exportButton.Text = "⏳ Rendering MP4...";
        }

        SetStandings("🎬 Rendering & mixing dubbed MP4 video with FFmpeg...");

        try
        {
            var folderPath = Coordinator.SelectedScenePackage?.PackageDirectory;
            var exportedFile = await VideoDubExporter.ExportDubbedVideoAsync(
                Coordinator.CurrentScene,
                folderPath,
                Coordinator.TakeStore,
                status =>
                {
                    SetStandings(status);
                });

            if (_exportButton is not null)
            {
                _exportButton.Disabled = false;
                _exportButton.Text = "🎬 Export Dubbed Video (.mp4)";
            }

            if (!string.IsNullOrEmpty(exportedFile) && System.IO.File.Exists(exportedFile))
            {
                SetStandings($"✅ Video exported: {System.IO.Path.GetFileName(exportedFile)}");
                OS.ShellOpen(System.IO.Path.GetDirectoryName(exportedFile) ?? exportedFile);
            }
            else
            {
                ShowError("Export failed. Please check if FFmpeg is installed.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Export failed: {ex.Message}");
            if (_exportButton is not null)
            {
                _exportButton.Disabled = false;
                _exportButton.Text = "🎬 Export Dubbed Video (.mp4)";
            }
        }
    }

    private void OnMenuPressed()
    {
        Coordinator?.ResetSession();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }

    private void SetWinner(string text)
    {
        if (_winnerLabel is not null)
        {
            _winnerLabel.Text = text;
        }
    }

    private void SetTally(string text)
    {
        if (_tallyLabel is not null)
        {
            _tallyLabel.Text = text;
        }
    }

    private void SetStandings(string text)
    {
        if (_standingsLabel is not null)
        {
            _standingsLabel.Text = text;
        }
    }

    private void ShowError(string text)
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Text = text;
            _errorLabel.Visible = true;
        }
    }
}
