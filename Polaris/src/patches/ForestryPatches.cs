using HarmonyLib;
using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Polaris;

[HarmonyPatch(typeof(ItemAxe), nameof(ItemAxe.OnBlockBrokenWith))]
public static class ForestryPatches
{


    [HarmonyPrefix]
    public static void Prefix(ItemAxe __instance, IWorldAccessor world, Entity byEntity,
        BlockSelection blockSel, out List<(BlockPos Position, int BlockId)>? __state)
    {
        __state = null;
        if (world.Side != EnumAppSide.Server || byEntity is not EntityPlayer || blockSel == null) return;

        // Use the axe's tree detection, excluding ordinary blocks and leaves.
        Stack<BlockPos> tree = __instance.FindTree(world, blockSel.Position, out _, out _);
        if (tree.Count == 0) return;
        __state = [];
        foreach (BlockPos position in tree)
        {
            Block block = world.BlockAccessor.GetBlock(position);
            if (block.BlockMaterial == EnumBlockMaterial.Wood)
                __state.Add((position.Copy(), block.BlockId));
        }
    }

    [HarmonyPostfix]
    public static void Postfix(IWorldAccessor world, Entity byEntity, bool __result,
        List<(BlockPos Position, int BlockId)>? __state)
    {
        if (!__result || __state == null || byEntity is not EntityPlayer player || player.Player == null) return;
        int felled = 0;
        foreach ((BlockPos Position, int BlockId) block in __state)
            if (world.BlockAccessor.GetBlock(block.Position).BlockId != block.BlockId) felled++;

        if (felled > 0)
            SystemPolaris.AddExperience("Forestry", player.Player, felled * ExpGlobals.ForestryExperiencePerWoodBlock);
    }
}
