using System.Collections.Generic;
using System.Text;

namespace Polaris;

public class NodeRequirement : PassiveNodeRequirement
{
    private readonly string requiredCode;

    public NodeRequirement(string constellation, string nodeCode)
    {
        requiredCode = $"{constellation}:{nodeCode}";
    }

    public override bool CanAllocate(EntityPlayer player, PlayerPolarisData data, HashSet<string> allocatedNodes)
    {
        return allocatedNodes.Contains(requiredCode);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, HashSet<string> allocatedNodes)
    {
        bool canAllocate = CanAllocate(player, data, allocatedNodes);
        string color = canAllocate ? "#00FF88" : "#FF4444";

        string[] parts = requiredCode.Split(':');

        PassiveNode? node = SystemPolarisPassiveTree.Instance(player.Api).GetNode(parts[0], parts[1]);
        if (node == null) return;

        builder.AppendLine($"<font color=\"{color}\">Requires Passive {node.Name}</font>");
    }

    public override bool ReliesOnNode(string nodeCode)
    {
        return requiredCode == nodeCode;
    }
}