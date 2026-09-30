using HarmonyLib;
using Vintagestory.GameContent;

namespace Polaris;

[HarmonyPatch(typeof(EntityBehaviorHunger), "ReduceSaturation")]
public static class FastingPatches
{
    [HarmonyPrefix]
    public static bool Prefix(EntityBehaviorHunger __instance, ref bool __result)
    {
        if (__instance.entity.Api.Side != EnumAppSide.Server ||
            __instance.entity is not EntityPlayer player ||
            !player.Controls.FloorSitting || player.GetSkillLevel("fasting") <= 0) return true;

        __result = true;
        return false;
    }
}
