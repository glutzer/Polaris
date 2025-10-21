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
public class SystemPolarisPassiveTree : NetworkedGameSystem
{
    private readonly List<Constellation> constellations = [];
    public IEnumerable<Constellation> AllConstellations => constellations;
    private readonly Dictionary<string, Constellation> constellationByName = [];

    private readonly Dictionary<string, PlayerPolarisData> playerDataByUid = [];

    public event Action<PlayerPolarisData>? OnClientDataUpdated;
    public event Action<Constellation, float, int>? OnClientExperienceGain;

    private static SystemPolarisPassiveTree clientInst = null!;
    private static SystemPolarisPassiveTree serverInst = null!;

    public SystemPolarisPassiveTree(bool isServer, ICoreAPI api) : base(isServer, api, "polaristree")
    {
    }

    public static SystemPolarisPassiveTree Instance(ICoreAPI api)
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
                    if (node?.Priority == priority) node.ContributeStats(context);
                }
            }
        }
    }

    public override void Initialize()
    {
        // Survival.
        Constellation survival = new Constellation("Survival").SetColor(1f, 0.7f, 0.7f, 1f);
        AddConstellation(survival);

        new FreeNode("", "start1", new NodePosition(), survival).MakeStartNode();
        new AdditiveValueNode("Movement Speed", "walkspeed", 0.05f, "move1", new NodePosition(100, 100), survival).AddParent("start1").AddLevelRequirement("Survival", 3);
        new AdditiveValueNode("Movement Speed", "walkspeed", 0.05f, "move2", new NodePosition(200, 150), survival).AddParent("move1").AddLevelRequirement("Survival", 5);
        new AdditiveValueNode("Movement Speed", "walkspeed", 0.05f, "move3", new NodePosition(300, 175), survival).AddParent("move2").AddLevelRequirement("Survival", 7);
        new MultiplicativeValueNode("Movement Speed", "walkspeed", 1.5f, "move4", new NodePosition(400, 190), survival).AddParent("move3").AddLevelRequirement("Survival", 9);

        // Time.
        Constellation time = new Constellation("Time").SetColor(0.2f, 1f, 0.6f, 0.5f);
        AddConstellation(time);

        new FreeNode("", "start1", new NodePosition(), time).MakeStartNode();
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

            if (p.Allocate)
                constData.AllocatedNodeIds.Add(p.NodeId);
            else
                constData.AllocatedNodeIds.Remove(p.NodeId);

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

            HashSet<string> allocatedNodes = data.GetAllAllocatedNodeCodes(this);

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
                constData.AllocatedNodeIds.Remove(node.Id);
                data.SetKnowledgePoints(data.KnowledgePoints + node.Cost);
            }

            // Allocation successful, echo back to the player.
            SendPacket(data, player);

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
        int pointsSpent = 0;
        float totalExpGained = 0f;

        foreach (KeyValuePair<string, PlayerConstellationData> constData in data.ConstellationData)
        {
            // Remove invalid node ids.
            if (!constellationByName.TryGetValue(constData.Key, out Constellation? constellation)) continue;

            // Remove allocated nodes that don't exist, get allocation count.
            HashSet<int> validNodeIds = [];
            foreach (PassiveNode node in constellation.AllNodes)
            {
                validNodeIds.Add(node.Id);

                if (constData.Value.AllocatedNodeIds.Contains(node.Id))
                {
                    pointsSpent += node.Cost;
                }
            }
            constData.Value.AllocatedNodeIds.RemoveWhere(id => !validNodeIds.Contains(id));

            // Calculate total exp gained.
            totalExpGained += constellation.GetTotalExpGained(constData.Value.Level, constData.Value.Experience);
        }

        // Remove all non-existent constellations, from old versions.
        List<string> toRemove = [];
        foreach (string constName in data.ConstellationData.Keys)
        {
            if (!constellationByName.ContainsKey(constName))
            {
                toRemove.Add(constName);
            }
        }
        foreach (string constName in toRemove)
        {
            data.ConstellationData.Remove(constName);
        }

        // Set new level from total experience gained.
        data.SetLevelFromTotalExp(totalExpGained);

        // At this point a player may have negative points, but they can simply not spend them until it's positive.
        data.SetKnowledgePoints(data.Level - 1 - pointsSpent);
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

    /// <summary>
    /// Check if any current allocated node relies on this node.
    /// If anything relies on it, it can't be unallocated.
    /// Also check pending nodes, it's not done here.
    /// </summary>
    public bool DoesAnythingRelyOnNode(PassiveNode node, SystemPolarisPassiveTree treeSystem)
    {
        List<PassiveNode> list = GetAllAllocatedNodes(treeSystem);
        string code = node.GetFullCode();
        foreach (PassiveNode allocatedNode in list)
        {
            if (allocatedNode.ReliesOnNode(code)) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns a list of allocated nodes.
    /// </summary>
    public List<PassiveNode> GetAllAllocatedNodes(SystemPolarisPassiveTree treeSystem)
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
    public HashSet<string> GetAllAllocatedNodeCodes(SystemPolarisPassiveTree treeSystem)
    {
        HashSet<string> nodes = [];

        foreach (KeyValuePair<string, PlayerConstellationData> constKvp in ConstellationData)
        {
            Constellation? constellation = treeSystem.GetConstellation(constKvp.Key);
            if (constellation == null) continue;

            PlayerConstellationData constData = constKvp.Value;
            foreach (int nodeId in constData.AllocatedNodeIds)
            {
                PassiveNode? node = constellation.GetNodeById(nodeId);
                if (node == null) continue;
                nodes.Add(node.GetFullCode());
            }
        }

        return nodes;
    }

    public void SetKnowledgePoints(int amount)
    {
        KnowledgePoints = amount;
    }

    public void SetLevelFromTotalExp(float exp)
    {
        Level = 1;
        Experience = exp;
        while (Experience >= GetExpToReachLevel(Level + 1))
        {
            Experience -= GetExpToReachLevel(Level + 1);
            Level++;
        }
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
    public bool IsNodeAllocatable(PassiveNode node, HashSet<PassiveNode> pendingNodes)
    {
        if (node.StartNode) return true;

        foreach (PassiveNode connection in node.Connections)
        {
            if (AllocatedNodeIds.Contains(connection.Id) || pendingNodes.Contains(connection)) return true;
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
    public bool IsNodeUnallocatable(PassiveNode node, HashSet<PassiveNode> pendingNodes)
    {
        PassiveNode? firstConnection = node.Connections.Where(x => AllocatedNodeIds.Contains(x.Id) || pendingNodes.Contains(x)).FirstOrDefault();
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
                if (AllocatedNodeIds.Contains(connection.Id) || pendingNodes.Contains(connection))
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
                if (AllocatedNodeIds.Contains(connection.Id) || pendingNodes.Contains(connection))
                {
                    nodeQueue.Enqueue(connection);
                }
            }
        }

        // Should path through all but one node (the removed one).
        return newNodeCount == oldNodeCount - 1 && !node.StartNode;
    }
}