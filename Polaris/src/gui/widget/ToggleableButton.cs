using System;

namespace Polaris;

public class ToggleableButton : WidgetBaseToggleableButton
{
    private readonly NineSliceTexture button = PolarisGuiThemes.Button;
    private readonly TextObject text;

    public ToggleableButton(Widget? parent, Gui gui, Action<bool> onClick, bool allowRelease, bool currentValue, string name) : base(parent, gui, onClick, allowRelease, currentValue)
    {
        OnResize += WidgetAllocationButton_OnResize;

        text = new(name, PolarisGuiThemes.Font, 24f, Vector4.One)
        {
            Shadow = true
        };
    }

    private void WidgetAllocationButton_OnResize()
    {
        text.SetScaleFromWidget(this, 0.6f, 0.9f);
    }

    public override void OnRender(float dt, NuttyShader shader)
    {
        Vector4 color = PolarisGuiThemes.VintageBrown;

        if (state == EnumButtonState.Hovered) color.Xyz *= 1.2f;
        if (state == EnumButtonState.Active) color.Xyz *= 0.8f;

        shader.Uniform("color", color);
        RenderTools.RenderNineSlice(button, shader, X, Y, Width, Height);
        shader.Uniform("color", Vector4.One);

        text.color = state == EnumButtonState.Active ? new Vector4(0.8f, 0.8f, 0.8f, 1f) : Vector4.One;
        text.RenderCenteredLine(XCenter, YCenter, shader, true);
    }
}