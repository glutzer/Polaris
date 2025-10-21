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
public abstract class PassiveNode : IEquatable<PassiveNode>
{
    public string Name { get; }
    public string Code { get; }
    public NodePosition Position { get; }
    public Constellation Constellation { get; }

    public virtual Vector4 Color => new(1f, 1f, 1f, 1f);

    public virtual int NodeSize => 10;
    public virtual int Cost => 1;

    public virtual EnumCalculationPriority Priority => EnumCalculationPriority.Increases;

    // For drawing.
    public List<PassiveNode> ChildConnections { get; } = [];
    public List<PassiveNode> Connections { get; } = [];
    public List<PassiveNodeRequirement> Requirements { get; } = [];

    public bool StartNode { get; private set; }

    /// <summary>
    /// Unique identifier for this node. Will be used to record if it's allocated.
    /// Per constellation.
    /// </summary>
    public int Id { get; }

    public PassiveNode(string name, string code, NodePosition position, Constellation constellation)
    {
        Name = name;
        Code = code;
        Position = position;
        Constellation = constellation;

        Id = constellation.GrabNextId();
        constellation.AddNode(this);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public bool CanAllocate(EntityPlayer player, PlayerPolarisData data, HashSet<string> allocatedNodes)
    {
        foreach (PassiveNodeRequirement requirement in Requirements)
        {
            if (!requirement.CanAllocate(player, data, allocatedNodes)) return false;
        }
        return true;
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

    public virtual void BuildDescription(StringBuilder builder, PlayerPolarisData data)
    {

    }

    public PassiveNode AddRequirement(PassiveNodeRequirement requirement)
    {
        Requirements.Add(requirement);
        return this;
    }

    /// <summary>
    /// Connect two nodes.
    /// </summary>
    public PassiveNode AddParent(PassiveNode node)
    {
        if (node.Connections.Contains(this)) return this; // Already connected, maybe log?

        Connections.Add(node);
        node.Connections.Add(this);
        node.ChildConnections.Add(this);

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
    /// Make this node allocatable from any point.
    /// </summary>
    public PassiveNode MakeStartNode()
    {
        StartNode = true;
        return this;
    }

    /// <summary>
    /// Contribute anything to the context, usually a number.
    /// </summary>
    public abstract void ContributeStats(PassiveContext context);

    public bool Equals(PassiveNode? other)
    {
        return other is not null && Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PassiveNode);
    }

    public string GetFullCode()
    {
        return $"{Constellation.Name}:{Code}";
    }
}