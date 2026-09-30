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
            builder.AppendLine($"{percentage:0.##}% increased {Lang.Get($"polaris:stat{stat}")}");
        }
        else if (percentage < 0f)
        {
            builder.AppendLine($"{-percentage:0.##}% decreased {Lang.Get($"polaris:stat{stat}")}");
        }
    }
}

public class AdditiveStatPerLevel : PassiveNodeStat
{
    private readonly string stat;
    private readonly float additionPerLevel;
    private readonly string constellationName;
    private readonly bool flatDisplay;

    public AdditiveStatPerLevel(string stat, float additionPerLevel, string constellationName, bool flatDisplay = false)
    {
        this.stat = stat;
        this.additionPerLevel = additionPerLevel;
        this.constellationName = constellationName;
        this.flatDisplay = flatDisplay;
    }

    public override void ContributeStats(PassiveContext context)
    {
        int level = context.PolarisData.GetConstellation(constellationName).Level;
        context.AddToFloatStat(stat, additionPerLevel * level);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        int level = data.GetConstellation(constellationName).Level;
        float total = additionPerLevel * level;

        if (flatDisplay)
        {
            string prefix = additionPerLevel > 0f ? "+" : "";
            builder.AppendLine($"{prefix}{additionPerLevel} per {constellationName} level to {Lang.Get($"polaris:stat{stat}")}");
            return;
        }

        float pctPerLevel = additionPerLevel * 100f;
        float pctTotal = total * 100f;
        if (pctPerLevel > 0f)
        {
            builder.AppendLine($"{pctPerLevel:0.##}% increased {Lang.Get($"polaris:stat{stat}")} per {constellationName} level");
        }
        else if (pctPerLevel < 0f)
        {
            builder.AppendLine($"{-pctPerLevel:0.##}% decreased {Lang.Get($"polaris:stat{stat}")} per {constellationName} level");
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
            builder.AppendLine($"{reduction:0.##}% less {Lang.Get($"polaris:stat{stat}")} gained from passives");
        }
        else if (multi > 1f)
        {
            float increase = (multi - 1f) * 100f;
            builder.AppendLine($"{increase:0.##}% more {Lang.Get($"polaris:stat{stat}")} gained from passives");
        }
    }
}
