using System.Text;

namespace Polaris;

public class AdditiveValueNode : PassiveNode
{
    private readonly string stat;
    private readonly int toAdd;

    public AdditiveValueNode(string name, string stat, float addition, string code, NodePosition position, Constellation constellation) : base(name, code, position, constellation)
    {
        this.stat = stat;
        toAdd = (int)(addition * 100);
    }

    public override void ContributeStats(PassiveContext context)
    {
        context.AddToStat(stat, toAdd);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data)
    {
        builder.AppendLine($"<strong>{toAdd}</strong>% increased <strong>{Name}</strong>");
    }
}

public class MultiplicativeValueNode : PassiveNode
{
    private readonly string stat;
    private readonly float multi;

    public override EnumCalculationPriority Priority => EnumCalculationPriority.Multipliers;

    public MultiplicativeValueNode(string name, string stat, float multi, string code, NodePosition position, Constellation constellation) : base(name, code, position, constellation)
    {
        this.stat = stat;
        this.multi = multi;
    }

    public override void ContributeStats(PassiveContext context)
    {
        context.MultiplyStat(stat, multi);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data)
    {
        if (multi < 1f)
        {
            float reduction = (1f - multi) * 100f;
            builder.AppendLine($"<strong>{reduction:F0}</strong>% less <strong>{Name}</strong> gained from passives");
        }
        else if (multi > 1f)
        {
            float increase = (multi - 1f) * 100f;
            builder.AppendLine($"<strong>{increase:F0}</strong>% more <strong>{Name}</strong> gained from passives");
        }
    }
}