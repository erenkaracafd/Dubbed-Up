namespace DubbedUp.Core.Game;

public sealed class GameRuleException(string message) : InvalidOperationException(message);
