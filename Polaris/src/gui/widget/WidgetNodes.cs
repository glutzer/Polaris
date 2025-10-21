using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

namespace Polaris;

public class WidgetNodes : Widget
{
    private readonly HashSet<(PositionedConstellation constellation, PassiveNode node)> pendingNodes = [];

    private readonly Offset offset;
    private readonly Texture blank = PolarisGuiThemes.Blank;
    private PassiveNode? hoveredNode;
    private readonly List<PositionedConstellation> positionedConstellations = [];

    private readonly NineSliceTexture expSides = PolarisGuiThemes.ExpSides;
    private readonly NineSliceTexture expInner = PolarisGuiThemes.ExpInner;

    private readonly SystemPolarisStarScreen starScreen;

    // Gathered when positioning constellations.
    private int currentLevel = 1;
    private int currentExp = 0;
    private int nextLevelExp = 100;

    private readonly WidgetNodeDescription nodeDescription;
    private bool refunding;

    private readonly TextObject nodeText = new("none", PolarisGuiThemes.Font, 12f, new Vector4(0.8f, 0.8f, 0.9f, 0.9f))
    {
        Shadow = true
    };

    private readonly TextObject constText = new("none", PolarisGuiThemes.Font, 36f, Vector4.One)
    {
        Shadow = true
    };

    private readonly TextObject expText = new("none", PolarisGuiThemes.Font, 24f, Vector4.One)
    {
        Shadow = true
    };

    public WidgetNodes(Widget? parent, Gui gui, Offset offset) : base(parent, gui)
    {
        this.offset = offset;
        starScreen = MainAPI.GetClientSystem<SystemPolarisStarScreen>();
        PositionConstellations();
        SystemPolarisPassiveTree.Instance(MainAPI.Capi).OnClientDataUpdated += OnClientDataUpdated;
        nodeDescription = new WidgetNodeDescription(this, gui);

        new ToggleableButton(this, gui, OnRefundToggle, true, false, "Refund Passives").Alignment(Align.LeftTop).Percent(0f, 0.25f, 0.1f, 0.05f);
    }

    private void OnClientDataUpdated(PlayerPolarisData data)
    {
        HashSet<(PositionedConstellation constellation, PassiveNode node)> toRemove = [];

        // Verify if pending nodes are still allocatable.
        foreach ((PositionedConstellation constellation, PassiveNode node) tuple in pendingNodes)
        {
            PlayerConstellationData constData = data.GetConstellation(tuple.constellation.Constellation.Name);
            HashSet<PassiveNode> onlyNodes = [.. pendingNodes.Where(x => x.constellation.Constellation == tuple.constellation.Constellation).Select(x => x.node)];
            if (!constData.IsNodeAllocatable(tuple.node, onlyNodes) || constData.AllocatedNodeIds.Contains(tuple.node.Id))
            {
                toRemove.Add(tuple);
            }
        }

        foreach ((PositionedConstellation constellation, PassiveNode node) tuple in toRemove)
        {
            pendingNodes.Remove(tuple);
        }
    }

    private void OnRefundToggle(bool on)
    {
        pendingNodes.Clear();
        DeleteChildren<WidgetAllocationButton>();

        refunding = on;
    }

    private void PositionConstellations()
    {
        const float step = 10f;
        const int maxSteps = 500;
        const int samples = 16;

        bool IntersectsAny(PositionedConstellation constellation)
        {
            foreach (PositionedConstellation otherConst in positionedConstellations)
            {
                if (otherConst.Intersects(constellation)) return true;
            }

            return false;
        }

        PlayerPolarisData playerData = SystemPolarisPassiveTree.Instance(MainAPI.Capi).GetClientData();

        currentExp = (int)playerData.Experience;
        currentLevel = playerData.Level;
        nextLevelExp = (int)PlayerPolarisData.GetExpToReachLevel(currentLevel + 1);

        foreach (Constellation constellation in SystemPolarisPassiveTree.Instance(MainAPI.Capi).AllConstellations)
        {
            PlayerConstellationData constData = playerData.GetConstellation(constellation.Name);
            PositionedConstellation positionedConstellation = new(constellation, (int)constData.Experience, (int)constellation.GetExpToReachLevel(constData.Level + 1), constData.Level);

            // Check if it can be placed with no offset.
            if (!IntersectsAny(positionedConstellation))
            {
                positionedConstellations.Add(positionedConstellation);
            }
            else
            {
                bool placed = false;
                float radius = step;
                while (radius < step * maxSteps)
                {
                    // Check points around a circle.
                    for (int i = 0; i < samples; i++)
                    {
                        float angle = (float)(i * Math.PI * 2 / samples);
                        Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                        positionedConstellation.Offset = (Vector2i)offset;

                        if (!IntersectsAny(positionedConstellation))
                        {
                            positionedConstellations.Add(positionedConstellation);
                            placed = true;
                            break;
                        }
                    }

                    if (placed) break;

                    radius += step;
                }

                if (!placed)
                {
                    Console.WriteLine($"Could not place constellation {constellation.Name}");
                }
            }

            // Set center and size for lights.
            positionedConstellation.Center = ((constellation.StartBounds + constellation.EndBounds) / new Vector2i(2)) + positionedConstellation.Offset;
            positionedConstellation.LightRadius = (int)((constellation.EndBounds - constellation.StartBounds).EuclideanLength / 2f);
        }

        List<StarLight> lights = [];
        // Update light positions, without offset.
        foreach (PositionedConstellation posConst in positionedConstellations)
        {
            foreach (PassiveNode node in posConst.Constellation.AllNodes)
            {
                float x = node.Position.X + posConst.Offset.X;
                float y = node.Position.Y + posConst.Offset.Y;

                lights.Add(new StarLight()
                {
                    PosRange = new Vector4(x, -y, 20f * node.NodeSize, 0f),
                    Color = posConst.Constellation.Color
                });
            }
        }

        starScreen.UpdateUbo(lights);
    }

    public override void RegisterEvents(GuiEvents guiEvents)
    {
        guiEvents.MouseDown += GuiEvents_MouseDown;
        guiEvents.MouseUp += GuiEvents_MouseUp;
        guiEvents.MouseMove += GuiEvents_MouseMove;
    }

    private void GuiEvents_MouseMove(MouseEvent obj)
    {
        foreach (PositionedConstellation posConst in positionedConstellations)
        {
            if (!IsConstellationInFrame(posConst)) continue;

            foreach (PassiveNode node in posConst.Constellation.AllNodes)
            {
                if (IsMouseOnNode(obj, node, posConst.Offset))
                {
                    hoveredNode = node;
                    nodeDescription.SetNode(hoveredNode);
                    return;
                }
            }
        }

        hoveredNode = null;
        nodeDescription.SetNode(null);
    }

    private void GuiEvents_MouseUp(MouseEvent obj)
    {
    }

    private void GuiEvents_MouseDown(MouseEvent obj)
    {
        if (obj.Handled) return;

        if (hoveredNode != null)
        {
            obj.Handled = true;

            PlayerPolarisData ownData = SystemPolarisPassiveTree.Instance(MainAPI.Capi).GetClientData();
            PlayerConstellationData constData = ownData.GetConstellation(hoveredNode.Constellation.Name);

            HashSet<string> allocatedNodes = ownData.GetAllAllocatedNodeCodes(SystemPolarisPassiveTree.Instance(MainAPI.Capi));
            // Add pending nodes to allocated nodes.
            foreach ((PositionedConstellation constellation, PassiveNode node) in pendingNodes)
            {
                allocatedNodes.Add($"{constellation.Constellation.Name}:{node.Code}");
            }

            HashSet<PassiveNode> onlyNodes = [.. pendingNodes.Where(x => x.constellation.Constellation == hoveredNode.Constellation).Select(x => x.node)];

            // Lol.
            PositionedConstellation posConst = positionedConstellations.Find(pc => pc.Constellation == hoveredNode.Constellation)!;
            (PositionedConstellation, PassiveNode) tuple = (posConst, hoveredNode);

            int remainingKnowledgePoints = ownData.KnowledgePoints - pendingNodes.Sum(x => x.node.Cost);

            if (!pendingNodes.Contains(tuple) && !constData.AllocatedNodeIds.Contains(hoveredNode.Id) && constData.IsNodeAllocatable(hoveredNode, onlyNodes))
            {
                // Too poor.
                if (remainingKnowledgePoints < hoveredNode.Cost)
                {
                    return;
                }

                if (MainAPI.Capi.World.Player.Entity == null || !hoveredNode.CanAllocate(MainAPI.Capi.World.Player.Entity, ownData, allocatedNodes))
                {
                    return;
                }

                // Allocate node.
                pendingNodes.Add(tuple);
                MainAPI.Capi.Gui.PlaySound("stone_switch");
                if (pendingNodes.Count == 1)
                {
                    AddChild(new WidgetAllocationButton(this, Gui, () =>
                    {
                        // Send allocation packet.
                        MainAPI.Capi.Gui.PlaySound("effect/timeswitch");
                        SendAllocationsInOrder();
                        pendingNodes.Clear();
                        DeleteChildren<WidgetAllocationButton>();
                    }).Alignment(Align.LeftTop).Percent(0f, 0.3f, 0.1f, 0.05f));
                }
            }
            else if (pendingNodes.Contains(tuple) && constData.IsNodeUnallocatable(hoveredNode, onlyNodes))
            {
                pendingNodes.Remove(tuple);
                MainAPI.Capi.Gui.PlaySound("menubutton");
                if (pendingNodes.Count == 0)
                {
                    DeleteChildren<WidgetAllocationButton>();
                }
            }
        }
    }

    private void SendAllocationsInOrder()
    {
        PlayerPolarisData ownData = SystemPolarisPassiveTree.Instance(MainAPI.Capi).GetClientData();
        EntityPlayer? self = MainAPI.Capi.World.Player.Entity;
        if (self == null) return;

        HashSet<string> allocatedNodes = ownData.GetAllAllocatedNodeCodes(SystemPolarisPassiveTree.Instance(MainAPI.Capi));

        // Group each pending allocation by it's constellation into a dictionary.
        Dictionary<Constellation, List<PassiveNode>> allocationsByConst = [];
        foreach ((PositionedConstellation constellation, PassiveNode node) in pendingNodes)
        {
            if (!allocationsByConst.TryGetValue(constellation.Constellation, out List<PassiveNode>? value))
            {
                value = [];
                allocationsByConst[constellation.Constellation] = value;
            }

            value.Add(node);
        }

        foreach (KeyValuePair<Constellation, List<PassiveNode>> kvp in allocationsByConst)
        {
            Constellation constellation = kvp.Key;
            PlayerConstellationData constData = ownData.GetConstellation(constellation.Name);
            SystemPolarisPassiveTree tree = SystemPolarisPassiveTree.Instance(MainAPI.Capi);
            List<PassiveNode> allocations = kvp.Value;
            HashSet<PassiveNode> sentNodes = [];

            while (allocations.Count > 0)
            {
                bool didAllocation = false;

                foreach (PassiveNode node in allocations)
                {
                    if (sentNodes.Contains(node)) continue;
                    if (!constData.IsNodeAllocatable(node, sentNodes)) continue;
                    if (!node.CanAllocate(self, ownData, allocatedNodes)) continue;

                    sentNodes.Add(node);
                    NodeAllocationRequest request = new()
                    {
                        ConstellationName = constellation.Name,
                        NodeId = node.Id,
                        Allocate = true
                    };
                    tree.SendPacket(request);
                    didAllocation = true;
                    allocatedNodes.Add($"{constellation.Name}:{node.Code}");
                }

                if (!didAllocation) break; // Prevent loop.
            }
        }
    }

    private bool IsMouseOnNode(MouseEvent obj, PassiveNode node, Vector2 constOffset)
    {
        Vector2 position = GetZoomedPosition(new Vector2(offset.X + node.Position.X, offset.Y + node.Position.Y) + constOffset);

        int mX = obj.X - (int)position.X;
        int mY = obj.Y - (int)position.Y;

        float nodeHalfSize = node.NodeSize * (1f / offset.Zoom);
        return mX >= -nodeHalfSize && mX <= nodeHalfSize && mY >= -nodeHalfSize && mY <= nodeHalfSize;
    }

    private Vector2 GetNodePosition(PassiveNode node, PositionedConstellation posConst)
    {
        float x = node.Position.X + offset.X + posConst.Offset.X;
        float y = node.Position.Y + offset.Y + posConst.Offset.Y;
        return GetZoomedPosition(new Vector2(x, y));
    }

    private Vector2 GetZoomedPosition(Vector2 position)
    {
        Vector2 offsetFromCenter = position - new Vector2(MainAPI.RenderWidth / 2f, MainAPI.RenderHeight / 2f);
        offsetFromCenter /= offset.Zoom;
        return new Vector2(MainAPI.RenderWidth / 2f, MainAPI.RenderHeight / 2f) + offsetFromCenter;
    }

    private bool IsConstellationInFrame(PositionedConstellation posConst)
    {
        // Frustum cull.
        float x1 = posConst.Offset.X + posConst.Constellation.StartBounds.X + offset.X;
        float y1 = posConst.Offset.Y + posConst.Constellation.StartBounds.Y + offset.Y;
        float x2 = posConst.Offset.X + posConst.Constellation.EndBounds.X + offset.X;
        float y2 = posConst.Offset.Y + posConst.Constellation.EndBounds.Y + offset.Y;

        Vector2 start = GetZoomedPosition(new Vector2(x1, y1));
        Vector2 end = GetZoomedPosition(new Vector2(x2, y2));

        return end.X >= 0 && end.Y >= 0 && start.X <= MainAPI.RenderWidth && start.Y <= MainAPI.RenderHeight;
    }

    public override void OnRender(float dt, NuttyShader shader)
    {
        PlayerPolarisData ownData = SystemPolarisPassiveTree.Instance(MainAPI.Capi).GetClientData();

        shader.BindTexture(blank, "tex2d");
        float invZoom = 1f / offset.Zoom;
        nodeText.SetScale(12f * invZoom);

        // Render lines.
        shader.Uniform("color", new Vector4(0.7f, 0.7f, 1f, 0.4f));
        foreach (PositionedConstellation posConst in positionedConstellations)
        {
            if (!IsConstellationInFrame(posConst)) continue;

            PlayerConstellationData constData = ownData.GetConstellation(posConst.Constellation.Name);
            Constellation constellation = posConst.Constellation;
            foreach (PassiveNode node in constellation.AllNodes)
            {
                if (node.ChildConnections.Count == 0) continue;

                bool allocated = constData.AllocatedNodeIds.Contains(node.Id);

                // Render line.
                Vector2 nodeA = GetNodePosition(node, posConst);

                foreach (PassiveNode child in node.ChildConnections)
                {
                    if (allocated && constData.AllocatedNodeIds.Contains(child.Id))
                    {
                        shader.Uniform("color", PolarisGuiThemes.TemporalColorDark);
                    }
                    else
                    {
                        shader.Uniform("color", new Vector4(1f, 1f, 1f, 0.25f));
                    }
                    Vector2 nodeB = GetNodePosition(child, posConst);
                    RenderTools.RenderLine(shader, nodeA.X, nodeA.Y, nodeB.X, nodeB.Y, 4f);
                }
            }
        }

        shader.Uniform("color", Vector4.One);

        // Render pending lines.
        shader.Uniform("color", new Vector4(1f, 1f, 0.3f, 0.4f));
        foreach ((PositionedConstellation constellation, PassiveNode node) in pendingNodes)
        {
            PlayerConstellationData constData = ownData.GetConstellation(constellation.Constellation.Name);

            Vector2 nodeA = GetNodePosition(node, constellation);

            // It can actually double render here.
            foreach (PassiveNode child in node.Connections)
            {
                if (!constData.AllocatedNodeIds.Contains(child.Id) && !pendingNodes.Contains((constellation, child))) continue;

                Vector2 nodeB = GetNodePosition(child, constellation);

                RenderTools.RenderLine(shader, nodeA.X, nodeA.Y, nodeB.X, nodeB.Y, 4f);
            }
        }
        shader.Uniform("color", Vector4.One);

        // Render nodes and text.
        foreach (PositionedConstellation posConst in positionedConstellations)
        {
            if (!IsConstellationInFrame(posConst)) continue;

            PlayerConstellationData constData = ownData.GetConstellation(posConst.Constellation.Name);

            shader.BindTexture(blank, "tex2d");
            foreach (PassiveNode node in posConst.Constellation.AllNodes)
            {
                Vector4 color = node.Color;

                if (constData.AllocatedNodeIds.Contains(node.Id))
                {
                    color = Vector4.Lerp(color, PolarisGuiThemes.TemporalColor, 0.5f);
                }

                if (node == hoveredNode)
                {
                    color = Vector4.Lerp(color, new Vector4(1f, 1f, 0f, 1f), 0.25f);
                }

                shader.Uniform("color", color);

                Vector2 pos = GetNodePosition(node, posConst);

                float nodeHalfSize = node.NodeSize * invZoom;
                RenderTools.RenderQuad(shader, pos.X - nodeHalfSize, pos.Y - nodeHalfSize, nodeHalfSize * 2f, nodeHalfSize * 2f);
            }

            foreach (PassiveNode node in posConst.Constellation.AllNodes)
            {
                nodeText.Text = node.Name;

                float nodeHalfSize = node.NodeSize * invZoom;

                Vector2 pos = GetNodePosition(node, posConst);

                nodeText.RenderCenteredLine(pos.X, pos.Y + (nodeHalfSize * 2f), shader, true);
            }
        }
        shader.Uniform("color", Vector4.One);

        // Render pending nodes.
        shader.Uniform("color", new Vector4(1f, 1f, 0.3f, 0.4f));
        shader.BindTexture(blank, "tex2d");
        foreach ((PositionedConstellation posConst, PassiveNode node) in pendingNodes)
        {
            float nodeHalfSize = node.NodeSize * invZoom;
            Vector2 nodePos = GetNodePosition(node, posConst);
            RenderTools.RenderQuad(shader, nodePos.X - nodeHalfSize, nodePos.Y - nodeHalfSize, nodeHalfSize * 2f, nodeHalfSize * 2f);

            // Render bounds debug.
            //Constellation constellation = posConst.Constellation;
            //RenderTools.RenderQuad(shader, constellation.StartBounds.X + posConst.Offset.X + offset.X, constellation.StartBounds.Y + posConst.Offset.Y + offset.Y, constellation.EndBounds.X - constellation.StartBounds.X, constellation.EndBounds.Y - constellation.StartBounds.Y);
        }
        shader.Uniform("color", Vector4.One);

        // Render constellation names, experience.
        foreach (PositionedConstellation posConst in positionedConstellations)
        {
            Constellation constellation = posConst.Constellation;

            float barRadius = 150f * invZoom;

            float cx = posConst.Center.X + offset.X;
            float cy = posConst.Center.Y + offset.Y;

            cy += (posConst.Constellation.EndBounds.Y - posConst.Constellation.StartBounds.Y) / 2f;
            cy += 100f;

            Vector2 cPos = GetZoomedPosition(new Vector2(cx, cy));

            cx = cPos.X;
            cy = cPos.Y;

            constText.color = constellation.Color;
            constText.Text = posConst.Constellation.Name;
            constText.RenderCenteredLine(cx, cy, shader);

            cPos.Y += 10f;

            Vector4 expColor = constellation.Color;
            expColor.W *= 0.5f;

            shader.Uniform("color", expColor);
            RenderTools.RenderNineSlice(expSides, shader, cx - barRadius, cy, barRadius * 2f, 40f);

            float expPercent = posConst.CurrentExp / (float)posConst.RequiredExp;
            RenderTools.PushScissor((int)(cx - barRadius), (int)cy, (int)(barRadius * 2f * expPercent), 40);
            RenderTools.RenderNineSlice(expInner, shader, cx - barRadius, cy, barRadius * 2f, 40f);
            RenderTools.PopScissor();

            cy += 20f;

            expText.Text = $"{posConst.CurrentExp} / {posConst.RequiredExp}";
            expText.RenderCenteredLine(cx, cy, shader, true);

            expText.Text = $"{posConst.CurrentLevel}";
            expText.RenderLeftAlignedLine(cx - barRadius - 10f, cy, shader, true);
        }

        // Render own level.
        shader.Uniform("color", PolarisGuiThemes.VintageBrown);

        RenderTools.RenderNineSlice(expSides, shader, 0, 0, 300f, 40f);

        float percent = currentExp / (float)nextLevelExp;
        RenderTools.PushScissor(0, 0, (int)(300f * percent), 40);
        RenderTools.RenderNineSlice(expInner, shader, 0, 0, 300f, 40f);
        RenderTools.PopScissor();

        expText.Text = $"{currentExp} / {nextLevelExp}";
        expText.RenderCenteredLine(150f, 20f, shader, true);

        expText.Text = $"Level {currentLevel}, {ownData.KnowledgePoints} Knowledge Points";
        expText.RenderLine(310f, 20f, shader, 0, true);
    }

    public override void Dispose()
    {
        if (MainAPI.Capi == null) return;
        SystemPolarisPassiveTree.Instance(MainAPI.Capi).OnClientDataUpdated -= OnClientDataUpdated;
    }

    private class PositionedConstellation
    {
        private const float Padding = 300f;

        public Vector2i Offset;
        public Constellation Constellation;

        // For lights, set when positioning.
        public Vector2i Center;
        public float LightRadius;

        public int CurrentExp;
        public int RequiredExp;
        public int CurrentLevel;

        public PositionedConstellation(Constellation constellation, int currentExp, int requiredExp, int currentLevel)
        {
            Constellation = constellation;
            CurrentExp = currentExp;
            RequiredExp = requiredExp;
            CurrentLevel = currentLevel;
        }

        public bool Intersects(PositionedConstellation otherConst)
        {
            Vector2 min = Constellation.StartBounds + Offset - new Vector2(Padding);
            Vector2 max = Constellation.EndBounds + Offset + new Vector2(Padding); // Padding I had to put somewhere.
            Vector2 otherMin = otherConst.Constellation.StartBounds + otherConst.Offset;
            Vector2 otherMax = otherConst.Constellation.EndBounds + otherConst.Offset;
            return min.X < otherMax.X && max.X > otherMin.X && min.Y < otherMax.Y && max.Y > otherMin.Y;
        }
    }
}