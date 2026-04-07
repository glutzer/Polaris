using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;
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
    private string stoneType = "ore";

    public PolarisStoneBehavior(Block block) : base(block)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        stoneType = properties["stoneType"].AsString("ore");
    }

    public List<ItemStack> GetDefaultDrops(float dropMultiplier)
    {
        List<ItemStack> stacks = [];

        for (int i = 0; i < block.Drops.Length; i++)
        {
            ItemStack? drop = block.Drops[i].GetNextItemStack(dropMultiplier);
            if (drop != null) stacks.Add(drop);
        }

        return stacks;
    }

    private bool IsRock => stoneType == "rock";
    private bool IsGemOre => stoneType == "gem";

    public override ItemStack[]? GetDrops(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, ref float dropChanceMultiplier, ref EnumHandling handling)
    {
        handling = EnumHandling.PreventDefault;

        if (IsRock && byPlayer != null && byPlayer.Entity.TryGetExtraStat("stoneDropBonus", out float stoneBonus) && stoneBonus > 0f)
            dropChanceMultiplier *= 1f + stoneBonus;

        IEnumerable<ItemStack> drops = GetDefaultDrops(dropChanceMultiplier);

        if (byPlayer != null && byPlayer.Entity.GetSkillLevel("eroder") > 0)
            drops = drops.Where(x => x.Item is not ItemStone);

        if (IsRock && byPlayer != null && byPlayer.Entity.TryGetExtraStat("stoneCutterChance", out float cutterChance) && cutterChance > 0f && world.Rand.NextDouble() < cutterChance)
            drops = drops.Append(new ItemStack(block));

        if (IsGemOre && byPlayer != null && byPlayer.Entity.TryGetExtraStat("gemDropBonus", out float gemBonus) && gemBonus > 0f)
        {
            dropChanceMultiplier *= 1f + gemBonus;
        }

        return [.. drops];
    }
}