using DubbedUp.Core.Characters;
using DubbedUp.Core.Game;
using DubbedUp.Core.Rounds;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Scoring;
using DubbedUp.Core.Sessions;
using DubbedUp.Core.Timeline;
using DubbedUp.Core.VoiceTakes;
using DubbedUp.Core.Voting;
using Xunit;

namespace DubbedUp.Core.Tests.Integration;

public sealed class PlaytestHardeningTests
{
    private static OfficialSceneDocument CreateFourCharacterScene() => new()
    {
        SchemaVersion = 1,
        SceneId = "heist-4p",
        Title = "Grand Bank Heist",
        DurationMilliseconds = 16_000,
        SourceMedia =
        [
            new SourceMediaAsset { MediaId = "video", Role = SourceMediaRole.SceneVideo, RelativePath = "media/heist.mp4" }
        ],
        Characters =
        [
            new CharacterDefinition { CharacterId = "mastermind", DisplayName = "The Mastermind" },
            new CharacterDefinition { CharacterId = "hacker", DisplayName = "The Hacker" },
            new CharacterDefinition { CharacterId = "driver", DisplayName = "Getaway Driver" },
            new CharacterDefinition { CharacterId = "guard", DisplayName = "Bank Guard" },
        ],
        VoiceSlots =
        [
            new VoiceSlotDefinition { VoiceSlotId = "slot-1", CharacterId = "mastermind", Prompt = "Initiate the hack." },
            new VoiceSlotDefinition { VoiceSlotId = "slot-2", CharacterId = "hacker", Prompt = "I'm bypassing the firewall!" },
            new VoiceSlotDefinition { VoiceSlotId = "slot-3", CharacterId = "guard", Prompt = "Hey! What are you doing here?" },
            new VoiceSlotDefinition { VoiceSlotId = "slot-4", CharacterId = "driver", Prompt = "Step on it, the police are coming!" },
        ],
        Timeline =
        [
            new TimelineEntry { TimelineEntryId = "t1", VoiceSlotId = "slot-1", StartMilliseconds = 1000, EndMilliseconds = 4000 },
            new TimelineEntry { TimelineEntryId = "t2", VoiceSlotId = "slot-2", StartMilliseconds = 4500, EndMilliseconds = 8000 },
            new TimelineEntry { TimelineEntryId = "t3", VoiceSlotId = "slot-3", StartMilliseconds = 8500, EndMilliseconds = 12000 },
            new TimelineEntry { TimelineEntryId = "t4", VoiceSlotId = "slot-4", StartMilliseconds = 12500, EndMilliseconds = 15500 },
        ]
    };

    [Fact]
    public void FourPlayer_CoopDubbingFlow_CompletesSuccessfully()
    {
        var players = new[]
        {
            new Player("p1", "Alice"),
            new Player("p2", "Bob"),
            new Player("p3", "Charlie"),
            new Player("p4", "Diana"),
        };

        var session = LocalSession.Create("session-4p", players);
        var scene = CreateFourCharacterScene();
        var round = session.StartRound("round-1", scene);

        // Assign characters to all 4 players
        round.AssignCharacter("mastermind", "p1");
        round.AssignCharacter("hacker", "p2");
        round.AssignCharacter("guard", "p3");
        round.AssignCharacter("driver", "p4");

        round.StartRecording();
        Assert.Equal(RoundPhase.Recording, round.Phase);

        var store = new VoiceTakeStore();
        foreach (var slot in scene.VoiceSlots)
        {
            var assignment = round.GetVoiceSlotAssignments().First(a => a.VoiceSlotId == slot.VoiceSlotId);
            var take = new VoiceTake(
                $"take-{slot.VoiceSlotId}",
                slot.VoiceSlotId,
                assignment.PlayerId,
                assignment.CharacterId,
                round.RoundId,
                $"takes/{slot.VoiceSlotId}.wav",
                3000,
                DateTimeOffset.UtcNow);
            store.AddTake(take);
            round.MarkVoiceSlotRecorded(slot.VoiceSlotId);
        }

        Assert.Equal(RoundPhase.ReadyForPlayback, round.Phase);
        round.StartPlayback();
        Assert.Equal(RoundPhase.Playing, round.Phase);
        round.FinishPlayback();
        Assert.Equal(RoundPhase.Voting, round.Phase);

        // Complete voting/round
        round.CompleteVoting();
        Assert.Equal(RoundPhase.Complete, round.Phase);

        Assert.Equal(4, store.GetAllTakes().Count);
    }

    [Fact]
    public void MultiRound_CumulativeScoring_CorrectlyTracksStandings()
    {
        var playerIds = new[] { "p1", "p2", "p3" };
        var scoreBoard = ScoreBoard.Create(playerIds);

        // Round 1 voting
        var candidates1 = new[]
        {
            new PerformanceCandidate("perf-1", "p1"),
            new PerformanceCandidate("perf-2", "p2"),
            new PerformanceCandidate("perf-3", "p3"),
        };
        var voting1 = VotingRound.Create("voting-r1", playerIds, candidates1);
        voting1.CastVote("p1", "perf-2"); // p1 votes for p2
        voting1.CastVote("p2", "perf-1"); // p2 votes for p1
        voting1.CastVote("p3", "perf-1"); // p3 votes for p1 -> p1 wins (2 votes)
        var result1 = voting1.Complete();

        scoreBoard.Apply(result1, winnerPoints: 2);

        var standings1 = scoreBoard.Standings;
        Assert.Equal("p1", standings1[0].PlayerId);
        Assert.Equal(2, standings1[0].Points);

        // Round 2 voting
        var candidates2 = new[]
        {
            new PerformanceCandidate("perf-4", "p1"),
            new PerformanceCandidate("perf-5", "p2"),
            new PerformanceCandidate("perf-6", "p3"),
        };
        var voting2 = VotingRound.Create("voting-r2", playerIds, candidates2);
        voting2.CastVote("p1", "perf-5"); // p1 votes for p2
        voting2.CastVote("p2", "perf-6"); // p2 votes for p3
        voting2.CastVote("p3", "perf-5"); // p3 votes for p2 -> p2 wins (2 votes)
        var result2 = voting2.Complete();

        scoreBoard.Apply(result2, winnerPoints: 3);

        var standings2 = scoreBoard.Standings;
        Assert.Equal("p2", standings2[0].PlayerId);
        Assert.Equal(3, standings2[0].Points);
        Assert.Equal("p1", standings2[1].PlayerId);
        Assert.Equal(2, standings2[1].Points);
        Assert.Equal("p3", standings2[2].PlayerId);
        Assert.Equal(0, standings2[2].Points);
    }
}

