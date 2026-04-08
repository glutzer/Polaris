using System;
using System.Collections.Generic;

namespace Polaris;

/// <summary>
/// Singleton constellation.
/// </summary>
public class Constellation
{
    public string Name { get; }
    private readonly List<PassiveNode> nodes = [];
    private readonly Dictionary<string, PassiveNode> nodesByCode = [];
    public IEnumerable<PassiveNode> AllNodes => nodes;

    public Vector4 Color { get; private set; } = new(1f, 1f, 1f, 1f);

    public Vector2i StartBounds { get; private set; }
    public Vector2i EndBounds { get; private set; }

    public float BaseExpCurve { get; private set; } = 1.5f;
    public float BaseExpRequirement { get; private set; } = 100f;

    private int indexCounter;

    public Constellation(string name)
    {
        Name = name;
    }

    public int GrabNextId()
    {
        return indexCounter++;
    }

    /// <summary>
    /// How much exp to reach this level.
    /// </summary>
    public float GetExpToReachLevel(int level)
    {
        return BaseExpRequirement * MathF.Pow(level - 1, BaseExpCurve);
    }

    public float GetTotalExpGained(int level, float currentExp)
    {
        float totalExp = 0f;
        for (int i = 1; i <= level; i++)
        {
            totalExp += GetExpToReachLevel(i);
        }
        return totalExp + currentExp;
    }

    public Constellation SetExpCurve(float power, float baseRequirement)
    {
        BaseExpCurve = power;
        BaseExpRequirement = baseRequirement;
        return this;
    }

    public PassiveNode? GetNodeById(int nodeId)
    {
        return nodeId < 0 || nodeId >= nodes.Count ? null : nodes[nodeId];
    }

    public PassiveNode? GetNodeByCode(string code)
    {
        nodesByCode.TryGetValue(code, out PassiveNode? node);
        return node;
    }

    public void AddNode(PassiveNode node)
    {
        if (nodesByCode.ContainsKey(node.Code))
        {
            throw new Exception($"Constellation {Name} already has a node with code {node.Code}!");
        }

        nodes.Add(node);
        nodesByCode[node.Code] = node;
        RecalculateBounds();
    }

    public Constellation SetColor(float r, float g, float b, float a)
    {
        Color = new Vector4(r, g, b, a);
        return this;
    }

    /// <summary>
    /// Recalculate bounds every time a node is changed.
    /// </summary>
    internal void RecalculateBounds()
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

    /// <summary>
    /// Adds a start node at 0, 0 called "start".
    /// </summary>
    public Constellation AddStartNode()
    {
        PassiveNode.Create("", "start", this).MakeStartNode().SetCost(0).SetSize(0.8f).SetColor(0.5f, 0.5f, 0.5f, 1f);
        return this;
    }
}