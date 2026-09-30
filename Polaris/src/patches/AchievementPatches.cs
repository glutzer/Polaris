using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace Polaris;

[HarmonyPatch(typeof(EntityBehaviorHunger), nameof(EntityBehaviorHunger.UpdateNutrientHealthBoost))]
public static class NutritionAchievementPatch
{
    [HarmonyPostfix]
    public static void Postfix(EntityBehaviorHunger __instance)
    {
        if (__instance.entity.Api.Side != EnumAppSide.Server ||
            __instance.entity is not EntityPlayer player || player.Player == null) return;

        float maximum = __instance.MaxSaturation;
        if (maximum > 0f &&
            __instance.FruitLevel >= maximum &&
            __instance.VegetableLevel >= maximum &&
            __instance.ProteinLevel >= maximum &&
            __instance.GrainLevel >= maximum &&
            __instance.DairyLevel >= maximum)
        {
            SystemPolaris.TriggerAchievement("balancedDiet", player.Player);
        }
    }
}

[HarmonyPatch(typeof(Entity), nameof(Entity.Die))]
public static class AchievementPatches
{
    [HarmonyPrefix]
    public static void Prefix(Entity __instance, EnumDespawnReason reason, DamageSource damageSourceForDeath)
    {
        if (__instance.Api.Side != EnumAppSide.Server || !__instance.Alive || reason != EnumDespawnReason.Death) return;
        string path = __instance.Code?.Path ?? "";
        if (__instance.Code?.Domain != "game" || (path != "wolf" && !path.StartsWith("wolf-"))) return;
        IPlayer? killer = (damageSourceForDeath?.CauseEntity as EntityPlayer)?.Player
            ?? (damageSourceForDeath?.SourceEntity as EntityPlayer)?.Player;
        if (killer == null && damageSourceForDeath?.SourceEntity is EntityProjectile projectile)
            killer = (projectile.FiredBy as EntityPlayer)?.Player ?? projectile.FiredBy as IPlayer;
        if (killer != null) SystemPolaris.TriggerAchievement("firstBlood", killer);
    }
}
