using System.Collections.Generic;
using System.Text;

namespace Polaris;

/// <summary>
/// A requirement for a passive node to be allocated, other than being connected.
/// </summary>
public abstract class PassiveNodeRequirement
{
    public abstract bool CanAllocate(EntityPlayer player, PlayerPolarisData data, HashSet<string> allocatedNodes);

    public virtual void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, HashSet<string> allocatedNodes)
    {

    }

    public virtual bool ReliesOnNode(string nodeCode)
    {
        return false;
    }
}