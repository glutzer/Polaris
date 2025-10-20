using System.Collections.Generic;

namespace Polaris;

public class PassiveContext
{
    public Dictionary<string, int> StatValues { get; } = [];
    public EntityPlayer Player { get; }

    public void MultiplyStat(string name, float multiplier)
    {
        if (StatValues.TryGetValue(name, out int val))
        {
            StatValues[name] = (int)(val * multiplier);
        }
    }

    public void AddToStat(string name, int value)
    {
        if (StatValues.ContainsKey(name))
        {
            StatValues[name] += value;
        }
        else
        {
            StatValues[name] = value;
        }
    }

    public PassiveContext(EntityPlayer player)
    {
        Player = player;
    }
}