using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase B/H：组军／解散 Node 合法性。正式规则 = OwnerFactionId 匹配。
    /// Ch01 presence-based 例外见 <see cref="Ch01ScenarioArmyFormationPolicy"/>。
    /// </summary>
    public static class ArmyFormationNodePolicy
    {
        public static bool IsFriendlyNodeForFaction(SimulationWorld world, string nodeId, string factionId)
        {
            if (world == null || string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(factionId))
                return false;
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                return false;

            if (string.IsNullOrEmpty(node.OwnerId))
                return false;

            return string.Equals(node.OwnerId, factionId, StringComparison.Ordinal);
        }

        public static bool HasFactionMemberAtNode(SimulationWorld world, string nodeId, string factionId)
        {
            if (world?.WorldPresence == null || string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(factionId))
                return false;

            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.EntityId.IsNone)
                    continue;
                if (presence.Mode != PartyWorldPresenceMode.AtNode)
                    continue;
                if (!string.Equals(presence.NodeId, nodeId, StringComparison.Ordinal))
                    continue;
                var charFaction = ArmyService.ResolveCharacterFactionId(world, presence.EntityId);
                if (string.Equals(charFaction, factionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool TryValidateFriendlyNode(
            SimulationWorld world,
            string factionId,
            string nodeId,
            out GameError error)
        {
            error = default;
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
            {
                error = new GameError(ErrorCode.NotFound, "Node not found.", nodeId);
                return false;
            }

            if (IsFriendlyNodeForFaction(world, nodeId, factionId))
                return true;

            if (!string.IsNullOrEmpty(node.OwnerId))
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "Army operations require friendly node owner.",
                    nodeId + ";owner=" + node.OwnerId + ";faction=" + factionId);
            }
            else
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "Army operations require friendly node (owner or faction presence).",
                    nodeId + ";faction=" + factionId);
            }

            return false;
        }
    }
}
