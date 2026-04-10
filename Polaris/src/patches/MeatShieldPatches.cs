using HarmonyLib;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Polaris;

public class MeatShieldPatches
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), "OnEntityReceiveDamage")]
    public class MeatShieldPatch
    {
        [HarmonyPrefix]
        public static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
        {
            if (__instance.entity?.World.Api.Side != EnumAppSide.Server) return;
            if (damageSource.Type == EnumDamageType.Heal) return;
            if (damage <= 0) return;
            if (__instance.entity is not EntityPlayer player) return;
            if (!player.TryGetExtraStat("meatShield", out float shield) || shield <= 0f) return;

            ITreeAttribute? hungerTree = player.WatchedAttributes.GetTreeAttribute("hunger");
            if (hungerTree == null) return;

            float absorbedDamage = damage * shield;
            float saturationCost = absorbedDamage * 20f;
            float currentSaturation = hungerTree.GetFloat("currentsaturation");

            if (currentSaturation > saturationCost)
            {
                hungerTree.SetFloat("currentsaturation", currentSaturation - saturationCost);
                player.WatchedAttributes.MarkPathDirty("hunger");
                damage -= absorbedDamage;
            }
        }
    }
}
