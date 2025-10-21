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
}