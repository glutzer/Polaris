using System.Collections.Generic;
using System.Text;

namespace Polaris;

public class ExclusiveRequirement : PassiveNodeRequirement
{
    private readonly string constellationName;
    private readonly string code;

    public ExclusiveRequirement(string constellationName, string code)
    {
        this.constellationName = constellationName;
        this.code = code;
    }

    public override bool CanAllocate(EntityPlayer player, PlayerPolarisData data, HashSet<string> allocatedNodes)
    {
        PlayerConstellationData? constellationData = data.GetConstellation(constellationName);
        if (constellationData == null) return true;

        PassiveNode? node = SystemPolarisPassiveTree.Instance(player.Api).GetNode(constellationName, code);
        return node == null || node == null || !constellationData.AllocatedNodeIds.Contains(node.Id);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, HashSet<string> allocatedNodes)
    {
        bool canAllocate = CanAllocate(player, data, allocatedNodes);
        string color = canAllocate ? "#00FF88" : "#FF4444";

        PlayerConstellationData? constellationData = data.GetConstellation(constellationName);
        if (constellationData == null) return;

        PassiveNode? node = SystemPolarisPassiveTree.Instance(player.Api).GetNode(constellationName, code);
        if (node == null) return;

        builder.AppendLine($"<font color=\"{color}\">Exclusive with {node.Name}</font>");
    }
}