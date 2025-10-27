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
            builder.AppendLine($"{prefix}{addition} to {Lang.Get($"polaris:stat{stat}")}");
            return;
        }

        float percentage = addition * 100f;
        if (percentage > 0f)
        {
            builder.AppendLine($"{percentage:F0}% increased {Lang.Get($"polaris:stat{stat}")}");
        }
        else if (percentage < 0f)
        {
            builder.AppendLine($"{-percentage:F0}% decreased {Lang.Get($"polaris:stat{stat}")}");
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
            builder.AppendLine($"{reduction:F0}% less {Lang.Get($"polaris:stat{stat}")} gained from passives");
        }
        else if (multi > 1f)
        {
            float increase = (multi - 1f) * 100f;
            builder.AppendLine($"{increase:F0}% more {Lang.Get($"polaris:stat{stat}")} gained from passives");
        }
    }
}