namespace Polaris;

/// <summary>
/// Takes values for a stat which a player would have, applies them.
/// A 0.01 multiplier would take a 0-100 int value and make it 0-1 float.
/// </summary>
public class StatAggregator : PassiveAggregator
{
    private readonly string statName;
    private readonly float multiplier;

    public StatAggregator(string statName, float multiplier = 0.01f)
    {
        this.statName = statName;
        this.multiplier = multiplier;
    }

    public override void AddStats(PassiveContext statContext)
    {
        float value = statContext.StatValues.TryGetValue(statName, out int val) ? val : 0f;
        if (value == 0f) return;

        value *= multiplier;

        statContext.Player.Stats.Set(statName, "polaris", value, true);
    }

    public override void RemoveStats(PassiveContext statContext)
    {
        statContext.Player.Stats.Remove(statName, "polaris");
    }
}