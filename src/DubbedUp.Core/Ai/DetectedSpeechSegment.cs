namespace DubbedUp.Core.Ai;

/// <summary>
/// Represents a detected speech utterance or dialogue segment extracted from an audio/video file.
/// </summary>
public sealed record DetectedSpeechSegment
{
    public DetectedSpeechSegment(
        string characterId,
        string speakerDisplayName,
        string prompt,
        long startMilliseconds,
        long endMilliseconds,
        double confidence = 1.0)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Segment prompt cannot be null or empty.", nameof(prompt));
        }

        if (endMilliseconds <= startMilliseconds)
        {
            throw new ArgumentException("End milliseconds must be strictly greater than start milliseconds.", nameof(endMilliseconds));
        }

        CharacterId = string.IsNullOrWhiteSpace(characterId) ? "speaker-1" : characterId;
        SpeakerDisplayName = string.IsNullOrWhiteSpace(speakerDisplayName) ? "Character" : speakerDisplayName;
        Prompt = prompt.Trim();
        StartMilliseconds = startMilliseconds;
        EndMilliseconds = endMilliseconds;
        Confidence = Math.Clamp(confidence, 0.0, 1.0);
    }

    public string CharacterId { get; init; }
    public string SpeakerDisplayName { get; init; }
    public string Prompt { get; init; }
    public long StartMilliseconds { get; init; }
    public long EndMilliseconds { get; init; }
    public double Confidence { get; init; }

    public long DurationMilliseconds => EndMilliseconds - StartMilliseconds;
}
