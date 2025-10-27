using System;
using System.Text;
using Vintagestory.API.Config;

namespace Polaris;

public class ExtraStatAdditive : PassiveNodeStat
{
    private readonly string stat;
    private readonly float amount;
    private readonly bool flatAmount;
    private readonly float statBase;

    public ExtraStatAdditive(string stat, float amount, bool flatAmount = false, float statBase = 1f)
    {
        this.stat = stat;
        this.amount = amount;
        this.flatAmount = flatAmount;
        this.statBase = statBase;
    }

    public override void ContributeStats(PassiveContext passiveContext)
    {
        passiveContext.SkillBehavior.AddToExtraStat(stat, amount, statBase);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        float amount = this.amount;

        if (!flatAmount)
        {
            amount = MathF.Round(amount * 100f, 2);
        }

        if (amount > 0f)
        {
            builder.AppendLine(Lang.Get($"polaris:extrastatinc{stat}", amount));
            return;
        }

        builder.AppendLine(Lang.Get($"polaris:extrastatdec{stat}", amount));
    }
}

public class ExtraStatMultiplicative : PassiveNodeStat
{
    private readonly string stat;
    private readonly float multi;
    private readonly bool flatAmount;
    private readonly float statBase;

    public ExtraStatMultiplicative(string stat, float multi, bool flatAmount = false, float statBase = 1f)
    {
        this.stat = stat;
        this.multi = multi;
        this.flatAmount = flatAmount;
        this.statBase = statBase;
    }

    public override void ContributeStats(PassiveContext passiveContext)
    {
        passiveContext.SkillBehavior.MultiplyExtraStat(stat, multi, statBase);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        float multi = this.multi;

        if (!flatAmount)
        {
            multi = multi >= 1f ? MathF.Round((multi - 1f) * 100f, 2) : MathF.Round((1f - multi) * 100f, 2);
        }

        if (this.multi > 1f)
        {
            builder.AppendLine(Lang.Get($"polaris:extrastatmul{stat}", multi));
            return;
        }

        builder.AppendLine(Lang.Get($"polaris:extrastatdiv{stat}", multi));
    }
}