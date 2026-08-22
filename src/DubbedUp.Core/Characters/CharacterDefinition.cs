namespace DubbedUp.Core.Characters;

public sealed record CharacterDefinition
{
    public required string CharacterId { get; init; }

    public required string DisplayName { get; init; }
}
