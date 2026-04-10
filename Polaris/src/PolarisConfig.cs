namespace Polaris;

/// <summary>
/// Server-side mod configuration for Polaris. Edit ModConfig/polarisconfig.json to change values.
/// </summary>
public class PolarisConfig
{
    /// <summary>
    /// Exponent used in the main (Knowledge) level experience curve.
    /// Formula: expToReachLevel = 100 * (level - 1) ^ Curve
    /// Higher values make high levels require exponentially more experience.
    /// </summary>
    public float MainExpCurve { get; set; } = 1.5f;

    /// <summary>
    /// Maximum Knowledge (main) level a player can reach.
    /// </summary>
    public int MaxMainLevel { get; set; } = 100;

    /// <summary>
    /// Maximum level cap applied to every constellation skill.
    /// </summary>
    public int MaxSkillLevel { get; set; } = 100;

    /// <summary>
    /// Exponent used in each constellation's experience curve.
    /// Formula: expToReachLevel = 100 * (level - 1) ^ Curve
    /// Higher values make high levels require exponentially more experience.
    /// </summary>
    public float SurvivalExpCurve { get; set; } = 1.5f;
    public float TimeExpCurve { get; set; } = 1.5f;
    public float ExcavationExpCurve { get; set; } = 1.5f;
    public float ForestryExpCurve { get; set; } = 1.5f;
    public float HorticultureExpCurve { get; set; } = 1.5f;
    public float HuntingExpCurve { get; set; } = 1.5f;
    public float CombatExpCurve { get; set; } = 1.5f;
    public float SmithingExpCurve { get; set; } = 1.5f;
    public float FormingExpCurve { get; set; } = 1.5f;
    public float CookingExpCurve { get; set; } = 1.5f;
    public float CraftingExpCurve { get; set; } = 1.5f;
    public float TradeExpCurve { get; set; } = 1.5f;
    public float MycologyExpCurve { get; set; } = 1.5f;
}
