using System;
using System.Collections.Generic;

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
    public NodePosition Position { get; }

    // Set when adding to constellation.
    public Constellation Constellation { get; private set; } = null!;

    public virtual Vector4 Color => new(1f, 1f, 1f, 1f);

    public virtual int NodeSize => 10;
    public virtual int Cost => 1;

    public virtual EnumCalculationPriority Priority => EnumCalculationPriority.Increases;

    // For drawing.
    public List<PassiveNode> ChildConnections { get; } = [];
    public List<PassiveNode> Connections { get; } = [];

    public bool StartNode { get; private set; }

    /// <summary>
    /// Unique identifier for this node. Will be used to record if it's allocated.
    /// Per constellation.
    /// </summary>
    public int Id { get; private set; } = -1;

    public PassiveNode(string name, NodePosition position)
    {
        Name = name;
        Position = position;
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public void SetId(int id, Constellation constellation)
    {
        if (Id != -1) throw new InvalidOperationException("Id is being set twice, it should only be set by the stat system when registering.");
        Id = id;
        Constellation = constellation;
    }

    /// <summary>
    /// Connect two nodes.
    /// </summary>
    public PassiveNode AddParentConnection(PassiveNode node)
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
    /// Contribute anything to the context, usually a number.
    /// </summary>
    public abstract void ContributeStats(PassiveContext context);

    public NodePosition GetOffsetPosition(int x, int y)
    {
        return new NodePosition(Position.X + x, Position.Y + y);
    }

    public bool Equals(PassiveNode? other)
    {
        return other is not null && Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PassiveNode);
    }

    public PassiveNode AddTo(Constellation constellation)
    {
        constellation.AddNode(this);
        return this;
    }
}