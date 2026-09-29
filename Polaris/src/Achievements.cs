using System.Collections.Generic;
using Vintagestory.API.Config;

namespace Polaris;

public sealed record Achievement(string Code, bool HiddenUntilUnlocked = false, params string[] Requirements)
{
    public string Name => Lang.Get($"polaris:achievement-{Code.ToLowerInvariant()}-name");
    public string Description => Lang.Get($"polaris:achievement-{Code.ToLowerInvariant()}-description");
    public int KnowledgePointReward { get; init; }
    public string? ExperienceConstellation { get; init; }
    /// <summary>Base EXP, subject to the player's normal EXP multipliers.</summary>
    public float ExperienceReward { get; init; }
}

public static class Achievements
{
    public static readonly IReadOnlyDictionary<string, Achievement> ByCode =
        new Dictionary<string, Achievement>
        {
            ["firstBlood"] = new("firstBlood")
            {
                KnowledgePointReward = 1,
                ExperienceConstellation = "Combat",
                ExperienceReward = 250f
            }
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
