using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Polaris;

public static class MetalProductionPatches
{
    private static readonly AccessTools.FieldRef<BlockEntityBloomery, InventoryGeneric> BloomeryInventory =
        AccessTools.FieldRefAccess<BlockEntityBloomery, InventoryGeneric>("bloomeryInv");

    private static void RecordIngot(IPlayer player, ItemStack stack)
    {
        string path = stack.Collectible.Code.Path;
        if (stack.StackSize <= 0 || path == "ingot-iron" || !path.StartsWith("ingot-")) return;
        SystemPolaris.TriggerAchievement("metalworker", player, path["ingot-".Length..]);
    }

    private static void AwardBloomery(IPlayer player, ItemStack stack)
    {
        if (stack.StackSize <= 0 || stack.Collectible.Code.Path == "ingot-iron"
            || !stack.Collectible.Code.Path.StartsWith("ingot-")) return;
        SystemPolaris.AddExperience("Smithing", player, stack.StackSize * 2f);
        RecordIngot(player, stack);
    }

    [HarmonyPatch(typeof(BlockEntityIngotMold), "TryTakeIngot")]
    public static class IngotMoldPatch
    {
        [HarmonyPrefix]
        public static void Prefix(BlockEntityIngotMold __instance, Vec3d hitPosition, out ItemStack? __state)
        {
            __state = null;
            if (__instance.Api.Side != EnumAppSide.Server) return;
            __instance.SetSelectedSide(hitPosition);
            if (!__instance.SelectedShattered && __instance.SelectedIsHardened)
                __state = __instance.GetSelectedStateAwareContents()?.Clone();
        }

        [HarmonyPostfix]
        public static void Postfix(BlockEntityIngotMold __instance, IPlayer byPlayer, bool __result, ItemStack? __state)
        {
            if (!__result || __state == null || __instance.SelectedContents != null || byPlayer == null) return;
            if (__state.Collectible.Code.Path == "ingot-iron") return;
            SystemPolaris.AddExperience("Smithing", byPlayer, __instance.RequiredUnits / 50f);
            RecordIngot(byPlayer, __state);
        }
    }

    [HarmonyPatch(typeof(BlockEntityToolMold), "TryTakeContents")]
    public static class ToolMoldPatch
    {
        [HarmonyPrefix]
        public static void Prefix(BlockEntityToolMold __instance, out ItemStack[]? __state)
        {
            __state = null;
            if (__instance.Api.Side != EnumAppSide.Server || __instance.Shattered || !__instance.IsFull
                || !__instance.IsHardened || __instance.MetalContent == null || __instance.BreaksWhenFilled) return;
            __state = __instance.GetStateAwareMoldedStacks();
        }

        [HarmonyPostfix]
        public static void Postfix(BlockEntityToolMold __instance, IPlayer byPlayer, bool __result,
            ItemStack[]? __state, int ___requiredUnits)
        {
            if (!__result || __state == null || __instance.MetalContent != null || byPlayer == null) return;
            SystemPolaris.AddExperience("Smithing", byPlayer, ___requiredUnits / 50f);
            foreach (ItemStack stack in __state) RecordIngot(byPlayer, stack);
        }
    }

    [HarmonyPatch(typeof(BlockBloomery), nameof(BlockBloomery.OnBlockInteractStart))]
    public static class BloomeryCollectionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref bool __result)
        {
            if (byPlayer == null || blockSel == null || !byPlayer.InventoryManager.ActiveHotbarSlot.Empty) return true;
            if (byPlayer.Entity.GetSkillLevel("bloomeryExtraction") <= 0) return true;
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityBloomery bloomery || bloomery.IsBurning) return true;
            ItemSlot output = BloomeryInventory(bloomery)[2];
            if (output.Empty) return true;
            __result = true;
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)) return false;
            if (world.Side != EnumAppSide.Server) return false;

            ItemStack stack = output.TakeOutWhole();
            ItemStack rewardStack = stack.Clone();
            output.MarkDirty();
            bloomery.MarkDirty(true);
            if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
            AwardBloomery(byPlayer, rewardStack);
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityBloomery), nameof(BlockEntityBloomery.OnBlockBroken))]
    public static class BloomeryBreakingPatch
    {
        [HarmonyPrefix]
        public static void Prefix(BlockEntityBloomery __instance, out ItemStack? __state)
        {
            __state = __instance.Api.Side == EnumAppSide.Server ? BloomeryInventory(__instance)[2].Itemstack?.Clone() : null;
        }

        [HarmonyPostfix]
        public static void Postfix(BlockEntityBloomery __instance, IPlayer? byPlayer, ItemStack? __state)
        {
            if (byPlayer == null || __state == null || !BloomeryInventory(__instance)[2].Empty) return;
            AwardBloomery(byPlayer, __state);
        }
    }
}
