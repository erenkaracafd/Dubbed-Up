namespace DubbedUp.Core.ProjectFormat;

public sealed record DubProjectDocument
{
    public required int SchemaVersion { get; init; }

    public required string ProjectId { get; init; }

    public required string SceneId { get; init; }

    public required IReadOnlyList<VoiceTakeSelection> SelectedTakes { get; init; }
}
