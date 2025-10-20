
namespace Polaris;

public class FreeNode : PassiveNode
{
    public override int Cost => 0;

    public override Vector4 Color => new(0.5f, 0.5f, 0.5f, 1f);

    public FreeNode(string name, NodePosition position) : base(name, position)
    {
    }

    public override void ContributeStats(PassiveContext context)
    {

    }
}