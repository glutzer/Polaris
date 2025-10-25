namespace Polaris;

/// <summary>
/// Base class for a stat aggregator.
/// Takes a dictionary of string->int stats and applies them to a player.
/// </summary>
public abstract class PassiveAggregator
{
    /// <summary>
    /// Remove all related stats or bonuses.
    /// Called on server shutdown, player leaving, or when stats are recalculated.
    /// </summary>
    public abstract void RemoveStats(PassiveContext statContext);

    /// <summary>
    /// Add bonuses
    /// </summary>
    public abstract void AddStats(PassiveContext statContext);
}