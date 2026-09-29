using Vintagestory.API.Client;

namespace Polaris;

public class PolarisHud : Gui
{
    public override EnumDialogType DialogType => EnumDialogType.HUD;

    public override void PopulateWidgets()
    {
        AddWidget(new WidgetSkillLevelUp(null, this).Percent(0f, 0f, 1f, 1f));
    }
}
