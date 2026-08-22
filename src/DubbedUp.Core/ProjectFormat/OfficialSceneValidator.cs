using DubbedUp.Core.Characters;
using DubbedUp.Core.Scenes;

namespace DubbedUp.Core.ProjectFormat;

internal static class OfficialSceneValidator
{
    public static void Validate(OfficialSceneDocument scene)
    {
        var errors = new List<string>();
        ValidationRules.ValidateSchemaVersion(scene.SchemaVersion, "scene", errors);
        ValidationRules.ValidateIdentifier(scene.SceneId, "sceneId", errors);
        ValidationRules.ValidateRequiredText(scene.Title, "title", errors);

        if (scene.DurationMilliseconds <= 0)
        {
            errors.Add("durationMilliseconds must be greater than zero.");
        }

        ValidateSourceMedia(scene, errors);
        ValidateCharacters(scene, errors);
        ValidateVoiceSlots(scene, errors);
        ValidateTimeline(scene, errors);
        ValidationRules.ThrowIfInvalid(errors);
    }

    private static void ValidateSourceMedia(OfficialSceneDocument scene, ICollection<string> errors)
    {
        if (scene.SourceMedia is null || scene.SourceMedia.Count == 0)
        {
            errors.Add("sourceMedia must contain at least one asset.");
            return;
        }

        foreach (var asset in scene.SourceMedia)
        {
            if (asset is null)
            {
                errors.Add("sourceMedia cannot contain null entries.");
                continue;
            }

            ValidationRules.ValidateIdentifier(asset.MediaId, "sourceMedia.mediaId", errors);
            ValidationRules.ValidateRelativePath(asset.RelativePath, "sourceMedia.relativePath", errors);
        }

        ValidationRules.AddDuplicateErrors(
            scene.SourceMedia.Where(asset => asset is not null).Select(asset => asset.MediaId),
            "sourceMedia.mediaId",
            errors);

        var videoCount = scene.SourceMedia.Count(asset => asset?.Role == SourceMediaRole.SceneVideo);
        if (videoCount != 1)
        {
            errors.Add("sourceMedia must contain exactly one sceneVideo asset.");
        }
    }

    private static void ValidateCharacters(OfficialSceneDocument scene, ICollection<string> errors)
    {
        if (scene.Characters is null || scene.Characters.Count == 0)
        {
            errors.Add("characters must contain at least one character.");
            return;
        }

        foreach (var character in scene.Characters)
        {
            if (character is null)
            {
                errors.Add("characters cannot contain null entries.");
                continue;
            }

            ValidationRules.ValidateIdentifier(character.CharacterId, "characters.characterId", errors);
            ValidationRules.ValidateRequiredText(character.DisplayName, "characters.displayName", errors);
        }

        ValidationRules.AddDuplicateErrors(
            scene.Characters.Where(character => character is not null).Select(character => character.CharacterId),
            "characters.characterId",
            errors);
    }

    private static void ValidateVoiceSlots(OfficialSceneDocument scene, ICollection<string> errors)
    {
        if (scene.VoiceSlots is null || scene.VoiceSlots.Count == 0)
        {
            errors.Add("voiceSlots must contain at least one slot.");
            return;
        }

        var characterIds = (scene.Characters ?? Array.Empty<CharacterDefinition>())
            .Where(character => character is not null)
            .Select(character => character.CharacterId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var slot in scene.VoiceSlots)
        {
            if (slot is null)
            {
                errors.Add("voiceSlots cannot contain null entries.");
                continue;
            }

            ValidationRules.ValidateIdentifier(slot.VoiceSlotId, "voiceSlots.voiceSlotId", errors);
            ValidationRules.ValidateIdentifier(slot.CharacterId, "voiceSlots.characterId", errors);
            ValidationRules.ValidateRequiredText(slot.Prompt, "voiceSlots.prompt", errors);

            if (!characterIds.Contains(slot.CharacterId))
            {
                errors.Add($"voiceSlotId '{slot.VoiceSlotId}' references unknown characterId '{slot.CharacterId}'.");
            }
        }

        ValidationRules.AddDuplicateErrors(
            scene.VoiceSlots.Where(slot => slot is not null).Select(slot => slot.VoiceSlotId),
            "voiceSlots.voiceSlotId",
            errors);
    }

    private static void ValidateTimeline(OfficialSceneDocument scene, ICollection<string> errors)
    {
        if (scene.Timeline is null || scene.Timeline.Count == 0)
        {
            errors.Add("timeline must contain at least one entry.");
            return;
        }

        var voiceSlotIds = (scene.VoiceSlots ?? Array.Empty<VoiceSlotDefinition>())
            .Where(slot => slot is not null)
            .Select(slot => slot.VoiceSlotId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var entry in scene.Timeline)
        {
            if (entry is null)
            {
                errors.Add("timeline cannot contain null entries.");
                continue;
            }

            ValidationRules.ValidateIdentifier(entry.TimelineEntryId, "timeline.timelineEntryId", errors);
            ValidationRules.ValidateIdentifier(entry.VoiceSlotId, "timeline.voiceSlotId", errors);

            if (!voiceSlotIds.Contains(entry.VoiceSlotId))
            {
                errors.Add($"timelineEntryId '{entry.TimelineEntryId}' references unknown voiceSlotId '{entry.VoiceSlotId}'.");
            }

            if (entry.StartMilliseconds < 0)
            {
                errors.Add($"timelineEntryId '{entry.TimelineEntryId}' starts before zero.");
            }

            if (entry.EndMilliseconds <= entry.StartMilliseconds)
            {
                errors.Add($"timelineEntryId '{entry.TimelineEntryId}' must end after it starts.");
            }

            if (entry.EndMilliseconds > scene.DurationMilliseconds)
            {
                errors.Add($"timelineEntryId '{entry.TimelineEntryId}' ends after the scene duration.");
            }
        }

        var entries = scene.Timeline.Where(entry => entry is not null).ToArray();
        ValidationRules.AddDuplicateErrors(
            entries.Select(entry => entry.TimelineEntryId),
            "timeline.timelineEntryId",
            errors);
        ValidationRules.AddDuplicateErrors(
            entries.Select(entry => entry.VoiceSlotId),
            "timeline.voiceSlotId",
            errors);

        var placedSlotIds = entries.Select(entry => entry.VoiceSlotId).ToHashSet(StringComparer.Ordinal);
        foreach (var voiceSlotId in voiceSlotIds.Where(id => !placedSlotIds.Contains(id)))
        {
            errors.Add($"voiceSlotId '{voiceSlotId}' has no timeline entry.");
        }
    }
}
