using System.Text;

namespace Polaris;

public class SpecializationRequirement : PassiveNodeRequirement
{
    public override bool CanAllocate(EntityPlayer player, PlayerPolarisData data, AllocatedNodesInfo info)
    {
        int limit = info.AllocatedNodeCodes.Contains("Survival:dualSpecialization") ? 2 : 1;
        return info.GetTagCount("specialization") < limit;
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        int limit = info.AllocatedNodeCodes.Contains("Survival:dualSpecialization") ? 2 : 1;
        string color = CanAllocate(player, data, info) ? "#00FF88" : "#FF4444";
        builder.AppendLine($"<font color=\"{color}\">Only {limit} specialization nodes may be allocated ({info.GetTagCount("specialization")}/{limit})</font>");
    }
}
