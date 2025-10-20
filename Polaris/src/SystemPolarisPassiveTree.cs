using ProtoBuf;
using System;
using System.Collections.Generic;
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

        foreach (Constellation constellation in constellations)
        {
            PlayerConstellationData constData = data.GetConstellation(constellation.Name);

            foreach (int nodeId in constData.AllocatedNodeIds)
            {
                PassiveNode? node = constellation.GetNodeById(nodeId);
                node?.ContributeStats(context);
            }
        }
    }

    public override void Initialize()
    {
        // Initialize base constellations.
        Constellation survival = new("Survival");
        survival.SetColor(1f, 0.7f, 0.7f, 1f);
        AddConstellation(survival);

        PassiveNode surv1 = new FreeNode("", new NodePosition()).AddTo(survival).MakeStartNode();
        PassiveNode surv2 = new AdditiveValueNode("movespeed", "walkspeed", 0.05f, surv1.GetOffsetPosition(50, 50)).AddTo(survival).AddParentConnection(surv1);
        new AdditiveValueNode("movespeed", "walkspeed", 0.1f, surv2.GetOffsetPosition(40, 60)).AddTo(survival).AddParentConnection(surv2);

        Constellation combat = new("Combat");
        combat.SetColor(1f, 0.3f, 0f, 1f);
        AddConstellation(combat);

        PassiveNode combat1 = new FreeNode("", new NodePosition()).AddTo(combat).MakeStartNode();
        new AdditiveValueNode("meleedamage", "meleeWeaponsDamage", 0.1f, combat1.GetOffsetPosition(50, 50)).AddTo(combat).AddParentConnection(combat1);
        new AdditiveValueNode("meleedamage", "meleeWeaponsDamage", 0.1f, combat1.GetOffsetPosition(50, -50)).AddTo(combat).AddParentConnection(combat1);
        new AdditiveValueNode("meleedamage", "meleeWeaponsDamage", 0.1f, combat1.GetOffsetPosition(100, 50)).AddTo(combat).AddParentConnection(combat1);

        Constellation time = new("Time");
        time.SetColor(0.2f, 1f, 0.6f, 0.5f);
        AddConstellation(time);
        PassiveNode time1 = new FreeNode("", new NodePosition()).AddTo(time).MakeStartNode();
        new AdditiveValueNode("miningspeed", "miningSpeedMul", 0.1f, time1.GetOffsetPosition(50, 50)).AddTo(time).AddParentConnection(time1);
        new AdditiveValueNode("miningspeed", "miningSpeedMul", 0.1f, time1.GetOffsetPosition(50, -50)).AddTo(time).AddParentConnection(time1);
        new AdditiveValueNode("miningspeed", "miningSpeedMul", 0.1f, time1.GetOffsetPosition(300, 50)).AddTo(time).AddParentConnection(time1);
    }

    protected override void RegisterMessages(INetworkChannel channel)
    {
        channel
            .RegisterMessageType<PlayerPolarisData>()
            .RegisterMessageType<NodeAllocationRequest>();
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
    }

    protected override void RegisterServerMessages(IServerNetworkChannel channel)
    {
        channel.SetMessageHandler<NodeAllocationRequest>((player, p) =>
        {
            if (!constellationByName.TryGetValue(p.ConstellationName, out Constellation? constellation)) return;
            PlayerPolarisData data = GetPlayerData(player.PlayerUID);
            PlayerConstellationData constData = data.GetConstellation(p.ConstellationName);

            PassiveNode? node = constellation.GetNodeById(p.NodeId);
            if (node == null) return;

            if (p.Allocate)
            {
                if (!constData.IsNodeAllocatable(node)) return;
                constData.AllocatedNodeIds.Add(p.NodeId);
            }
            else
            {
                if (!constData.IsNodeUnallocatable(node)) return;
                constData.AllocatedNodeIds.Remove(p.NodeId);
            }

            // Allocation successful, echo back to the player.
            SendPacket(data, player);

            // Do stat re-calculation here, now that something is changed. Passive bonuses are server only. Effects or watched attribute booleans will determine client behavior.
            RecalculatePlayerStats(player.Entity);
        });
    }

    /// <summary>
    /// Adds experience to a constellation, triggers events.
    /// </summary>
    public void AddExperience(string constellationName, string uid, float amount)
    {
        PlayerConstellationData data = GetConstellationData(constellationName, uid);
        data.Experience += amount;

        while (data.Experience >= 100f * data.Level)
        {
            data.Experience -= 100f * data.Level;
            data.Level++;
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
                playerDataByUid[uid] = data;
            }
        }
    }
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

            if (AllocatedNodeIds.Contains(currentNode.Id))
            {
                foreach (PassiveNode connection in currentNode.Connections)
                {
                    nodeQueue.Enqueue(connection);
                }
            }
        }

        nodeQueue.Enqueue(node.Connections[0]);
        foundIds.Clear();
        int newNodeCount = 0;

        // Path through only the first child and skip the current node.
        while (nodeQueue.Count > 0)
        {
            PassiveNode currentNode = nodeQueue.Dequeue();
            if (foundIds.Contains(currentNode.Id) || currentNode == node) continue;

            foundIds.Add(currentNode.Id);
            newNodeCount++;

            if (AllocatedNodeIds.Contains(currentNode.Id))
            {
                foreach (PassiveNode connection in currentNode.Connections)
                {
                    nodeQueue.Enqueue(connection);
                }
            }
        }

        // Should path through all but one node (the removed one).
        return newNodeCount == oldNodeCount - 1;
    }

    /// <summary>
    /// Use graph to check if node can be unallocated without breaking allocation of other nodes, for client.
    /// </summary>
    public bool IsNodeUnallocatable(PassiveNode node, HashSet<PassiveNode> pendingNodes)
    {
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

            if (AllocatedNodeIds.Contains(currentNode.Id) || pendingNodes.Contains(currentNode))
            {
                foreach (PassiveNode connection in currentNode.Connections)
                {
                    nodeQueue.Enqueue(connection);
                }
            }
        }

        nodeQueue.Enqueue(node.Connections[0]);
        foundIds.Clear();
        int newNodeCount = 0;

        // Path through only the first child and skip the current node.
        while (nodeQueue.Count > 0)
        {
            PassiveNode currentNode = nodeQueue.Dequeue();
            if (foundIds.Contains(currentNode.Id) || currentNode == node) continue;

            foundIds.Add(currentNode.Id);
            newNodeCount++;

            if (AllocatedNodeIds.Contains(currentNode.Id) || pendingNodes.Contains(currentNode))
            {
                foreach (PassiveNode connection in currentNode.Connections)
                {
                    nodeQueue.Enqueue(connection);
                }
            }
        }

        // Should path through all but one node (the removed one).
        return newNodeCount == oldNodeCount - 1;
    }
}