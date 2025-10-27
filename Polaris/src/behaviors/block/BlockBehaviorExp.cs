using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Polaris;

/// <summary>
/// Behavior that will give the player exp when broken.
/// </summary>
[BlockBehavior]
public class BlockBehaviorExp : BlockBehavior
{
    protected float exp;
    protected string skill = "";

    public BlockBehaviorExp(Block block) : base(block)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        exp = properties["exp"].AsFloat(0f);
        skill = properties["skill"].AsString("");
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref EnumHandling handling)
    {
        if (byPlayer == null || world.Side.IsClient() || exp == 0f) return;
        Polaris.AddExperience(skill, byPlayer, exp);
    }
}