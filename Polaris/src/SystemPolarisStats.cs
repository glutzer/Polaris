using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace Polaris;

// Stats use a weighted sum unless specified otherwise. Weighted sum is just a normal sum with a multiplier?
// There is effectively no difference between a flat sum and a weighted sum.
// All stats start at one.
// Most things are +- a float value.
// Things like the loot chance are subtracted by 1 when calculated to get a % chance.

// healingeffectivness
// maxhealthExtraPoints - 2240 = 22.40 extra max health.
// walkspeed
// hungerrate
// rangedWeaponsAcc
// rangedWeaponsSpeed
// rangedWeaponsDamage
// meleeWeaponsDamage
// mechanicalsDamage
// animalLootDropRate
// forageDropRate
// wildCropDropRate
// vesselContentsDropRate
// oreDropRate
// rustyGearDropRate
// miningSpeedMul
// animalSeekingRange
// armorDurabilityLoss
// armorWalkSpeedAffectedness - Blackguard has -0.25, which means the affectedness will be 75%.
// bowDrawingStrength
// wholeVesselLootChance - Flat sum?
// temporalGearTLRepairCost - Flat sum?
// animalHarvestingTime
// gliderLiftMax
// gliderSpeedMax
// jumpHeightMul

[GameSystem]
public class SystemPolarisStats : NetworkedGameSystem
{
    private readonly List<PassiveAggregator> aggregators = [];

    public SystemPolarisStats(bool isServer, ICoreAPI api) : base(isServer, api, "polarisstats")
    {
    }

    protected override void RegisterMessages(INetworkChannel channel)
    {

    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {

    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {

    }

    public override void Initialize()
    {
        // Register one for each vanilla stat.
        RegisterAggregator(new StatAggregator("healingeffectivness"));
        RegisterAggregator(new StatAggregator("maxhealthExtraPoints"));
        RegisterAggregator(new StatAggregator("walkspeed"));
        RegisterAggregator(new StatAggregator("hungerrate"));
        RegisterAggregator(new StatAggregator("rangedWeaponsAcc"));
        RegisterAggregator(new StatAggregator("rangedWeaponsSpeed"));
        RegisterAggregator(new StatAggregator("rangedWeaponsDamage"));
        RegisterAggregator(new StatAggregator("meleeWeaponsDamage"));
        RegisterAggregator(new StatAggregator("mechanicalsDamage"));
        RegisterAggregator(new StatAggregator("animalLootDropRate"));
        RegisterAggregator(new StatAggregator("forageDropRate"));
        RegisterAggregator(new StatAggregator("wildCropDropRate"));
        RegisterAggregator(new StatAggregator("vesselContentsDropRate"));
        RegisterAggregator(new StatAggregator("oreDropRate"));
        RegisterAggregator(new StatAggregator("rustyGearDropRate"));
        RegisterAggregator(new StatAggregator("miningSpeedMul"));
        RegisterAggregator(new StatAggregator("animalSeekingRange"));
        RegisterAggregator(new StatAggregator("armorDurabilityLoss"));
        RegisterAggregator(new StatAggregator("armorWalkSpeedAffectedness"));
        RegisterAggregator(new StatAggregator("bowDrawingStrength"));
        RegisterAggregator(new StatAggregator("wholeVesselLootChance"));
        RegisterAggregator(new StatAggregator("temporalGearTLRepairCost"));
        RegisterAggregator(new StatAggregator("animalHarvestingTime"));
        RegisterAggregator(new StatAggregator("gliderLiftMax"));
        RegisterAggregator(new StatAggregator("gliderSpeedMax"));
        RegisterAggregator(new StatAggregator("jumpHeightMul"));

        if (api is ICoreServerAPI sapi)
        {
            sapi.Event.PlayerJoin += p =>
            {
                // Players passive data should already be loaded here.
                CalculatePlayerStats(p.Entity);
            };

            sapi.Event.PlayerLeave += p =>
            {
                CalculatePlayerStats(p.Entity, true);
            };

            // Load passive data from world...
            MainAPI.GetServerSystem<SystemPolarisPassiveTree>().LoadDataFromWorld();
        }
    }

    public void RegisterAggregator(PassiveAggregator aggregator)
    {
        aggregators.Add(aggregator);
    }

    /// <summary>
    /// Recalculate a player's stats based on their passives.
    /// Sends a packet on the server to let client recalculate them.
    /// </summary>
    public void CalculatePlayerStats(EntityPlayer player, bool onlyRemove = false)
    {
        PassiveContext context = new(player);

        foreach (PassiveAggregator aggregator in aggregators)
        {
            aggregator.RemoveStats(context);
        }

        if (onlyRemove) return;

        // Gather stats from player's passive information...
        SystemPolarisPassiveTree.Instance(api).GatherPassiveInformation(context);

        foreach (PassiveAggregator aggregator in aggregators)
        {
            aggregator.AddStats(context);
        }
    }

    public override void OnClose()
    {
        if (api is not ICoreServerAPI sapi) return;

        foreach (IPlayer? player in sapi.World.AllPlayers)
        {
            CalculatePlayerStats(player.Entity, true);
        }

        // Save and unload passive data...
        MainAPI.GetServerSystem<SystemPolarisPassiveTree>().SaveDataToWorld();
    }
}