using DubbedUp.Core.Game;
using DubbedUp.Core.VoiceTakes;
using Xunit;

namespace DubbedUp.Core.Tests.VoiceTakes;

public sealed class VoiceTakeTests
{
    [Fact]
    public void VoiceTake_creates_valid_instance()
    {
        var now = DateTimeOffset.UtcNow;
        var take = new VoiceTake(
            takeId: "take-1",
            voiceSlotId: "slot-guard-1",
            playerId: "player-1",
            characterId: "guard",
            roundId: "round-1",
            audioRelativePath: "recordings/take-1.wav",
            durationMilliseconds: 2500,
            recordedAtUtc: now);

        Assert.Equal("take-1", take.TakeId);
        Assert.Equal("slot-guard-1", take.VoiceSlotId);
        Assert.Equal("player-1", take.PlayerId);
        Assert.Equal("guard", take.CharacterId);
        Assert.Equal("round-1", take.RoundId);
        Assert.Equal("recordings/take-1.wav", take.AudioRelativePath);
        Assert.Equal(2500, take.DurationMilliseconds);
        Assert.Equal(now, take.RecordedAtUtc);
    }

    [Theory]
    [InlineData("", "slot-1", "player-1", "char-1", "round-1", "path.wav", 1000)]
    [InlineData("take-1", "", "player-1", "char-1", "round-1", "path.wav", 1000)]
    [InlineData("take-1", "slot-1", "", "char-1", "round-1", "path.wav", 1000)]
    [InlineData("take-1", "slot-1", "player-1", "", "round-1", "path.wav", 1000)]
    [InlineData("take-1", "slot-1", "player-1", "char-1", "", "path.wav", 1000)]
    [InlineData("take-1", "slot-1", "player-1", "char-1", "round-1", "", 1000)]
    [InlineData("take-1", "slot-1", "player-1", "char-1", "round-1", "path.wav", -1)]
    public void VoiceTake_rejects_invalid_arguments(
        string takeId,
        string voiceSlotId,
        string playerId,
        string characterId,
        string roundId,
        string audioRelativePath,
        int duration)
    {
        Assert.Throws<GameRuleException>(() => new VoiceTake(
            takeId,
            voiceSlotId,
            playerId,
            characterId,
            roundId,
            audioRelativePath,
            duration,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void VoiceTakeStore_stores_and_retrieves_takes()
    {
        var store = new VoiceTakeStore();
        var take1 = new VoiceTake("take-1", "slot-1", "player-1", "char-1", "round-1", "path-1.wav", 1500, DateTimeOffset.UtcNow);
        var take2 = new VoiceTake("take-2", "slot-1", "player-1", "char-1", "round-1", "path-2.wav", 1600, DateTimeOffset.UtcNow);

        store.AddTake(take1);
        Assert.True(store.HasTakeForSlot("slot-1"));
        Assert.Equal("take-1", store.GetLatestTakeForSlot("slot-1")?.TakeId);

        // Re-recording the slot updates latest take
        store.AddTake(take2);
        Assert.Equal("take-2", store.GetLatestTakeForSlot("slot-1")?.TakeId);
        Assert.Equal(2, store.GetAllTakes().Count);
    }
}
