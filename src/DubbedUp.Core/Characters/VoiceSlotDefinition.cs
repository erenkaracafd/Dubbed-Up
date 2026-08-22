namespace DubbedUp.Core.Characters;

public sealed record VoiceSlotDefinition
{
    public required string VoiceSlotId { get; init; }

    public required string CharacterId { get; init; }

    public required string Prompt { get; init; }
}
