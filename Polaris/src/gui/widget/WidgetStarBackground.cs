namespace Polaris;

public class WidgetStarBackground : Widget
{
    private readonly Offset offset;
    private bool dragging;

    public WidgetStarBackground(Widget? parent, Gui gui, Offset offset) : base(parent, gui)
    {
        this.offset = offset;
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
                    offset.Value.X += e.DeltaX;
                    offset.Value.Y += e.DeltaY;
                }

                e.Handled = true;
            }
        };
    }

    public override void OnRender(float dt, NuttyShader shader)
    {
        NuttyShader starShader = NuttyShaderRegistry.Get("polarisstars");
        starShader.Use();

        starShader.Uniform("time", MainAPI.Capi.ElapsedMilliseconds / 1000f);
        starShader.Uniform("offset", offset.Value);
        starShader.Uniform("fade", Fade);

        // No texture needed.
        RenderTools.RenderQuad(starShader, X, Y, Width, Height);

        shader.Use();
    }
}