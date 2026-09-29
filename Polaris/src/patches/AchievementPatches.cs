using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace Polaris;

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
