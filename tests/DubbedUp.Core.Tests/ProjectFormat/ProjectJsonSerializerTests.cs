using System.Text.Json;
using DubbedUp.Core.ProjectFormat;
using Xunit;

namespace DubbedUp.Core.Tests.ProjectFormat;

public sealed class ProjectJsonSerializerTests
{
    [Fact]
    public void Official_scene_round_trips_as_versioned_camel_case_json()
    {
        var json = ProjectJsonSerializer.SerializeScene(TestDocuments.ValidScene());
        var roundTripped = ProjectJsonSerializer.DeserializeScene(json);

        Assert.Equal(json, ProjectJsonSerializer.SerializeScene(roundTripped));
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"sceneVideo\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Godot", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dub_project_round_trips_without_embedding_other_data_categories()
    {
        var json = ProjectJsonSerializer.SerializeProject(TestDocuments.ValidProject());
        var roundTripped = ProjectJsonSerializer.DeserializeProject(json);

        Assert.Equal(json, ProjectJsonSerializer.SerializeProject(roundTripped));
        Assert.Contains("\"takeId\": \"take-guard-1\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceMedia", json, StringComparison.Ordinal);
        Assert.DoesNotContain("relativePath", json, StringComparison.Ordinal);
        Assert.DoesNotContain("audioPath", json, StringComparison.Ordinal);
        Assert.DoesNotContain("session", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("player", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_json_properties_are_rejected()
    {
        var json = ProjectJsonSerializer.SerializeProject(TestDocuments.ValidProject());
        var jsonWithUnknownProperty = json.Replace(
            "{",
            "{\n  \"futureField\": true,",
            StringComparison.Ordinal);

        var exception = Assert.Throws<ProjectValidationException>(
            () => ProjectJsonSerializer.DeserializeProject(jsonWithUnknownProperty));

        Assert.Contains(exception.Errors, error => error.Contains("malformed", StringComparison.Ordinal));
    }

    [Fact]
    public void Malformed_json_is_reported_as_project_validation_failure()
    {
        var exception = Assert.Throws<ProjectValidationException>(
            () => ProjectJsonSerializer.DeserializeScene("{ not-json }"));

        Assert.Contains(exception.Errors, error => error.Contains("malformed", StringComparison.Ordinal));
        Assert.IsNotType<JsonException>(exception);
    }
}
