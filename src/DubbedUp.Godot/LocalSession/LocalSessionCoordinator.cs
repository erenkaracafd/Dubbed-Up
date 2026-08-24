using DubbedUp.Core.Characters;
using DubbedUp.Core.Game;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Rounds;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Scoring;
using DubbedUp.Core.Sessions;
using DubbedUp.Core.Timeline;
using DubbedUp.Core.VoiceTakes;
using DubbedUp.Core.Voting;
using DubbedUp.Godot.Microphone;

namespace DubbedUp.Godot.LocalSession;

public sealed class LocalSessionCoordinator
{
    private readonly IVoiceRecorder _voiceRecorder;

    public LocalSessionCoordinator(IVoiceRecorder? voiceRecorder = null)
    {
        _voiceRecorder = voiceRecorder ?? new GodotVoiceRecorder();
    }

    public DubbedUp.Core.Sessions.LocalSession? CurrentSession { get; private set; }

    public Round? ActiveRound { get; private set; }

    public ScoreBoard? ScoreBoard { get; private set; }

    public VoiceTakeStore TakeStore { get; } = new();

    public VotingRound? CurrentVotingRound { get; private set; }

    public VotingResult? LatestVotingResult { get; private set; }

    public GameMode Mode { get; private set; } = GameMode.CoopDubbing;

    public ScenePackage? SelectedScenePackage { get; set; }

    public OfficialSceneDocument? CurrentScene { get; private set; }

    public IVoiceRecorder VoiceRecorder => _voiceRecorder;

    public static OfficialSceneDocument CreateDefaultScene() => new()
    {
        SchemaVersion = ProjectSchema.CurrentVersion,
        SceneId = "museum-mixup",
        Title = "Museum Mix-up",
        DurationMilliseconds = 8_000,
        SourceMedia =
        [
            new SourceMediaAsset
            {
                MediaId = "scene-video",
                Role = SourceMediaRole.SceneVideo,
                RelativePath = "media/scene.ogv",
            },
        ],
        Characters =
        [
            new CharacterDefinition { CharacterId = "guard", DisplayName = "Museum Guard" },
            new CharacterDefinition { CharacterId = "thief", DisplayName = "Sneaky Thief" },
        ],
        VoiceSlots =
        [
            new VoiceSlotDefinition
            {
                VoiceSlotId = "guard-line-1",
                CharacterId = "guard",
                Prompt = "React with shock as the ancient statue suddenly moves!",
            },
            new VoiceSlotDefinition
            {
                VoiceSlotId = "thief-line-1",
                CharacterId = "thief",
                Prompt = "Whisper a clever excuse for being inside the display case.",
            },
        ],
        Timeline =
        [
            new TimelineEntry
            {
                TimelineEntryId = "entry-1",
                VoiceSlotId = "guard-line-1",
                StartMilliseconds = 1_000,
                EndMilliseconds = 4_000,
            },
            new TimelineEntry
            {
                TimelineEntryId = "entry-2",
                VoiceSlotId = "thief-line-1",
                StartMilliseconds = 4_500,
                EndMilliseconds = 7_500,
            },
        ],
    };

    public void StartSession(IEnumerable<string> playerNames, OfficialSceneDocument? scene = null, GameMode mode = GameMode.CoopDubbing)
    {
        ArgumentNullException.ThrowIfNull(playerNames);
        var names = playerNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
        if (names.Length < 2)
        {
            throw new GameRuleException("A local session requires at least two players.");
        }

        var players = names
            .Select((name, i) => new Player($"player-{i + 1}", name.Trim()))
            .ToArray();

        Mode = mode;
        CurrentSession = DubbedUp.Core.Sessions.LocalSession.Create($"session-{Guid.NewGuid():N}", players);
        ScoreBoard = ScoreBoard.Create(players.Select(p => p.PlayerId));
        CurrentScene = scene ?? CreateDefaultScene();

        StartRoundInternal(CurrentScene);
    }

    public void StartNextRound(OfficialSceneDocument? scene = null)
    {
        if (CurrentSession is null)
        {
            throw new GameRuleException("No active session to start a round.");
        }

        if (ActiveRound is not null && ActiveRound.Phase != RoundPhase.Complete)
        {
            throw new GameRuleException("The active round must be completed before starting another round.");
        }

        CurrentScene = scene ?? CurrentScene ?? CreateDefaultScene();
        StartRoundInternal(CurrentScene);
    }

    private void StartRoundInternal(OfficialSceneDocument scene)
    {
        if (CurrentSession is null)
        {
            throw new GameRuleException("No active session.");
        }

        var roundId = $"round-{CurrentSession.Rounds.Count + 1}";
        ActiveRound = CurrentSession.StartRound(roundId, scene);

        // Assign characters to players round-robin
        var players = CurrentSession.Players;
        for (var i = 0; i < scene.Characters.Count; i++)
        {
            var charId = scene.Characters[i].CharacterId;
            var player = players[i % players.Count];
            ActiveRound.AssignCharacter(charId, player.PlayerId);
        }

        ActiveRound.StartRecording();
    }

    public void StartRecording(string voiceSlotId)
    {
        if (ActiveRound is null || ActiveRound.Phase != RoundPhase.Recording)
        {
            throw new GameRuleException("Active round is not in Recording phase.");
        }

        var assignment = ActiveRound.GetVoiceSlotAssignments()
            .FirstOrDefault(a => string.Equals(a.VoiceSlotId, voiceSlotId, StringComparison.Ordinal));
        if (assignment is null)
        {
            throw new GameRuleException($"VoiceSlotId '{voiceSlotId}' is not assigned in the current round.");
        }

        _voiceRecorder.StartRecording(voiceSlotId, assignment.PlayerId, assignment.CharacterId, ActiveRound.RoundId);
    }

    public VoiceTake StopRecording()
    {
        if (ActiveRound is null)
        {
            throw new GameRuleException("No active round.");
        }

        var take = _voiceRecorder.StopRecording();
        TakeStore.AddTake(take);

        if (!ActiveRound.RecordedVoiceSlotIds.Contains(take.VoiceSlotId))
        {
            ActiveRound.MarkVoiceSlotRecorded(take.VoiceSlotId);
        }

        return take;
    }

    public void FinishRecording()
    {
        if (ActiveRound is null)
        {
            throw new GameRuleException("No active round.");
        }

        if (CurrentScene is null)
        {
            throw new GameRuleException("No active scene.");
        }

        foreach (var slot in CurrentScene.VoiceSlots)
        {
            if (!TakeStore.HasTakeForSlot(slot.VoiceSlotId))
            {
                throw new GameRuleException($"Voice slot '{slot.VoiceSlotId}' has not been recorded.");
            }
        }
    }

    public void StartPlayback()
    {
        if (ActiveRound is null)
        {
            throw new GameRuleException("No active round.");
        }

        if (ActiveRound.Phase == RoundPhase.ReadyForPlayback)
        {
            ActiveRound.StartPlayback();
        }
    }

    public void FinishPlayback()
    {
        if (ActiveRound is null || CurrentSession is null || CurrentScene is null)
        {
            throw new GameRuleException("No active round or session.");
        }

        if (ActiveRound.Phase == RoundPhase.Playing)
        {
            ActiveRound.FinishPlayback();
        }

        if (Mode == GameMode.CompetitiveVoting)
        {
            // Prepare voting round
            var eligiblePlayerIds = CurrentSession.Players.Select(p => p.PlayerId).ToArray();
            var candidates = ActiveRound.CharacterAssignments
                .Select(assignment =>
                    new PerformanceCandidate($"perf-{assignment.CharacterId}", assignment.PlayerId))
                .ToArray();

            CurrentVotingRound = VotingRound.Create($"voting-{ActiveRound.RoundId}", eligiblePlayerIds, candidates);
        }
    }

    public void CastVote(string voterPlayerId, string performanceId)
    {
        if (CurrentVotingRound is null)
        {
            throw new GameRuleException("No active voting round.");
        }

        CurrentVotingRound.CastVote(voterPlayerId, performanceId);
    }

    public VotingResult FinishVoting()
    {
        if (CurrentVotingRound is null || ScoreBoard is null || ActiveRound is null)
        {
            throw new GameRuleException("No active voting round or score board.");
        }

        LatestVotingResult = CurrentVotingRound.Complete();
        ScoreBoard.Apply(LatestVotingResult);

        if (ActiveRound.Phase == RoundPhase.Voting)
        {
            ActiveRound.CompleteVoting();
        }

        return LatestVotingResult;
    }

    public void ResetSession()
    {
        if (_voiceRecorder.IsRecording)
        {
            _voiceRecorder.CancelRecording();
        }

        CurrentSession = null;
        ActiveRound = null;
        ScoreBoard = null;
        CurrentVotingRound = null;
        LatestVotingResult = null;
        CurrentScene = null;
    }
}
