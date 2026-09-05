using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Tests.ProjectFormat;
using Xunit;

namespace DubbedUp.Core.Tests.Scenes;

public sealed class ScenePackageChecksumTests
{
    [Fact]
    public void Checksum_ReturnsDeterministicSha256Hex()
    {
        var doc1 = TestDocuments.ValidScene();
        var doc2 = TestDocuments.ValidScene();

        var package1 = new ScenePackage(doc1, @"C:\mock\scenes\1");
        var package2 = new ScenePackage(doc2, @"C:\mock\scenes\2");

        var checksum1 = package1.Checksum;
        var checksum2 = package2.Checksum;

        Assert.NotNull(checksum1);
        Assert.Equal(64, checksum1.Length);
        Assert.Equal(checksum1, checksum2);
        Assert.Equal(checksum1, package1.ComputeChecksum());
    }

    [Fact]
    public void Checksum_Differs_WhenSceneContentChanges()
    {
        var doc1 = TestDocuments.ValidScene();
        var doc2 = TestDocuments.ValidScene() with
        {
            Title = "Museum Mix-up (Edited)",
        };

        var package1 = new ScenePackage(doc1, @"C:\mock\scenes\1");
        var package2 = new ScenePackage(doc2, @"C:\mock\scenes\1");

        Assert.NotEqual(package1.Checksum, package2.Checksum);
    }

    [Fact]
    public void Checksum_Differs_WhenTimelineChanges()
    {
        var doc1 = TestDocuments.ValidScene();
        var doc2 = TestDocuments.ValidScene() with
        {
            DurationMilliseconds = 25_000,
        };

        var package1 = new ScenePackage(doc1, @"C:\mock\scenes\1");
        var package2 = new ScenePackage(doc2, @"C:\mock\scenes\1");

        Assert.NotEqual(package1.Checksum, package2.Checksum);
    }
}
