namespace Polaris;

/// <summary>Balance values for experience rewards and level progression.</summary>
public static class ExpGlobals
{
    public const float MainExpCurve = 2.5f;
    public const float ConstellationExpCurve = 1.3f;
    public const float BaseExpRequirement = 100f;
    public const float SpecializationExperienceBonus = 0.4f;
    public const float SurvivalRewardIntervalSeconds = 10f;
    public const float SurvivalExperiencePerInterval = 1f;
    public const float TimeExperiencePerStability = 200f;
    public const float MinimumStabilityLoss = 0.001f;
    public const float RockExperience = 1f;
    public const float GemExperience = 2f;
    public const float PoorOreExperience = 2f;
    public const float MediumOreExperience = 3f;
    public const float RichOreExperience = 4f;
    public const float BountifulOreExperience = 5f;
    public const float UngradedOreExperience = 2f;
    public const float ForestryExperiencePerWoodBlock = 1f;
    public const float CropExperienceMultiplier = 50f;
    public const float CropExperiencePerGrowthMonth = 0.5f;
    public const float ImmatureCropExperienceFactor = 0.5f;
    public const float MaximumBaseCropExperience = 3f;
    public const float HoneyHarvestExperience = 10f;
    public const float CombatKillExperienceMultiplier = 10f;
    public const float HuntingKillExperienceMultiplier = 5f;
    public const float HuntingHarvestExperienceMultiplier = 100f;
    public const float DefaultHarvestExperience = 1f;
    public const float DrifterHarvestExperience = 0.25f;
    public const float SmithingCompletionExperience = 10f;
    public const float SmithingExperiencePerHit = 0.2f;
    public const float MetalProductionExperiencePerIngot = 20f;
    public const float MoldExperiencePerMetalUnit = 0.2f;
    public const float FormingExperiencePerClay = 10f;
    public const float KnappingExperience = 20f;
    public const float CookingExperiencePerServing = 20f;
    public const float TradeExperiencePerGear = 20f;
    public const float RepairExperiencePerDurability = 50f;
    public const float MushroomHarvestExperience = 100f;
    public const int SurvivalDeathPenaltyDivisor = 2;

    public static float GetEntityExperience(string reward) => reward switch
    {
        "tier1" => 1f,
        "tier2" => 2f,
        "tier3" => 3f,
        "tier4" => 4f,
        "tier5" => 5f,
        "tier6" => 6f,
        "tier7" => 7f,
        "elite" => 10f,
        "bell" => 30f,
        "boss" => 100f,
        _ => 0f
    };
}
