using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;

namespace DubbedUp.Core.Tests.ProjectFormat;

internal static class TestDocuments
{
    public static OfficialSceneDocument ValidScene() => new()
    {
        SchemaVersion = ProjectSchema.CurrentVersion,
        SceneId = "museum-mixup",
        Title = "Museum Mix-up",
        DurationMilliseconds = 12_000,
        SourceMedia =
        [
            new SourceMediaAsset
            {
                MediaId = "scene-video",
                Role = SourceMediaRole.SceneVideo,
                RelativePath = "media/scene.ogv",
            },
            new SourceMediaAsset
            {
                MediaId = "background-audio",
                Role = SourceMediaRole.BackgroundAudio,
                RelativePath = "media/background.ogg",
            },
        ],
        Characters =
        [
            new CharacterDefinition { CharacterId = "guard", DisplayName = "Guard" },
            new CharacterDefinition { CharacterId = "tourist", DisplayName = "Tourist" },
        ],
        VoiceSlots =
        [
            new VoiceSlotDefinition
            {
                VoiceSlotId = "guard-line-1",
                CharacterId = "guard",
                Prompt = "React to the suspicious statue.",
            },
            new VoiceSlotDefinition
            {
                VoiceSlotId = "tourist-line-1",
                CharacterId = "tourist",
                Prompt = "Explain why the statue moved.",
            },
        ],
        Timeline =
        [
            new TimelineEntry
            {
                TimelineEntryId = "entry-1",
                VoiceSlotId = "guard-line-1",
                StartMilliseconds = 1_500,
                EndMilliseconds = 4_300,
            },
            new TimelineEntry
            {
                TimelineEntryId = "entry-2",
                VoiceSlotId = "tourist-line-1",
                StartMilliseconds = 3_900,
                EndMilliseconds = 7_000,
            },
        ],
    };

    public static DubProjectDocument ValidProject() => new()
    {
        SchemaVersion = ProjectSchema.CurrentVersion,
        ProjectId = "friday-round-1",
        SceneId = "museum-mixup",
        SelectedTakes =
        [
            new VoiceTakeSelection { VoiceSlotId = "guard-line-1", TakeId = "take-guard-1" },
            new VoiceTakeSelection { VoiceSlotId = "tourist-line-1", TakeId = "take-tourist-1" },
        ],
    };
}
