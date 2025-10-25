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
}