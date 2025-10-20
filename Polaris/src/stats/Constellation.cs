using System.Collections.Generic;

namespace Polaris;

/// <summary>
/// Singleton constellation.
/// </summary>
public class Constellation
{
    public string Name { get; }
    private readonly List<PassiveNode> nodes = [];
    public IEnumerable<PassiveNode> AllNodes => nodes;

    public Vector4 Color { get; private set; } = new(1f, 1f, 1f, 1f);

    public Vector2i StartBounds { get; private set; }
    public Vector2i EndBounds { get; private set; }

    private int indexCounter;

    public Constellation(string name)
    {
        Name = name;
    }

    public PassiveNode? GetNodeById(int nodeId)
    {
        return nodeId < 0 || nodeId >= nodes.Count ? null : nodes[nodeId];
    }

    public void AddNode(PassiveNode node)
    {
        nodes.Add(node);
        node.SetId(indexCounter++, this);
        RecalculateBounds();
    }

    public void SetColor(float r, float g, float b, float a)
    {
        Color = new Vector4(r, g, b, a);
    }

    /// <summary>
    /// Recalculate bounds every time a node is changed.
    /// </summary>
    private void RecalculateBounds()
    {
        Vector2i min = new();
        Vector2i max = new();

        foreach (PassiveNode node in nodes)
        {
            if (node.Position.X < min.X) min.X = node.Position.X;
            if (node.Position.Y < min.Y) min.Y = node.Position.Y;
            if (node.Position.X > max.X) max.X = node.Position.X;
            if (node.Position.Y > max.Y) max.Y = node.Position.Y;
        }

        StartBounds = min;
        EndBounds = max;
    }
}