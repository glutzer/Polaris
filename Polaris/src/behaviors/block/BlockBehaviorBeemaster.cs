using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Polaris;

/// <summary>
/// Attached to skep blocks. When the player has the "beemaster" skill allocated,
/// right-clicking a harvestable skep collects the honeycomb over a short animation
/// without destroying the skep. The hive is reset to Poor health afterward.
/// </summary>
[BlockBehavior]
public class BlockBehaviorBeemaster : BlockBehavior
{
    private const float HarvestTime = 3f;

    // Index into block.Drops that holds the honeycomb stack (index 1 in vanilla skep).
    private BlockDropItemStack? honeyDrop;

    public BlockBehaviorBeemaster(Block block) : base(block) { }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        // Vanilla harvestable skep drop is at index 1 (index 0 is the skep item itself).
        honeyDrop = block.Drops?.Length > 1 ? block.Drops[1] : null;
    }
    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection,
        IPlayer forPlayer, ref EnumHandling handling)
    {
        return world.Side != EnumAppSide.Client
            ? []
            : forPlayer.Entity.GetSkillLevel("beemaster") <= 0
            ? []
            : world.BlockAccessor.GetBlockEntity(selection.Position) is not BlockEntityBeehive beh || !beh.Harvestable
            ? []
            : [
            new WorldInteraction
            {
                ActionLangCode = "blockhelp-beehive-harvest",
                HotKeyCode = null,
                MouseButton = EnumMouseButton.Right,
                Itemstacks = null,
            }
        ];
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel,
        ref EnumHandling handling)
    {
        if (blockSel == null || byPlayer == null) return false;
        if (block is BlockSkep skep && skep.IsEmpty()) return false;
        if (byPlayer.Entity.GetSkillLevel("beemaster") <= 0) return false;

        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityBeehive beh || !beh.Harvestable) return false;

        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)) return false;

        handling = EnumHandling.PreventDefault;
        world.PlaySoundAt(new AssetLocation("game:sounds/block/plant"),
            blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
        return true;
    }

    public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer,
        BlockSelection blockSel, ref EnumHandling handling)
    {
        if (blockSel == null) return false;

        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityBeehive beh || !beh.Harvestable) return false;

        handling = EnumHandling.PreventDefault;

        if (world.Rand.NextDouble() < 0.1)
        {
            world.PlaySoundAt(new AssetLocation("game:sounds/block/plant"),
                blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
        }

        // Client keeps animating; server stops returning true when time is up.
        return world.Side == EnumAppSide.Client || secondsUsed < HarvestTime;
    }

    public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer,
        BlockSelection blockSel, ref EnumHandling handling)
    {
        if (blockSel == null || byPlayer == null) return;
        if (world.Side == EnumAppSide.Client) return;
        if (secondsUsed < HarvestTime - 0.05f) return;
        if (honeyDrop == null) return;

        if (byPlayer.Entity.GetSkillLevel("beemaster") <= 0) return;

        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityBeehive beh || !beh.Harvestable) return;

        handling = EnumHandling.PreventDefault;

        // Give the honeycomb to the player (or drop it).
        ItemStack? stack = honeyDrop.GetNextItemStack(1f);
        if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
            world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));

        // Reset hive state: not harvestable, Poor health, new growth timer.
        ITreeAttribute tree = new TreeAttribute();
        beh.ToTreeAttributes(tree);
        tree.SetInt("harvestable", 0);
        tree.SetInt("hiveHealth", (int)EnumHivePopSize.Poor);
        double nextHarvestHours = world.Calendar.TotalHours + (12.0 * (3.0 + (world.Rand.NextDouble() * 8.0)));
        tree.SetDouble("harvestableAtTotalHours", nextHarvestHours);
        beh.FromTreeAttributes(tree, world);
        beh.MarkDirty();

        world.PlaySoundAt(new AssetLocation("game:sounds/block/plant"),
            blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, byPlayer);
    }
}
