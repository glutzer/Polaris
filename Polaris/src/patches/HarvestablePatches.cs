using HarmonyLib;
using Vintagestory.GameContent;

namespace Polaris;

public class HarvestablePatches
{
    [HarmonyPatch(typeof(EntityBehaviorHarvestable), "SetHarvested")]
    public class SetHarvestedPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityBehaviorHarvestable __instance, IPlayer byPlayer, InventoryGeneric ___inv)
        {
            if (__instance.entity.World.Side == EnumAppSide.Client) return;
            if (byPlayer == null) return;

            EntityBehaviorPolarisExp? expBehavior = __instance.entity.GetBehavior<EntityBehaviorPolarisExp>();
            if (expBehavior == null) return;

            SystemPolaris.AddExperience("Hunting", byPlayer, (expBehavior?.HarvestXp ?? EntityBehaviorPolarisExp.DefaultHarvestExp) * EntityBehaviorPolarisExp.HARVESTING_EXP_MULTI);
        }
    }
}