namespace Polaris;

/// <summary>
/// A requirement for a passive node to be allocated, other than being connected.
/// </summary>
public abstract class PassiveNodeRequirement
{
    public abstract bool CanAllocate(EntityPlayer player, ref string reason);
}