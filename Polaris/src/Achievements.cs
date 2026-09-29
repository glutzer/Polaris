using System.Collections.Generic;

namespace Polaris;

public sealed record Achievement(string Code, string Name, string Description,
    bool HiddenUntilUnlocked = false, params string[] Requirements);

public static class Achievements
{
    public static readonly IReadOnlyDictionary<string, Achievement> ByCode =
        new Dictionary<string, Achievement>
        {
            ["firstBlood"] = new("firstBlood", "First Blood", "Kill a wolf.")
        };

    public static bool IsVisible(string code, ISet<string> unlocked)
    {
        return Visit(code, []);

        bool Visit(string current, HashSet<string> visiting)
        {
            if (!ByCode.TryGetValue(current, out Achievement? achievement) || !visiting.Add(current)) return false;
            if (achievement.HiddenUntilUnlocked && !unlocked.Contains(current)) return false;
            foreach (string requirement in achievement.Requirements)
                if (!Visit(requirement, visiting)) return false;
            visiting.Remove(current);
            return true;
        }
    }

    public static bool TryUnlock(string code, ISet<string> unlocked)
    {
        if (!ByCode.TryGetValue(code, out Achievement? achievement)) return false;
        foreach (string requirement in achievement.Requirements)
            if (!unlocked.Contains(requirement)) return false;
        return unlocked.Add(code);
    }
}
