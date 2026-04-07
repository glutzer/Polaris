using System.Collections.Generic;

namespace Polaris;

public class PassiveContext
{
    public Dictionary<string, int> IntValues { get; } = [];
    public Dictionary<string, float> FloatValues { get; } = [];
    public EntityPlayer Player { get; }
    public PlayerBehaviorPolaris SkillBehavior { get; }
    public PlayerPolarisData PolarisData { get; }

    public void MultiplyFloatStat(string name, float multiplier)
    {
        if (FloatValues.TryGetValue(name, out float val))
        {
            FloatValues[name] = val * multiplier;
        }
    }

    public void AddToFloatStat(string name, float value)
    {
        if (FloatValues.ContainsKey(name))
        {
            FloatValues[name] += value;
        }
        else
        {
            FloatValues[name] = value;
        }
    }

    public void MultiplyIntStat(string name, float multiplier)
    {
        if (IntValues.TryGetValue(name, out int val))
        {
            IntValues[name] = (int)(val * multiplier);
        }
    }

    public void AddToIntStat(string name, int value)
    {
        if (IntValues.ContainsKey(name))
        {
            IntValues[name] += value;
        }
        else
        {
            IntValues[name] = value;
        }
    }

    public float GetFloat(string name)
    {
        return FloatValues.TryGetValue(name, out float val) ? val : 0f;
    }

    public float GetInt(string name)
    {
        return IntValues.TryGetValue(name, out int val) ? val : 0;
    }

    public PassiveContext(EntityPlayer player, PlayerPolarisData polarisData)
    {
        Player = player;
        SkillBehavior = player.GetBehavior<PlayerBehaviorPolaris>()!; // This better be here.
        PolarisData = polarisData;
    }
}