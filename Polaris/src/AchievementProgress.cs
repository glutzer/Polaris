using ProtoBuf;
using System.Collections.Generic;

namespace Polaris;

[ProtoContract]
public class AchievementProgress
{
    [ProtoMember(1)]
    public HashSet<string> CompletedGoals = [];
}
