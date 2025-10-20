namespace Polaris;

public class AdditiveValueNode : PassiveNode
{
    private readonly string stat;
    private readonly int toAdd;

    public AdditiveValueNode(string name, string stat, float addition, NodePosition position) : base(name, position)
    {
        this.stat = stat;
        toAdd = (int)(addition * 100);
    }

    public override void ContributeStats(PassiveContext context)
    {
        context.AddToStat(stat, toAdd);
    }
}

public class MultiplicativeValueNode : PassiveNode
{
    private readonly string stat;
    private readonly float multi;

    public override EnumCalculationPriority Priority => EnumCalculationPriority.Multipliers;

    public MultiplicativeValueNode(string name, string stat, float multi, NodePosition position) : base(name, position)
    {
        this.stat = stat;
        this.multi = multi;
    }

    public override void ContributeStats(PassiveContext context)
    {
        context.MultiplyStat(stat, multi);
    }
}