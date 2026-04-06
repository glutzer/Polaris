using System.Collections.Generic;

namespace Polaris;

/// <summary>
/// Rich text widget, will set it's size to the text size.
/// </summary>
public class WidgetRichText : Widget
{
    private List<MarkdownRun> runs = [];
    private readonly Font font;
    private readonly float fontScale;
    private readonly int padding;

    public WidgetRichText(Widget? parent, Gui gui, string text, float fontScale, Font font, int padding = 20) : base(parent, gui)
    {
        this.font = font;
        this.fontScale = fontScale;
        this.padding = padding;
        SetText(text);
    }

    public void SetText(string text)
    {
        runs = Markdown.ConvertMarkdownLine(text, fontScale, font);

        // Remove last element from runs if it's a BreakRun.
        if (runs.Count > 0 && runs[^1] is BreakRun)
        {
            runs.RemoveAt(runs.Count - 1);
        }

        int maxX = 0;
        int maxY = 0;
        int xAdv = 0;
        int yAdv = (int)(font.LineHeight * fontScale * 0.5f); // Hardcoded to move down half a line and not center.

        foreach (MarkdownRun run in runs)
        {
            run.GetPositionAtRunEnd(0, 0, ref xAdv, ref yAdv);
            if (xAdv > maxX) maxX = xAdv;
            maxY = yAdv;
        }

        FixedSize(maxX + (padding * 2), maxY + (padding * 2)).NoScaling();
    }

    public override void OnRender(float dt, ShaderGui shader)
    {
        int xAdv = 0;
        int yAdv = (int)(font.LineHeight * fontScale * 0.5f);

        foreach (MarkdownRun run in runs)
        {
            run.Render(X + padding, Y + padding, ref xAdv, ref yAdv, shader);
        }
    }
}