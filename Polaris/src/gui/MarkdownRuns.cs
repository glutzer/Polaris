namespace Polaris;

public abstract class MarkdownRun
{
    /// <summary>
    /// Used for calculating bounds.
    /// </summary>
    public abstract void GetPositionAtRunEnd(float x, float y, ref int xAdvance, ref int yAdvance);
    public abstract void Render(float x, float y, ref int xAdvance, ref int yAdvance, NuttyShader shader);
}

public class TextRun : MarkdownRun
{
    private readonly TextObject textObject;

    public TextRun(TextObject textObject)
    {
        this.textObject = textObject;
    }

    public override void GetPositionAtRunEnd(float x, float y, ref int xAdvance, ref int yAdvance)
    {
        xAdvance += textObject.PixelLength;
    }

    public override void Render(float x, float y, ref int xAdvance, ref int yAdvance, NuttyShader shader)
    {
        xAdvance = textObject.RenderLine(x, y + yAdvance, shader, xAdvance);
    }
}

public class BreakRun : MarkdownRun
{
    private readonly int lineHeight;

    public BreakRun(Font font, float scale)
    {
        lineHeight = (int)(font.LineHeight * scale);
    }

    public override void GetPositionAtRunEnd(float x, float y, ref int xAdvance, ref int yAdvance)
    {
        xAdvance = 0;
        yAdvance += lineHeight;
    }

    public override void Render(float x, float y, ref int xAdvance, ref int yAdvance, NuttyShader shader)
    {
        xAdvance = 0;
        yAdvance += lineHeight;
    }
}