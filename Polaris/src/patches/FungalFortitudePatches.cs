using HarmonyLib;

namespace Polaris;

public class FungalFortitudePatches
{
    [HarmonyPatch(typeof(CollectibleObject), "tryEatStop")]
    public class FungalFortitudePatch
    {
        [HarmonyPostfix]
        public static void Postfix(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity?.World.Api.Side != EnumAppSide.Server) return;
            if (secondsUsed < 0.95f) return;
            if (slot?.Itemstack == null) return;
            if (byEntity is not EntityPlayer player) return;
            if (player.Player == null) return;
            if (!byEntity.TryGetExtraStat("fungalFortitude", out float fortitude) || fortitude <= 0f) return;

            // Only apply to mushroom items.
            string codePath = slot.Itemstack.Collectible?.Code?.Path ?? "";
            if (!codePath.StartsWith("mushroom-")) return;

            FoodNutritionProperties? nutriProps = slot.Itemstack.Collectible?.GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity);
            if (nutriProps == null) return;

            player.ReceiveSaturation(nutriProps.Satiety * fortitude, nutriProps.FoodCategory);
            SystemPolaris.AddExperience("Mycology", player.Player, nutriProps.Satiety * fortitude);
        }
    }
}
