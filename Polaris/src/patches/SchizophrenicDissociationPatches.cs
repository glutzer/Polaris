using HarmonyLib;
using Vintagestory.GameContent;

namespace Polaris;

public class SchizophrenicDissociationPatches
{
    [HarmonyPatch(typeof(EntityBehaviorHealth), "OnEntityReceiveDamage")]
    public class DissociationDamagePatch
    {
        [HarmonyPrefix]
        public static void Prefix(EntityBehaviorHealth __instance, DamageSource damageSource, ref float damage)
        {
            if (__instance.entity?.World.Api.Side != EnumAppSide.Server) return;
            if (damageSource.Type == EnumDamageType.Heal) return;
            if (damage <= 0) return;
            if (__instance.entity is not EntityPlayer player) return;
            if (player.GetSkillLevel("schizoDissociation") <= 0) return;

            float deferred = damage * 0.4f;
            damage -= deferred;

            player.World.RegisterCallback(_ =>
            {
                if (!player.Alive) return;

                EntityBehaviorHealth bhp = player.GetHealth();
                bhp.Health -= deferred;

                //int counter = player.WatchedAttributes.GetInt("onHurtCounter");
                //player.WatchedAttributes.SetInt("onHurtCounter", counter + 1);
                //player.WatchedAttributes.SetFloat("onHurt", deferred);

                if (bhp.Health <= 0)
                {
                    bhp.Health = 0;
                    player.Die(EnumDespawnReason.Death, new DamageSource
                    {
                        Source = EnumDamageSource.Internal,
                        Type = EnumDamageType.Poison
                    });
                }
            }, 4000);
        }
    }
}