namespace Polaris;

public static class RequirementHelper
{
    public static PassiveNode AddLevelRequirement(this PassiveNode node, string constellation, int level)
    {
        node.AddRequirement(new LevelRequirement(constellation, level));
        return node;
    }

    public static PassiveNode AddPlayerLevelRequirement(this PassiveNode node, int level)
    {
        node.AddRequirement(new PlayerLevelRequirement(level));
        return node;
    }

    public static PassiveNode AddExclusiveRequirement(this PassiveNode node, string constellation, string code)
    {
        node.AddRequirement(new ExclusiveRequirement(constellation, code));
        return node;
    }

    public static PassiveNode AddTagExclusiveRequirement(this PassiveNode node, string tag, int amount)
    {
        node.AddRequirement(new TagExclusiveRequirement(tag, amount));
        return node;
    }
}