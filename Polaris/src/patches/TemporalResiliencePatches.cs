using HarmonyLib;
using System;
using Vintagestory.GameContent;

namespace Polaris;

public class TemporalResiliencePatches
{
    [HarmonyPatch(typeof(EntityBehaviorTemporalStabilityAffected), "OnGameTick")]
    public class TemporalResiliencePatch
    {
        [HarmonyPrefix]
        public static void Prefix(EntityBehaviorTemporalStabilityAffected __instance, out double __state)
        {
            __state = __instance.OwnStability;
        }

        [HarmonyPostfix]
        public static void Postfix(EntityBehaviorTemporalStabilityAffected __instance, double __state)
        {
            if (__instance.entity?.World.Api.Side != EnumAppSide.Server) return;
            if (__instance.entity is not EntityPlayer player) return;
            if (!player.TryGetExtraStat("temporalResilience", out float resilience) || resilience == 1f) return;

            double drain = __state - __instance.OwnStability;
            if (drain <= 0) return;

            // Scale the drain by the resilience multiplier.
            // resilience < 1 reduces drain, resilience > 1 amplifies it.
            __instance.OwnStability = Math.Clamp(__state - drain * resilience, 0.0, 1.0);
        }
    }
}
