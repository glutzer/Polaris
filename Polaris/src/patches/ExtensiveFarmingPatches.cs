using HarmonyLib;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Polaris;

public class ExtensiveFarmingPatches
{
    // toolMode 0 = 1x1 (default), 1 = 2x2, 2 = 3x3.
    // Skill level 1 unlocks mode 1 (2x2), skill level 2 unlocks mode 2 (3x3).
    private static int ModeToRange(int toolMode) => toolMode + 1;

    private static (int startX, int startZ) GetAreaStart(int blockX, int blockZ, int range, Vec3d hitPos)
    {
        if (range % 2 == 1)
        {
            // Odd: center on the block.
            return (blockX - (range / 2), blockZ - (range / 2));
        }

        int startX = hitPos.X >= 0.5 ? blockX : blockX - 1;
        int startZ = hitPos.Z >= 0.5 ? blockZ : blockZ - 1;
        return (startX, startZ);
    }

    [HarmonyPatch(typeof(CollectibleObject), "GetToolModes")]
    public class HoeGetToolModesPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CollectibleObject __instance, ItemSlot slot, IClientPlayer forPlayer, ref SkillItem[]? __result)
        {
            if (__instance is not ItemHoe) return true;
            if (forPlayer == null || forPlayer.Entity == null) return true;

            int skillLevel = forPlayer.Entity.GetSkillLevel("extensivefarming");
            if (skillLevel <= 0)
            {
                __result = null;
                return false;
            }

            if (forPlayer.Entity.Api is not ICoreClientAPI capi) return true;

            SkillItem[] allModes = ObjectCacheUtil.GetOrCreate(capi, "polarisHoeToolModes", () =>
            {
                return new SkillItem[]
                {
                    new SkillItem { Code = new AssetLocation("1size"), Name = Lang.Get("1x1") }.WithIcon(capi, ItemClay.Drawcreate1_svg),
                    new SkillItem { Code = new AssetLocation("2size"), Name = Lang.Get("2x2") }.WithIcon(capi, ItemClay.Drawcreate4_svg),
                    new SkillItem { Code = new AssetLocation("3size"), Name = Lang.Get("3x3") }.WithIcon(capi, new ItemClay().Drawcreate9_svg),
                };
            });

            __result = [.. allModes.Take(skillLevel + 1)];
            return false;
        }
    }

    [HarmonyPatch(typeof(CollectibleObject), "GetToolMode")]
    public class HoeGetToolModePatch
    {
        [HarmonyPostfix]
        public static void Postfix(CollectibleObject __instance, ItemSlot slot, IPlayer byPlayer, ref int __result)
        {
            if (__instance is not ItemHoe) return;

            int skillLevel = byPlayer?.Entity?.GetSkillLevel("extensivefarming") ?? 0;
            __result = GameMath.Clamp(slot.Itemstack?.Attributes.GetInt("polarisToolMode") ?? 0, 0, skillLevel);
        }
    }

    [HarmonyPatch(typeof(CollectibleObject), "SetToolMode")]
    public class HoeSetToolModePatch
    {
        [HarmonyPostfix]
        public static void Postfix(CollectibleObject __instance, ItemSlot slot, int toolMode)
        {
            if (__instance is not ItemHoe) return;

            slot.Itemstack?.Attributes.SetInt("polarisToolMode", toolMode);
        }
    }

    [HarmonyPatch(typeof(ItemHoe), "DoTill")]
    public class HoeDoTillPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel)
        {
            if (blockSel == null) return;
            if (byEntity?.World.Api.Side != EnumAppSide.Server) return;
            if (byEntity is not EntityPlayer entityPlayer) return;
            IPlayer byPlayer = entityPlayer.Player;
            if (byPlayer == null) return;

            int toolMode = slot.Itemstack?.Attributes.GetInt("polarisToolMode") ?? 0;
            if (toolMode <= 0) return;

            int skillLevel = byEntity.GetSkillLevel("extensivefarming");
            // Clamp tool mode to what the player has actually unlocked.
            toolMode = GameMath.Clamp(toolMode, 0, skillLevel);
            if (toolMode <= 0) return;

            int range = ModeToRange(toolMode);
            int centerX = blockSel.Position.X;
            int centerY = blockSel.Position.Y;
            int centerZ = blockSel.Position.Z;

            (int startX, int startZ) = GetAreaStart(centerX, centerZ, range, blockSel.HitPosition);

            IWorldAccessor world = byEntity.World;
            int used = 0;

            for (int xx = startX; xx < startX + range; xx++)
            {
                for (int zz = startZ; zz < startZ + range; zz++)
                {
                    // Skip the center tile — base DoTill already handled it.
                    if (xx == centerX && zz == centerZ) continue;

                    BlockPos pos = new(xx, centerY, zz, blockSel.Position.dimension);

                    // Must have air above.
                    Block above = world.BlockAccessor.GetBlock(new BlockPos(xx, centerY + 1, zz, blockSel.Position.dimension));
                    if (above?.Id != 0) continue;

                    Block block = world.BlockAccessor.GetBlock(pos);
                    if (block?.Code?.Path?.StartsWith("soil") != true) continue;

                    string fertility = block.LastCodePart(1);
                    Block? farmland = world.GetBlock(new AssetLocation("farmland-dry-" + fertility));
                    if (farmland == null) continue;

                    world.BlockAccessor.SetBlock(farmland.BlockId, pos);
                    used++;

                    if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityFarmland beFarmland)
                        beFarmland.OnCreatedFromSoil(block);

                    world.BlockAccessor.MarkBlockDirty(pos);
                }
            }

            // Extra durability cost: half a use per additional tile tilled.
            used = (int)((used * 0.5f) + 0.6f);
            if (used > 0 && slot.Itemstack != null)
                slot.Itemstack.Collectible.DamageItem(world, byEntity, byPlayer.InventoryManager.ActiveHotbarSlot, used);
        }
    }

    private static IPlayer? _currentShearsPlayer;

    [HarmonyPatch(typeof(ItemShears), "OnBlockBrokenWith")]
    public class ShearsOnBlockBrokenWithPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Entity byEntity)
        {
            _currentShearsPlayer = (byEntity as EntityPlayer)?.Player;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            _currentShearsPlayer = null;
        }
    }

    [HarmonyPatch(typeof(ItemShears), "GetNearblyMultibreakables")]
    public class ShearsGetNearblyPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ItemShears __instance, IWorldAccessor world, BlockPos pos, Vec3d hitPos,
            ref Vintagestory.API.Datastructures.OrderedDictionary<BlockPos, float> __result)
        {
            if (_currentShearsPlayer == null) return true;
            EntityPlayer? entityPlayer = _currentShearsPlayer.Entity;
            if (entityPlayer == null) return true;

            IPlayer byPlayer = world.PlayerByUid(entityPlayer.PlayerUID);
            if (byPlayer == null) return true;

            int skillLevel = entityPlayer.GetSkillLevel("extensivefarming");
            if (skillLevel <= 0) return true; // Fall through to vanilla

            int r = 1 + skillLevel;

            Vintagestory.API.Datastructures.OrderedDictionary<BlockPos, float> orderedDictionary = [];
            for (int i = -r; i <= r; i++)
            {
                for (int j = -r; j <= r; j++)
                {
                    for (int k = -r; k <= r; k++)
                    {
                        if (i != 0 || j != 0 || k != 0)
                        {
                            BlockPos blockPos = pos.AddCopy(i, j, k);
                            if (__instance.CanMultiBreak(world.BlockAccessor.GetBlock(blockPos)))
                            {
                                orderedDictionary.Add(blockPos, hitPos.SquareDistanceTo(blockPos.X + 0.5, blockPos.Y + 0.5, blockPos.Z + 0.5));
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}