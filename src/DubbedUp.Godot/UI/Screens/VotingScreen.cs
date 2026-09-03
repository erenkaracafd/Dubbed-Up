using DubbedUp.Core.Voting;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

/// <summary>
/// Voting screen: each eligible player selects the performance they found most entertaining.
/// Players cannot vote for their own performance. All votes must be cast before proceeding.
/// </summary>
public partial class VotingScreen : BaseScreen
{
    // Per-voter controls are created dynamically at runtime.
    private VBoxContainer? _voterContainer;
    private Label? _titleLabel;
    private Label? _statusLabel;
    private Label? _errorLabel;
    private Button? _submitButton;
    private Button? _menuButton;

    // voterPlayerId -> chosen performanceId
    private readonly Dictionary<string, string?> _pendingVotes = new(StringComparer.Ordinal);

    public override void _Ready()
    {
        _titleLabel = GetNodeOrNull<Label>("CenterArea/VotingCard/CardMargin/VBoxContainer/TitleLabel");
        _statusLabel = GetNodeOrNull<Label>("CenterArea/VotingCard/CardMargin/VBoxContainer/StatusLabel");
        _errorLabel = GetNodeOrNull<Label>("CenterArea/VotingCard/CardMargin/VBoxContainer/ErrorLabel");
        _voterContainer = GetNodeOrNull<VBoxContainer>("CenterArea/VotingCard/CardMargin/VBoxContainer/VoterContainer");
        _submitButton = GetNodeOrNull<Button>("CenterArea/VotingCard/CardMargin/VBoxContainer/ActionsHBox/SubmitButton");
        _menuButton = GetNodeOrNull<Button>("CenterArea/VotingCard/CardMargin/VBoxContainer/ActionsHBox/MenuButton");

        ApplyStyling();

        if (_submitButton is not null)
        {
            _submitButton.Pressed += OnSubmitPressed;
            _submitButton.Disabled = true;
            AudioPlayback.UiSoundManager.Attach(_submitButton);
        }

        if (_menuButton is not null)
        {
            _menuButton.Pressed += OnMenuPressed;
            AudioPlayback.UiSoundManager.Attach(_menuButton);
        }

        BuildVotingUi();
    }

    private void ApplyStyling()
    {
        var card = GetNodeOrNull<PanelContainer>("CenterArea/VotingCard");
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

        if (_submitButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = new Color(1.0f, 0.540f, 0.680f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23, ShadowSize = 8, ShadowColor = new Color(1.0f, 0.540f, 0.680f, 0.35f) };
            var hover = new StyleBoxFlat { BgColor = new Color(1.0f, 0.660f, 0.780f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23, ShadowSize = 12, ShadowColor = new Color(1.0f, 0.540f, 0.680f, 0.45f) };
            var pressed = new StyleBoxFlat { BgColor = new Color(0.900f, 0.420f, 0.560f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23, ShadowSize = 2 };
            _submitButton.AddThemeStyleboxOverride("normal", normal);
            _submitButton.AddThemeStyleboxOverride("hover", hover);
            _submitButton.AddThemeStyleboxOverride("pressed", pressed);
            _submitButton.AddThemeStyleboxOverride("focus", hover);
            _submitButton.AddThemeColorOverride("font_color", Colors.White);
        }

        if (_menuButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = new Color(0.955f, 0.975f, 1.0f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.780f, 0.850f, 0.950f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23 };
            var hover = new StyleBoxFlat { BgColor = new Color(0.910f, 0.945f, 0.990f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23 };
            _menuButton.AddThemeStyleboxOverride("normal", normal);
            _menuButton.AddThemeStyleboxOverride("hover", hover);
            _menuButton.AddThemeStyleboxOverride("focus", hover);
            _menuButton.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.42f));
        }
    }

    private void BuildVotingUi()
    {
        if (Coordinator?.CurrentVotingRound is null)
        {
            SetStatus("No voting round available.");
            return;
        }

        var votingRound = Coordinator.CurrentVotingRound;
        var candidates = votingRound.Candidates;
        var voters = votingRound.EligiblePlayerIds;

        if (_voterContainer is null)
        {
            return;
        }

        // Build one section per voter
        foreach (var voterId in voters)
        {
            _pendingVotes[voterId] = null;

            var voterName = Coordinator.CurrentSession?.Players
                .FirstOrDefault(p => p.PlayerId == voterId)?.DisplayName ?? voterId;

            var sectionLabel = new Label
            {
                Text = $"{voterName} — choose the best dub:",
                CustomMinimumSize = new Vector2(0, 0),
            };
            sectionLabel.AddThemeFontSizeOverride("font_size", 16);
            _voterContainer.AddChild(sectionLabel);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 12);
            _voterContainer.AddChild(hbox);

            // ButtonGroup enforces single selection per voter
            var buttonGroup = new ButtonGroup();

            foreach (var candidate in candidates)
            {
                // Players cannot vote for their own performance
                if (string.Equals(candidate.PlayerId, voterId, StringComparison.Ordinal))
                {
                    continue;
                }

                var candidateName = Coordinator.CurrentSession?.Players
                    .FirstOrDefault(p => p.PlayerId == candidate.PlayerId)?.DisplayName
                    ?? candidate.PlayerId;

                // perf-<charId> => charId is the part after "perf-"
                var charId = candidate.PerformanceId.StartsWith("perf-", StringComparison.Ordinal)
                    ? candidate.PerformanceId["perf-".Length..]
                    : candidate.PerformanceId;
                var charDef = Coordinator.CurrentScene?.Characters
                    .FirstOrDefault(c => string.Equals(c.CharacterId, charId, StringComparison.Ordinal));
                var charDisplayName = charDef?.DisplayName ?? charId;

                var btn = new Button
                {
                    Text = $"{charDisplayName}\n({candidateName})",
                    ToggleMode = true,
                    ButtonGroup = buttonGroup,
                    CustomMinimumSize = new Vector2(200, 60),
                };

                // Capture locals for closure
                var capturedVoterId = voterId;
                var capturedPerfId = candidate.PerformanceId;
                btn.Toggled += (pressed) =>
                {
                    if (pressed)
                    {
                        _pendingVotes[capturedVoterId] = capturedPerfId;
                    }
                    else if (_pendingVotes.TryGetValue(capturedVoterId, out var current)
                             && string.Equals(current, capturedPerfId, StringComparison.Ordinal))
                    {
                        _pendingVotes[capturedVoterId] = null;
                    }
                    UpdateSubmitState();
                };

                hbox.AddChild(btn);
            }
        }

        SetStatus($"All players must cast a vote before submitting ({voters.Count} voters, {candidates.Count} candidates).");
    }

    private void UpdateSubmitState()
    {
        if (_submitButton is null)
        {
            return;
        }

        var allVoted = _pendingVotes.Count > 0 && _pendingVotes.Values.All(v => v is not null);
        _submitButton.Disabled = !allVoted;

        if (allVoted)
        {
            SetStatus("All votes ready — press Submit to finalise.");
        }
    }

    private void OnSubmitPressed()
    {
        HideError();

        if (Coordinator is null)
        {
            ShowError("No coordinator available.");
            return;
        }

        try
        {
            foreach (var (voterId, perfId) in _pendingVotes)
            {
                if (perfId is null)
                {
                    ShowError($"Missing vote from player '{voterId}'.");
                    return;
                }

                Coordinator.CastVote(voterId, perfId);
            }

            Coordinator.FinishVoting();
            Navigator?.NavigateTo(AppScreen.Results);
        }
        catch (Exception ex)
        {
            ShowError($"Voting error: {ex.Message}");
        }
    }

    private void OnMenuPressed()
    {
        Coordinator?.ResetSession();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }

    private void SetStatus(string text)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = text;
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

    private void HideError()
    {
        if (_errorLabel is not null)
        {
            _errorLabel.Visible = false;
        }
    }
}
