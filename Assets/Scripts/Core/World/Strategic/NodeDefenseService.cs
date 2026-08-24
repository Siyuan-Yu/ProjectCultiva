using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase H：地点防御力统计（参数名 nodeId 兼容 SiteId）。
    /// 完整攻防公式 DEFER；Capture 门禁已接 WarGate。
    /// </summary>
    public static class NodeDefenseService
    {
        public static int CountResidents(SimulationWorld world, string nodeId)
        {
            if (world?.WorldPresence == null || string.IsNullOrEmpty(nodeId))
                return 0;

            if (HexStrategicRuntime.IsActive(world) &&
                world.Strategic.Sites.TryGet(nodeId, out var site) &&
                site != null)
                return StrategicNodeAccessService.CountPartyMembersAtSite(world, site.SiteId);

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

            if (HexStrategicRuntime.IsActive(world) &&
                world.Strategic.Sites.TryGet(nodeId, out var site) &&
                site != null)
                return CountGarrisonedArmiesAtSite(world, site, ownerFactionId);

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

        static int CountGarrisonedArmiesAtSite(
            SimulationWorld world,
            WorldSite site,
            string ownerFactionId)
        {
            var count = 0;
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !army.UsesHexStrategicPosition)
                    continue;
                if (!site.OccupiesHex(army.CurrentHex))
                    continue;
                if (army.State != FormalArmyState.Idle &&
                    army.State != FormalArmyState.Moving &&
                    army.State != FormalArmyState.Garrisoned)
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
