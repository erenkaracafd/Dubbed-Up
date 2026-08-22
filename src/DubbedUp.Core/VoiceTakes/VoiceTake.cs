using DubbedUp.Core.Game;

namespace DubbedUp.Core.VoiceTakes;

public sealed class VoiceTake
{
    public VoiceTake(
        string takeId,
        string voiceSlotId,
        string playerId,
        string characterId,
        string roundId,
        string audioRelativePath,
        int durationMilliseconds,
        DateTimeOffset recordedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(takeId))
        {
            throw new GameRuleException("takeId is required.");
        }

        if (string.IsNullOrWhiteSpace(voiceSlotId))
        {
            throw new GameRuleException("voiceSlotId is required.");
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            throw new GameRuleException("playerId is required.");
        }

        if (string.IsNullOrWhiteSpace(characterId))
        {
            throw new GameRuleException("characterId is required.");
        }

        if (string.IsNullOrWhiteSpace(roundId))
        {
            throw new GameRuleException("roundId is required.");
        }

        if (string.IsNullOrWhiteSpace(audioRelativePath))
        {
            throw new GameRuleException("audioRelativePath is required.");
        }

        if (durationMilliseconds < 0)
        {
            throw new GameRuleException("durationMilliseconds cannot be negative.");
        }

        TakeId = takeId;
        VoiceSlotId = voiceSlotId;
        PlayerId = playerId;
        CharacterId = characterId;
        RoundId = roundId;
        AudioRelativePath = audioRelativePath;
        DurationMilliseconds = durationMilliseconds;
        RecordedAtUtc = recordedAtUtc;
    }

    public string TakeId { get; }

    public string VoiceSlotId { get; }

    public string PlayerId { get; }

    public string CharacterId { get; }

    public string RoundId { get; }

    public string AudioRelativePath { get; }

    public int DurationMilliseconds { get; }

    public DateTimeOffset RecordedAtUtc { get; }
}
