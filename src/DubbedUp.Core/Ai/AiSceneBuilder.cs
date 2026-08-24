using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;

namespace DubbedUp.Core.Ai;

/// <summary>
/// Converts detected speech segments and video metadata into a fully validated OfficialSceneDocument.
/// Ensures characters, voice slots, and timeline entries are correctly mapped and sorted.
/// </summary>
public static class AiSceneBuilder
{
    public static OfficialSceneDocument BuildScene(
        string title,
        string sceneId,
        long totalDurationMilliseconds,
        string videoRelativePath,
        IReadOnlyList<DetectedSpeechSegment> segments)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(sceneId))
        {
            throw new ArgumentException("Scene ID cannot be empty.", nameof(sceneId));
        }

        if (segments is null || segments.Count == 0)
        {
            throw new ArgumentException("At least one speech segment is required to build a scene.", nameof(segments));
        }

        var normalizedSceneId = sceneId.Trim().ToLowerInvariant().Replace(' ', '-').Replace(".", "");

        // Determine unique characters
        var characterMap = new Dictionary<string, CharacterDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in segments)
        {
            if (!characterMap.ContainsKey(segment.CharacterId))
            {
                characterMap[segment.CharacterId] = new CharacterDefinition
                {
                    CharacterId = segment.CharacterId,
                    DisplayName = segment.SpeakerDisplayName
                };
            }
        }

        var voiceSlots = new List<VoiceSlotDefinition>();
        var timeline = new List<TimelineEntry>();

        // Sort segments by start time
        var sortedSegments = segments.OrderBy(s => s.StartMilliseconds).ToList();

        // Calculate minimum required duration
        var maxSegmentEnd = sortedSegments.Max(s => s.EndMilliseconds);
        var effectiveDuration = Math.Max(totalDurationMilliseconds, maxSegmentEnd + 1000); // 1s buffer

        for (int i = 0; i < sortedSegments.Count; i++)
        {
            var seg = sortedSegments[i];
            var slotId = $"slot-{i + 1}";
            var entryId = $"entry-{i + 1}";

            voiceSlots.Add(new VoiceSlotDefinition
            {
                VoiceSlotId = slotId,
                CharacterId = seg.CharacterId,
                Prompt = seg.Prompt
            });

            timeline.Add(new TimelineEntry
            {
                TimelineEntryId = entryId,
                VoiceSlotId = slotId,
                StartMilliseconds = seg.StartMilliseconds,
                EndMilliseconds = seg.EndMilliseconds
            });
        }

        var doc = new OfficialSceneDocument
        {
            SchemaVersion = ProjectSchema.CurrentVersion,
            SceneId = normalizedSceneId,
            Title = title.Trim(),
            DurationMilliseconds = effectiveDuration,
            SourceMedia =
            [
                new SourceMediaAsset
                {
                    MediaId = "scene-video",
                    Role = SourceMediaRole.SceneVideo,
                    RelativePath = string.IsNullOrWhiteSpace(videoRelativePath) ? "media/video.mp4" : videoRelativePath.Trim()
                }
            ],
            Characters = characterMap.Values.ToList(),
            VoiceSlots = voiceSlots,
            Timeline = timeline
        };

        ProjectValidator.Validate(doc);
        return doc;
    }
}

