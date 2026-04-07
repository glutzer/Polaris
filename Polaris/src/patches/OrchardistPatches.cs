using HarmonyLib;
using System;
using Vintagestory.GameContent;

namespace Polaris;

public class OrchardistPatches
{
    [HarmonyPatch(typeof(BlockFruitTreeBranch), "GetDrops")]
    public class FruitTreeBranchPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref ItemStack[] __result, IWorldAccessor world, IPlayer byPlayer)
        {
            if (byPlayer?.Entity == null) return;
            if (!byPlayer.Entity.TryGetExtraStat("orchardistBonus", out float bonus) || bonus <= 0f) return;
            if (__result == null || __result.Length == 0) return;

            foreach (ItemStack stack in __result)
            {
                stack.StackSize = (int)MathF.Round(stack.StackSize * (1f + bonus));
            }
        }
    }
}
