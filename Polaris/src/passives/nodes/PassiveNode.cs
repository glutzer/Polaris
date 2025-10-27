using System;
using System.Collections.Generic;
using System.Text;

namespace Polaris;

public struct NodePosition
{
    public int X;
    public int Y;

    public NodePosition(int x, int y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// A passive node that may appear in a constellation.
/// Allocating it will contribute a value when calculating stats.
/// </summary>
public class PassiveNode : IEquatable<PassiveNode>
{
    public string Name { get; }
    public string Code { get; }
    public NodePosition Position { get; }
    public Constellation Constellation { get; }

    // Node tags which will be gathered.
    public HashSet<string> Tags { get; } = [];
    public Vector4 Color { get; private set; } = new Vector4(1f, 1f, 1f, 1f);

    public int NodeSize => (int)(Size * 10f);
    public float Size { get; private set; } = 1f;

    public int Cost { get; private set; } = 1;

    // For drawing.
    public List<PassiveNode> ChildConnections { get; } = [];
    public List<PassiveNode> Connections { get; } = [];

    public List<PassiveNodeRequirement> Requirements { get; } = [];
    public List<PassiveNodeStat> Stats { get; } = [];

    public bool StartNode { get; private set; }

    /// <summary>
    /// Unique identifier for this node. Will be used to record if it's allocated.
    /// Per constellation.
    /// </summary>
    public int Id { get; }

    public PassiveNode(string name, string code, int x, int y, Constellation constellation)
    {
        Name = name;
        Code = code;
        Position = new NodePosition(x, y);
        Constellation = constellation;

        Id = constellation.GrabNextId();
        constellation.AddNode(this);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public bool CanAllocate(EntityPlayer player, PlayerPolarisData data, AllocatedNodesInfo info)
    {
        foreach (PassiveNodeRequirement requirement in Requirements)
        {
            if (!requirement.CanAllocate(player, data, info)) return false;
        }
        return true;
    }

    public static PassiveNode Create(string name, string code, int x, int y, Constellation constellation)
    {
        return new PassiveNode(name, code, x, y, constellation);
    }

    public PassiveNode WithTag(string tag)
    {
        Tags.Add(tag);
        return this;
    }

    /// <summary>
    /// Called when:
    /// Trying to unallocate node in passive tree on client on every other node.
    /// A server processing an unallocation request.
    /// Called on all allocated nodes.
    /// </summary>
    public bool ReliesOnNode(string nodeCode)
    {
        foreach (PassiveNodeRequirement requirement in Requirements)
        {
            if (requirement.ReliesOnNode(nodeCode)) return true;
        }
        return false;
    }

    public void BuildDescription(StringBuilder builder, PlayerPolarisData data, AllocatedNodesInfo info)
    {
        // Rone code roadblock.
        foreach (PassiveNodeStat stat in Stats)
        {
            stat.BuildDescription(builder, data, MainAPI.Capi.World.Player.Entity, info);
        }

        foreach (string tag in Tags)
        {
            builder.AppendLine($"<font color=\"#AAAAFF\">{char.ToUpper(tag[0]) + tag[1..]}</font>");
        }

        foreach (PassiveNodeRequirement requirement in Requirements)
        {
            requirement.BuildDescription(builder, data, MainAPI.Capi.World.Player.Entity, info);
        }
    }

    public PassiveNode AddRequirement(PassiveNodeRequirement requirement)
    {
        Requirements.Add(requirement);
        return this;
    }

    public PassiveNode AddStat(PassiveNodeStat stat)
    {
        Stats.Add(stat);
        return this;
    }

    public PassiveNode AddParent(string nodeCode)
    {
        PassiveNode? node = Constellation.GetNodeByCode(nodeCode);
        return node == null
            ? throw new ArgumentException($"No node with code {nodeCode} found in constellation {Constellation.Name}.")
            : AddParent(node);
    }

    /// <summary>
    /// Connect two nodes.
    /// </summary>
    private PassiveNode AddParent(PassiveNode node)
    {
        if (node.Connections.Contains(this)) return this; // Already connected, maybe log?

        Connections.Add(node);
        node.Connections.Add(this);
        node.ChildConnections.Add(this);

        return this;
    }

    /// <summary>
    /// Make this node allocatable from any point.
    /// </summary>
    public PassiveNode MakeStartNode()
    {
        StartNode = true;
        return this;
    }

    /// <summary>
    /// Set knowledge point cost of this node.
    /// Can it be negative? that would be funny.
    /// </summary>
    public PassiveNode SetCost(int cost)
    {
        Cost = cost;
        return this;
    }

    public PassiveNode SetSize(float size)
    {
        Size = size;
        return this;
    }

    public PassiveNode SetColor(float x, float y, float z, float a)
    {
        Color = new Vector4(x, y, z, a);
        return this;
    }

    /// <summary>
    /// Contribute anything to the context, usually a number.
    /// </summary>
    public void ContributeStats(PassiveContext context, EnumCalculationPriority calculationPriority)
    {
        foreach (PassiveNodeStat stat in Stats)
        {
            if (stat.Priority != calculationPriority) continue;
            stat.ContributeStats(context);
        }
    }

    public bool Equals(PassiveNode? other)
    {
        return other is not null && Id == other.Id && Constellation == other.Constellation;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PassiveNode);
    }

    public string GetFullCode()
    {
        return $"{Constellation.Name}:{Code}";
    }

    public PassiveNode KeystoneStyle()
    {
        Size = 2f;
        return this;
    }

    public PassiveNode NotableStyle()
    {
        Size = 1.5f;
        return this;
    }
}