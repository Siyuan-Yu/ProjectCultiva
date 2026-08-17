using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战略接战空间判定（唯一真源）。
    /// 过路／同路 ≠ 接战；须与敌军栈在节点或道路进度上真正重合。
    /// 自动弹窗只由主动攻击／追击抵达触发，不在此处做「同路即遇」。
    /// </summary>
    public static class StrategicEngageRules
    {
        public const float RouteProgressEpsilon = 0.05f;

        /// <summary>单人是否已与该栈处于可接战位置。</summary>
        public static bool IsAgentColocatedWithStack(
            SimulationWorld world,
            WorldAgentPresence p,
            ArmyStack stack)
        {
            if (world == null || p == null || stack == null)
                return false;

            // 节点驻军：双方同节点
            if (!stack.IsRoutePositioned && !string.IsNullOrEmpty(stack.NodeId))
            {
                return (p.Mode == PartyWorldPresenceMode.AtNode ||
                        p.Mode == PartyWorldPresenceMode.InEncounter) &&
                       string.Equals(p.NodeId, stack.NodeId, StringComparison.Ordinal);
            }

            // 道路上（行军中或路锚）：同 Route 且进度足够近
            if (!stack.IsRoutePositioned || string.IsNullOrEmpty(stack.RouteId))
                return false;

            var stackProgress = stack.GetRouteDisplayProgress();

            // 仍在端点节点上、且敌军就在该端点附近
            if (p.Mode == PartyWorldPresenceMode.AtNode)
            {
                if (stackProgress <= RouteProgressEpsilon &&
                    string.Equals(p.NodeId, stack.NodeId, StringComparison.Ordinal))
                    return true;
                if (stackProgress >= 1f - RouteProgressEpsilon &&
                    string.Equals(p.NodeId, stack.DestNodeId, StringComparison.Ordinal))
                    return true;
                return false;
            }

            if (string.IsNullOrEmpty(p.RouteId) ||
                !string.Equals(p.RouteId, stack.RouteId, StringComparison.Ordinal))
                return false;

            if (p.Mode != PartyWorldPresenceMode.Traveling &&
                p.Mode != PartyWorldPresenceMode.RouteAnchored &&
                p.Mode != PartyWorldPresenceMode.InEncounter)
                return false;

            var playerProgress = GetAgentRouteProgress(p);
            return Math.Abs(playerProgress - stackProgress) <= RouteProgressEpsilon;
        }

        public static bool CanEngageStackNow(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack)
        {
            if (world == null || stack == null || party == null || party.Count == 0)
                return false;

            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone ||
                    !world.WorldPresence.TryGet(party[i], out var p) ||
                    p == null)
                    continue;
                if (IsAgentColocatedWithStack(world, p, stack))
                    return true;
            }

            return false;
        }

        public static void CollectPartyReadyToEngageStack(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack,
            List<EntityId> into)
        {
            into.Clear();
            if (world == null || stack == null || party == null || into == null)
                return;

            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone)
                    continue;
                if (!world.WorldPresence.TryGet(party[i], out var p) || p == null)
                    continue;
                if (!IsAgentColocatedWithStack(world, p, stack))
                    continue;
                into.Add(party[i]);
            }
        }

        public static float GetAgentRouteProgress(WorldAgentPresence p)
        {
            if (p == null)
                return 0f;
            if (p.Mode == PartyWorldPresenceMode.RouteAnchored ||
                p.Mode == PartyWorldPresenceMode.InEncounter)
                return p.RouteAnchorProgress >= 0f ? p.RouteAnchorProgress : p.TravelProgress;
            return p.TravelProgress;
        }
    }
}
