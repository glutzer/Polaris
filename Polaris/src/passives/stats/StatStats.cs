using System.Text;
using Vintagestory.API.Config;

namespace Polaris;

public class AdditiveStat : PassiveNodeStat
{
    private readonly string stat;
    private readonly float addition;
    private readonly bool flatDisplay;

    public AdditiveStat(string stat, float addition, bool flatDisplay = false)
    {
        this.stat = stat;
        this.addition = addition;
        this.flatDisplay = flatDisplay;
    }

    public override void ContributeStats(PassiveContext context)
    {
        context.AddToFloatStat(stat, addition);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        if (flatDisplay)
        {
            string prefix = addition > 0f ? "+" : "";
            builder.AppendLine($"{prefix}<strong>{addition}</strong> to <strong>{Lang.Get($"polaris:stat{stat}")}</strong>");
            return;
        }

        float percentage = addition * 100f;
        if (percentage > 0f)
        {
            builder.AppendLine($"<strong>{percentage:F0}</strong>% increased <strong>{Lang.Get($"polaris:stat{stat}")}</strong>");
        }
        else if (percentage < 0f)
        {
            builder.AppendLine($"<strong>{-percentage:F0}</strong>% decreased <strong>{Lang.Get($"polaris:stat{stat}")}</strong>");
        }
    }
}

public class MultiplicativeStat : PassiveNodeStat
{
    private readonly string stat;
    private readonly float multi;

    public override EnumCalculationPriority Priority => EnumCalculationPriority.Multipliers;

    public MultiplicativeStat(string stat, float multi)
    {
        this.stat = stat;
        this.multi = multi;
    }

    public override void ContributeStats(PassiveContext context)
    {
        context.MultiplyFloatStat(stat, multi);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        if (multi < 1f)
        {
            float reduction = (1f - multi) * 100f;
            builder.AppendLine($"<strong>{reduction:F0}</strong>% less <strong>{Lang.Get($"polaris:stat{stat}")}</strong> gained from passives");
        }
        else if (multi > 1f)
        {
            float increase = (multi - 1f) * 100f;
            builder.AppendLine($"<strong>{increase:F0}</strong>% more <strong>{Lang.Get($"polaris:stat{stat}")}</strong> gained from passives");
        }
    }
}