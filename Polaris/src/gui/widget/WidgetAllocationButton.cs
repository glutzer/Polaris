using System;

namespace Polaris;

public class WidgetAllocationButton : WidgetBaseButton
{
    private readonly NineSliceTexture button = PolarisGuiThemes.Button;
    private readonly TextObject expText = new("Allocate", PolarisGuiThemes.Font, 24, Vector4.One)
    {
        Shadow = true
    };

    public WidgetAllocationButton(Widget? parent, Gui gui, Action onClick) : base(parent, gui, onClick)
    {
        OnResize += WidgetAllocationButton_OnResize;
    }

    private void WidgetAllocationButton_OnResize()
    {
        expText.SetScaleFromWidget(this, 0.6f, 0.9f);
    }

    public override void OnRender(float dt, NuttyShader shader)
    {
        Vector4 color = new(0.7f, 0.7f, 0.9f, 0.5f);

        if (state == EnumButtonState.Hovered) color.Xyz *= 1.2f;
        if (state == EnumButtonState.Active) color.Xyz *= 0.8f;

        shader.Uniform("color", color);
        RenderTools.RenderNineSlice(button, shader, X, Y, Width, Height);
        shader.Uniform("color", Vector4.One);

        expText.color = state == EnumButtonState.Active ? new Vector4(0.8f, 0.8f, 0.8f, 1f) : Vector4.One;
        expText.RenderCenteredLine(XCenter, YCenter, shader, true);
    }
}