using Vintagestory.GameContent;

namespace Polaris;

public class HungerAggregator : PassiveAggregator
{
    public override void AddStats(PassiveContext statContext)
    {
        if (statContext.Player.TryGetExtraStat("satMultiplier", out float multi))
        {
            float baseHunger = 1500f;
            float newMaxSaturation = baseHunger * multi;
            if (newMaxSaturation <= 1f) newMaxSaturation = 1f;

            SetMaxSaturation(statContext.Player.GetHunger(), (int)newMaxSaturation);
        }
    }

    public override void RemoveStats(PassiveContext statContext)
    {
        SetMaxSaturation(statContext.Player.GetHunger(), 1500f);
    }

    private static void SetMaxSaturation(EntityBehaviorHunger behavior, float value)
    {
        // This...does not actually update until you lose hunger again.
        behavior.MaxSaturation = value;
        behavior.UpdateNutrientHealthBoost();
    }
}