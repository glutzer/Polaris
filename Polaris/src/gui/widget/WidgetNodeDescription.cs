using System.Text;

namespace Polaris;

public class WidgetNodeDescription : Widget
{
    private readonly NineSliceTexture bg = PolarisGuiThemes.Background;
    private readonly WidgetRichText richText;

    public override int SortPriority => 1;

    public WidgetNodeDescription(Widget? parent, Gui gui) : base(parent, gui)
    {
        richText = new WidgetRichText(this, gui, "", 16f, PolarisGuiThemes.Font);
        SetChildSizing(ChildSizing.Width | ChildSizing.Height);
    }

    public override void RegisterEvents(GuiEvents guiEvents)
    {
        guiEvents.BeforeRender += GuiEvents_BeforeRender;
    }

    private void GuiEvents_BeforeRender(float obj)
    {
        FixedPos(Gui.MouseX + 64, Gui.MouseY);
        Alignment(Align.None);
    }

    public void SetNode(PassiveNode? node)
    {
        if (node == null)
        {
            SetFade = 1f;
            return;
        }

        StringBuilder builder = new();

        PlayerPolarisData playerData = Polaris.Instance(MainAPI.Capi).GetClientData();
        AllocatedNodesInfo allocatedNodesInfo = playerData.GetAllocatedNodesInfo(Polaris.Instance(MainAPI.Capi));

        if (node.Name.Length > 0)
        {
            builder.AppendLine($"<font color=\"#FFFFFF\"><strong>{node.Name}</strong></font>");
            builder.AppendLine();
        }

        node.BuildDescription(builder, playerData);

        // Rone code roadblock.
        if (MainAPI.Capi.World.Player.Entity != null)
        {
            foreach (PassiveNodeRequirement requirement in node.Requirements)
            {
                requirement.BuildDescription(builder, playerData, MainAPI.Capi.World.Player.Entity, allocatedNodesInfo);
            }
        }

        richText.SetText(builder.ToString());
        SetFade = 0f;
    }

    public override void OnRender(float dt, NuttyShader shader)
    {
        shader.Uniform("color", PolarisGuiThemes.VintageBrown);
        RenderTools.RenderNineSlice(bg, shader, X, Y, Width, Height);
        shader.Uniform("color", Vector4.One);
    }
}