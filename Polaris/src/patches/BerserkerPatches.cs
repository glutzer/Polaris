using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace Polaris;

public class BerserkerPatches
{
    [HarmonyPatch(typeof(Entity), "Die")]
    public class BerserkerPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Entity __instance, DamageSource damageSourceForDeath)
        {
            if (__instance.Api.Side != EnumAppSide.Server) return;
            if (__instance is EntityPlayer) return;
            if (damageSourceForDeath?.SourceEntity is not EntityPlayer attacker) return;
            if (attacker.GetSkillLevel("berserker") <= 0) return;

            EntityBehaviorHealth health = attacker.GetHealth();
            health.Health = System.MathF.Min(health.MaxHealth, health.Health + 2f);
        }
    }
}
