using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Polaris;

/// <summary>
/// Entity behavior that awards constellation experience to the killing player on death.
/// Add to entities via JSON patch with "xp" and optionally "constellation" attributes.
/// Supports byType resolution for type-variant entities.
/// </summary>
[EntityBehavior]
public class EntityBehaviorPolarisExp : EntityBehavior
{
    private float xp;
    public float HarvestXp { get; private set; }

    public const float COMBAT_EXP_MULTI = 5f;
    public const float HARVESTING_EXP_MULTI = 10f;

    public const float DefaultHarvestExp = 1f;

    public EntityBehaviorPolarisExp(Entity entity) : base(entity)
    {
    }

    public override string PropertyName() => "PolarisEntityExp";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        xp = attributes["xp"].AsFloat(0f);
        HarvestXp = attributes["harvestXp"].AsFloat(0f);
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
            SystemPolaris.AddExperience("Hunting", killer, xp * COMBAT_EXP_MULTI);
        }
        else
        {
            SystemPolaris.AddExperience("Combat", killer, xp * COMBAT_EXP_MULTI);
        }
    }
}