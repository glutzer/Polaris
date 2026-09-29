using HarmonyLib;

namespace Polaris;

[HarmonyPatch(typeof(CollectibleObject), "tryEatStop")]
public static class MushroomConsumptionPatches
{
    [HarmonyPrefix]
    public static void Prefix(float secondsUsed, ItemSlot slot, EntityAgent byEntity, out ItemStack? __state)
    {
        __state = null;
        if (byEntity?.World.Api.Side != EnumAppSide.Server || secondsUsed < 0.95f) return;
        if (byEntity is not EntityPlayer player || player.Player == null || slot?.Itemstack == null) return;

        ItemStack stack = slot.Itemstack;
        if (!(stack.Collectible.Code?.Path.StartsWith("mushroom-") ?? false)) return;
        if (stack.Collectible.GetNutritionProperties(byEntity.World, stack, byEntity) == null) return;

        // Eating can empty the slot, so retain the original stack and quantity.
        __state = stack.Clone();
    }

    [HarmonyPostfix]
    public static void Postfix(ItemSlot slot, EntityAgent byEntity, ItemStack? __state)
    {
        if (__state == null || byEntity is not EntityPlayer player || player.Player == null) return;
        if (slot.Itemstack?.Collectible == __state.Collectible && slot.Itemstack.StackSize >= __state.StackSize) return;

        string? mushroomType = __state.Collectible.Variant?["mushroom"];
        if (__state.Collectible.Code?.Domain == "game" && mushroomType != null)
            SystemPolaris.TriggerAchievement("fungalFeast", player.Player, mushroomType);

        if (__state.Collectible.GetNutritionProperties(byEntity.World, __state, byEntity)?.Psychedelic > 0f)
            SystemPolaris.TriggerAchievement("psychedelicMushroom", player.Player);
    }
}
