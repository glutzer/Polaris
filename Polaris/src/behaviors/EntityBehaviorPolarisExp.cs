using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Polaris;

/// <summary>
/// Entity behavior that awards constellation experience to the killing player on death.
/// Add to entities via JSON patch with an "xpReward" balance key or numeric "xp" override.
/// Supports byType resolution for type-variant entities.
/// </summary>
[EntityBehavior]
public class EntityBehaviorPolarisExp : EntityBehavior
{
    private float xp;
    public float HarvestXp { get; private set; }

    public EntityBehaviorPolarisExp(Entity entity) : base(entity)
    {
    }

    public override string PropertyName() => "PolarisEntityExp";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        xp = attributes["xp"].AsFloat(ExpGlobals.GetEntityExperience(attributes["xpReward"].AsString("")));
        HarvestXp = attributes["harvestXp"].AsFloat(attributes["harvestReward"].AsString("") == "drifter" ? ExpGlobals.DrifterHarvestExperience : 0f);
    }

    public override void OnEntityDeath(DamageSource damageSourceForDeath)
    {
        if (damageSourceForDeath == null || xp <= 0f) return;
        if (entity.Api.Side != EnumAppSide.Server) return;

        IPlayer? killer = null;
        if (damageSourceForDeath.SourceEntity is EntityPlayer srcPlayer)
            killer = srcPlayer.Player;
        else if (damageSourceForDeath.CauseEntity is EntityPlayer causePlayer)
            killer = causePlayer.Player;
        else if (damageSourceForDeath.SourceEntity is EntityProjectile projectile)
            killer = (projectile.FiredBy as EntityPlayer)?.Player ?? projectile.FiredBy as IPlayer;

        if (killer == null) return;

        if (damageSourceForDeath.SourceEntity is EntityProjectile)
        {
            SystemPolaris.AddExperience("Hunting", killer, xp * ExpGlobals.HuntingKillExperienceMultiplier);
        }
        else
        {
            SystemPolaris.AddExperience("Combat", killer, xp * ExpGlobals.CombatKillExperienceMultiplier);
        }
    }
}