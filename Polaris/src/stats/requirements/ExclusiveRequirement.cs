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

    public override bool CanAllocate(EntityPlayer player, PlayerPolarisData data, AllocatedNodesInfo info)
    {
        PlayerConstellationData? constellationData = data.GetConstellation(constellationName);
        if (constellationData == null) return true;

        PassiveNode? node = Polaris.Instance(player.Api).GetNode(constellationName, code);
        return node == null || node == null || !constellationData.AllocatedNodeIds.Contains(node.Id);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        bool canAllocate = CanAllocate(player, data, info);
        string color = canAllocate ? "#00FF88" : "#FF4444";

        PlayerConstellationData? constellationData = data.GetConstellation(constellationName);
        if (constellationData == null) return;

        PassiveNode? node = Polaris.Instance(player.Api).GetNode(constellationName, code);
        if (node == null) return;

        builder.AppendLine($"<font color=\"{color}\">Exclusive with {node.Name}</font>");
    }
}