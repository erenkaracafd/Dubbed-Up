using System.Reflection;
using DubbedUp.Core;
using Xunit;

namespace DubbedUp.Core.Tests.Architecture;

public sealed class CoreDependencyTests
{
    [Fact]
    public void Core_has_no_engine_or_platform_dependencies()
    {
        var forbiddenPrefixes = new[] { "Godot", "Steamworks", "FFmpeg" };
        var references = CoreAssembly.MarkerType.Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            reference => forbiddenPrefixes.Any(
                prefix => reference.Name?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public void Core_targets_a_distinct_assembly()
    {
        Assert.Equal("DubbedUp.Core", CoreAssembly.MarkerType.Assembly.GetName().Name);
    }
}
