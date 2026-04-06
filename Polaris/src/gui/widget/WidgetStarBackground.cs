using System;

namespace Polaris;

public class WidgetStarBackground : Widget
{
    private readonly Offset offset;
    private bool dragging;
    private readonly SystemPolarisStarScreen starScreen;

    public WidgetStarBackground(Widget? parent, Gui gui, Offset offset) : base(parent, gui)
    {
        this.offset = offset;
        starScreen = MainAPI.GetClientSystem<SystemPolarisStarScreen>();
    }

    public override void RegisterEvents(GuiEvents guiEvents)
    {
        guiEvents.MouseDown += (e) =>
        {
            if (e.Handled) return;

            if (IsInAllBounds(e))
            {
                dragging = true;
                e.Handled = true;
            }
        };

        guiEvents.MouseUp += (e) =>
        {
            dragging = false;

            if (IsInAllBounds(e))
            {
                e.Handled = true;
            }
        };

        guiEvents.MouseMove += (e) =>
        {
            if (IsInAllBounds(e))
            {
                if (dragging)
                {
                    offset.Value.X += e.DeltaX * offset.Zoom;
                    offset.Value.Y += e.DeltaY * offset.Zoom;
                }

                e.Handled = true;
            }
        };

        guiEvents.MouseWheel += (e) =>
        {
            if (e.IsHandled) return;
            e.SetHandled();

            offset.Zoom += e.delta * -0.1f;
            offset.Zoom = Math.Clamp(offset.Zoom, 0.2f, 3f);
        };
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        NuttyShader starShader = NuttyShaderRegistry.Get("polarisstars");
        starShader.Use();

        starShader.Uniform("starCount", starScreen.StarCount);
        starShader.Uniform("renderWidth", (float)MainAPI.RenderWidth);
        starShader.Uniform("renderHeight", (float)MainAPI.RenderHeight);

        starShader.Uniform("time", MainAPI.Capi.ElapsedMilliseconds / 1000f);
        starShader.Uniform("offset", offset.Value);
        starShader.Uniform("fade", Fade);
        starShader.Uniform("zoom", offset.Zoom);

        // No texture needed.
        RenderTools.RenderQuad(starShader, X, Y, Width, Height);

        shader.Use();
    }
}