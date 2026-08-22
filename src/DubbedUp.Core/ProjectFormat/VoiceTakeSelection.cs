namespace DubbedUp.Core.ProjectFormat;

/// <summary>
/// Connects a project slot to voice data by identifier without embedding a
/// recording path or session/player state in the project document.
/// </summary>
public sealed record VoiceTakeSelection
{
    public required string VoiceSlotId { get; init; }

    public required string TakeId { get; init; }
}
