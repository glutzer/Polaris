using HarmonyLib;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Polaris;

public static class CookingPatches
{


    // Inventory packets identify the cook; merely opening the firepit or taking its output does not.
    [HarmonyPatch(typeof(BlockEntityFirepit), nameof(BlockEntityFirepit.OnReceivedClientPacket))]
    public static class CookAttributionPatch
    {
        [HarmonyPrefix]
        public static void Prefix(BlockEntityFirepit __instance, int packetid, out ItemStack?[]? __state)
        {
            __state = __instance.Api.Side == EnumAppSide.Server && packetid < 1000
                ? new[] { __instance.inputSlot }.Concat(__instance.otherCookingSlots).Select(slot => slot.Itemstack?.Clone()).ToArray()
                : null;
        }

        [HarmonyPostfix]
        public static void Postfix(BlockEntityFirepit __instance, IPlayer player, ItemStack?[]? __state)
        {
            if (__state == null || player == null || !__instance.IsClaimable(out BlockEntityBehaviorClaimable? claimable)) return;
            ItemSlot[] slots = new[] { __instance.inputSlot }.Concat(__instance.otherCookingSlots).ToArray();
            bool changed = slots.Length != __state.Length;
            for (int i = 0; !changed && i < slots.Length; i++)
            {
                ItemStack? before = __state[i];
                ItemStack? after = slots[i].Itemstack;
                changed = before == null ? after != null : after == null || before.StackSize != after.StackSize ||
                    !before.Equals(__instance.Api.World, after, GlobalConstants.IgnoredStackAttributes);
            }
            if (changed) claimable.SetOwner(player);
        }
    }

    [HarmonyPatch(typeof(BlockEntityFirepit), nameof(BlockEntityFirepit.smeltItems))]
    public static class MealCompletionPatch
    {
        [HarmonyPrefix]
        public static void Prefix(BlockEntityFirepit __instance, out string? __state)
        {
            __state = __instance.Api.Side == EnumAppSide.Server &&
                __instance.inputSlot.Itemstack?.Collectible is BlockCookingContainer &&
                __instance.IsClaimable(out BlockEntityBehaviorClaimable? claimable) ? claimable.OwnerUid : null;
        }

        [HarmonyPostfix]
        public static void Postfix(BlockEntityFirepit __instance, string? __state)
        {
            if (string.IsNullOrEmpty(__state) || !__instance.inputSlot.Empty ||
                __instance.outputSlot.Itemstack?.Collectible is not BlockCookedContainer cooked) return;

            float servings = cooked.GetQuantityServings(__instance.Api.World, __instance.outputSlot.Itemstack);
            if (servings > 0f)
                SystemPolaris.Instance(__instance.Api).AddExperience("Cooking", __state, servings * ExpGlobals.CookingExperiencePerServing);
        }
    }
}
