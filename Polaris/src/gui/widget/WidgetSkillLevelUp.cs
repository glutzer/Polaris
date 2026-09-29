using System;
using System.Collections.Generic;
using Vintagestory.API.Config;

namespace Polaris;

/// <summary>Queued, stationary skill-increase notices near the top center of the HUD.</summary>
public class WidgetSkillLevelUp : Widget
{
    private const float FadeInSeconds = 0.6f;
    private const float HoldSeconds = 2.4f;
    private const float FadeOutSeconds = 1f;
    private const float Duration = FadeInSeconds + HoldSeconds + FadeOutSeconds;
    private readonly Queue<(Constellation Skill, int Level)> pending = [];
    private readonly TextObject text = new("", PolarisGuiThemes.Font, 32f, Vector4.One) { Shadow = true };
    private float age;
    private bool showing;

    public WidgetSkillLevelUp(Widget? parent, Gui gui) : base(parent, gui)
    {
        SystemPolaris.Instance(MainAPI.Capi).OnClientSkillLevelUp += OnLevelUp;
    }

    private void OnLevelUp(Constellation skill, int level)
    {
        pending.Enqueue((skill, level));
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        if (!showing)
        {
            if (!pending.TryDequeue(out (Constellation Skill, int Level) notice)) return;
            text.Text = Lang.Get("polaris:skill-level-increased", notice.Skill.DisplayName, notice.Level);
            age = 0f;
            showing = true;
        }

        age += dt;
        float opacity = age < FadeInSeconds ? age / FadeInSeconds
            : age <= FadeInSeconds + HoldSeconds ? 1f
            : 1f - ((age - FadeInSeconds - HoldSeconds) / FadeOutSeconds);
        opacity = Math.Clamp(opacity, 0f, 1f);
        // Smooth the fade at both ends without changing the shared menu fade behavior.
        opacity = opacity * opacity * (3f - (2f * opacity));
        text.color = new Vector4(0.95f, 0.93f, 0.85f, opacity);
        text.RenderCenteredLine(MainAPI.RenderWidth / 2f, MainAPI.RenderHeight * 0.2f, shader, true);
        if (age >= Duration) showing = false;
    }

    public override void Dispose()
    {
        if (MainAPI.Capi != null)
            SystemPolaris.Instance(MainAPI.Capi).OnClientSkillLevelUp -= OnLevelUp;
        pending.Clear();
        base.Dispose();
    }
}
