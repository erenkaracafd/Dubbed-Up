using DubbedUp.Core.Ai;
using DubbedUp.Core.ProjectFormat;
using Xunit;

namespace DubbedUp.Core.Tests.Ai;

public sealed class AiSceneBuilderTests
{
    [Fact]
    public void BuildScene_WithValidSegments_ProducesCorrectOfficialSceneDocument()
    {
        var segments = new List<DetectedSpeechSegment>
        {
            new("speaker-1", "Acey", "Please Speed I need this!", 1000, 5000),
            new("speaker-2", "IShowSpeed", "Why are you smiling?", 5500, 9000),
        };

        var doc = AiSceneBuilder.BuildScene("Speed Meme", "speed-meme", 10000, "media/speed.mp4", segments);

        Assert.NotNull(doc);
        Assert.Equal("speed-meme", doc.SceneId);
        Assert.Equal("Speed Meme", doc.Title);
        Assert.Equal(2, doc.Characters.Count);
        Assert.Equal(2, doc.VoiceSlots.Count);
        Assert.Equal(2, doc.Timeline.Count);
        Assert.Equal("Please Speed I need this!", doc.VoiceSlots[0].Prompt);
        Assert.Equal(1000, doc.Timeline[0].StartMilliseconds);
        Assert.Equal(5000, doc.Timeline[0].EndMilliseconds);
    }

    [Fact]
    public void BuildScene_WithEmptySegments_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AiSceneBuilder.BuildScene("Test", "test-id", 5000, "media/test.mp4", new List<DetectedSpeechSegment>()));
    }

    [Fact]
    public void DetectedSpeechSegment_InvalidDuration_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new DetectedSpeechSegment("spk", "Speaker", "Prompt text", 5000, 2000));
    }
}

