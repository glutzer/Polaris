using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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

    public event Action<PlayerPolarisData>? OnClientDataUpdated;
    public event Action<Constellation, float, int>? OnClientExperienceGain;

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
        PassiveContext context = new(player);

        context.SkillBehavior.ResetForPassiveChange();

        foreach (PassiveAggregator aggregator in aggregators)
        {
            aggregator.RemoveStats(context);
        }

        if (onlyRemove) return;

        // Gather stats from player's passive information...
        PlayerPolarisData data = GetPlayerData(context.Player.PlayerUID);

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

        // TODO: move these somewhere more modular.
        player.GetHealth().MarkDirty();
    }

    public override void Initialize()
    {
        // Main survival tree.
        Constellation survival = new Constellation("Survival").SetColor(1f, 0.7f, 0.7f, 1f).AddStartNode();
        AddConstellation(survival);

        // Temporal tree.
        Constellation time = new Constellation("Time").SetColor(0f, 1f, 0.6f, 0.5f).AddStartNode();
        AddConstellation(time);

        // Mining and digging combined.
        Constellation excavation = new Constellation("Excavation").SetColor(0.6f, 0.4f, 0.4f, 1f).AddStartNode();
        AddConstellation(excavation);

        // Tree stuff.
        Constellation forestry = new Constellation("Forestry").SetColor(0f, 0.6f, 0f, 1f).AddStartNode();
        AddConstellation(forestry);

        // Farming.
        Constellation horticulture = new Constellation("Horticulture").SetColor(0.2f, 1f, 0.2f, 1f).AddStartNode();
        AddConstellation(horticulture);

        // Hunting.
        Constellation hunting = new Constellation("Hunting").SetColor(0.6f, 0.2f, 0.2f, 0.75f).AddStartNode();
        AddConstellation(hunting);

        // Smithing.
        Constellation smithing = new Constellation("Smithing").SetColor(0.7f, 0.4f, 0.2f, 1f).AddStartNode();
        AddConstellation(smithing);

        // Clay/knapping.
        Constellation forming = new Constellation("Forming").SetColor(0.1f, 0.1f, 0.3f, 1f).AddStartNode();
        AddConstellation(forming);

        // Cooking.
        Constellation cooking = new Constellation("Cooking").SetColor(0.7f, 0.7f, 0f, 1f).AddStartNode();
        AddConstellation(cooking);

        // Crafting - leatherworking and sewing.
        Constellation crafting = new Constellation("Crafting").SetColor(0.7f, 0.3f, 0.5f, 1f).AddStartNode();
        AddConstellation(crafting);

        // Trade.
        Constellation trade = new Constellation("Trade").SetColor(1f, 0f, 1f, 1f).AddStartNode();
        AddConstellation(trade);

        // Survival passives.
        PassiveNode.Create("Movement Speed", "move1", 100, 100, survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("start").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Movement Speed", "move2", 200, 150, survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("move1").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Movement Speed", "move3", 300, 175, survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("move2").AddLevelRequirement("Survival", 7);
        PassiveNode.Create("Movement Speed", "move4", 400, 190, survival).AddMultiplicativeStat("walkspeed", 1.5f).AddParent("move3").AddLevelRequirement("Survival", 9).NotableStyle();

        PassiveNode.Create("Health", "health1", -100, 100, survival).AddAdditiveExtraStat("healthMultiplier", 0.05f).AddParent("start");
        PassiveNode.Create("Health", "health2", -200, 150, survival).AddAdditiveExtraStat("healthMultiplier", 0.05f).AddParent("health1").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Health", "health3", -300, 175, survival).AddAdditiveExtraStat("healthMultiplier", 0.05f).AddParent("health2").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Health", "health4", -400, 190, survival).AddMultiplicativeExtraStat("healthMultiplier", 1.2f).AddParent("health3").AddLevelRequirement("Survival", 7).NotableStyle();

        PassiveNode.Create("Saturation", "sat1", 0, 100, survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("start");
        PassiveNode.Create("Saturation", "sat2", 0, 200, survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("sat1").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Saturation", "sat3", 0, 300, survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("sat2").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Saturation", "sat4", 0, 400, survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("sat3").AddLevelRequirement("Survival", 7);
        PassiveNode.Create("Saturation", "sat5", 0, 500, survival).AddAdditiveExtraStat("satMultiplier", 0.2f).AddParent("sat4").AddLevelRequirement("Survival", 9);
        PassiveNode.Create("Saturation", "sat6", 0, 600, survival).AddMultiplicativeExtraStat("satMultiplier", 1.5f).AddParent("sat5").AddLevelRequirement("Survival", 20).NotableStyle();

        PassiveNode.Create("Primalist", "primalist", -200, -200, survival).AddParent("start").AddSkillStat("primalist", 1, """
            You can eat raw meat
            Grain provides no nutrition
            """).KeystoneStyle();

        // Crafting passives.
        PassiveNode.Create("Sewing Effectiveness", "sewing1", -100, -100, crafting).AddAdditiveExtraStat("sewingeffectiveness", 0.1f).AddLevelRequirement("Crafting", 2).AddParent("start");
        PassiveNode.Create("Sewing Effectiveness", "sewing2", -200, -200, crafting).AddAdditiveExtraStat("sewingeffectiveness", 0.1f).AddLevelRequirement("Crafting", 3).AddParent("sewing1");
        PassiveNode.Create("Sewing Effectiveness", "sewing3", -300, -300, crafting).AddAdditiveExtraStat("sewingeffectiveness", 0.1f).AddLevelRequirement("Crafting", 4).AddParent("sewing2");

        PassiveNode.Create("Clothier", "clothier", -300, 0, crafting).AddParent("sewing3").AddSkillStat("clothier", 1, """
            You may sew certain kinds of clothing
            """).AddTagExclusiveRequirement("class", 2).WithTag("class").KeystoneStyle().AddLevelRequirement("Crafting", 5);

        // Smithing passives.
        PassiveNode.Create("Master Smith", "masterSmith1", -300, -100, smithing).AddSkillStat("masterSmith", 1, """
            Your heavy hit also moves 1 voxel of material to the correct position
            """).KeystoneStyle().AddParent("start").AddLevelRequirement("Smithing", 5).AddTagExclusiveRequirement("class", 2).WithTag("class");

        PassiveNode.Create("Master Smith", "masterSmith2", -500, 0, smithing).AddSkillStat("masterSmith", 1, """
            Your heavy hit also moves 1 voxel of material to the correct position
            """).NotableStyle().AddParent("masterSmith1").AddLevelRequirement("Smithing", 10);

        PassiveNode.Create("Cracker", "cracker1", 0, 200, smithing).AddAdditiveExtraStat("bloomeryDrops", 0.1f).AddParent("start").AddLevelRequirement("Smithing", 4);
        PassiveNode.Create("Cracker", "cracker2", 0, 400, smithing).AddAdditiveExtraStat("bloomeryDrops", 0.1f).AddParent("cracker1").AddLevelRequirement("Smithing", 8);

        // Excavation passives.
        PassiveNode.Create("Eroder", "eroder", 100, 200, excavation).AddSkillStat("eroder", 1, """
            Rock does not drop stones
            """).AddParent("start").AddLevelRequirement("Excavation", 5).KeystoneStyle();

        // Time passives.
        PassiveNode.Create("Stable Settler", "stableSettler", 200, 200, time).AddSkillStat("stableSettler", 1, """
            Temporally unstable areas do not affect you near the surface
            """).AddParent("start").AddLevelRequirement("Time", 5).KeystoneStyle();

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
            LoadDataFromWorld();
        }
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

                constData.AllocatedNodeIds.Remove(p.NodeId);
                constData.Level--;
                constData.Experience = newExp;
                data.SetLevelAndKnowledgeFromTotalExp(this);

                OnClientExperienceGain?.Invoke(node.Constellation, -totalExpLoss, constData.Level);
            }

            OnClientDataUpdated?.Invoke(data);
        });

        channel.SetMessageHandler<S2CExpPacket>(p =>
        {
            AddExperience(p.Constellation, MainAPI.Capi.World.Player.PlayerUID, p.ExpGain);
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
    public static void AddExperience(string constellationName, IPlayer player, float amount)
    {
        Instance(MainAPI.Sapi).AddExperience(constellationName, player.PlayerUID, amount);
    }

    /// <summary>
    /// Adds experience to a constellation, triggers events.
    /// Called on client and server.
    /// </summary>
    public void AddExperience(string constellationName, string uid, float amount)
    {
        if (!constellationByName.TryGetValue(constellationName, out Constellation? constellation)) return; // Invalid.

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
        data.Experience += amount;

        bool shouldServerRecalculate = false;

        while (data.Experience >= constellation.GetExpToReachLevel(data.Level + 1) && data.Level < 100)
        {
            float expNeeded = constellation.GetExpToReachLevel(data.Level + 1);

            data.Experience -= expNeeded;
            playerData.Experience += expNeeded;

            data.Level++;
            shouldServerRecalculate = true;
        }

        while (playerData.Experience >= PlayerPolarisData.GetExpToReachLevel(playerData.Level + 1))
        {
            playerData.Experience -= PlayerPolarisData.GetExpToReachLevel(playerData.Level + 1);
            playerData.Level++;
            playerData.SetKnowledgePoints(playerData.KnowledgePoints + 1);
            shouldServerRecalculate = true;
        }

        if (api.Side.IsServer())
        {
            IPlayer? player = api.World.PlayerByUid(uid);
            if (player == null) return;

            S2CExpPacket packet = new()
            {
                Constellation = constellationName,
                ExpGain = amount
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
            OnClientExperienceGain?.Invoke(constellation, amount, data.Level);
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
        while (Experience >= GetExpToReachLevel(Level + 1))
        {
            Experience -= GetExpToReachLevel(Level + 1);
            Level++;
        }

        SetKnowledgePoints(Level - 1 - pointsSpent);
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

    public static float GetExpToReachLevel(int level)
    {
        return 100f * MathF.Pow(level - 1, 2f);
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