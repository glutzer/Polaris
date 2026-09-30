namespace Polaris;

/// <summary>
/// Server-side mod configuration for Polaris. Edit ModConfig/polarisconfig.json to change values.
/// </summary>
public class PolarisConfig
{
    /// <summary>
    /// Maximum Knowledge (main) level a player can reach.
    /// </summary>
    public int MaxMainLevel { get; set; } = 100;

    /// <summary>
    /// Maximum level cap applied to every constellation skill.
    /// </summary>
    public int MaxSkillLevel { get; set; } = 100;

}
