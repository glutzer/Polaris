using System.Text;

namespace Polaris;

public class LevelRequirement : PassiveNodeRequirement
{
    private readonly string constellation;
    private readonly int requirement;

    public LevelRequirement(string constellation, int level)
    {
        this.constellation = constellation;
        requirement = level;
    }

    public override bool CanAllocate(EntityPlayer player, PlayerPolarisData data, AllocatedNodesInfo info)
    {
        int level = data.GetConstellation(constellation).Level;
        return level >= requirement;
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        bool canAllocate = CanAllocate(player, data, info);
        string color = canAllocate ? "#00FF88" : "#FF4444";
        builder.AppendLine($"<font color=\"{color}\">Requires {constellation} Level {requirement}</font>");
    }
}

public class PlayerLevelRequirement : PassiveNodeRequirement
{
    private readonly int requirement;

    public PlayerLevelRequirement(int level)
    {
        requirement = level;
    }

    public override bool CanAllocate(EntityPlayer player, PlayerPolarisData data, AllocatedNodesInfo info)
    {
        return data.Level >= requirement;
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        bool canAllocate = CanAllocate(player, data, info);
        string color = canAllocate ? "#00FF88" : "#FF4444";
        builder.AppendLine($"<font color=\"{color}\">Requires Knowledge Level {requirement}</font>");
    }
}