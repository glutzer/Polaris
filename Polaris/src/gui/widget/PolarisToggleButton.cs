using System;

namespace Polaris;

public class PolarisToggleButton : WidgetBaseToggleableButton
{
    private readonly TextObject text;

    public PolarisToggleButton(Widget? parent, Gui gui, Action<bool> onClick, bool allowRelease, bool currentValue, string name) : base(parent, gui, onClick, currentValue, allowRelease)
    {
        OnResize += WidgetAllocationButton_OnResize;

        text = new(name, PolarisGuiThemes.Font, 24f, Vector4.One)
        {
            Shadow = true
        };
    }

    protected override void OnMousedOver()
    {
        MainAPI.Capi.Gui.PlaySound("menubutton");
    }

    protected override void OnClicked()
    {
        MainAPI.Capi.Gui.PlaySound("menubutton_press");
    }

    private void WidgetAllocationButton_OnResize()
    {
        text.SetScaleFromWidget(this, 0.8f, 0.9f);
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        Vector4 color = Vector4.One;

        if (state == EnumButtonState.Hovered) color.Xyz *= 1.2f;
        if (enabled) color.Xyz = new Vector3(0.3f, 0.1f, 0.1f);

        shader.Color = color;
        RenderTools.RenderNineSlice(enabled ? VanillaThemes.InsetTexture : VanillaThemes.OutsetTexture, shader, X, Y, Width, Height);
        shader.ResetColor();

        text.color = VanillaThemes.WhitishTextColor;
        text.RenderCenteredLine(XCenter, YCenter, shader, true);
    }
}