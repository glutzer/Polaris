using System;
using System.Collections.Generic;
using System.Linq;

namespace Polaris;

public class WidgetAchievements : Widget
{
    private readonly Offset offset;
    private readonly WidgetNodeDescription description;
    private Achievement? lastHovered;
    private bool lastUnlocked;
    private readonly TextObject label = new("", PolarisGuiThemes.Font, 18f, Vector4.One) { Shadow = true };
    private readonly TextObject title = new("Achievements", PolarisGuiThemes.Font, 36f, Vector4.One) { Shadow = true };

    public WidgetAchievements(Widget? parent, Gui gui, Offset offset) : base(parent, gui)
    {
        this.offset = offset;
        description = new WidgetNodeDescription(this, gui);
        description.SetAchievement(null, false);
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        PlayerPolarisData data = SystemPolaris.Instance(MainAPI.Capi).GetClientData();
        List<Achievement> visible = Achievements.ByCode.Values.Where(a => Achievements.IsVisible(a.Code, data.Achievements)).ToList();
        Dictionary<string, Vector2> positions = [];
        Vector2 center = new(MainAPI.RenderWidth / 2f, MainAPI.RenderHeight / 2f);
        for (int i = 0; i < visible.Count; i++)
            positions[visible[i].Code] = center + ((offset.Value + new Vector2(i % 4 * 250f, i / 4 * 150f) - center) / offset.Zoom);

        shader.BindTexture(PolarisGuiThemes.Blank, "tex2d");
        shader.Color = new Vector4(0.6f, 0.7f, 1f, 0.4f);
        foreach (Achievement achievement in visible)
            foreach (string requirement in achievement.Requirements)
                if (positions.TryGetValue(requirement, out Vector2 start))
                {
                    Vector2 end = positions[achievement.Code];
                    RenderTools.RenderLine(shader, start.X, start.Y, end.X, end.Y, 2f / offset.Zoom);
                }

        Achievement? hovered = null;
        float radius = 18f / offset.Zoom;
        label.SetScale(18f / offset.Zoom);
        foreach (Achievement achievement in visible)
        {
            Vector2 pos = positions[achievement.Code];
            bool hover = Math.Abs(Gui.MouseX - pos.X) <= radius && Math.Abs(Gui.MouseY - pos.Y) <= radius;
            if (hover) hovered = achievement;
            bool unlocked = data.Achievements.Contains(achievement.Code);
            shader.BindTexture(PolarisGuiThemes.Blank, "tex2d");
            shader.Color = hover ? new Vector4(1f, 0.9f, 0.5f, 1f)
                : unlocked ? PolarisGuiThemes.TemporalColor : new Vector4(0.4f, 0.45f, 0.6f, 0.8f);
            RenderTools.RenderQuad(shader, pos.X - radius, pos.Y - radius, radius * 2, radius * 2);
            shader.ResetColor();
            label.Text = achievement.Name;
            label.RenderCenteredLine(pos.X, pos.Y + (radius * 2), shader, true);
            label.Text = unlocked ? "Unlocked" : "Locked";
            label.RenderCenteredLine(pos.X, pos.Y + (radius * 3.5f), shader, true);
        }
        shader.ResetColor();
        title.RenderCenteredLine(center.X, 50f, shader, true);
        bool hoveredUnlocked = hovered != null && data.Achievements.Contains(hovered.Code);
        if (hovered != lastHovered || hoveredUnlocked != lastUnlocked)
        {
            description.SetAchievement(hovered, hoveredUnlocked);
            lastHovered = hovered;
            lastUnlocked = hoveredUnlocked;
        }
    }

}
