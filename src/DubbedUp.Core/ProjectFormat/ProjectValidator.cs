using DubbedUp.Core.Scenes;

namespace DubbedUp.Core.ProjectFormat;

public static class ProjectValidator
{
    public static void Validate(OfficialSceneDocument scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        OfficialSceneValidator.Validate(scene);
    }

    public static void Validate(DubProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);
        DubProjectValidator.Validate(project);
    }

    public static void Validate(DubProjectDocument project, OfficialSceneDocument scene)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scene);
        OfficialSceneValidator.Validate(scene);
        DubProjectValidator.Validate(project);

        var errors = new List<string>();
        if (!string.Equals(project.SceneId, scene.SceneId, StringComparison.Ordinal))
        {
            errors.Add($"project sceneId '{project.SceneId}' does not match scene '{scene.SceneId}'.");
        }

        var voiceSlotIds = scene.VoiceSlots
            .Select(slot => slot.VoiceSlotId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var selection in project.SelectedTakes)
        {
            if (!voiceSlotIds.Contains(selection.VoiceSlotId))
            {
                errors.Add($"selected take '{selection.TakeId}' references unknown voiceSlotId '{selection.VoiceSlotId}'.");
            }
        }

        ValidationRules.ThrowIfInvalid(errors);
    }
}
