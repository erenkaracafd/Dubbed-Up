namespace DubbedUp.Core.Game;

/// <summary>
/// Defines the gameplay mode for a session.
/// </summary>
public enum GameMode
{
    /// <summary>
    /// Cooperative dubbing mode: Players voice assigned characters and immediately watch
    /// the synchronized result together for pure party fun without competitive voting.
    /// </summary>
    CoopDubbing = 0,

    /// <summary>
    /// Competitive voting mode: Players voice assigned characters, watch the dub,
    /// cast votes for the best performance, and accumulate scores across rounds.
    /// </summary>
    CompetitiveVoting = 1,
}
