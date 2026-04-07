using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

namespace Polaris;

public class WidgetNodes : Widget
{
    private readonly HashSet<(PositionedConstellation constellation, PassiveNode node)> pendingNodes = [];
    private bool refunding;

    private readonly Offset offset;
    private readonly Texture blank = PolarisGuiThemes.Blank;
    private PassiveNode? hoveredNode;
    private readonly List<PositionedConstellation> positionedConstellations = [];

    // Node-moving mode.
    private bool nodeMoveMode;
    private PassiveNode? dragNode;
    private PositionedConstellation? dragConst;
    private Vector2 dragStartWorldMouse;
    private NodePosition dragStartNodePos;

    private readonly NineSliceTexture expSides = PolarisGuiThemes.ExpSides;
    private readonly NineSliceTexture expInner = PolarisGuiThemes.ExpInner;

    private readonly SystemPolarisStarScreen starScreen;

    // Gathered when positioning constellations.
    private int currentLevel = 1;
    private int currentExp = 0;
    private int nextLevelExp = 100;

    private readonly WidgetNodeDescription nodeDescription;

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

    private readonly TextObject moveText = new("", PolarisGuiThemes.Font, 24f, new Vector4(1f, 1f, 0f, 1f))
    {
        Shadow = true
    };

    public WidgetNodes(Widget? parent, Gui gui, Offset offset) : base(parent, gui)
    {
        this.offset = offset;
        starScreen = MainAPI.GetClientSystem<SystemPolarisStarScreen>();
        PositionConstellations();
        SystemPolaris.Instance(MainAPI.Capi).OnClientDataUpdated += OnClientDataUpdated;
        SystemPolaris.Instance(MainAPI.Capi).OnClientExperienceGain += UpdateConstellationExperience;
        nodeDescription = new WidgetNodeDescription(this, gui);

        new PolarisToggleButton(this, gui, OnRefundToggle, true, false, "Refund Passives").Alignment(Align.LeftTop).Percent(0f, 0.25f, 0.1f, 0.05f);
        new PolarisToggleButton(this, gui, OnNodeMoveToggle, true, false, "Node Moving").Alignment(Align.LeftTop).Percent(0f, 0.31f, 0.1f, 0.05f);
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
        DeleteChildren<WidgetVanillaButton>();

        refunding = on;
    }

    private void OnNodeMoveToggle(bool on)
    {
        nodeMoveMode = on;
        dragNode = null;
        dragConst = null;
    }

    /// <summary>
    /// Spaghetti from refunding.
    /// </summary>
    private void UpdateConstellationExperience(Constellation constellation, float amount, int currentLevel, bool alert)
    {
        PlayerPolarisData playerData = SystemPolaris.Instance(MainAPI.Capi).GetClientData();
        foreach (PositionedConstellation posConst in positionedConstellations)
        {
            PlayerConstellationData constData = playerData.GetConstellation(posConst.Constellation.Name);
            posConst.CurrentExp = (int)constData.Experience;
            posConst.RequiredExp = (int)posConst.Constellation.GetExpToReachLevel(constData.Level + 1);
            posConst.CurrentLevel = constData.Level;
        }

        // Update player exp.
        currentExp = (int)playerData.Experience;
        nextLevelExp = (int)PlayerPolarisData.GetExpToReachLevel(this.currentLevel + 1);
        this.currentLevel = playerData.Level;
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

        PlayerPolarisData playerData = SystemPolaris.Instance(MainAPI.Capi).GetClientData();

        currentExp = (int)playerData.Experience;
        currentLevel = playerData.Level;
        nextLevelExp = (int)PlayerPolarisData.GetExpToReachLevel(currentLevel + 1);

        foreach (Constellation constellation in SystemPolaris.Instance(MainAPI.Capi).AllConstellations)
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

    private void RefreshStarLights()
    {
        List<StarLight> lights = [];

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
        if (nodeMoveMode && dragNode != null && dragConst != null)
        {
            Vector2 worldMouse = ScreenToWorld(obj.X, obj.Y);
            Vector2 delta = worldMouse - dragStartWorldMouse;
            int snappedX = SnapToGrid(dragStartNodePos.X + delta.X);
            int snappedY = SnapToGrid(dragStartNodePos.Y + delta.Y);
            dragNode.SetPosition(snappedX, snappedY);
            RefreshStarLights();
            return;
        }

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
        if (nodeMoveMode && dragNode != null)
        {
            Console.WriteLine($"[Polaris] Node '{dragNode.Code}' ({dragNode.Constellation.Name}): ({dragNode.Position.X}, {dragNode.Position.Y})");
            dragNode = null;
            dragConst = null;
        }
    }

    private void GuiEvents_MouseDown(MouseEvent obj)
    {
        if (obj.Handled) return;

        if (nodeMoveMode)
        {
            if (hoveredNode != null)
            {
                dragNode = hoveredNode;
                dragConst = positionedConstellations.Find(pc => pc.Constellation == hoveredNode.Constellation);
                dragStartWorldMouse = ScreenToWorld(obj.X, obj.Y);
                dragStartNodePos = hoveredNode.Position;
                obj.Handled = true;
            }
            return;
        }

        if (hoveredNode != null)
        {
            obj.Handled = true;

            PlayerPolarisData ownData = SystemPolaris.Instance(MainAPI.Capi).GetClientData();
            PlayerConstellationData constData = ownData.GetConstellation(hoveredNode.Constellation.Name);
            SystemPolaris treeSystem = SystemPolaris.Instance(MainAPI.Capi);

            // Lol.
            PositionedConstellation posConst = positionedConstellations.Find(pc => pc.Constellation == hoveredNode.Constellation)!;
            (PositionedConstellation, PassiveNode) tuple = (posConst, hoveredNode);

            AllocatedNodesInfo allocatedNodes = ownData.GetAllocatedNodesInfo(SystemPolaris.Instance(MainAPI.Capi));
            foreach ((PositionedConstellation constel, PassiveNode node) in pendingNodes)
            {
                if (refunding)
                {
                    allocatedNodes.AllocatedNodeCodes.Remove($"{constel.Constellation.Name}:{node.Code}");
                    allocatedNodes.DecrementTags(node.Tags);
                }
                else
                {
                    allocatedNodes.AllocatedNodeCodes.Add($"{constel.Constellation.Name}:{node.Code}");
                    allocatedNodes.IncrementTags(node.Tags);
                }
            }

            HashSet<PassiveNode> onlyNodes = [.. pendingNodes.Where(x => x.constellation.Constellation == hoveredNode.Constellation).Select(x => x.node)];

            // Add pending nodes to allocated nodes.
            if (refunding)
            {
                // Costs 1 level to refund a node.
                // If this refund would cause the player to go below level 1, don't allow it.
                int remainingLevels = constData.Level - pendingNodes.Count(x => x.constellation.Constellation == hoveredNode.Constellation);
                HashSet<PassiveNode> allNodes = [.. pendingNodes.Select(x => x.node)];

                if (!pendingNodes.Contains(tuple) && constData.AllocatedNodeIds.Contains(hoveredNode.Id) && constData.IsNodeUnallocatable(hoveredNode, onlyNodes, true) && !ownData.DoesAnythingRelyOnNode(hoveredNode, treeSystem, allNodes))
                {
                    if (remainingLevels < 2) return;

                    // Unallocate node.
                    pendingNodes.Add(tuple);
                    MainAPI.Capi.Gui.PlaySound("stone_switch");
                    if (pendingNodes.Count == 1)
                    {
                        AddChild(new WidgetVanillaButton(this, Gui, () =>
                        {
                            // Send allocation packet.
                            MainAPI.Capi.Gui.PlaySound("effect/timeswitch");
                            SendAllocationsInOrder(true);
                            pendingNodes.Clear();
                            DeleteChildren<WidgetVanillaButton>();
                        }, "Allocate").Alignment(Align.LeftTop).Percent(0f, 0.3f, 0.1f, 0.05f));
                    }
                }
                else if (pendingNodes.Contains(tuple) && constData.IsNodeAllocatable(hoveredNode, onlyNodes, true))
                {
                    if (MainAPI.Capi.World.Player.Entity == null || !hoveredNode.CanAllocate(MainAPI.Capi.World.Player.Entity, ownData, allocatedNodes))
                    {
                        return;
                    }

                    pendingNodes.Remove(tuple);
                    MainAPI.Capi.Gui.PlaySound("menubutton");
                    if (pendingNodes.Count == 0)
                    {
                        DeleteChildren<WidgetVanillaButton>();
                    }
                }
            }
            else
            {
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
                        AddChild(new WidgetVanillaButton(this, Gui, () =>
                        {
                            // Send allocation packet.
                            MainAPI.Capi.Gui.PlaySound("effect/timeswitch");
                            SendAllocationsInOrder(false);
                            pendingNodes.Clear();
                            DeleteChildren<WidgetVanillaButton>();
                        }, "Allocate").Alignment(Align.LeftTop).Percent(0f, 0.3f, 0.1f, 0.05f));
                    }
                }
                else if (pendingNodes.Contains(tuple) && constData.IsNodeUnallocatable(hoveredNode, onlyNodes))
                {
                    pendingNodes.Remove(tuple);
                    MainAPI.Capi.Gui.PlaySound("menubutton");
                    if (pendingNodes.Count == 0)
                    {
                        DeleteChildren<WidgetVanillaButton>();
                    }
                }
            }
        }
    }

    private void SendAllocationsInOrder(bool refund)
    {
        PlayerPolarisData ownData = SystemPolaris.Instance(MainAPI.Capi).GetClientData();
        EntityPlayer? self = MainAPI.Capi.World.Player.Entity;
        SystemPolaris tree = SystemPolaris.Instance(MainAPI.Capi);
        if (self == null) return;

        // Every current allocated node.
        AllocatedNodesInfo allocatedNodes = ownData.GetAllocatedNodesInfo(SystemPolaris.Instance(MainAPI.Capi));

        int toSend = pendingNodes.Count;
        HashSet<PassiveNode> sentNodes = [];

        while (sentNodes.Count < toSend)
        {
            bool didAllocation = false;

            foreach ((PositionedConstellation constellation, PassiveNode node) tuple in pendingNodes)
            {
                if (sentNodes.Contains(tuple.node)) continue;

                PlayerConstellationData constData = ownData.GetConstellation(tuple.node.Constellation.Name);

                if (refund)
                {
                    if (!constData.IsNodeUnallocatable(tuple.node, sentNodes, true)) continue;
                    if (ownData.DoesAnythingRelyOnNode(tuple.node, tree, sentNodes)) continue;
                }
                else
                {
                    if (!constData.IsNodeAllocatable(tuple.node, sentNodes)) continue;
                    if (!tuple.node.CanAllocate(self, ownData, allocatedNodes)) continue;

                    allocatedNodes.AllocatedNodeCodes.Add($"{tuple.node.Constellation.Name}:{tuple.node.Code}");
                    allocatedNodes.IncrementTags(tuple.node.Tags);
                }

                sentNodes.Add(tuple.node);
                NodeAllocationRequest request = new()
                {
                    ConstellationName = tuple.node.Constellation.Name,
                    NodeId = tuple.node.Id,
                    Allocate = !refund
                };
                tree.SendPacket(request);
                didAllocation = true;
            }

            // Infinite loop.
            if (!didAllocation) break;
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

    public override void OnRender(float dt, ShaderGui shader)
    {
        PlayerPolarisData ownData = SystemPolaris.Instance(MainAPI.Capi).GetClientData();

        shader.BindTexture(blank, "tex2d");
        float invZoom = 1f / offset.Zoom;
        nodeText.SetScale(12f * invZoom);

        // Render lines.
        shader.Color = new Vector4(0.7f, 0.7f, 1f, 0.4f);
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

        // Render pending lines.
        shader.Color = refunding ? new Vector4(1f, 0.2f, 0.2f, 0.4f) : new Vector4(1f, 1f, 0.3f, 0.4f);
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
        shader.ResetColor();

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

                if (node == dragNode)
                {
                    color = Vector4.Lerp(color, new Vector4(1f, 0.5f, 0f, 1f), 0.5f);
                }

                shader.Color = color;

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

        // Render pending nodes.
        shader.Color = refunding ? new Vector4(1f, 0.2f, 0.2f, 0.4f) : new Vector4(1f, 1f, 0.3f, 0.4f);
        shader.BindTexture(blank, "tex2d");
        foreach ((PositionedConstellation posConst, PassiveNode node) in pendingNodes)
        {
            float nodeHalfSize = node.NodeSize * invZoom;
            Vector2 nodePos = GetNodePosition(node, posConst);
            RenderTools.RenderQuad(shader, nodePos.X - nodeHalfSize, nodePos.Y - nodeHalfSize, nodeHalfSize * 2f, nodeHalfSize * 2f);
        }
        shader.ResetColor();

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

            shader.Color = expColor;
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
        shader.Color = PolarisGuiThemes.VintageBrown;

        RenderTools.RenderNineSlice(expSides, shader, 0, 0, 300f, 40f);

        float percent = currentExp / (float)nextLevelExp;
        RenderTools.PushScissor(0, 0, (int)(300f * percent), 40);
        RenderTools.RenderNineSlice(expInner, shader, 0, 0, 300f, 40f);
        RenderTools.PopScissor();

        expText.Text = $"{currentExp} / {nextLevelExp}";
        expText.RenderCenteredLine(150f, 20f, shader, true);

        expText.Text = $"Level {currentLevel}, {ownData.KnowledgePoints} Knowledge Points";
        expText.RenderLine(310f, 20f, shader, 0, true);

        shader.ResetColor();

        if (nodeMoveMode && dragNode != null)
        {
            moveText.Text = $"{dragNode.Code}: ({dragNode.Position.X}, {dragNode.Position.Y})";
            moveText.RenderCenteredLine(MainAPI.RenderWidth / 2f, 60f, shader, true);
        }
    }

    private Vector2 ScreenToWorld(float screenX, float screenY)
    {
        float cx = MainAPI.RenderWidth / 2f;
        float cy = MainAPI.RenderHeight / 2f;
        return new Vector2(cx + (screenX - cx) * offset.Zoom, cy + (screenY - cy) * offset.Zoom);
    }

    private static int SnapToGrid(float value) => (int)(MathF.Round(value / 10f) * 10);

    public override void Dispose()
    {
        if (MainAPI.Capi == null) return;
        SystemPolaris.Instance(MainAPI.Capi).OnClientDataUpdated -= OnClientDataUpdated;
        SystemPolaris.Instance(MainAPI.Capi).OnClientExperienceGain -= UpdateConstellationExperience;
    }

    private class PositionedConstellation
    {
        private const float Padding = 500f;

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