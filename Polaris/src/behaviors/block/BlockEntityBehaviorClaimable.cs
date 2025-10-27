using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Datastructures;

namespace Polaris;

public static class BEExtensions
{
    public static bool IsClaimable(this BlockEntity blockEntity, [NotNullWhen(true)] out BlockEntityBehaviorClaimable? claimable)
    {
        claimable = blockEntity.GetBehavior<BlockEntityBehaviorClaimable>();
        return claimable != null;
    }
}

[BlockEntityBehavior]
public class BlockEntityBehaviorClaimable : BlockEntityBehavior
{
    public string OwnerUid { get; private set; } = "";
    public IPlayer? OwningPlayer { get; private set; }

    public BlockEntityBehaviorClaimable(BlockEntity blockEntity) : base(blockEntity)
    {
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        OwningPlayer = api.World.PlayerByUid(OwnerUid);
    }

    public void SetOwner(IPlayer player)
    {
        OwnerUid = player.PlayerUID;
        OwningPlayer = player;
        Blockentity.MarkDirty();
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        OwnerUid = tree.GetString("ownerUid");
        OwningPlayer = worldAccessForResolve.PlayerByUid(OwnerUid);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        tree.SetString("ownerUid", OwnerUid);
    }
}