using HarmonyLib;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Polaris;

public class VeinMinerPatches
{
    private const int MaxVeinBlocks = 10;

    [HarmonyPatch(typeof(BlockOre), "OnBlockBroken")]
    public class VeinMinerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(BlockOre __instance, IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
        {
            if (world.Side != EnumAppSide.Server) return;
            if (byPlayer?.Entity == null) return;
            if (byPlayer.Entity.GetSkillLevel("veinminer") <= 0) return;
            if (!IsOreBlock(__instance)) return;

            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot?.Itemstack?.Collectible?.Tool != EnumTool.Pickaxe) return;

            BreakConnectedOre(world, pos, __instance, byPlayer, activeSlot, []);
        }

        private static bool IsOreBlock(Block block)
        {
            return block.Code.Path.StartsWith("ore-");
        }

        private static void BreakConnectedOre(IWorldAccessor world, BlockPos origin, Block oreType, IPlayer player, ItemSlot tool, HashSet<BlockPos> visited)
        {
            if (visited.Count >= MaxVeinBlocks) return;

            BlockPos[] neighbors =
            [
                origin.NorthCopy(), origin.SouthCopy(), origin.EastCopy(), origin.WestCopy(),
                origin.UpCopy(), origin.DownCopy()
            ];

            foreach (BlockPos neighbor in neighbors)
            {
                if (visited.Contains(neighbor)) continue;

                Block neighborBlock = world.BlockAccessor.GetBlock(neighbor);
                if (neighborBlock.Code.Path != oreType.Code.Path) continue;

                visited.Add(neighbor);

                // Consume extra durability per extra block broken.
                if (tool.Itemstack != null)
                {
                    tool.Itemstack.Collectible.DamageItem(world, player.Entity, tool, 1);
                    if (tool.Itemstack == null) return;
                }

                world.BlockAccessor.BreakBlock(neighbor, player);
                BreakConnectedOre(world, neighbor, oreType, player, tool, visited);
            }
        }
    }
}