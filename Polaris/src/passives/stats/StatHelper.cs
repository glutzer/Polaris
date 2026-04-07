namespace Polaris;

public static class StatHelper
{
    public static PassiveNode AddAdditiveStat(this PassiveNode node, string stat, float addition, bool flatDisplay = false)
    {
        node.AddStat(new AdditiveStat(stat, addition, flatDisplay));
        return node;
    }

    public static PassiveNode AddMultiplicativeStat(this PassiveNode node, string stat, float multi)
    {
        node.AddStat(new MultiplicativeStat(stat, multi));
        return node;
    }

    public static PassiveNode AddSkillStat(this PassiveNode node, string skill, int levels, string description)
    {
        node.AddStat(new SkillStat(skill, levels, description));
        return node;
    }

    public static PassiveNode AddAdditiveExtraStat(this PassiveNode node, string stat, float amount, bool flatAmount = false, float statBase = 1f)
    {
        node.AddStat(new ExtraStatAdditive(stat, amount, flatAmount, statBase));
        return node;
    }

    public static PassiveNode AddAdditiveExtraStatPerLevel(this PassiveNode node, string stat, float amountPerLevel, string constellationName, float statBase = 0f)
    {
        node.AddStat(new ExtraStatAdditivePerLevel(stat, amountPerLevel, constellationName, statBase));
        return node;
    }

    public static PassiveNode AddMultiplicativeExtraStat(this PassiveNode node, string stat, float multi, bool flatAmount = false, float statBase = 1f)
    {
        node.AddStat(new ExtraStatMultiplicative(stat, multi, flatAmount, statBase));
        return node;
    }
}