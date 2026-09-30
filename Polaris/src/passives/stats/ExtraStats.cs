using System;
using System.Text;
using Vintagestory.API.Config;

namespace Polaris;

public class ExtraStatAdditive : PassiveNodeStat
{
    private readonly bool flatAmount;
    private readonly float statBase;

    public string StatName { get; }
    public float Amount { get; }

    public ExtraStatAdditive(string stat, float amount, bool flatAmount = false, float statBase = 1f)
    {
        StatName = stat;
        Amount = amount;
        this.flatAmount = flatAmount;
        this.statBase = statBase;
    }

    public override void ContributeStats(PassiveContext passiveContext)
    {
        passiveContext.SkillBehavior.AddToExtraStat(StatName, Amount, statBase);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        float amount = Amount;

        if (!flatAmount)
        {
            amount = MathF.Round(amount * 100f, 2);
        }

        if (amount > 0f)
        {
            builder.AppendLine(Lang.Get($"polaris:extrastatinc{StatName}", Math.Abs(amount)));
            return;
        }

        builder.AppendLine(Lang.Get($"polaris:extrastatdec{StatName}", Math.Abs(amount)));
    }
}

/// <summary>
/// Contributes <c>amount * constellationLevel</c> to an additive extra stat each time stats are recalculated.
/// Used for nodes whose bonus scales with the player's level in a given constellation.
/// </summary>
public class ExtraStatAdditivePerLevel : PassiveNodeStat
{
    private readonly string stat;
    private readonly float amountPerLevel;
    private readonly string constellationName;
    private readonly float statBase;

    public ExtraStatAdditivePerLevel(string stat, float amountPerLevel, string constellationName, float statBase = 0f)
    {
        this.stat = stat;
        this.amountPerLevel = amountPerLevel;
        this.constellationName = constellationName;
        this.statBase = statBase;
    }

    public override void ContributeStats(PassiveContext passiveContext)
    {
        int level = passiveContext.PolarisData.GetConstellation(constellationName).Level;
        passiveContext.SkillBehavior.AddToExtraStat(stat, amountPerLevel * level, statBase);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        float displayAmount = MathF.Round(amountPerLevel * 100f, 3);
        builder.AppendLine(Lang.Get($"polaris:extrastatinc{stat}perlevel", displayAmount, constellationName));
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
            builder.AppendLine(Lang.Get($"polaris:extrastatmul{stat}", Math.Abs(multi)));
            return;
        }

        builder.AppendLine(Lang.Get($"polaris:extrastatdiv{stat}", Math.Abs(multi)));
    }
}
