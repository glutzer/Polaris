using System.Text;

namespace Polaris;

public class KeystoneNode : PassiveNode
{
    private readonly string keystoneStat;
    private readonly string description;

    public KeystoneNode(string name, string code, NodePosition position, Constellation constellation, string description, string keystoneStat) : base(name, code, position, constellation)
    {
        this.description = description;
        this.keystoneStat = keystoneStat;
    }

    public override int NodeSize => 20;

    public override void ContributeStats(PassiveContext context)
    {
        context.SkillBehavior.AddToSkillLevel(keystoneStat);
    }

    public override void BuildDescription(StringBuilder builder, PlayerPolarisData data)
    {
        builder.AppendLine(description);
    }
}