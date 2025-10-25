using System.Text;

namespace Polaris;

public abstract class PassiveNodeStat
{
    public virtual EnumCalculationPriority Priority => EnumCalculationPriority.Increases;
    public abstract void ContributeStats(PassiveContext passiveContext);
    public virtual void BuildDescription(StringBuilder builder, PlayerPolarisData data, EntityPlayer player, AllocatedNodesInfo info)
    {

    }
}