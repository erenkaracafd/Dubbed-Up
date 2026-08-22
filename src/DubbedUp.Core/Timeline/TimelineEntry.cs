namespace DubbedUp.Core.Timeline;

public sealed record TimelineEntry
{
    public required string TimelineEntryId { get; init; }

    public required string VoiceSlotId { get; init; }

    public required long StartMilliseconds { get; init; }

    public required long EndMilliseconds { get; init; }
}
