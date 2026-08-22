using DubbedUp.Core.Characters;
using DubbedUp.Core.Timeline;

namespace DubbedUp.Core.Scenes;

public sealed record OfficialSceneDocument
{
    public required int SchemaVersion { get; init; }

    public required string SceneId { get; init; }

    public required string Title { get; init; }

    public required long DurationMilliseconds { get; init; }

    public required IReadOnlyList<SourceMediaAsset> SourceMedia { get; init; }

    public required IReadOnlyList<CharacterDefinition> Characters { get; init; }

    public required IReadOnlyList<VoiceSlotDefinition> VoiceSlots { get; init; }

    public required IReadOnlyList<TimelineEntry> Timeline { get; init; }
}
