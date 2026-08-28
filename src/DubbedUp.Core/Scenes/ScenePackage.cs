namespace DubbedUp.Core.Scenes;

/// <summary>
/// Represents a discovered scene package containing the validated scene document,
/// directory path, and resolved local media asset paths.
/// </summary>
public sealed record ScenePackage
{
    public ScenePackage(
        OfficialSceneDocument document,
        string packageDirectory,
        string? videoFilePath = null,
        string? thumbnailFilePath = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        PackageDirectory = packageDirectory ?? throw new ArgumentNullException(nameof(packageDirectory));
        VideoFilePath = videoFilePath;
        ThumbnailFilePath = thumbnailFilePath;
    }

    /// <summary>
    /// The validated scene definition document.
    /// </summary>
    public OfficialSceneDocument Document { get; init; }

    /// <summary>
    /// The absolute or normalized folder path containing this scene package.
    /// </summary>
    public string PackageDirectory { get; init; }

    /// <summary>
    /// The resolved path to the primary scene video file (e.g. video.mp4, video.ogv), if present on disk.
    /// </summary>
    public string? VideoFilePath { get; init; }

    /// <summary>
    /// The resolved path to the scene preview image (e.g. preview.png, thumbnail.png), if present on disk.
    /// </summary>
    public string? ThumbnailFilePath { get; init; }

    /// <summary>
    /// Scene unique ID shortcut.
    /// </summary>
    public string SceneId => Document.SceneId;

    /// <summary>
    /// Scene display title shortcut.
    /// </summary>
    public string Title => Document.Title;

    /// <summary>
    /// Total duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds => Document.DurationMilliseconds;
}
