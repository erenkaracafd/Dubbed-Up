using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using Xunit;

namespace DubbedUp.Core.Tests.ProjectFormat;

public sealed class ProjectValidatorTests
{
    [Fact]
    public void Valid_scene_and_project_pass_cross_document_validation()
    {
        ProjectValidator.Validate(TestDocuments.ValidScene());
        ProjectValidator.Validate(TestDocuments.ValidProject(), TestDocuments.ValidScene());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void Unsupported_scene_schema_versions_are_rejected(int schemaVersion)
    {
        var scene = TestDocuments.ValidScene() with { SchemaVersion = schemaVersion };

        AssertInvalid(() => ProjectValidator.Validate(scene), "schemaVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Museum Mixup")]
    [InlineData("museum_mixup")]
    [InlineData("Museum-mixup")]
    [InlineData("museum--mixup")]
    public void Non_portable_identifiers_are_rejected(string sceneId)
    {
        var scene = TestDocuments.ValidScene() with { SceneId = sceneId };

        AssertInvalid(() => ProjectValidator.Validate(scene), "kebab-case");
    }

    [Theory]
    [InlineData("../scene.ogv")]
    [InlineData("/media/scene.ogv")]
    [InlineData("C:/media/scene.ogv")]
    [InlineData("media\\scene.ogv")]
    [InlineData("media//scene.ogv")]
    public void Unsafe_or_platform_specific_media_paths_are_rejected(string relativePath)
    {
        var scene = TestDocuments.ValidScene();
        var media = scene.SourceMedia.ToArray();
        media[0] = media[0] with { RelativePath = relativePath };

        AssertInvalid(
            () => ProjectValidator.Validate(scene with { SourceMedia = media }),
            "portable forward-slash relative path");
    }

    [Fact]
    public void Duplicate_character_identifiers_are_rejected()
    {
        var scene = TestDocuments.ValidScene();
        var characters = scene.Characters.Append(
            new CharacterDefinition { CharacterId = "guard", DisplayName = "Other Guard" }).ToArray();

        AssertInvalid(
            () => ProjectValidator.Validate(scene with { Characters = characters }),
            "duplicate identifier 'guard'");
    }

    [Fact]
    public void Voice_slot_must_reference_a_known_character()
    {
        var scene = TestDocuments.ValidScene();
        var slots = scene.VoiceSlots.ToArray();
        slots[0] = slots[0] with { CharacterId = "ghost" };

        AssertInvalid(
            () => ProjectValidator.Validate(scene with { VoiceSlots = slots }),
            "unknown characterId 'ghost'");
    }

    [Fact]
    public void Timeline_entry_must_reference_a_known_voice_slot()
    {
        var scene = TestDocuments.ValidScene();
        var timeline = scene.Timeline.ToArray();
        timeline[0] = timeline[0] with { VoiceSlotId = "missing-slot" };

        AssertInvalid(
            () => ProjectValidator.Validate(scene with { Timeline = timeline }),
            "unknown voiceSlotId 'missing-slot'");
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(100, 100)]
    [InlineData(100, 12_001)]
    public void Invalid_timeline_bounds_are_rejected(long startMilliseconds, long endMilliseconds)
    {
        var scene = TestDocuments.ValidScene();
        var timeline = scene.Timeline.ToArray();
        timeline[0] = timeline[0] with
        {
            StartMilliseconds = startMilliseconds,
            EndMilliseconds = endMilliseconds,
        };

        Assert.Throws<ProjectValidationException>(
            () => ProjectValidator.Validate(scene with { Timeline = timeline }));
    }

    [Fact]
    public void Every_voice_slot_requires_exactly_one_timeline_entry()
    {
        var scene = TestDocuments.ValidScene();

        AssertInvalid(
            () => ProjectValidator.Validate(scene with { Timeline = [scene.Timeline[0]] }),
            "has no timeline entry");

        var duplicatePlacement = scene.Timeline.Append(new TimelineEntry
        {
            TimelineEntryId = "entry-3",
            VoiceSlotId = "guard-line-1",
            StartMilliseconds = 8_000,
            EndMilliseconds = 9_000,
        }).ToArray();

        AssertInvalid(
            () => ProjectValidator.Validate(scene with { Timeline = duplicatePlacement }),
            "timeline.voiceSlotId contains duplicate");
    }

    [Fact]
    public void Project_rejects_duplicate_take_or_slot_selections()
    {
        var project = TestDocuments.ValidProject();
        var duplicate = project.SelectedTakes.Append(new VoiceTakeSelection
        {
            VoiceSlotId = "guard-line-1",
            TakeId = "take-tourist-1",
        }).ToArray();

        var exception = Assert.Throws<ProjectValidationException>(
            () => ProjectValidator.Validate(project with { SelectedTakes = duplicate }));

        Assert.Contains(exception.Errors, error => error.Contains("voiceSlotId contains duplicate", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("takeId contains duplicate", StringComparison.Ordinal));
    }

    [Fact]
    public void Cross_document_validation_rejects_scene_and_slot_mismatches()
    {
        var project = TestDocuments.ValidProject() with
        {
            SceneId = "different-scene",
            SelectedTakes =
            [
                new VoiceTakeSelection { VoiceSlotId = "unknown-slot", TakeId = "take-1" },
            ],
        };

        var exception = Assert.Throws<ProjectValidationException>(
            () => ProjectValidator.Validate(project, TestDocuments.ValidScene()));

        Assert.Contains(exception.Errors, error => error.Contains("does not match scene", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("unknown voiceSlotId", StringComparison.Ordinal));
    }

    private static void AssertInvalid(Action validation, string expectedErrorFragment)
    {
        var exception = Assert.Throws<ProjectValidationException>(validation);
        Assert.Contains(
            exception.Errors,
            error => error.Contains(expectedErrorFragment, StringComparison.Ordinal));
    }
}
