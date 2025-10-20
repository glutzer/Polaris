using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

namespace Polaris;

public class WidgetNodes : Widget
{
    private readonly HashSet<(PositionedConstellation constellation, PassiveNode node)> pendingNodes = [];
    //private readonly HashSet<(PositionedConstellation constellation, PassiveNode node)> pendingUnallocationNodes = [];
    private int pendingCount = 0;

    private readonly Offset offset;
    private readonly Texture blank = PolarisGuiThemes.Blank;
    private PassiveNode? hoveredNode;
    private readonly List<PositionedConstellation> positionedConstellations = [];

    private readonly Texture newTex;

    private readonly NineSliceTexture expSides = PolarisGuiThemes.ExpSides;
    private readonly NineSliceTexture expInner = PolarisGuiThemes.ExpInner;

    private readonly TextObject nodeText = new("none", PolarisGuiThemes.Font, 12, new Vector4(0.8f, 0.8f, 0.9f, 0.9f))
    {
        Shadow = true
    };

    private readonly TextObject constText = new("none", PolarisGuiThemes.Font, 36, Vector4.One)
    {
        Shadow = true
    };

    private readonly TextObject expText = new("none", PolarisGuiThemes.Font, 24, Vector4.One)
    {
        Shadow = true
    };

    public WidgetNodes(Widget? parent, Gui gui, Offset offset) : base(parent, gui)
    {
        this.offset = offset;
        PositionConstellations();
        SystemPolarisPassiveTree.Instance(MainAPI.Capi).OnClientDataUpdated += OnClientDataUpdated;

        newTex = TextureBuilder.Begin(128, 128, 4).SetColor(0.8f, 0.8f, 0.8f, 1f).FillMode().DrawHexagon(0, 0, 128, 128).End(false);
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
                pendingCount--;
            }
        }

        foreach ((PositionedConstellation constellation, PassiveNode node) tuple in toRemove)
        {
            pendingNodes.Remove(tuple);
        }
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

        foreach (Constellation constellation in SystemPolarisPassiveTree.Instance(MainAPI.Capi).AllConstellations)
        {
            PositionedConstellation positionedConstellation = new(constellation);

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
                    return;
                }
            }
        }

        hoveredNode = null;
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

            HashSet<PassiveNode> onlyNodes = [.. pendingNodes.Where(x => x.constellation.Constellation == hoveredNode.Constellation).Select(x => x.node)];

            if (constData.IsNodeAllocatable(hoveredNode, onlyNodes))
            {
                // Allocate node.
                pendingNodes.Add((positionedConstellations.Find(pc => pc.Constellation == hoveredNode.Constellation)!, hoveredNode));
                pendingCount += hoveredNode.Cost;
            }
        }
    }

    private bool IsMouseOnNode(MouseEvent obj, PassiveNode node, Vector2 offset)
    {
        int mX = obj.X - (int)this.offset.X - node.Position.X - (int)offset.X;
        int mY = obj.Y - (int)this.offset.Y - node.Position.Y - (int)offset.Y;

        int nodeHalfSize = node.NodeSize;
        return mX >= -nodeHalfSize && mX <= nodeHalfSize && mY >= -nodeHalfSize && mY <= nodeHalfSize;
    }

    private bool IsConstellationInFrame(PositionedConstellation posConst)
    {
        // Frustum cull.
        int x1 = posConst.Offset.X + posConst.Constellation.StartBounds.X + (int)offset.X;
        int y1 = posConst.Offset.Y + posConst.Constellation.StartBounds.Y + (int)offset.Y;
        int x2 = posConst.Offset.X + posConst.Constellation.EndBounds.X + (int)offset.X;
        int y2 = posConst.Offset.Y + posConst.Constellation.EndBounds.Y + (int)offset.Y;
        return x2 >= 0 && y2 >= 0 && x1 <= MainAPI.RenderWidth && y1 <= MainAPI.RenderHeight;
    }

    public override void OnRender(float dt, NuttyShader shader)
    {
        PlayerPolarisData ownData = SystemPolarisPassiveTree.Instance(MainAPI.Capi).GetClientData();

        shader.BindTexture(blank, "tex2d");

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

                // Render line.
                float x1 = node.Position.X + offset.X + posConst.Offset.X;
                float y1 = node.Position.Y + offset.Y + posConst.Offset.Y;

                foreach (PassiveNode child in node.ChildConnections)
                {
                    float x2 = child.Position.X + offset.X + posConst.Offset.X;
                    float y2 = child.Position.Y + offset.Y + posConst.Offset.Y;
                    RenderTools.RenderLine(shader, x1, y1, x2, y2, 4);
                }
            }
        }
        shader.Uniform("color", Vector4.One);

        // Render pending lines.
        shader.Uniform("color", new Vector4(1f, 1f, 0.3f, 0.4f));
        foreach ((PositionedConstellation constellation, PassiveNode node) in pendingNodes)
        {
            PlayerConstellationData constData = ownData.GetConstellation(constellation.Constellation.Name);
            float x1 = node.Position.X + offset.X + constellation.Offset.X;
            float y1 = node.Position.Y + offset.Y + constellation.Offset.Y;

            // It can actually double render here.
            foreach (PassiveNode child in node.Connections)
            {
                if (!constData.AllocatedNodeIds.Contains(child.Id) && !pendingNodes.Contains((constellation, child))) continue;

                float x2 = child.Position.X + offset.X + constellation.Offset.X;
                float y2 = child.Position.Y + offset.Y + constellation.Offset.Y;

                RenderTools.RenderLine(shader, x1, y1, x2, y2, 4);
            }
        }
        shader.Uniform("color", Vector4.One);

        // Render nodes and text.
        foreach (PositionedConstellation posConst in positionedConstellations)
        {
            if (!IsConstellationInFrame(posConst)) continue;

            PlayerConstellationData constData = ownData.GetConstellation(posConst.Constellation.Name);

            shader.BindTexture(newTex, "tex2d");
            foreach (PassiveNode node in posConst.Constellation.AllNodes)
            {
                Vector4 color = node.Color;

                if (constData.AllocatedNodeIds.Contains(node.Id))
                {
                    color = Vector4.Lerp(color, new Vector4(0f, 1f, 1f, 1f), 0.5f);
                }

                if (node == hoveredNode)
                {
                    color = Vector4.Lerp(color, new Vector4(1f, 1f, 0f, 1f), 0.25f);
                }

                shader.Uniform("color", color);

                int nodeHalfSize = node.NodeSize;
                RenderTools.RenderQuad(shader, node.Position.X + offset.X - nodeHalfSize + posConst.Offset.X, node.Position.Y + offset.Y - nodeHalfSize + posConst.Offset.Y, nodeHalfSize * 2, nodeHalfSize * 2);
            }

            foreach (PassiveNode node in posConst.Constellation.AllNodes)
            {
                nodeText.Text = node.Name;

                int nodeHalfSize = node.NodeSize;
                nodeText.RenderCenteredLine(node.Position.X + offset.X + posConst.Offset.X, node.Position.Y + offset.Y + posConst.Offset.Y + (nodeHalfSize * 2), shader);
            }
        }
        shader.Uniform("color", Vector4.One);

        // Render pending nodes.
        shader.Uniform("color", new Vector4(1f, 1f, 0.3f, 0.4f));
        shader.BindTexture(newTex, "tex2d");
        foreach ((PositionedConstellation constellation, PassiveNode node) in pendingNodes)
        {
            int nodeHalfSize = node.NodeSize + 5;
            RenderTools.RenderQuad(shader, node.Position.X + offset.X - nodeHalfSize + constellation.Offset.X, node.Position.Y + offset.Y - nodeHalfSize + constellation.Offset.Y, nodeHalfSize * 2, nodeHalfSize * 2);
            //RenderTools.RenderNineSlice(newTex, shader, node.Position.X + offset.X - nodeHalfSize + constellation.Offset.X, node.Position.Y + offset.Y - nodeHalfSize + constellation.Offset.Y, nodeHalfSize * 2, nodeHalfSize * 2);
        }
        shader.Uniform("color", Vector4.One);

        // Render bounds debug.
        //RenderTools.RenderQuad(shader, constellation.StartBounds.X + posConst.Offset.X + offset.X, constellation.StartBounds.Y + posConst.Offset.Y + offset.Y, constellation.EndBounds.X - constellation.StartBounds.X, constellation.EndBounds.Y - constellation.StartBounds.Y);

        // Render constellation names, experience.
        foreach (PositionedConstellation posConst in positionedConstellations)
        {
            Constellation constellation = posConst.Constellation;

            float cx = posConst.Center.X + offset.X;
            float cy = posConst.Center.Y + offset.Y;

            cy += posConst.Constellation.EndBounds.Y - posConst.Constellation.StartBounds.Y;
            cy += 10f;

            constText.color = constellation.Color;
            constText.Text = posConst.Constellation.Name;
            constText.RenderCenteredLine(cx, cy, shader);

            cy += 10f;

            Vector4 expColor = constellation.Color;
            expColor.W *= 0.5f;

            shader.Uniform("color", expColor);
            RenderTools.RenderNineSlice(expSides, shader, cx - 150f, cy, 300f, 40f);

            float expPercent = 0.75f;
            RenderTools.PushScissor((int)cx - 150, (int)cy, (int)(300f * expPercent), 40);
            RenderTools.RenderNineSlice(expInner, shader, cx - 150f, cy, 300f, 40f);
            RenderTools.PopScissor();

            cy += 20f;

            expText.Text = "75 / 100";
            expText.RenderCenteredLine(cx, cy, shader, true);

            expText.Text = "1";
            expText.RenderLeftAlignedLine(cx - 160f, cy, shader, true);
        }

        // Render own level.
        int level = ownData.Level;

        shader.Uniform("color", new Vector4(0.7f, 0.7f, 0.9f, 0.5f));

        RenderTools.RenderNineSlice(expSides, shader, 0, 0, 300f, 40f);

        float percent = 0.75f;
        RenderTools.PushScissor(0, 0, (int)(300f * percent), 40);
        RenderTools.RenderNineSlice(expInner, shader, 0, 0, 300f, 40f);
        RenderTools.PopScissor();

        expText.Text = "75 / 100";
        expText.RenderCenteredLine(150f, 20f, shader, true);

        expText.Text = $"Level {level}";
        expText.RenderLine(310f, 20f, shader, 0f, true);
    }

    public override void Dispose()
    {
        newTex.Dispose();
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

        public PositionedConstellation(Constellation constellation)
        {
            Constellation = constellation;
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