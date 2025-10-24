using System.Collections.Generic;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Polaris;

public static class PolarisExtensions
{
    public static int GetSkillLevel(this Entity entity, string skillCode)
    {
        return entity.GetBehavior<PlayerBehaviorPolaris>()?.GetSkillLevel(skillCode) ?? 0;
    }
}

/// <summary>
/// Much simpler behavior than whatever I was doing with effects. All you really need is to track the level of a skill.
/// </summary>
[EntityBehavior]
public class PlayerBehaviorPolaris : EntityBehavior
{
    private Dictionary<string, int> skillLevels = [];

    public PlayerBehaviorPolaris(Entity entity) : base(entity)
    {
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
        byte[] bytes = SerializerUtil.Serialize(skillLevels);
        entity.WatchedAttributes.SetBytes("polStats", bytes);
    }

    protected virtual void LoadData()
    {
        // Load bytes.
        byte[] bytes = entity.WatchedAttributes.GetBytes("polStats");
        if (bytes == null) return;

        Dictionary<string, int> skillLevelsDes = SerializerUtil.Deserialize<Dictionary<string, int>>(bytes);
        if (skillLevelsDes == null) return;

        skillLevels = skillLevelsDes;
    }
}