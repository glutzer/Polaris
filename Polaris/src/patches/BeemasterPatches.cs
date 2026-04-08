using HarmonyLib;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Polaris;

public class BeemasterPatches
{
    [HarmonyPatch(typeof(BlockSkep), "OnBlockInteractStart")]
    public class SkepInteractStartPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(BlockSkep __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref bool __result)
        {
            if (byPlayer?.Entity == null || blockSel == null) return true;
            if (__instance.IsEmpty()) return true;
            if (byPlayer.Entity.GetSkillLevel("beemaster") <= 0) return true;

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityBeehive beh || !beh.Harvestable) return true;

            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)) return true;

            if (world.Side == EnumAppSide.Server)
            {
                Block skep = world.BlockAccessor.GetBlock(blockSel.Position);
                ItemStack[]? drops = skep.GetDrops(world, blockSel.Position, byPlayer);
                if (drops != null)
                {
                    foreach (ItemStack stack in drops)
                    {
                        if (!stack.Collectible.Code.Path.Contains("honey")) continue;

                        if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                            world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }

                // Reset hive: not harvestable, Poor health, new growth timer
                TreeAttribute tree = new();
                beh.ToTreeAttributes(tree);
                tree.SetInt("harvestable", 0);
                tree.SetInt("hiveHealth", (int)EnumHivePopSize.Poor);
                double nextHarvestHours = world.Calendar.TotalHours + (12.0 * (3.0 + (world.Rand.NextDouble() * 8.0)));
                tree.SetDouble("harvestableAtTotalHours", nextHarvestHours);
                beh.FromTreeAttributes(tree, world);
                beh.MarkDirty();
            }

            world.PlaySoundAt(new AssetLocation("sounds/block/plant"), blockSel.Position, -0.5, byPlayer);

            __result = true;
            return false;
        }
    }
}
