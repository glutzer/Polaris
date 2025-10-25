using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Polaris;

/// <summary>
/// Loads passive tree from mods, tells client to allocate nodes for him on success.
/// </summary>
[GameSystem]
public class Polaris : NetworkedGameSystem
{
    private readonly List<Constellation> constellations = [];
    public IEnumerable<Constellation> AllConstellations => constellations;
    private readonly Dictionary<string, Constellation> constellationByName = [];

    private readonly Dictionary<string, PlayerPolarisData> playerDataByUid = [];

    public event Action<PlayerPolarisData>? OnClientDataUpdated;
    public event Action<Constellation, float, int>? OnClientExperienceGain;

    private static Polaris clientInst = null!;
    private static Polaris serverInst = null!;

    public Polaris(bool isServer, ICoreAPI api) : base(isServer, api, "polaristree")
    {
    }

    public static Polaris Instance(ICoreAPI api)
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

    private void RecalculatePlayerStats(EntityPlayer player)
    {
        if (api.Side != EnumAppSide.Server) throw new Exception("Recalculating on the client.");
        MainAPI.GetGameSystem<SystemPolarisStats>(api.Side).CalculatePlayerStats(player);
    }

    public void GatherPassiveInformation(PassiveContext context)
    {
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
        }
    }

    public override void Initialize()
    {
        // Survival.
        Constellation survival = new Constellation("Survival").SetColor(1f, 0.7f, 0.7f, 1f);
        AddConstellation(survival);

        PassiveNode.Create("", "start1", 0, 0, survival).MakeStartNode().SetCost(0).SetSize(0.8f);
        PassiveNode.Create("Movement Speed", "move1", 100, 100, survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("start1").AddLevelRequirement("Survival", 3);
        PassiveNode.Create("Movement Speed", "move2", 200, 150, survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("move1").AddLevelRequirement("Survival", 5);
        PassiveNode.Create("Movement Speed", "move3", 300, 175, survival).AddAdditiveStat("walkspeed", 0.05f).AddParent("move2").AddLevelRequirement("Survival", 7);
        PassiveNode.Create("Movement Speed", "move4", 400, 190, survival).AddMultiplicativeStat("walkspeed", 1.5f).AddParent("move3").AddLevelRequirement("Survival", 9);

        //clothier

        PassiveNode.Create("Primalist", "primalist", -200, -200, survival).SetSize(2f).AddParent("start1").AddSkillStat("primalist", 1, """
            You can eat raw meat
            Grain provides no nutrition
            """);

        PassiveNode.Create("Clothier", "clothier", -200, 200, survival).SetSize(2f).AddParent("start1").AddSkillStat("primalist", 1, """
            You may sew certain kinds of clothing
            """).AddTagExclusiveRequirement("class", 2).WithTag("class");

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
                float totalExpLoss = constData.Experience + node.Constellation.GetExpToReachLevel(constData.Level);

                constData.AllocatedNodeIds.Remove(p.NodeId);
                constData.Level--;
                constData.Experience = 0f;
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

                constData.AllocatedNodeIds.Remove(node.Id);
                constData.Level--;
                constData.Experience = 0f;
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
            RecalculatePlayerStats(player.Entity);
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
                MainAPI.GetServerSystem<SystemPolarisStats>().CalculatePlayerStats(player.Entity);
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

    public void SaveDataToWorld()
    {
        if (api.Side != EnumAppSide.Server) throw new Exception("Saving on the client.");

        Dictionary<string, byte[]> dataToSave = [];

        foreach (KeyValuePair<string, PlayerPolarisData> kv in playerDataByUid)
        {
            string uid = kv.Key;
            PlayerPolarisData data = kv.Value;
            dataToSave[uid] = SerializerUtil.Serialize(data);
        }

        // Save dataToSave to world storage...
        MainAPI.Sapi.WorldManager.SaveGame.StoreData("polarisplayerdata", dataToSave);
    }

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
                VerifyPlayerData(data);
                playerDataByUid[uid] = data;
            }
        }
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

    public void SetLevelAndKnowledgeFromTotalExp(Polaris tree)
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
    public bool DoesAnythingRelyOnNode(PassiveNode node, Polaris treeSystem, HashSet<PassiveNode> pendingUnallocations)
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
    public List<PassiveNode> GetAllAllocatedNodes(Polaris treeSystem)
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
    public AllocatedNodesInfo GetAllocatedNodesInfo(Polaris treeSystem)
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
        if (firstConnection == null) return node.StartNode;

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
        if (firstConnection == null) return node.StartNode;

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