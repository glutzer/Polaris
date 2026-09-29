namespace Polaris;

public class GuiPolarisMenu : Gui
{
    public bool ShowAchievements { get; set; }
    public override double DrawOrder => 0;
    public override double InputOrder => 0;

    public override bool OnEscapePressed()
    {
        return false;
    }

    public override void OnKeyDown(Vintagestory.API.Client.KeyEvent args)
    {
        if (args.KeyCode == (int)Vintagestory.API.Client.GlKeys.Escape)
        {
            MainAPI.GetClientSystem<SystemPolarisStarScreen>().StopLookingAtStars();
            args.Handled = true;
            return;
        }
        base.OnKeyDown(args);
    }

    public override void PopulateWidgets()
    {
        Offset offset = new();
        offset.Value.X = (int)(MainAPI.RenderWidth / 2f);
        offset.Value.Y = (int)(MainAPI.RenderHeight / 2f);

        Widget bg;
        AddWidget(bg = new WidgetStarBackground(null, this, offset).Percent(0f, 0f, 1f, 1f));

        if (ShowAchievements)
            new WidgetAchievements(bg, this, offset).Percent(0f, 0f, 1f, 1f);
        else
            new WidgetNodes(bg, this, offset).Percent(0f, 0f, 1f, 1f);
    }

    public void FadeIn(float totalTime)
    {
        TryOpen();
        foreach (Widget widget in ForWidgets<WidgetStarBackground>())
        {
            float t = totalTime;
            widget.SetFade = 1f;

            // Fade in stars once complete.
            foreach (Widget w in ForWidgets<WidgetNodes>())
            {
                w.SetFade = 1f;
            }

            widget.FadeTo(1f, totalTime, () =>
            {
                foreach (Widget widget in ForWidgets<WidgetNodes>())
                {
                    widget.FadeTo(1f, 1f);
                }
            });
        }
    }

    public void FadeOut(float totalTime)
    {
        foreach (Widget widget in ForWidgets<WidgetStarBackground>())
        {
            widget.FadeTo(0f, totalTime, () =>
            {
                TryClose();
            });
        }
    }
}
