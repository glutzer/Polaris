using HarmonyLib;
using System;
using System.Reflection;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace Polaris;

public class CraftingPatches
{
    public static IPlayer? LastSlotActivator { get; set; }
    private static long lastActivationTimeServer;
    public static float SecondsSinceLastActivation => (MainAPI.Sapi.World.ElapsedMilliseconds - lastActivationTimeServer) / 1000f;

    [HarmonyPatch]
    public class InventoryPatch
    {
        public static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName("Vintagestory.Server.ServerSystemInventory");
            return AccessTools.Method(type, "HandleActivateInventorySlot");
        }

        [HarmonyPrefix]
        public static bool Prefix(ConnectedClient client)
        {
            LastSlotActivator = client.Player;
            lastActivationTimeServer = MainAPI.Sapi.World.ElapsedMilliseconds;
            return true;
        }
    }

    // Sewing patches.
    [HarmonyPatch(typeof(CollectibleBehaviorWearable), "ChangeCondition")]
    public class CharPatch1
    {
        [HarmonyPrefix]
        public static bool Prefix(ItemSlot slot, ref float changeVal)
        {
            if (slot.Itemstack == null) return true;

            if (slot.Inventory.Api is ICoreServerAPI)
            {
                // I think it actually uses this when taking damage.
                // If something else repairs the clothes this could be bad.
                if (LastSlotActivator == null || changeVal <= 0f || SecondsSinceLastActivation > 1f) return true;

                if (LastSlotActivator.Entity.TryGetExtraStat("sewingEffectiveness", out float value))
                {
                    changeVal *= value;
                }

                float condition = slot.Itemstack.Attributes.GetFloat("condition", 1f);
                float toRepair = Math.Min(1f - condition, changeVal);
                SystemPolaris.AddExperience("Crafting", LastSlotActivator, toRepair * 50f);
            }

            return true;
        }
    }
}