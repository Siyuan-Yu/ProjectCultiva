using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase H：Node Defense 接口（数值公式 DEFER）。</summary>
    public static class NodeDefenseService
    {
        public static int CountResidents(SimulationWorld world, string nodeId)
        {
            if (world?.WorldPresence == null || string.IsNullOrEmpty(nodeId))
                return 0;
            var count = 0;
            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.EntityId.IsNone)
                    continue;
                if (presence.Mode != PartyWorldPresenceMode.AtNode)
                    continue;
                if (!string.Equals(presence.NodeId, nodeId, StringComparison.Ordinal))
                    continue;
                count++;
            }

            return count;
        }

        public static int CountGarrisonedArmies(SimulationWorld world, string nodeId, string ownerFactionId)
        {
            if (world?.Strategic?.FormalArmies == null || string.IsNullOrEmpty(nodeId))
                return 0;
            var count = 0;
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null)
                    continue;
                if (!string.Equals(army.NodeId, nodeId, StringComparison.Ordinal))
                    continue;
                if (army.State != FormalArmyState.AtNode && army.State != FormalArmyState.Garrisoned)
                    continue;
                if (!string.IsNullOrEmpty(ownerFactionId) &&
                    !string.Equals(army.FactionId, ownerFactionId, StringComparison.Ordinal))
                    continue;
                count++;
            }

            return count;
        }

        public static int EstimateDefenseStrength(
            SimulationWorld world,
            string nodeId,
            string ownerFactionId)
        {
            var residents = CountResidents(world, nodeId);
            var garrisons = CountGarrisonedArmies(world, nodeId, ownerFactionId);
            return residents + garrisons * NodeDefenseConfig.GarrisonWeightPlaceholder;
        }
    }

    /// <summary>占位配置：完整 Node Defense 公式 DEFER。</summary>
    public static class NodeDefenseConfig
    {
        public static int GarrisonWeightPlaceholder { get; set; } = 4;
    }
}
