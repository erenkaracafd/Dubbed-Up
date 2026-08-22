namespace DubbedUp.Core.ProjectFormat;

internal static class DubProjectValidator
{
    public static void Validate(DubProjectDocument project)
    {
        var errors = new List<string>();
        ValidationRules.ValidateSchemaVersion(project.SchemaVersion, "project", errors);
        ValidationRules.ValidateIdentifier(project.ProjectId, "projectId", errors);
        ValidationRules.ValidateIdentifier(project.SceneId, "sceneId", errors);

        if (project.SelectedTakes is null)
        {
            errors.Add("selectedTakes is required.");
            ValidationRules.ThrowIfInvalid(errors);
            return;
        }

        foreach (var selection in project.SelectedTakes)
        {
            if (selection is null)
            {
                errors.Add("selectedTakes cannot contain null entries.");
                continue;
            }

            ValidationRules.ValidateIdentifier(selection.VoiceSlotId, "selectedTakes.voiceSlotId", errors);
            ValidationRules.ValidateIdentifier(selection.TakeId, "selectedTakes.takeId", errors);
        }

        var selections = project.SelectedTakes.Where(selection => selection is not null).ToArray();
        ValidationRules.AddDuplicateErrors(
            selections.Select(selection => selection.VoiceSlotId),
            "selectedTakes.voiceSlotId",
            errors);
        ValidationRules.AddDuplicateErrors(
            selections.Select(selection => selection.TakeId),
            "selectedTakes.takeId",
            errors);
        ValidationRules.ThrowIfInvalid(errors);
    }
}
