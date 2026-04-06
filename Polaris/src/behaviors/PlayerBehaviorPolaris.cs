using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;

namespace Polaris;

public static class PolarisExtensions
{
    public static PlayerBehaviorPolaris GetPolarisStats(this Entity entity)
    {
        return entity.GetBehavior<PlayerBehaviorPolaris>()!;
    }

    public static int GetSkillLevel(this Entity entity, string skillCode)
    {
        return entity.GetBehavior<PlayerBehaviorPolaris>()?.GetSkillLevel(skillCode) ?? 0;
    }

    public static bool TryGetExtraStat(this Entity entity, string statCode, out float value)
    {
        value = 0f;
        return entity.GetBehavior<PlayerBehaviorPolaris>()?.TryGetExtraStat(statCode, out value) ?? false;
    }

    /// <summary>
    /// Gets the health behavior of an entity.
    /// </summary>
    public static EntityBehaviorHealth GetHealth(this Entity entity)
    {
        return entity.GetBehavior<EntityBehaviorHealth>()!;
    }

    /// <summary>
    /// Gets the health behavior of an entity.
    /// </summary>
    public static EntityBehaviorHunger GetHunger(this Entity entity)
    {
        return entity.GetBehavior<EntityBehaviorHunger>()!;
    }
}

/// <summary>
/// Much simpler behavior than whatever I was doing with effects. All you really need is to track the level of a skill, or extra stats to do things in the code.
/// Player stats work good for things that are from multiple different sources. Extra stats are good for my mod's passives.
/// An OnCalculation event can be used to add stuff to extra stats through other methods, in Polaris.cs.
/// </summary>
[EntityBehavior]
public class PlayerBehaviorPolaris : EntityBehavior
{
    private Dictionary<string, int> skillLevels = [];
    private Dictionary<string, float> extraStats = [];

    private double previousStability = double.NaN;
    private long stabilityLastChecked = 0;

    public PlayerBehaviorPolaris(Entity entity) : base(entity)
    {
    }

    /// <summary>
    /// Do temporal stability stuff here since it's really messy in the stability behavior.
    /// </summary>
    public override void OnGameTick(float dt)
    {
        if (entity.Api.Side != EnumAppSide.Server) return; // Only do stability effects server-side.

        EntityBehaviorTemporalStabilityAffected? behavior = entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
        if (behavior == null) return;

        if (entity is not EntityPlayer ePlayer) return;

        // Only do this once every second.
        float delta = (entity.World.ElapsedMilliseconds - stabilityLastChecked) / 1000f;
        if (delta < 1f)
        {
            return;
        }

        SystemTemporalStability systemTemporalStability = MainAPI.Sapi.ModLoader.GetModSystem<SystemTemporalStability>();
        IPlayer player = ePlayer.Player;

        if (double.IsNaN(previousStability))
        {
            previousStability = behavior.OwnStability;
            stabilityLastChecked = entity.World.ElapsedMilliseconds;
        }

        double loss = previousStability - behavior.OwnStability;

        previousStability = behavior.OwnStability;
        stabilityLastChecked = entity.World.ElapsedMilliseconds;

        if (loss > 0.0)
        {
            float lossMultiplier = 1f;

            // Experience is gained from losing stability.
            if (loss > 0.001)
            {
                SystemPolaris.AddExperience("Time", player, (float)(loss * 10.0));
            }

            if (ePlayer.TryGetExtraStat("stabilityLossMul", out float stabilityLossMul))
            {
                lossMultiplier *= stabilityLossMul;
            }

            if (!systemTemporalStability.StormData.nowStormActive && ePlayer.GetSkillLevel("stableSettler") > 0)
            {
                int yPos = (int)ePlayer.Pos.Y;
                int seaLevel = TerraGenConfig.seaLevel;

                // At 20 below sea level have 1x loss multiplier, at sea level and above have 0x loss.
                float depthMul = 1f - GameMath.Clamp((yPos - (seaLevel - 20)) / 20f, 0f, 1f);
                lossMultiplier *= depthMul;
            }

            // Add back stability that shouldn't have been lost.
            double newLoss = loss * lossMultiplier;
            behavior.OwnStability -= newLoss - loss;
        }
    }

    public float AddToExtraStat(string statCode, float amount, float statBase = 1f)
    {
        if (!extraStats.ContainsKey(statCode))
        {
            extraStats[statCode] = statBase;
        }
        extraStats[statCode] += amount;
        return extraStats[statCode];
    }

    /// <summary>
    /// Multiply a stat.
    /// The stat base is what the stat will initialize to if non-existent.
    /// </summary>
    public float MultiplyExtraStat(string statCode, float multi, float statBase = 1f)
    {
        if (!extraStats.ContainsKey(statCode))
        {
            extraStats[statCode] = statBase;
        }
        extraStats[statCode] *= multi;
        return extraStats[statCode];
    }

    /// <summary>
    /// Extra stat, like drops.
    /// </summary>
    public bool TryGetExtraStat(string statCode, out float value)
    {
        return extraStats.TryGetValue(statCode, out value);
    }

    public int AddToSkillLevel(string skillCode, int levels = 1)
    {
        if (!skillLevels.ContainsKey(skillCode))
        {
            skillLevels[skillCode] = 0;
        }
        skillLevels[skillCode] += levels;
        return skillLevels[skillCode];
    }

    public void SyncToPlayer()
    {
        SaveData();
    }

    public void ResetForPassiveChange()
    {
        skillLevels.Clear();
        extraStats.Clear();
        SaveData();
    }

    public int GetSkillLevel(string skillCode)
    {
        return skillLevels.TryGetValue(skillCode, out int level) ? level : 0;
    }

    public override string PropertyName()
    {
        return "polStats";
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        if (entity.Api.Side == EnumAppSide.Client)
        {
            entity.WatchedAttributes.RegisterModifiedListener("polStats", LoadData);
        }

        LoadData();
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        if (entity.Api.Side == EnumAppSide.Client)
        {
            entity.WatchedAttributes.UnregisterListener(LoadData);
        }
        else
        {
            SaveData();
        }
    }

    protected virtual void SaveData()
    {
        byte[] bytes = SerializerUtil.Serialize((skillLevels, extraStats));
        entity.WatchedAttributes.SetBytes("polStats", bytes);
    }

    protected virtual void LoadData()
    {
        // Load bytes.
        byte[] bytes = entity.WatchedAttributes.GetBytes("polStats");
        if (bytes == null) return;

        (Dictionary<string, int>, Dictionary<string, float>) skillLevelsDes = SerializerUtil.Deserialize<(Dictionary<string, int>, Dictionary<string, float>)>(bytes);

        skillLevels = skillLevelsDes.Item1 ?? [];
        extraStats = skillLevelsDes.Item2 ?? [];
    }
}