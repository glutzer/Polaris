using System.Text;

namespace Polaris;

public class WidgetNodeDescription : Widget
{
    private readonly NineSliceTexture bg = VanillaThemes.OutsetTexture;
    private readonly WidgetRichText richText;

    public override int SortPriority => 1;

    public WidgetNodeDescription(Widget? parent, Gui gui) : base(parent, gui)
    {
        richText = new WidgetRichText(this, gui, "", 16f, PolarisGuiThemes.Font);
        SetChildSizing(ChildSizing.Width | ChildSizing.Height | ChildSizing.LegacyCalc);
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

        PlayerPolarisData playerData = SystemPolaris.Instance(MainAPI.Capi).GetClientData();
        AllocatedNodesInfo allocatedNodesInfo = playerData.GetAllocatedNodesInfo(SystemPolaris.Instance(MainAPI.Capi));

        if (node.Name.Length > 0)
        {
            builder.AppendLine($"<font color=\"#FFFFFF\"><strong>{node.Name}</strong></font>");
            builder.AppendLine();
        }

        node.BuildDescription(builder, playerData, allocatedNodesInfo);

        if (builder.Length == 0)
        {
            SetFade = 1f;
            return;
        }

        richText.SetText(builder.ToString());
        SetFade = 0f;
    }

    public void SetAchievement(Achievement? achievement, bool unlocked)
    {
        if (achievement == null)
        {
            SetFade = 1f;
            return;
        }
        StringBuilder text = new();
        text.AppendLine($"<strong>{achievement.Name}</strong>");
        text.AppendLine(achievement.Description);
        text.AppendLine(unlocked ? "Unlocked" : "Locked");
        if (achievement.KnowledgePointReward > 0)
            text.AppendLine($"Reward: {achievement.KnowledgePointReward} knowledge points");
        if (achievement.ExperienceReward > 0)
        {
            Constellation? rewardConstellation = SystemPolaris.Instance(MainAPI.Capi).GetConstellation(achievement.ExperienceConstellation!);
            string constellationName = rewardConstellation?.DisplayName ?? achievement.ExperienceConstellation!;
            text.AppendLine($"Reward: {achievement.ExperienceReward:0.##} {constellationName} EXP");
        }
        foreach (string code in achievement.Requirements)
            if (Achievements.ByCode.TryGetValue(code, out Achievement? required))
                text.AppendLine($"Requires: {required.Name}");
        richText.SetText(text.ToString());
        SetFade = 0f;
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        RenderTools.RenderNineSlice(bg, shader, X, Y, Width, Height);
    }
}
