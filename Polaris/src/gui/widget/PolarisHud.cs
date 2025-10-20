using Vintagestory.API.Client;

namespace Polaris;

public class PolarisHud : Gui
{
    public override EnumDialogType DialogType => EnumDialogType.HUD;

    public override void PopulateWidgets()
    {
        AddWidget(new WidgetExpGainParticles(null, this).Alignment(Align.RightBottom).Fixed(0, 0, 100, 50));
    }
}