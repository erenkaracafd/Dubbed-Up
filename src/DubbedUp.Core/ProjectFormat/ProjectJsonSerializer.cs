using System.Text.Json;
using System.Text.Json.Serialization;
using DubbedUp.Core.Scenes;

namespace DubbedUp.Core.ProjectFormat;

public static class ProjectJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static string SerializeScene(OfficialSceneDocument scene)
    {
        ProjectValidator.Validate(scene);
        return JsonSerializer.Serialize(scene, SerializerOptions);
    }

    public static OfficialSceneDocument DeserializeScene(string json)
    {
        var scene = Deserialize<OfficialSceneDocument>(json, "scene");
        ProjectValidator.Validate(scene);
        return scene;
    }

    public static string SerializeProject(DubProjectDocument project)
    {
        ProjectValidator.Validate(project);
        return JsonSerializer.Serialize(project, SerializerOptions);
    }

    public static DubProjectDocument DeserializeProject(string json)
    {
        var project = Deserialize<DubProjectDocument>(json, "project");
        ProjectValidator.Validate(project);
        return project;
    }

    private static T Deserialize<T>(string json, string documentName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ProjectValidationException([$"{documentName} JSON is required."]);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions)
                ?? throw new ProjectValidationException([$"{documentName} JSON produced no document."]);
        }
        catch (JsonException exception)
        {
            throw new ProjectValidationException([$"{documentName} JSON is malformed: {exception.Message}"]);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
