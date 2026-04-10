using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace Polaris;

public class PlayerPatches
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), "UpdateMaxHealth")]
    public class HealthPatch
    {
        [HarmonyPrefix]
        public static void Prefix(EntityBehaviorHealth __instance, out float __state)
        {
            __state = __instance.MaxHealth;
        }

        [HarmonyPostfix]
        public static void Postfix(EntityBehaviorHealth __instance, float __state)
        {
            Entity entity = __instance.entity;

            if (!entity.TryGetExtraStat("healthMultiplier", out float healthMultiplier)) return;

            float vanillaMax = __instance.MaxHealth;
            __instance.MaxHealth *= healthMultiplier;

            if (__instance.Health >= vanillaMax)
            {
                __instance.Health = __instance.MaxHealth;
            }
        }
    }
}