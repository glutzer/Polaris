using System.Text;

namespace Polaris;

public class SkillStat : PassiveNodeStat
{
    private readonly string skillName;
    private readonly int levels;
    private readonly string description;

    public SkillStat(string skillName, int levels, string description)
    {
        this.skillName = skillName;
        this.levels = levels;
        this.description = description;
    }

    public override void ContributeStats(PassiveContext passiveContext)
    {
        passiveContext.SkillBehavior.AddToSkillLevel(skillName, levels);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        builder.AppendLine(description);
    }
}