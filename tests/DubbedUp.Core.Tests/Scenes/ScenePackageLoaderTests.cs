using DubbedUp.Core.Game;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Tests.ProjectFormat;
using Xunit;

namespace DubbedUp.Core.Tests.Scenes;

public sealed class ScenePackageLoaderTests
{
    [Fact]
    public void LoadPackageFromDirectory_LoadsValidPackage_WithResolvedMedia()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dubbedup_test_scene_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var mediaDir = Path.Combine(tempDir, "media");
            Directory.CreateDirectory(mediaDir);

            var scene = TestDocuments.ValidScene();
            var json = ProjectJsonSerializer.SerializeScene(scene);
            File.WriteAllText(Path.Combine(tempDir, "scene.json"), json);

            var videoPath = Path.Combine(tempDir, "media", "scene.ogv");
            File.WriteAllText(videoPath, "dummy video content");

            var previewPath = Path.Combine(tempDir, "preview.png");
            File.WriteAllText(previewPath, "dummy preview content");

            var package = ScenePackageLoader.LoadPackageFromDirectory(tempDir);

            Assert.NotNull(package);
            Assert.Equal("museum-mixup", package.SceneId);
            Assert.Equal("Museum Mix-up", package.Title);
            Assert.Equal(12_000, package.DurationMilliseconds);
            Assert.Equal(Path.GetFullPath(videoPath), package.VideoFilePath);
            Assert.Equal(Path.GetFullPath(previewPath), package.ThumbnailFilePath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadPackageFromDirectory_Throws_WhenDirectoryNotFound()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            ScenePackageLoader.LoadPackageFromDirectory("non_existent_folder_path_xyz_123"));
    }

    [Fact]
    public void LoadPackageFromDirectory_Throws_WhenSceneJsonMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dubbedup_test_empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            Assert.Throws<FileNotFoundException>(() =>
                ScenePackageLoader.LoadPackageFromDirectory(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DiscoverPackages_FindsMultipleValidPackages_InRootFolder()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), $"dubbedup_test_root_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        try
        {
            // Scene 1
            var scene1Dir = Path.Combine(rootDir, "scene1");
            Directory.CreateDirectory(scene1Dir);
            var scene1 = TestDocuments.ValidScene() with { SceneId = "scene-1", Title = "Alpha Scene" };
            File.WriteAllText(Path.Combine(scene1Dir, "scene.json"), ProjectJsonSerializer.SerializeScene(scene1));

            // Scene 2
            var scene2Dir = Path.Combine(rootDir, "scene2");
            Directory.CreateDirectory(scene2Dir);
            var scene2 = TestDocuments.ValidScene() with { SceneId = "scene-2", Title = "Beta Scene" };
            File.WriteAllText(Path.Combine(scene2Dir, "scene.json"), ProjectJsonSerializer.SerializeScene(scene2));

            // Invalid Scene folder (corrupted JSON)
            var scene3Dir = Path.Combine(rootDir, "invalid_scene");
            Directory.CreateDirectory(scene3Dir);
            File.WriteAllText(Path.Combine(scene3Dir, "scene.json"), "{ invalid json }");

            var packages = ScenePackageLoader.DiscoverPackages(rootDir);

            Assert.Equal(2, packages.Count);
            Assert.Equal("Alpha Scene", packages[0].Title);
            Assert.Equal("Beta Scene", packages[1].Title);
        }
        finally
        {
            if (Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, recursive: true);
            }
        }
    }

    [Fact]
    public void GameMode_HasExpectedValues()
    {
        Assert.Equal(0, (int)GameMode.CoopDubbing);
        Assert.Equal(1, (int)GameMode.CompetitiveVoting);
    }
}
