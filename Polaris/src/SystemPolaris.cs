using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Common;

namespace Polaris;

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

/// <summary>
/// Loads passive tree from mods, tells client to allocate nodes for him on success.
/// </summary>
[GameSystem]
public class SystemPolaris : NetworkedGameSystem
{
    private readonly List<Constellation> constellations = [];
    public IEnumerable<Constellation> AllConstellations => constellations;
    private readonly Dictionary<string, Constellation> constellationByName = [];

    private readonly List<PassiveAggregator> aggregators = [];

    private readonly Dictionary<string, PlayerPolarisData> playerDataByUid = [];

    public PolarisConfig Config { get; private set; } = new();

    public event Action<PlayerPolarisData>? OnClientDataUpdated;
    public event Action<Constellation, float, int, bool>? OnClientExperienceGain;
    public event Action<Constellation, int>? OnClientSkillLevelUp;

    private static SystemPolaris clientInst = null!;
    private static SystemPolaris serverInst = null!;

    /// <summary>
    /// Invoked for each priority. So effects can register stats the same way passives do.
    /// Only called on the server.
    /// </summary>
    public event Action<PassiveContext, EnumCalculationPriority>? OnGatherPassiveStats;

    public SystemPolaris(bool isServer, ICoreAPI api) : base(isServer, api, "polaristree")
    {
    }

    public static SystemPolaris Instance(ICoreAPI api)
    {
        return api.Side == EnumAppSide.Client ? clientInst : serverInst;
    }

    public override void PreInitialize()
    {
        if (api.Side == EnumAppSide.Client) clientInst = this;
        else serverInst = this;

        try
        {
            Config = api.LoadModConfig<PolarisConfig>("polarisconfig.json") ?? new PolarisConfig();
        }
        catch
        {
            Config = new PolarisConfig();
        }
        api.StoreModConfig(Config, "polarisconfig.json");
        PlayerPolarisData.ExpCurve = Config.MainExpCurve;

        if (api is ICoreServerAPI sapi)
        {
            // Send the client his data.
            sapi.Event.PlayerJoin += p =>
            {
                SendPacket(GetPlayerData(p.PlayerUID), p);
            };
        }
    }

    private void CalculatePlayerStats(EntityPlayer player, bool onlyRemove = false)
    {
        if (api.Side != EnumAppSide.Server) throw new Exception("Recalculating on the client.");

        // Gather stats from player's passive information...
        PlayerPolarisData data = GetPlayerData(player.PlayerUID);

        PassiveContext context = new(player, data);

        context.SkillBehavior.ResetForPassiveChange();

        foreach (PassiveAggregator aggregator in aggregators)
        {
            aggregator.RemoveStats(context);
        }

        if (onlyRemove) return;

        for (int i = 0; i < 3; i++)
        {
            EnumCalculationPriority priority = (EnumCalculationPriority)i;
            foreach (Constellation constellation in constellations)
            {
                PlayerConstellationData constData = data.GetConstellation(constellation.Name);

                foreach (int nodeId in constData.AllocatedNodeIds)
                {
                    PassiveNode? node = constellation.GetNodeById(nodeId);
                    node?.ContributeStats(context, priority);
                }
            }
            OnGatherPassiveStats?.Invoke(context, priority);
        }

        foreach (PassiveAggregator aggregator in aggregators)
        {
            aggregator.AddStats(context);
        }

        context.SkillBehavior.SyncToPlayer();

        DropBackpacksFromLockedSlots(player);

        // TODO: move these somewhere more modular.
        player.GetHealth().MarkDirty();
    }

    private static void DropBackpacksFromLockedSlots(EntityPlayer player)
    {
        IPlayer? iPlayer = player.World.PlayerByUid(player.PlayerUID);
        if (iPlayer == null) return;

        if (iPlayer.InventoryManager.GetOwnInventory("backpack") is not InventoryPlayerBackpacks inv) return;

        player.TryGetExtraStat("strongBack", out float strongBack);
        int allowedSlots = StrongBackInventoryPatches.VanillaBagSlots + (int)strongBack;

        for (int i = allowedSlots; i < StrongBackInventoryPatches.TotalBagSlots; i++)
        {
            ItemSlot slot = inv[i];
            if (slot.Empty) continue;

            ItemStack stack = slot.TakeOutWhole();
            player.World.SpawnItemEntity(stack, player.Pos.XYZ);
        }
    }

    public override void Initialize()
    {
        // Main survival tree.
        Constellation survival = new Constellation("Survival").SetColor(1f, 0.7f, 0.7f, 1f).SetExpCurve(Config.SurvivalExpCurve, 100f).AddStartNode();
        AddConstellation(survival);

        // Temporal tree.
        Constellation time = new Constellation("Time").SetColor(0f, 1f, 0.6f, 0.5f).SetExpCurve(Config.TimeExpCurve, 100f).AddStartNode();
        AddConstellation(time);

        // Mining and digging combined.
        Constellation excavation = new Constellation("Excavation").SetColor(0.6f, 0.4f, 0.4f, 1f).SetExpCurve(Config.ExcavationExpCurve, 100f).AddStartNode();
        AddConstellation(excavation);

        // Tree stuff.
        Constellation forestry = new Constellation("Forestry").SetColor(0f, 0.6f, 0f, 1f).SetExpCurve(Config.ForestryExpCurve, 100f).AddStartNode();
        AddConstellation(forestry);

        // Farming.
        Constellation horticulture = new Constellation("Horticulture").SetColor(0.2f, 1f, 0.2f, 1f).SetExpCurve(Config.HorticultureExpCurve, 100f).AddStartNode();
        AddConstellation(horticulture);

        // Hunting — ranged combat and animal loot.
        Constellation hunting = new Constellation("Hunting").SetColor(0.6f, 0.2f, 0.2f, 0.75f).SetExpCurve(Config.HuntingExpCurve, 100f).AddStartNode();
        AddConstellation(hunting);

        // Combat — melee combat.
        Constellation combat = new Constellation("Combat").SetColor(0.8f, 0.15f, 0.15f, 1f).SetExpCurve(Config.CombatExpCurve, 100f).AddStartNode();
        AddConstellation(combat);

        // Smithing.
        Constellation smithing = new Constellation("Smithing").SetColor(0.7f, 0.4f, 0.2f, 1f).SetExpCurve(Config.SmithingExpCurve, 100f).AddStartNode();
        AddConstellation(smithing);

        // Clay/knapping.
        Constellation forming = new Constellation("Forming").SetColor(0.1f, 0.1f, 0.3f, 1f).SetExpCurve(Config.FormingExpCurve, 100f).AddStartNode();
        AddConstellation(forming);

        // Cooking.
        Constellation cooking = new Constellation("Cooking").SetColor(0.7f, 0.7f, 0f, 1f).SetExpCurve(Config.CookingExpCurve, 100f).AddStartNode();
        AddConstellation(cooking);

        // Crafting - leatherworking and sewing.
        Constellation crafting = new Constellation("Crafting").SetColor(0.7f, 0.3f, 0.5f, 1f).SetExpCurve(Config.CraftingExpCurve, 100f).AddStartNode();
        AddConstellation(crafting);

        // Trade.
        Constellation trade = new Constellation("Trade").SetColor(1f, 0f, 1f, 1f).SetExpCurve(Config.TradeExpCurve, 100f).AddStartNode();
        AddConstellation(trade);

        PassiveNode.Create("Mint", "mint", trade).AddParent("start").AddSkillStat("mint", 1, """
            Craft a Rusty Gear from Metal Parts
            """).KeystoneStyle().AddLevelRequirement("Trade", 100);

        // Mycology — mushroom harvesting and bonuses.
        Constellation mycology = new Constellation("Mycology").SetColor(0.6f, 0.3f, 0.8f, 1f).SetExpCurve(Config.MycologyExpCurve, 100f).AddStartNode();
        AddConstellation(mycology);

        // Survival passives.
        PassiveNode.Create("Fasting", "fasting", survival).AddParent("start")
            .AddSkillStat("fasting", 1, "Hunger does not decrease while sitting.")
            .AddAdditiveExtraStat("healthMultiplier", -0.1f)
            .AddLevelRequirement("Cooking", 5).AddLevelRequirement("Survival", 5).AddLevelRequirement("Time", 5).NotableStyle();

        PassiveNode.Create("Movement Speed", "move1", survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("start").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Movement Speed", "move2", survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("move1").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Movement Speed", "move3", survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("move2").AddLevelRequirement("Survival", 7);
        PassiveNode.Create("Movement Speed", "move4", survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("move3").AddLevelRequirement("Survival", 9);

        PassiveNode.Create("Health", "health1", survival).AddAdditiveExtraStat("healthMultiplier", 0.05f).AddParent("start");
        PassiveNode.Create("Health", "health2", survival).AddAdditiveExtraStat("healthMultiplier", 0.05f).AddParent("health1").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Health", "health3", survival).AddAdditiveExtraStat("healthMultiplier", 0.05f).AddParent("health2").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Health", "health4", survival).AddMultiplicativeExtraStat("healthMultiplier", 1.2f).AddParent("health3").AddLevelRequirement("Survival", 7).NotableStyle();

        PassiveNode.Create("Saturation", "sat1", survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("start");
        PassiveNode.Create("Saturation", "sat2", survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("sat1").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Saturation", "sat3", survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("sat2").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Saturation", "sat4", survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("sat3").AddLevelRequirement("Survival", 7);
        PassiveNode.Create("Saturation", "sat5", survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("sat4").AddLevelRequirement("Survival", 9);
        PassiveNode.Create("Saturation", "sat6", survival).AddMultiplicativeExtraStat("satMultiplier", 1.5f).AddParent("sat5").AddLevelRequirement("Survival", 20).NotableStyle();

        // Feather Falling chain — reduced fall damage.
        PassiveNode.Create("Feather Falling", "featherfall1", survival).AddAdditiveExtraStat("featherFall", 0.2f, statBase: 0f).AddParent("start").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Feather Falling", "featherfall2", survival).AddAdditiveExtraStat("featherFall", 0.2f, statBase: 0f).AddParent("featherfall1").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Feather Falling", "featherfall3", survival).AddAdditiveExtraStat("featherFall", 0.2f, statBase: 0f).AddParent("featherfall2").AddLevelRequirement("Survival", 7);

        // Meat Shield chain — absorb incoming damage at the cost of saturation.
        PassiveNode.Create("Meat Shield", "meatShield1", survival).AddAdditiveExtraStat("meatShield", 0.1f, statBase: 0f).AddParent("sat1").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Meat Shield", "meatShield2", survival).AddAdditiveExtraStat("meatShield", 0.1f, statBase: 0f).AddParent("sat2").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Meat Shield", "meatShield3", survival).AddAdditiveExtraStat("meatShield", 0.1f, statBase: 0f).AddParent("sat3").AddLevelRequirement("Survival", 7);

        PassiveNode.Create("Primalist", "primalist", survival).AddParent("start").AddSkillStat("primalist", 1, """
            You can eat raw meat
            Grain provides no nutrition
            """).KeystoneStyle();

        PassiveNode.Create("Shroud Walker", "shroudWalker", survival).AddParent("featherfall2").AddSkillStat("shroudWalker", 1, """
            Gain Chameleon while sneaking
            """).AddAdditiveExtraStat("healthMultiplier", -0.5f).AddLevelRequirement("Survival", 8).KeystoneStyle();

        PassiveNode.Create("Luminiferous", "luminiferous1", survival).AddParent("start").AddSkillStat("luminiferous", 1, """
            +15 ambient light emission
            """).NotableStyle().AddLevelRequirement("Survival", 8);
        PassiveNode.Create("Luminiferous", "luminiferous2", survival).AddParent("luminiferous1").AddSkillStat("luminiferous", 1, """
            +5 ambient light emission
            """).NotableStyle().AddLevelRequirement("Survival", 10);
        PassiveNode.Create("Luminiferous", "luminiferous3", survival).AddParent("luminiferous2").AddSkillStat("luminiferous", 1, """
            +5 ambient light emission
            """).NotableStyle().AddLevelRequirement("Survival", 20);

        // Strong Back chain — extra backpack slots.
        PassiveNode.Create("Strong Back", "strongBack1", survival).AddAdditiveExtraStat("strongBack", 1f, true, statBase: 0f).AddAdditiveStat("walkspeed", -0.04f).AddParent("start").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Strong Back", "strongBack2", survival).AddAdditiveExtraStat("strongBack", 1f, true, statBase: 0f).AddAdditiveStat("walkspeed", -0.04f).AddParent("strongBack1").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Strong Back", "strongBack3", survival).AddAdditiveExtraStat("strongBack", 1f, true, statBase: 0f).AddAdditiveStat("walkspeed", -0.04f).AddParent("strongBack2").AddLevelRequirement("Survival", 7);
        PassiveNode.Create("Pack Mule", "strongBack4", survival).AddAdditiveExtraStat("strongBack", 3f, true, statBase: 0f).AddAdditiveStat("walkspeed", -0.2f).AddParent("strongBack3").AddLevelRequirement("Survival", 10).NotableStyle();

        PassiveNode.Create("Improviser", "improviser", survival).AddParent("start").AddSkillStat("improviser", 1, """
            May craft a sling
            """).KeystoneStyle();

        // Crafting passives.
        PassiveNode.Create("Dual Specialization", "dualSpecialization", survival).AddParent("start")
            .AddSkillStat("dualSpecialization", 1, "You may allocate up to 2 specialization nodes.")
            .NotableStyle().AddLevelRequirement("Survival", 6);

        PassiveNode.Create("Sewing Effectiveness", "sewing1", crafting).AddAdditiveExtraStat("sewingeffectiveness", 0.1f).AddLevelRequirement("Crafting", 2).AddParent("start");
        PassiveNode.Create("Sewing Effectiveness", "sewing2", crafting).AddAdditiveExtraStat("sewingeffectiveness", 0.1f).AddLevelRequirement("Crafting", 4).AddParent("sewing1");
        PassiveNode.Create("Sewing Effectiveness", "sewing3", crafting).AddAdditiveExtraStat("sewingeffectiveness", 0.1f).AddLevelRequirement("Crafting", 6).AddParent("sewing2");

        PassiveNode.Create("Path of the Seamster", "pts1", crafting).AddMultiplicativeExtraStat("healthMultiplier", 0.9f).AddParent("sewing3");
        PassiveNode.Create("Path of the Seamster", "pts2", crafting).AddMultiplicativeExtraStat("healthMultiplier", 0.9f).AddParent("pts1");

        PassiveNode.Create("Clothier", "clothier", crafting).AddParent("pts2").AddSkillStat("clothier", 1, """
            You may sew certain kinds of clothing
            """).KeystoneStyle().AddLevelRequirement("Crafting", 8);

        // Smithing passives.
        PassiveNode.Create("Smith", "smith", smithing).AddParent("start").WithTag("specialization")
            .AddRequirement(new SpecializationRequirement()).NotableStyle();

        PassiveNode.Create("Master Smith", "masterSmith1", smithing).AddSkillStat("masterSmith", 1, """
            Your heavy hit also moves 1 voxel of material to the correct position
            """).KeystoneStyle().AddParent("smith").AddRequirement(new NodeRequirement("Smithing", "smith")).AddLevelRequirement("Smithing", 5);

        PassiveNode.Create("Master Smith", "masterSmith2", smithing).AddSkillStat("masterSmith", 1, """
            Your heavy hit also moves 1 voxel of material to the correct position
            """).NotableStyle().AddParent("masterSmith1").AddRequirement(new NodeRequirement("Smithing", "smith")).AddLevelRequirement("Smithing", 10);

        PassiveNode.Create("Cracker", "cracker1", smithing).AddAdditiveExtraStat("bloomeryDrops", 0.1f).AddParent("start").AddLevelRequirement("Smithing", 4);
        PassiveNode.Create("Cracker", "cracker2", smithing).AddAdditiveExtraStat("bloomeryDrops", 0.1f).AddParent("cracker1").AddLevelRequirement("Smithing", 8);

        PassiveNode.Create("Merciless", "merciless", smithing).AddParent("start").AddSkillStat("merciless", 1, """
            May craft the Blackguard Blade
            """).KeystoneStyle().AddLevelRequirement("Smithing", 8);

        PassiveNode.Create("Bloomery Extraction", "bloomeryExtraction", smithing).AddParent("smith").AddRequirement(new NodeRequirement("Smithing", "smith"))
            .AddSkillStat("bloomeryExtraction", 1, "May collect finished bloomery output with an empty hand without breaking the bloomery.")
            .NotableStyle().AddLevelRequirement("Smithing", 10);

        PassiveNode.Create("Careful Quenching", "carefulQuenching", smithing).AddParent("smith")
            .AddRequirement(new NodeRequirement("Smithing", "smith"))
            .AddAdditiveExtraStatPerLevel("quenchBreakChanceReduction", 0.01f, "Smithing").NotableStyle();

        // Excavation passives.
        PassiveNode.Create("Eroder", "eroder", excavation).AddSkillStat("eroder", 1, """
            Rock does not drop small stones
            """).AddParent("start").AddLevelRequirement("Excavation", 5).KeystoneStyle();

        // Ore Miner chain — more ore drop rate.
        PassiveNode.Create("Ore Miner", "oremine1", excavation).AddAdditiveStat("oreDropRate", 0.1f).AddParent("start").AddLevelRequirement("Excavation", 2);
        PassiveNode.Create("Ore Miner", "oremine2", excavation).AddAdditiveStat("oreDropRate", 0.1f).AddParent("oremine1").AddLevelRequirement("Excavation", 4);
        PassiveNode.Create("Ore Miner", "oremine3", excavation).AddAdditiveStat("oreDropRate", 0.1f).AddParent("oremine2").AddLevelRequirement("Excavation", 6);
        PassiveNode.Create("Ore Miner", "oremine4", excavation).AddAdditiveStatPerLevel("oreDropRate", 0.02f, "Excavation").AddParent("oremine3").AddLevelRequirement("Excavation", 8).NotableStyle().AddTagExclusiveRequirement("miningmastery", 1).WithTag("miningmastery");

        // Pickaxe Expert chain — faster mining speed.
        PassiveNode.Create("Pickaxe Expert", "minespeed1", excavation).AddAdditiveStat("miningSpeedMul", 0.1f).AddParent("start").AddLevelRequirement("Excavation", 3);
        PassiveNode.Create("Pickaxe Expert", "minespeed2", excavation).AddAdditiveStat("miningSpeedMul", 0.1f).AddParent("minespeed1").AddLevelRequirement("Excavation", 5);
        PassiveNode.Create("Pickaxe Expert", "minespeed3", excavation).AddAdditiveStatPerLevel("miningSpeedMul", 0.02f, "Excavation").AddParent("minespeed2").AddLevelRequirement("Excavation", 7).NotableStyle().AddTagExclusiveRequirement("miningmastery", 1).WithTag("miningmastery");

        // Vein Miner keystone — mining an ore block breaks connected ore of the same type.
        PassiveNode.Create("Vein Miner", "veinminer", excavation).AddParent("oremine3").AddSkillStat("veinminer", 1, """
            Mining an ore block breaks some connected ore blocks of the same type
            Costs extra tool durability per block broken
            """).KeystoneStyle().AddLevelRequirement("Excavation", 10);

        // Stone Breaker chain — more drops when breaking rock.
        PassiveNode.Create("Stone Breaker", "stonebreak1", excavation).AddAdditiveExtraStat("stoneDropBonus", 0.1f, statBase: 0f).AddParent("start").AddLevelRequirement("Excavation", 2);
        PassiveNode.Create("Stone Breaker", "stonebreak2", excavation).AddAdditiveExtraStat("stoneDropBonus", 0.1f, statBase: 0f).AddParent("stonebreak1").AddLevelRequirement("Excavation", 4);
        PassiveNode.Create("Stone Breaker", "stonebreak3", excavation).AddAdditiveExtraStatPerLevel("stoneDropBonus", 0.02f, "Excavation", statBase: 0f).AddParent("stonebreak2").AddLevelRequirement("Excavation", 9).NotableStyle().AddTagExclusiveRequirement("miningmastery", 1).WithTag("miningmastery");

        // Stone Cutter chain — chance to drop an intact stone block when mining rock.
        PassiveNode.Create("Stone Cutter", "stonecutter1", excavation).AddAdditiveExtraStat("stoneCutterChance", 0.1f, statBase: 0f).AddParent("start").AddLevelRequirement("Excavation", 2);
        PassiveNode.Create("Stone Cutter", "stonecutter2", excavation).AddAdditiveExtraStat("stoneCutterChance", 0.1f, statBase: 0f).AddParent("stonecutter1").AddLevelRequirement("Excavation", 4);
        PassiveNode.Create("Stone Cutter", "stonecutter3", excavation).AddAdditiveExtraStatPerLevel("stoneCutterChance", 0.02f, "Excavation", statBase: 0f).AddParent("stonecutter2").AddLevelRequirement("Excavation", 9).NotableStyle().AddTagExclusiveRequirement("miningmastery", 1).WithTag("miningmastery");

        // Gemstone Miner chain — more gemstone drops from gem ore blocks.
        PassiveNode.Create("Gemstone Miner", "gemmine1", excavation).AddAdditiveExtraStat("gemDropBonus", 0.1f, statBase: 0f).AddParent("oremine1").AddLevelRequirement("Excavation", 2);
        PassiveNode.Create("Gemstone Miner", "gemmine2", excavation).AddAdditiveExtraStat("gemDropBonus", 0.1f, statBase: 0f).AddParent("gemmine1").AddLevelRequirement("Excavation", 4);
        PassiveNode.Create("Gemstone Miner", "gemmine3", excavation).AddAdditiveExtraStatPerLevel("gemDropBonus", 0.02f, "Excavation", statBase: 0f).AddParent("gemmine2").AddLevelRequirement("Excavation", 9).NotableStyle().AddTagExclusiveRequirement("miningmastery", 1).WithTag("miningmastery");

        // Horticulture passives.
        // Green Thumb chain — more wild crop drops.
        PassiveNode.Create("Green Thumb", "cropgain1", horticulture).AddAdditiveStat("wildCropDropRate", 0.1f).AddParent("start").AddLevelRequirement("Horticulture", 2);
        PassiveNode.Create("Green Thumb", "cropgain2", horticulture).AddAdditiveStat("wildCropDropRate", 0.1f).AddParent("cropgain1").AddLevelRequirement("Horticulture", 4);
        PassiveNode.Create("Green Thumb", "cropgain3", horticulture).AddAdditiveStat("wildCropDropRate", 0.1f).AddParent("cropgain2").AddLevelRequirement("Horticulture", 6);
        PassiveNode.Create("Green Thumb", "cropgain4", horticulture).AddAdditiveStat("wildCropDropRate", 0.1f).AddParent("cropgain3").AddLevelRequirement("Horticulture", 8);

        // Gatherer chain — more forage drops (berries, mushrooms, etc.).
        PassiveNode.Create("Gatherer", "forage1", horticulture).AddAdditiveStat("forageDropRate", 0.1f).AddParent("start").AddLevelRequirement("Horticulture", 2);
        PassiveNode.Create("Gatherer", "forage2", horticulture).AddAdditiveStat("forageDropRate", 0.1f).AddParent("forage1").AddLevelRequirement("Horticulture", 4);
        PassiveNode.Create("Gatherer", "forage3", horticulture).AddAdditiveStat("forageDropRate", 0.1f).AddParent("forage2").AddLevelRequirement("Horticulture", 6);

        // Beemaster — harvest skeps without breaking them.
        PassiveNode.Create("Beemaster", "beemaster", horticulture).NotableStyle()
            .AddParent("forage3")
            .AddSkillStat("beemaster", 1, "May harvest skeps without breaking them")
            .AddLevelRequirement("Horticulture", 10);

        // Extensive Farming — till in a larger area with hoe tool modes; shears cut in a wider radius.
        PassiveNode.Create("Extensive Farming", "extfarming1", horticulture)
            .AddParent("start")
            .AddSkillStat("extensivefarming", 1, "+1 to hoe and shear radius tool modes")
            .AddLevelRequirement("Horticulture", 6).NotableStyle();
        PassiveNode.Create("Extensive Farming", "extfarming2", horticulture)
            .AddParent("extfarming1")
            .AddSkillStat("extensivefarming", 1, "+1 to hoe and shear radius tool modes")
            .AddLevelRequirement("Horticulture", 10).KeystoneStyle();

        // Orchardist chain — more fruit tree drops.
        PassiveNode.Create("Orchardist", "orchardist1", horticulture).AddAdditiveExtraStat("orchardistBonus", 0.2f).AddParent("start").AddLevelRequirement("Horticulture", 3);
        PassiveNode.Create("Orchardist", "orchardist2", horticulture).AddAdditiveExtraStat("orchardistBonus", 0.2f).AddParent("orchardist1").AddLevelRequirement("Horticulture", 5);
        PassiveNode.Create("Orchardist", "orchardist3", horticulture).AddAdditiveExtraStat("orchardistBonus", 0.2f).AddParent("orchardist2").AddLevelRequirement("Horticulture", 7);

        // Hunting passives.
        // Archer chain — increased ranged weapon damage.
        PassiveNode.Create("Archer", "rangeddmg1", hunting).AddAdditiveStat("rangedWeaponsDamage", 0.1f).AddParent("start").AddLevelRequirement("Hunting", 2);
        PassiveNode.Create("Archer", "rangeddmg2", hunting).AddAdditiveStat("rangedWeaponsDamage", 0.1f).AddParent("rangeddmg1").AddLevelRequirement("Hunting", 4);
        PassiveNode.Create("Archer", "rangeddmg3", hunting).AddAdditiveStat("rangedWeaponsDamage", 0.1f).AddParent("rangeddmg2").AddLevelRequirement("Hunting", 6);
        PassiveNode.Create("Archer", "rangeddmg4", hunting).AddAdditiveStat("rangedWeaponsDamage", 0.1f).AddParent("rangeddmg3").AddLevelRequirement("Hunting", 8);

        PassiveNode.Create("Sharpshooter", "rangedacc1", hunting).AddAdditiveStat("rangedWeaponsAcc", 0.15f).AddParent("start").AddLevelRequirement("Hunting", 2);
        PassiveNode.Create("Sharpshooter", "rangedacc2", hunting).AddAdditiveStat("rangedWeaponsAcc", 0.15f).AddParent("rangedacc1").AddLevelRequirement("Hunting", 4);
        PassiveNode.Create("Sharpshooter", "rangedacc3", hunting).AddAdditiveStat("rangedWeaponsAcc", 0.15f).AddParent("rangedacc2").AddLevelRequirement("Hunting", 6);

        // Looter chain — more drops from animals.
        PassiveNode.Create("Looter", "lootdrop1", hunting).AddAdditiveStat("animalLootDropRate", 0.1f).AddParent("start").AddLevelRequirement("Hunting", 2);
        PassiveNode.Create("Looter", "lootdrop2", hunting).AddAdditiveStat("animalLootDropRate", 0.1f).AddParent("lootdrop1").AddLevelRequirement("Hunting", 4);
        PassiveNode.Create("Looter", "lootdrop3", hunting).AddAdditiveStat("animalLootDropRate", 0.1f).AddParent("lootdrop2").AddLevelRequirement("Hunting", 6);
        PassiveNode.Create("Looter", "lootdrop4", hunting).AddAdditiveStat("animalLootDropRate", 0.1f).AddParent("lootdrop3").AddLevelRequirement("Hunting", 8);

        PassiveNode.Create("Biogenesis", "biogenesis", hunting).NotableStyle()
            .AddParent("lootdrop4")
            .AddSkillStat("biogenesis", 1, "1% chance to receive an itemized version of the entity when harvesting")
            .AddLevelRequirement("Hunting", 10);

        // 20% ranged damage
        PassiveNode.Create("Hunter", "hunter", hunting).KeystoneStyle()
            .AddParent("lootdrop3").AddParent("rangeddmg3")
            .AddLevelRequirement("Hunting", 6)
            .AddAdditiveStat("animalHarvestingTime", 0.25f)
            .AddAdditiveStat("bowDrawingStrength", 0.25f)
            .AddSkillStat("bowyer", 1, "May craft powerful bows");

        // Combat passives.
        PassiveNode.Create("Warrior", "warrior", combat).AddParent("start").WithTag("specialization")
            .AddRequirement(new SpecializationRequirement()).NotableStyle();

        // Swordsman chain — increased melee weapon damage.
        PassiveNode.Create("Swordsman", "meleedmg1", combat).AddAdditiveStat("meleeWeaponsDamage", 0.1f).AddParent("start").AddLevelRequirement("Combat", 2);
        PassiveNode.Create("Swordsman", "meleedmg2", combat).AddAdditiveStat("meleeWeaponsDamage", 0.1f).AddParent("meleedmg1").AddLevelRequirement("Combat", 4);
        PassiveNode.Create("Swordsman", "meleedmg3", combat).AddAdditiveStatPerLevel("meleeWeaponsDamage", 0.02f, "Combat").AddParent("meleedmg2").AddLevelRequirement("Combat", 7).NotableStyle();
        PassiveNode.Create("Swordsman", "meleedmg4", combat).AddAdditiveStatPerLevel("meleeWeaponsDamage", 0.02f, "Combat")
            .AddParent("warrior").AddRequirement(new NodeRequirement("Combat", "warrior")).NotableStyle();

        PassiveNode.Create("Armor Training", "armorTraining1", combat).AddAdditiveStat("armorWalkSpeedAffectedness", -0.15f).AddParent("start");
        PassiveNode.Create("Armor Training", "armorTraining2", combat).AddAdditiveStatPerLevel("armorWalkSpeedAffectedness", -0.005f, "Combat")
            .AddParent("warrior").AddRequirement(new NodeRequirement("Combat", "warrior")).NotableStyle();

        // Berserker keystone — killing an entity restores a small amount of health.
        PassiveNode.Create("Berserker", "berserker", combat).AddParent("warrior").AddRequirement(new NodeRequirement("Combat", "warrior")).AddSkillStat("berserker", 1, """
            Killing an entity restores 2 health
            """).KeystoneStyle().AddLevelRequirement("Combat", 8);

        // Spore Cloud chain — chance to find a second mushroom when harvesting.
        PassiveNode.Create("Spore Cloud", "sporeCloud1", mycology).AddAdditiveExtraStat("sporeCloud", 0.3f, statBase: 0f).AddParent("start").AddLevelRequirement("Mycology", 3);
        PassiveNode.Create("Spore Cloud", "sporeCloud2", mycology).AddAdditiveExtraStat("sporeCloud", 0.3f, statBase: 0f).AddParent("sporeCloud1").AddLevelRequirement("Mycology", 5);
        PassiveNode.Create("Spore Storm", "sporeStorm", mycology).AddAdditiveExtraStatPerLevel("sporeCloud", 0.1f, "Mycology").AddParent("sporeCloud2").AddLevelRequirement("Mycology", 8).NotableStyle();

        // Fungal Fortitude chain — mushrooms restore more saturation.
        PassiveNode.Create("Fungal Fortitude", "fungalFortitude1", mycology).AddAdditiveExtraStat("fungalFortitude", 0.5f, statBase: 0f).AddParent("start").AddLevelRequirement("Mycology", 3);
        PassiveNode.Create("Fungal Fortitude", "fungalFortitude2", mycology).AddAdditiveExtraStat("fungalFortitude", 0.5f, statBase: 0f).AddParent("fungalFortitude1").AddLevelRequirement("Mycology", 6);
        PassiveNode.Create("Fungal Fortitude", "fungalFortitude3", mycology).AddAdditiveExtraStatPerLevel("fungalFortitude", 0.1f, "Mycology", statBase: 0f).AddParent("fungalFortitude2").AddLevelRequirement("Mycology", 9).NotableStyle();
        // Time passives.
        PassiveNode.Create("Stable Settler", "stableSettler", time).AddSkillStat("stableSettler", 1, """
            Temporally unstable areas do not affect you near the surface
            """).AddParent("start").AddLevelRequirement("Time", 5).KeystoneStyle();

        PassiveNode.Create("Tinkerer", "tinkerer", time).AddParent("start").AddSkillStat("tinkerer", 1, """
            May craft hacking spears
            """).KeystoneStyle().AddLevelRequirement("Time", 5);

        // Temporal Resilience chain — reduce temporal stability drain rate.
        PassiveNode.Create("Temporal Resilience", "temporalResilience1", time).AddAdditiveExtraStat("temporalResilience", -0.2f, statBase: 1f).AddParent("start").AddLevelRequirement("Time", 3);
        PassiveNode.Create("Temporal Resilience", "temporalResilience2", time).AddAdditiveExtraStat("temporalResilience", -0.2f, statBase: 1f).AddParent("temporalResilience1").AddLevelRequirement("Time", 5);
        PassiveNode.Create("Temporal Resilience", "temporalResilience3", time).AddAdditiveExtraStat("temporalResilience", -0.2f, statBase: 1f).AddParent("temporalResilience2").AddLevelRequirement("Time", 7);

        // Schizophrenic Dissociation keystone — damage reduction with deferred health loss and amplified stability drain.
        PassiveNode.Create("Schizophrenic Dissociation", "schizoDissociation", time).AddParent("start").AddSkillStat("schizoDissociation", 1, """
            40% less damage taken
            40% of damage taken is removed from health 4 seconds later
            """).AddAdditiveExtraStat("temporalResilience", 2f, statBase: 1f).KeystoneStyle().AddLevelRequirement("Time", 10);

        // Temporal Ward keystone — no mob can spawn within 10 meters of you during temporal storms.
        PassiveNode.Create("Temporal Ward", "temporalWard", time).AddParent("start").AddSkillStat("temporalWard", 1, """
            Temporal entities cannot spawn within 8 meters of you
            """).KeystoneStyle().AddLevelRequirement("Time", 8);

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

        RegisterAggregator(new HungerAggregator());

        //LoadNodePositions();

        if (api is ICoreServerAPI sapi)
        {
            sapi.Event.PlayerJoin += p =>
            {
                // Players passive data should already be loaded here.
                CalculatePlayerStats(p.Entity);

                // Chain onto the existing OnCanSpawnNearby delegate set by SystemTemporalStability.
                // Blocks any entity from spawning within 10m of the player if they have the temporalWard skill.
                CanSpawnNearbyDelegate? existing = p.Entity.OnCanSpawnNearby;
                p.Entity.OnCanSpawnNearby = (type, spawnPos, sc) =>
                {
                    if (p.Entity.GetSkillLevel("temporalWard") > 0)
                    {
                        if (p.Entity.Pos.SquareDistanceTo(spawnPos) < 10.0 * 10.0) return false;
                    }
                    return existing == null || existing(type, spawnPos, sc);
                };
            };

            sapi.Event.PlayerLeave += p =>
            {
                CalculatePlayerStats(p.Entity, true);
            };

            // Load passive data from world...
            LoadDataFromWorld();
        }
    }

    public override void OnAssetsLoaded()
    {
        LoadNodePositions();
    }

    public void RegisterAggregator(PassiveAggregator aggregator)
    {
        aggregators.Add(aggregator);
    }

    protected override void RegisterMessages(INetworkChannel channel)
    {
        channel
            .RegisterMessageType<PlayerPolarisData>()
            .RegisterMessageType<NodeAllocationRequest>()
            .RegisterMessageType<S2CExpPacket>();
    }

    protected override void RegisterClientMessages(IClientNetworkChannel channel)
    {
        channel.SetMessageHandler<PlayerPolarisData>(p =>
        {
            MainAPI.Client.EnqueueMainThreadTask(() =>
            {
                playerDataByUid[MainAPI.Capi.World.Player.PlayerUID] = p;
                OnClientDataUpdated?.Invoke(p);
            }, "");
        });

        channel.SetMessageHandler<NodeAllocationRequest>(p =>
        {
            // Don't verify server -> client.
            PlayerPolarisData data = GetClientData();
            PlayerConstellationData constData = data.GetConstellation(p.ConstellationName);
            PassiveNode? node = GetConstellation(p.ConstellationName)?.GetNodeById(p.NodeId);
            if (node == null) return;

            if (p.Allocate)
            {
                constData.AllocatedNodeIds.Add(p.NodeId);
                data.SetKnowledgePoints(data.KnowledgePoints - node.Cost);
            }
            else
            {
                float currentExpRatio = constData.Experience / node.Constellation.GetExpToReachLevel(constData.Level + 1);
                float newExp = MathF.Round(currentExpRatio * node.Constellation.GetExpToReachLevel(constData.Level), 2);

                float totalExpLoss = constData.Experience + node.Constellation.GetExpToReachLevel(constData.Level) + newExp;
                //float totalExpLoss = constData.Experience + node.Constellation.GetExpToReachLevel(constData.Level) - newExp; Claude said this was correct didn't test.

                constData.AllocatedNodeIds.Remove(p.NodeId);
                constData.Level--;
                constData.Experience = newExp;
                data.SetLevelAndKnowledgeFromTotalExp(this);

                OnClientExperienceGain?.Invoke(node.Constellation, -totalExpLoss, constData.Level, false);
            }

            OnClientDataUpdated?.Invoke(data);
        });

        channel.SetMessageHandler<S2CExpPacket>(p =>
        {
            AddExperience(p.Constellation, MainAPI.Capi.World.Player.PlayerUID, p.ExpGain, p.GiveAlert);
        });
    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {
        channel.SetMessageHandler<NodeAllocationRequest>((player, p) =>
        {
            if (player.Entity == null) return;

            if (!constellationByName.TryGetValue(p.ConstellationName, out Constellation? constellation)) return;
            PlayerPolarisData data = GetPlayerData(player.PlayerUID);
            PlayerConstellationData constData = data.GetConstellation(p.ConstellationName);

            PassiveNode? node = constellation.GetNodeById(p.NodeId);
            if (node == null) return;

            if (p.Allocate && data.KnowledgePoints < node.Cost) return;

            AllocatedNodesInfo allocatedNodes = data.GetAllocatedNodesInfo(this);

            if (p.Allocate)
            {
                if (!constData.IsNodeAllocatable(node)) return;
                if (!node.CanAllocate(player.Entity, data, allocatedNodes)) return;
                constData.AllocatedNodeIds.Add(node.Id);
                data.SetKnowledgePoints(data.KnowledgePoints - node.Cost);
            }
            else
            {
                if (!constData.IsNodeUnallocatable(node)) return;
                if (data.DoesAnythingRelyOnNode(node, this, [])) return;
                if (constData.Level < 2) return; // Can't unallocate if level 1, would cause negative points.

                float currentExpRatio = MathF.Round(constData.Experience / constellation.GetExpToReachLevel(constData.Level + 1), 2);

                constData.AllocatedNodeIds.Remove(node.Id);
                constData.Level--;
                constData.Experience = currentExpRatio * constellation.GetExpToReachLevel(constData.Level + 1);
                data.SetLevelAndKnowledgeFromTotalExp(this);
            }

            NodeAllocationRequest packet = new()
            {
                ConstellationName = p.ConstellationName,
                NodeId = p.NodeId,
                Allocate = p.Allocate
            };

            // Allocation successful, echo back to the player.
            SendPacket(packet, player);

            // Do stat re-calculation here, now that something is changed. Passive bonuses are server only. Effects or watched attribute booleans will determine client behavior.
            CalculatePlayerStats(player.Entity);
        });
    }

    public Constellation? GetConstellation(string constellation)
    {
        constellationByName.TryGetValue(constellation, out Constellation? constel);
        return constel;
    }

    public PassiveNode? GetNode(string constellation, string nodeCode)
    {
        Constellation? constel = GetConstellation(constellation);
        return constel?.GetNodeByCode(nodeCode);
    }

    /// <summary>
    /// Server-side static helper for experience.
    /// </summary>
    public static void AddExperience(string constellationName, IPlayer player, float amount, bool giveAlert = true)
    {
        Instance(MainAPI.Sapi).AddExperience(constellationName, player.PlayerUID, amount, giveAlert);
    }

    /// <summary>
    /// Adds experience to a constellation, triggers events.
    /// Called on client and server.
    /// </summary>
    public void AddExperience(string constellationName, string uid, float amount, bool giveAlert = true)
    {
        if (!constellationByName.TryGetValue(constellationName, out Constellation? constellation)) return; // Invalid.

        // Prevent weird numbers.
        amount = MathF.Round(amount, 2);

        // Apply exp multipliers from extra stats.
        if (api is ICoreServerAPI sapi
            && sapi.World.PlayerByUid(uid) is IPlayer sPlayer
            && sPlayer.Entity != null)
        {
            float multi = 1f;

            // Both separate modifiers.
            if (sPlayer.Entity.TryGetExtraStat($"{constellationName}ExpMul", out float mul))
            {
                multi *= mul;
            }

            if (sPlayer.Entity.TryGetExtraStat("allExpMul", out float allMul))
            {
                multi *= allMul;
            }

            amount *= multi;
        }

        PlayerPolarisData playerData = GetPlayerData(uid);
        PlayerConstellationData data = playerData.GetConstellation(constellationName);
        int previousLevel = data.Level;
        data.Experience += amount;

        bool shouldServerRecalculate = false;

        while (data.Experience >= constellation.GetExpToReachLevel(data.Level + 1) && data.Level < Config.MaxSkillLevel)
        {
            float expNeeded = constellation.GetExpToReachLevel(data.Level + 1);

            data.Experience -= expNeeded;
            playerData.Experience += expNeeded;

            data.Level++;
            shouldServerRecalculate = true;
        }

        if (data.Level >= Config.MaxSkillLevel)
        {
            data.Experience = MathF.Min(data.Experience, constellation.GetExpToReachLevel(Config.MaxSkillLevel + 1));
        }

        while (playerData.Experience >= PlayerPolarisData.GetExpToReachLevel(playerData.Level + 1) && playerData.Level < Config.MaxMainLevel)
        {
            playerData.Experience -= PlayerPolarisData.GetExpToReachLevel(playerData.Level + 1);
            playerData.Level++;
            playerData.SetKnowledgePoints(playerData.KnowledgePoints + 1);
            shouldServerRecalculate = true;
        }

        if (playerData.Level >= Config.MaxMainLevel)
        {
            playerData.Experience = MathF.Min(playerData.Experience, PlayerPolarisData.GetExpToReachLevel(Config.MaxMainLevel + 1));
        }

        if (api.Side.IsServer())
        {
            IPlayer? player = api.World.PlayerByUid(uid);
            if (player == null) return;

            S2CExpPacket packet = new()
            {
                Constellation = constellationName,
                ExpGain = amount,
                GiveAlert = giveAlert
            };
            SendPacket(packet, (IServerPlayer)player);

            // Recalculate when leveling up.
            if (shouldServerRecalculate)
            {
                CalculatePlayerStats(player.Entity);
            }
        }
        else
        {
            OnClientExperienceGain?.Invoke(constellation, amount, data.Level, giveAlert);
            if (data.Level > previousLevel)
                OnClientSkillLevelUp?.Invoke(constellation, data.Level);
        }
    }

    public void AddConstellation(Constellation constellation)
    {
        constellations.Add(constellation);
        constellationByName.Add(constellation.Name, constellation);
    }

    /// <summary>
    /// Get a player's current constellation level.
    /// </summary>
    public int GetConstellationLevel(string constellationName, string uid)
    {
        PlayerConstellationData data = GetConstellationData(constellationName, uid);
        return data.Level;
    }

    public PlayerConstellationData GetConstellationData(string constellationName, string uid)
    {
        PlayerPolarisData playerData = GetPlayerData(uid);
        return playerData.GetConstellation(constellationName);
    }

    public PlayerPolarisData GetPlayerData(string uid)
    {
        if (!playerDataByUid.TryGetValue(uid, out PlayerPolarisData? data))
        {
            data = new PlayerPolarisData();
            playerDataByUid[uid] = data;
        }

        return data;
    }

    /// <summary>Server-only, idempotent achievement award. Requirements must already be unlocked.</summary>
    public static bool TriggerAchievement(string code, IPlayer player, string? goalCode = null)
    {
        if (player is not IServerPlayer serverPlayer || player.Entity?.Api is not ICoreServerAPI sapi) return false;
        if (!Achievements.ByCode.TryGetValue(code, out Achievement? achievement)) return false;
        SystemPolaris system = Instance(sapi);
        // Reject invalid rewards before recording the unlock or granting any reward.
        if (achievement.KnowledgePointReward < 0 || !float.IsFinite(achievement.ExperienceReward)
            || achievement.ExperienceReward < 0
            || (achievement.ExperienceReward > 0 && (achievement.ExperienceConstellation == null
                || system.GetConstellation(achievement.ExperienceConstellation) == null)))
        {
            sapi.Logger.Error($"Invalid reward configuration for achievement '{code}'.");
            return false;
        }
        PlayerPolarisData data = system.GetPlayerData(player.PlayerUID);
        if (data.Achievements.Contains(code)) return false;
        data.AchievementProgress.TryGetValue(code, out AchievementProgress? progress);
        bool changed = false;
        if (goalCode != null)
        {
            if (!achievement.Goals.Contains(goalCode)) return false;
            if (progress == null)
                data.AchievementProgress[code] = progress = new AchievementProgress();
            changed = progress.CompletedGoals.Add(goalCode);
        }
        if (!Achievements.TryUnlock(code, data.Achievements, progress?.CompletedGoals))
        {
            if (changed) system.SendPacket(data, serverPlayer);
            return false;
        }

        data.AchievementKnowledgePoints += achievement.KnowledgePointReward;
        data.SetKnowledgePoints(data.KnowledgePoints + achievement.KnowledgePointReward);
        if (achievement.ExperienceReward > 0)
            system.AddExperience(achievement.ExperienceConstellation!, player.PlayerUID, achievement.ExperienceReward);

        system.SendPacket(data, serverPlayer);
        sapi.BroadcastMessageToAllGroups($"{player.PlayerName} has received the achievement {achievement.Name}", EnumChatType.Notification);
        return true;
    }

    public PlayerPolarisData GetClientData()
    {
        if (!playerDataByUid.TryGetValue(MainAPI.Capi.World.Player.PlayerUID, out PlayerPolarisData? data))
        {
            data = new PlayerPolarisData();
            playerDataByUid[MainAPI.Capi.World.Player.PlayerUID] = data;
        }

        return data;
    }

    // Two save methods called from SystemPolarisStats.

    /// <summary>
    /// Fixes data when loading.
    /// </summary>
    private void VerifyPlayerData(PlayerPolarisData data)
    {
        List<string> invalidConstellations = [];
        foreach (KeyValuePair<string, PlayerConstellationData> constData in data.ConstellationData)
        {
            // Remove invalid node ids.
            if (!constellationByName.TryGetValue(constData.Key, out Constellation? constellation))
            {
                invalidConstellations.Add(constData.Key);
                continue;
            }

            HashSet<int> validNodeIds = [];
            foreach (PassiveNode node in constellation.AllNodes)
            {
                validNodeIds.Add(node.Id);
            }

            constData.Value.AllocatedNodeIds.RemoveWhere(id => !validNodeIds.Contains(id));
        }

        foreach (string invalid in invalidConstellations)
        {
            data.ConstellationData.Remove(invalid);
        }

        // Set new level from total experience gained.
        data.SetLevelAndKnowledgeFromTotalExp(this);
    }

    private void LoadNodePositions()
    {
        List<IAsset> assets = api.Assets.GetMany("config/polarispositions.json");

        foreach (IAsset asset in assets)
        {
            NodePositionsConfig config = asset.ToObject<NodePositionsConfig>() ?? new NodePositionsConfig();
            foreach (KeyValuePair<string, int[]> kvp in config.Positions)
            {
                if (kvp.Value.Length < 2) continue;
                string[] parts = kvp.Key.Split(':', 2);
                if (parts.Length != 2) continue;

                PassiveNode? node = GetNode(parts[0], parts[1]);
                node?.SetPosition(kvp.Value[0], kvp.Value[1]);
            }
        }
    }

    public void SaveDataToWorld()
    {
        if (api.Side != EnumAppSide.Server) throw new Exception("Saving on the client.");

        Dictionary<string, byte[]> dataToSave = [];

        foreach (KeyValuePair<string, PlayerPolarisData> kv in playerDataByUid)
        {
            string uid = kv.Key;
            PlayerPolarisData data = kv.Value;
            data.ConvertToSaveableData(this);
            dataToSave[uid] = SerializerUtil.Serialize(data);
        }

        // Save dataToSave to world storage...
        MainAPI.Sapi.WorldManager.SaveGame.StoreData("polarisplayerdata", dataToSave);
    }

    public void LoadDataFromWorld()
    {
        if (api.Side != EnumAppSide.Server) throw new Exception("Loading on the client.");

        playerDataByUid.Clear();
        Dictionary<string, byte[]> loadedData = MainAPI.Sapi.WorldManager.SaveGame.GetData<Dictionary<string, byte[]>>("polarisplayerdata") ?? [];

        foreach (KeyValuePair<string, byte[]> kv in loadedData)
        {
            string uid = kv.Key;
            byte[] bytes = kv.Value;
            PlayerPolarisData? data = SerializerUtil.Deserialize<PlayerPolarisData>(bytes);
            if (data != null)
            {
                data.ConvertToLoadedData(this);
                VerifyPlayerData(data);
                playerDataByUid[uid] = data;
            }
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
        MainAPI.GetServerSystem<SystemPolaris>().SaveDataToWorld();
    }
}

/// <summary>
/// Packet sent from the server to the client who gained experience.
/// </summary>
[ProtoContract]
public class S2CExpPacket
{
    [ProtoMember(1)]
    public string Constellation = "";

    [ProtoMember(2)]
    public float ExpGain;

    [ProtoMember(3)]
    public bool GiveAlert;
}

[ProtoContract]
public class NodeAllocationRequest
{
    [ProtoMember(1)]
    public string ConstellationName = "";

    [ProtoMember(2)]
    public int NodeId;

    /// <summary>
    /// Allocate or unallocate.
    /// </summary>
    [ProtoMember(3)]
    public bool Allocate;
}

[ProtoContract]
public class PlayerPolarisData
{
    [ProtoMember(1)]
    public Dictionary<string, PlayerConstellationData> ConstellationData = [];

    [ProtoMember(2)]
    public int Level = 1;

    [ProtoMember(3)]
    public float Experience;

    // Knowledge points, loaded when verifying data.
    [ProtoMember(4)]
    public int KnowledgePoints { get; private set; }

    [ProtoMember(5)]
    public HashSet<string> Achievements = [];

    // Historical reward total, retained even after points are spent or definitions change.
    [ProtoMember(6)]
    public int AchievementKnowledgePoints;

    [ProtoMember(7)]
    public Dictionary<string, AchievementProgress> AchievementProgress = [];

    public void ConvertToSaveableData(SystemPolaris tree)
    {
        foreach (KeyValuePair<string, PlayerConstellationData> constKvp in ConstellationData)
        {
            Constellation? constellation = tree.GetConstellation(constKvp.Key);
            if (constellation == null) continue;
            constKvp.Value.ConvertToSaveableData(constellation);
        }
    }

    public void ConvertToLoadedData(SystemPolaris tree)
    {
        foreach (KeyValuePair<string, PlayerConstellationData> constKvp in ConstellationData)
        {
            Constellation? constellation = tree.GetConstellation(constKvp.Key);
            if (constellation == null) continue;
            constKvp.Value.ConvertToLoadedData(constellation);
        }
    }

    public void SetLevelAndKnowledgeFromTotalExp(SystemPolaris tree)
    {
        float exp = 0f;
        int pointsSpent = 0;

        foreach (KeyValuePair<string, PlayerConstellationData> constData in ConstellationData)
        {
            Constellation? constellation = tree.GetConstellation(constData.Key);
            if (constellation == null) continue;

            exp += constellation.GetTotalExpGained(constData.Value.Level, constData.Value.Experience);

            foreach (PassiveNode node in constellation.AllNodes)
            {
                if (constData.Value.AllocatedNodeIds.Contains(node.Id))
                {
                    pointsSpent += node.Cost;
                }
            }
        }

        // Set level.
        Level = 1;
        Experience = exp;
        while (Experience >= GetExpToReachLevel(Level + 1) && Level < tree.Config.MaxMainLevel)
        {
            Experience -= GetExpToReachLevel(Level + 1);
            Level++;
        }

        if (Level >= tree.Config.MaxMainLevel)
        {
            Experience = MathF.Min(Experience, GetExpToReachLevel(tree.Config.MaxMainLevel + 1));
        }

        SetKnowledgePoints(Level - 1 + AchievementKnowledgePoints - pointsSpent);
    }

    /// <summary>
    /// Check if any current allocated node relies on this node.
    /// If anything relies on it, it can't be unallocated.
    /// Also check pending nodes, it's not done here.
    /// </summary>
    public bool DoesAnythingRelyOnNode(PassiveNode node, SystemPolaris treeSystem, HashSet<PassiveNode> pendingUnallocations)
    {
        List<PassiveNode> list = GetAllAllocatedNodes(treeSystem);
        string code = node.GetFullCode();

        if (code == "Survival:dualSpecialization" && list.Count(allocatedNode => allocatedNode.Tags.Contains("specialization") && !pendingUnallocations.Contains(allocatedNode)) > 1)
        {
            return true;
        }

        foreach (PassiveNode allocatedNode in list)
        {
            if (pendingUnallocations.Contains(allocatedNode) || node == allocatedNode) continue;
            if (allocatedNode.ReliesOnNode(code)) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns a list of allocated nodes.
    /// </summary>
    public List<PassiveNode> GetAllAllocatedNodes(SystemPolaris treeSystem)
    {
        List<PassiveNode> nodes = [];

        foreach (KeyValuePair<string, PlayerConstellationData> constKvp in ConstellationData)
        {
            Constellation? constellation = treeSystem.GetConstellation(constKvp.Key);
            if (constellation == null) continue;

            PlayerConstellationData constData = constKvp.Value;
            foreach (int nodeId in constData.AllocatedNodeIds)
            {
                PassiveNode? node = constellation.GetNodeById(nodeId);
                if (node == null) continue;
                nodes.Add(node);
            }
        }

        return nodes;
    }

    /// <summary>
    /// Returns all allocated nodes in the format constellation:code.
    /// </summary>
    public AllocatedNodesInfo GetAllocatedNodesInfo(SystemPolaris treeSystem)
    {
        AllocatedNodesInfo info = new();

        foreach (KeyValuePair<string, PlayerConstellationData> constKvp in ConstellationData)
        {
            Constellation? constellation = treeSystem.GetConstellation(constKvp.Key);
            if (constellation == null) continue;

            PlayerConstellationData constData = constKvp.Value;
            foreach (int nodeId in constData.AllocatedNodeIds)
            {
                PassiveNode? node = constellation.GetNodeById(nodeId);
                if (node == null) continue;
                info.AllocatedNodeCodes.Add(node.GetFullCode());
                info.IncrementTags(node.Tags);
            }
        }

        return info;
    }

    public void SetKnowledgePoints(int amount)
    {
        KnowledgePoints = amount;
    }

    public static float ExpCurve = 1.5f;

    public static float GetExpToReachLevel(int level)
    {
        return 100f * MathF.Pow(level - 1, ExpCurve);
    }

    public PlayerConstellationData GetConstellation(string name)
    {
        if (!ConstellationData.TryGetValue(name, out PlayerConstellationData? data))
        {
            data = new PlayerConstellationData();
            ConstellationData[name] = data;
        }
        return data;
    }
}

// TODO: reduce code duplication.
[ProtoContract]
public class PlayerConstellationData
{
    [ProtoMember(1)]
    public HashSet<int> AllocatedNodeIds = [];

    [ProtoMember(2)]
    public int Level = 1;

    [ProtoMember(3)]
    public float Experience;

    [ProtoMember(4)]
    public HashSet<string> AllocatedNodeCodes = [];

    public void ConvertToSaveableData(Constellation constellation)
    {
        AllocatedNodeCodes.Clear();

        foreach (int nodeId in AllocatedNodeIds)
        {
            PassiveNode? node = constellation.GetNodeById(nodeId);
            if (node == null) continue;
            AllocatedNodeCodes.Add(node.Code);
        }

        AllocatedNodeIds.Clear();
    }

    public void ConvertToLoadedData(Constellation constellation)
    {
        AllocatedNodeIds.Clear();

        foreach (string nodeCode in AllocatedNodeCodes)
        {
            PassiveNode? node = constellation.GetNodeByCode(nodeCode);
            if (node == null) continue;
            AllocatedNodeIds.Add(node.Id);
        }

        AllocatedNodeCodes.Clear();
    }

    /// <summary>
    /// Check if a node is a start node or connected to an allocated node.
    /// </summary>
    public bool IsNodeAllocatable(PassiveNode node)
    {
        if (node.StartNode) return true;

        foreach (PassiveNode connection in node.Connections)
        {
            if (AllocatedNodeIds.Contains(connection.Id)) return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a node is a start node or connected to an allocated node, for client.
    /// </summary>
    public bool IsNodeAllocatable(PassiveNode node, HashSet<PassiveNode> pendingNodes, bool exceptNodes = false)
    {
        if (node.StartNode) return true;

        if (exceptNodes)
        {
            foreach (PassiveNode connection in node.Connections)
            {
                if (AllocatedNodeIds.Contains(connection.Id) && !pendingNodes.Contains(connection)) return true;
            }
        }
        else
        {
            foreach (PassiveNode connection in node.Connections)
            {
                if (AllocatedNodeIds.Contains(connection.Id) || pendingNodes.Contains(connection)) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Use graph to check if node can be unallocated without breaking allocation of other nodes.
    /// </summary>
    public bool IsNodeUnallocatable(PassiveNode node)
    {
        PassiveNode? firstConnection = node.Connections.Where(x => AllocatedNodeIds.Contains(x.Id)).FirstOrDefault();
        if (firstConnection == null) return true;

        int oldNodeCount = 0;
        HashSet<int> foundIds = [];
        Queue<PassiveNode> nodeQueue = [];

        nodeQueue.Enqueue(node);

        // Graph through node counting all nodes currently allocated.
        while (nodeQueue.Count > 0)
        {
            PassiveNode currentNode = nodeQueue.Dequeue();
            if (foundIds.Contains(currentNode.Id)) continue;

            foundIds.Add(currentNode.Id);
            oldNodeCount++;

            foreach (PassiveNode connection in currentNode.Connections)
            {
                if (AllocatedNodeIds.Contains(connection.Id))
                {
                    nodeQueue.Enqueue(connection);
                }
            }
        }

        nodeQueue.Enqueue(firstConnection);
        foundIds.Clear();
        int newNodeCount = 0;

        // Path through only the first child and skip the current node.
        while (nodeQueue.Count > 0)
        {
            PassiveNode currentNode = nodeQueue.Dequeue();
            if (foundIds.Contains(currentNode.Id) || currentNode == node) continue;

            foundIds.Add(currentNode.Id);
            newNodeCount++;

            foreach (PassiveNode connection in currentNode.Connections)
            {
                if (AllocatedNodeIds.Contains(connection.Id))
                {
                    nodeQueue.Enqueue(connection);
                }
            }
        }

        // Should path through all but one node (the removed one).
        return newNodeCount == oldNodeCount - 1 && !node.StartNode;
    }

    /// <summary>
    /// Use graph to check if node can be unallocated without breaking allocation of other nodes, for client.
    /// </summary>
    public bool IsNodeUnallocatable(PassiveNode node, HashSet<PassiveNode> pendingNodes, bool exceptNodes = false)
    {
        PassiveNode? firstConnection = !exceptNodes
            ? node.Connections.FirstOrDefault(x => AllocatedNodeIds.Contains(x.Id) || pendingNodes.Contains(x))
            : node.Connections.FirstOrDefault(x => AllocatedNodeIds.Contains(x.Id) && !pendingNodes.Contains(x));
        if (firstConnection == null) return true;

        int oldNodeCount = 0;
        HashSet<int> foundIds = [];
        Queue<PassiveNode> nodeQueue = [];

        nodeQueue.Enqueue(node);

        // Graph through node counting all nodes currently allocated.
        while (nodeQueue.Count > 0)
        {
            PassiveNode currentNode = nodeQueue.Dequeue();
            if (foundIds.Contains(currentNode.Id)) continue;

            foundIds.Add(currentNode.Id);
            oldNodeCount++;

            foreach (PassiveNode connection in currentNode.Connections)
            {
                if (exceptNodes)
                {
                    if (AllocatedNodeIds.Contains(connection.Id) && !pendingNodes.Contains(connection))
                    {
                        nodeQueue.Enqueue(connection);
                    }
                }
                else
                {
                    if (AllocatedNodeIds.Contains(connection.Id) || pendingNodes.Contains(connection))
                    {
                        nodeQueue.Enqueue(connection);
                    }
                }
            }
        }

        nodeQueue.Enqueue(firstConnection);
        foundIds.Clear();
        int newNodeCount = 0;

        // Path through only the first child and skip the current node.
        while (nodeQueue.Count > 0)
        {
            PassiveNode currentNode = nodeQueue.Dequeue();
            if (foundIds.Contains(currentNode.Id) || currentNode == node) continue;

            foundIds.Add(currentNode.Id);
            newNodeCount++;

            foreach (PassiveNode connection in currentNode.Connections)
            {
                if (exceptNodes)
                {
                    if (AllocatedNodeIds.Contains(connection.Id) && !pendingNodes.Contains(connection))
                    {
                        nodeQueue.Enqueue(connection);
                    }
                }
                else
                {
                    if (AllocatedNodeIds.Contains(connection.Id) || pendingNodes.Contains(connection))
                    {
                        nodeQueue.Enqueue(connection);
                    }
                }
            }
        }

        // Should path through all but one node (the removed one).
        return newNodeCount == oldNodeCount - 1 && !node.StartNode;
    }
}
