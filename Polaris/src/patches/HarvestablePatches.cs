using HarmonyLib;
using Vintagestory.API.Datastructures;
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

            SystemPolaris.AddExperience("Hunting", byPlayer, (expBehavior?.HarvestXp ?? EntityBehaviorPolarisExp.DefaultHarvestExp) * EntityBehaviorPolarisExp.HARVESTING_EXP_MULTI);

            if (byPlayer.Entity.GetSkillLevel("biogenesis") > 0 && __instance.entity.World.Rand.NextDouble() <= 0.01)
            {
                Item? creatureItem = __instance.entity.World.GetItem(
                    new AssetLocation("game", $"creature-{__instance.entity.Code.Path}"));
                if (creatureItem != null)
                {
                    ItemStack creatureStack = new(creatureItem);
                    for (int i = 0; i < ___inv.Count; i++)
                    {
                        if (___inv[i].Empty)
                        {
                            ___inv[i].Itemstack = creatureStack;
                            TreeAttribute tree = new();
                            ___inv.ToTreeAttributes(tree);
                            __instance.entity.WatchedAttributes["harvestableInv"] = tree;
                            __instance.entity.WatchedAttributes.MarkPathDirty("harvestableInv");
                            break;
                        }
                    }
                }
            }
        }
    }
}