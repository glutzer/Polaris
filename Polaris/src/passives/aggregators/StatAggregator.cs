namespace Polaris;

/// <summary>
/// Takes values for a stat which a player would have, applies them.
/// </summary>
public class StatAggregator : PassiveAggregator
{
    private readonly string statName;

    public StatAggregator(string statName)
    {
        this.statName = statName;
    }

    public override void AddStats(PassiveContext statContext)
    {
        float value = statContext.FloatValues.TryGetValue(statName, out float val) ? val : 0f;
        if (value == 0f) return;

        statContext.Player.Stats.Set(statName, "polaris", value, true);
    }

    public override void RemoveStats(PassiveContext statContext)
    {
        statContext.Player.Stats.Remove(statName, "polaris");
    }
}