using DubbedUp.Core.Scoring;
using DubbedUp.Core.Voting;
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
    private Button? _nextRoundButton;
    private Button? _replayButton;
    private Button? _menuButton;

    public override void _Ready()
    {
        _winnerLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/WinnerLabel");
        _tallyLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/TallyLabel");
        _standingsLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StandingsLabel");
        _errorLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/ErrorLabel");
        _nextRoundButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/NextRoundButton");
        _replayButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ReplayRoundButton");
        _menuButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/MenuButton");

        if (_nextRoundButton is not null)
        {
            _nextRoundButton.Pressed += OnNextRoundPressed;
        }

        if (_replayButton is not null)
        {
            _replayButton.Pressed += OnReplayRoundPressed;
        }

        if (_menuButton is not null)
        {
            _menuButton.Pressed += OnMenuPressed;
        }

        PopulateResults();
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
