using System.Text;
using Vintagestory.API.Config;

namespace Polaris;

public class TagExclusiveRequirement : PassiveNodeRequirement
{
    private readonly string tag;
    private readonly int tagAmount;

    public TagExclusiveRequirement(string tag, int tagAmount)
    {
        this.tag = tag;
        this.tagAmount = tagAmount;
    }

    public override bool CanAllocate(EntityPlayer player, PlayerPolarisData data, AllocatedNodesInfo info)
    {
        int tagCount = info.GetTagCount(tag);
        return tagCount < tagAmount;
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {
        bool canAllocate = CanAllocate(player, data, info);
        string color = canAllocate ? "#00FF88" : "#FF4444";

        int tagCount = info.GetTagCount(tag);

        string langTag = Lang.Get($"polaris:nodetag-{tag}");

        if (tagAmount > 1)
        {
            builder.AppendLine($"<font color=\"{color}\">Only {tagAmount} {langTag} passives may be allocated ({tagCount}/{tagAmount})</font>");
        }
        else
        {
            builder.AppendLine($"<font color=\"{color}\">Only 1 {langTag} passive may be allocated</font>");
        }
    }
}