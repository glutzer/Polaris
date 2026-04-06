using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Polaris;

/// <summary>
/// Block behavior for stones.
/// Can:
/// 1. Remove drops
/// 2. Multiply the dropMultiplier for certain drops (more stones).
/// 3. Have a chance for the block to drop a rock INSTEAD of stones.
/// </summary>
[BlockBehavior]
public class PolarisStoneBehavior : BlockBehavior
{
    public PolarisStoneBehavior(Block block) : base(block)
    {
    }

    public List<ItemStack> GetDefaultDrops(float dropMultiplier)
    {
        List<ItemStack> stacks = [];

        for (int i = 0; i < block.Drops.Length; i++)
        {
            ItemStack drop = block.Drops[i].GetNextItemStack(dropMultiplier);
            if (drop != null) stacks.Add(drop);
        }

        return stacks;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;

        IEnumerable<ItemStack> drops = GetDefaultDrops(dropChanceMultiplier);

        if (byPlayer != null && byPlayer.Entity.GetSkillLevel("eroder") > 0)
        {
            drops = drops.Where(x => x.Item is not ItemStone);
        }

        // Drop literally nothing.
        return [.. drops];
    }
}