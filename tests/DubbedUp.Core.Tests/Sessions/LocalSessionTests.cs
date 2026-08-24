using DubbedUp.Core.Game;
using DubbedUp.Core.Rounds;
using DubbedUp.Core.Sessions;
using DubbedUp.Core.Tests.ProjectFormat;
using Xunit;

namespace DubbedUp.Core.Tests.Sessions;

public sealed class LocalSessionTests
{
    [Fact]
    public void Session_requires_at_least_one_unique_player()
    {
        Assert.Throws<GameRuleException>(() => LocalSession.Create("session-1", []));
        Assert.Throws<GameRuleException>(() => LocalSession.Create(
            "session-1",
            [Player("p1"), Player("p1")]));
        
        var soloSession = LocalSession.Create("session-solo", [Player("solo")]);
        Assert.Single(soloSession.Players);
    }

    [Fact]
    public void Valid_session_creates_a_round_for_the_official_scene()
    {
        var session = Session();

        var round = session.StartRound("round-1", TestDocuments.ValidScene());

        Assert.Equal("museum-mixup", round.SceneId);
        Assert.Equal(RoundPhase.AssigningCharacters, round.Phase);
        Assert.Same(round, session.ActiveRound);
    }

    [Fact]
    public void Required_characters_map_every_voice_slot_to_a_player()
    {
        var round = Session().StartRound("round-1", TestDocuments.ValidScene());
        round.AssignCharacter("guard", "p1");
        round.AssignCharacter("tourist", "p2");

        var assignments = round.GetVoiceSlotAssignments();

        Assert.Collection(
            assignments,
            assignment => Assert.Equal(
                new VoiceSlotAssignment("guard-line-1", "guard", "p1"),
                assignment),
            assignment => Assert.Equal(
                new VoiceSlotAssignment("tourist-line-1", "tourist", "p2"),
                assignment));
    }

    [Fact]
    public void Missing_character_assignment_blocks_recording_and_slot_mapping()
    {
        var round = Session().StartRound("round-1", TestDocuments.ValidScene());
        round.AssignCharacter("guard", "p1");

        var mappingError = Assert.Throws<GameRuleException>(() => round.GetVoiceSlotAssignments());
        var recordingError = Assert.Throws<GameRuleException>(() => round.StartRecording());

        Assert.Contains("tourist", mappingError.Message, StringComparison.Ordinal);
        Assert.Contains("tourist", recordingError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_or_unknown_character_assignments_are_rejected()
    {
        var round = Session().StartRound("round-1", TestDocuments.ValidScene());
        round.AssignCharacter("guard", "p1");

        Assert.Throws<GameRuleException>(() => round.AssignCharacter("guard", "p2"));
        Assert.Throws<GameRuleException>(() => round.AssignCharacter("ghost", "p2"));
        Assert.Throws<GameRuleException>(() => round.AssignCharacter("tourist", "missing-player"));
    }

    [Fact]
    public void Recording_each_required_slot_advances_to_playback_readiness()
    {
        var round = AssignedRound();
        round.StartRecording();

        round.MarkVoiceSlotRecorded("guard-line-1");
        Assert.Equal(RoundPhase.Recording, round.Phase);

        round.MarkVoiceSlotRecorded("tourist-line-1");
        Assert.Equal(RoundPhase.ReadyForPlayback, round.Phase);
    }

    [Fact]
    public void Duplicate_or_unknown_recordings_are_rejected()
    {
        var round = AssignedRound();
        round.StartRecording();
        round.MarkVoiceSlotRecorded("guard-line-1");

        Assert.Throws<GameRuleException>(() => round.MarkVoiceSlotRecorded("guard-line-1"));
        Assert.Throws<GameRuleException>(() => round.MarkVoiceSlotRecorded("unknown-slot"));
    }

    [Fact]
    public void Round_enforces_record_playback_vote_lifecycle()
    {
        var round = AssignedRound();

        Assert.Throws<GameRuleException>(() => round.StartPlayback());
        round.StartRecording();
        round.MarkVoiceSlotRecorded("guard-line-1");
        round.MarkVoiceSlotRecorded("tourist-line-1");
        round.StartPlayback();
        Assert.Equal(RoundPhase.Playing, round.Phase);
        round.FinishPlayback();
        Assert.Equal(RoundPhase.Voting, round.Phase);
        round.CompleteVoting();
        Assert.Equal(RoundPhase.Complete, round.Phase);
    }

    [Fact]
    public void Session_blocks_a_second_round_until_the_active_round_completes()
    {
        var session = Session();
        var round = session.StartRound("round-1", TestDocuments.ValidScene());

        Assert.Throws<GameRuleException>(
            () => session.StartRound("round-2", TestDocuments.ValidScene()));

        round.AssignCharacter("guard", "p1");
        round.AssignCharacter("tourist", "p2");
        round.StartRecording();
        round.MarkVoiceSlotRecorded("guard-line-1");
        round.MarkVoiceSlotRecorded("tourist-line-1");
        round.StartPlayback();
        round.FinishPlayback();
        round.CompleteVoting();

        var nextRound = session.StartRound("round-2", TestDocuments.ValidScene());
        Assert.Same(nextRound, session.ActiveRound);
        Assert.Equal(2, session.Rounds.Count);
    }

    private static Round AssignedRound()
    {
        var round = Session().StartRound("round-1", TestDocuments.ValidScene());
        round.AssignCharacter("guard", "p1");
        round.AssignCharacter("tourist", "p2");
        return round;
    }

    private static LocalSession Session() => LocalSession.Create(
        "session-1",
        [Player("p1"), Player("p2")]);

    private static Player Player(string id) => new(id, $"Player {id}");
}
