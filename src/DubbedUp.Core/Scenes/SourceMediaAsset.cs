namespace DubbedUp.Core.Scenes;

public sealed record SourceMediaAsset
{
    public required string MediaId { get; init; }

    public required SourceMediaRole Role { get; init; }

    public required string RelativePath { get; init; }
}
