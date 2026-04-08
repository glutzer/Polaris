namespace Polaris;

/// <summary>
/// A backpack slot that requires a minimum <c>strongBack</c> extra stat value to accept items.
/// The first extra slot (bag index 4) requires strongBack >= 1, the second >= 2, and so on.
/// </summary>
public class ItemSlotLockedBackpack : ItemSlotBackpack
{
    private readonly int requiredStrongBack;

    public ItemSlotLockedBackpack(InventoryBase inventory, int extraSlotIndex) : base(inventory)
    {
        requiredStrongBack = extraSlotIndex + 1;
    }

    private bool IsUnlocked()
    {
        if (inventory is not InventoryBasePlayer playerInv) return false;
        IPlayer? player = playerInv.Player;
        if (player?.Entity == null) return false;
        player.Entity.TryGetExtraStat("strongBack", out float strongBack);
        return strongBack >= requiredStrongBack;
    }

    public override bool DrawUnavailable
    {
        get => !IsUnlocked();
        set { }
    }

    public override bool CanHold(ItemSlot sourceSlot) => IsUnlocked() && base.CanHold(sourceSlot);

    public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        => IsUnlocked() && base.CanTakeFrom(sourceSlot, priority);
}
