using System.Text;

namespace Polaris;

public class FreeNode : PassiveNode
{
    public override int Cost => 0;

    public override Vector4 Color => new(0.5f, 0.5f, 0.5f, 1f);

    public FreeNode(string name, string code, NodePosition position, Constellation constellation) : base(name, code, position, constellation)
    {
    }

    public override void ContributeStats(PassiveContext context)
    {

    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data)
    {
        builder.AppendLine("Free");
    }
}