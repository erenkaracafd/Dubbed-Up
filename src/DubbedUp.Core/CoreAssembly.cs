namespace DubbedUp.Core;

/// <summary>
/// Provides a stable assembly marker without introducing gameplay behavior.
/// </summary>
public static class CoreAssembly
{
    public static Type MarkerType => typeof(CoreAssembly);
}

