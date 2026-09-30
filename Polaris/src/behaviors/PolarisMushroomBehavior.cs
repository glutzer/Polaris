using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Polaris;

/// <summary>
/// Block behavior for mushroom blocks.
/// Can:
/// 1. Increase mushroom drop rate (mushroomYield extra stat).
/// 2. Chance to find an extra mushroom (sporeCloud extra stat).
/// </summary>
[BlockBehavior]
public class PolarisMushroomBehavior : BlockBehavior
{
    public PolarisMushroomBehavior(Block block) : base(block)
    {
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, float dropQuantityMultiplier, ref EnumHandling handling)
    {
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier, ref handling);

        // Add 100 mycology exp.
        if (world.Side.IsServer() && byPlayer != null)
        {
            SystemPolaris.AddExperience("Mycology", byPlayer, ExpGlobals.MushroomHarvestExperience);
        }
    }

    public override ItemStack[]? GetDrops(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
    {
        if (byPlayer?.Entity == null) return null;

        bool hasSpore = byPlayer.Entity.TryGetExtraStat("sporeCloud", out float sporeChance) && sporeChance > 0f;

        if (!hasSpore) return null;

        handling = EnumHandling.PreventDefault;

        List<ItemStack> drops = [];
        foreach (BlockDropItemStack dropDef in block.Drops)
        {
            ItemStack? drop = dropDef.GetNextItemStack(dropChanceMultiplier);
            if (drop != null) drops.Add(drop);
        }

        if (hasSpore && drops.Count > 0 && world.Rand.NextDouble() < sporeChance)
        {
            int extraCount = 0;
            float doubleSporeChance = sporeChance - 1f;

            if (doubleSporeChance > 1f)
            {
                extraCount += (int)doubleSporeChance;
                doubleSporeChance %= 1f;

                if (world.Rand.NextDouble() < doubleSporeChance) extraCount++;
            }

            ItemStack extra = drops[0].Clone();
            extra.StackSize = 1 + extraCount;
            drops.Add(extra);
        }

        return [.. drops];
    }
}

