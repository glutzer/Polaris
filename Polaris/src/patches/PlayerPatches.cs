using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace Polaris;

public class PlayerPatches
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), "UpdateMaxHealth")]
    public class HealthPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityBehaviorHealth __instance)
        {
            Entity entity = __instance.entity;

            // Total health increase stat. 
            if (entity.TryGetExtraStat("healthMultiplier", out float healthMultiplier))
            {
                __instance.MaxHealth *= healthMultiplier;
            }
        }
    }
}