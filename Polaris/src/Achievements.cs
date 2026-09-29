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
    /// <summary>Every goal code must be recorded before this achievement unlocks.</summary>
    public string[] Goals { get; init; } = [];
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
            },
            ["psychedelicMushroom"] = new("psychedelicMushroom", HiddenUntilUnlocked: true)
            {
                ExperienceConstellation = "Mycology",
                ExperienceReward = 1000f
            },
            ["fungalFeast"] = new("fungalFeast")
            {
                // All 45 ground mushrooms and polypores listed on the Mushroom wiki page.
                Goals = [
                    "orangeoakbolete", "kingbolete", "bitterbolete", "devilbolete", "flyagaric",
                    "greencrackedrussula", "violetwebcap", "almondmushroom", "redwinecap", "paddystraw",
                    "deathcap", "puffball", "earthball", "chanterelle", "blacktrumpet", "fieldmushroom",
                    "golddropmilkcap", "indigomilkcap", "saffronmilkcap", "commonmorel", "witchhat",
                    "lobster", "jackolantern", "devilstooth", "elfinsaddle", "bluemeanie", "foolsconecap",
                    "goldcap", "honeymushroom", "laughingjim", "libertycap", "sickener", "wavycap",
                    "shiitake", "pinkbonnet", "livermushroom", "funeralbell", "deerear", "reishi",
                    "whiteoyster", "beardedtooth", "chickenofthewoods", "tinderhoof", "dryadsaddle", "pinkoyster"
                ],
                KnowledgePointReward = 1,
                ExperienceConstellation = "Mycology",
                ExperienceReward = 1000f
            },
            ["metalworker"] = new("metalworker")
            {
                Goals = ["copper", "tinbronze", "bismuthbronze", "blackbronze", "iron"],
                KnowledgePointReward = 1,
                ExperienceConstellation = "Smithing",
                ExperienceReward = 500f
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

    public static bool TryUnlock(string code, ISet<string> unlocked, ISet<string>? completedGoals = null)
    {
        if (!ByCode.TryGetValue(code, out Achievement? achievement)) return false;
        foreach (string goal in achievement.Goals)
            if (completedGoals == null || !completedGoals.Contains(goal)) return false;
        foreach (string requirement in achievement.Requirements)
            if (!unlocked.Contains(requirement)) return false;
        return unlocked.Add(code);
    }
}
