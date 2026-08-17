using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>宏观节点 LocalMap 准入：有我方在场即可进（暂不做敌对封锁）。</summary>
    public static class StrategicNodeAccessService
    {
        public static bool HasPartyMemberAtNode(SimulationWorld world, string nodeId)
        {
            if (world == null || string.IsNullOrEmpty(nodeId))
                return false;

            foreach (var kv in world.WorldPresence.All)
            {
                if (IsPartyMemberAssociatedWithNode(world, kv.Value, nodeId))
                    return true;
            }

            return false;
        }

        public static int CountPartyMembersAtNode(SimulationWorld world, string nodeId)
        {
            if (world == null || string.IsNullOrEmpty(nodeId))
                return 0;
            var count = 0;
            foreach (var kv in world.WorldPresence.All)
            {
                if (IsPartyMemberAssociatedWithNode(world, kv.Value, nodeId))
                    count++;
            }

            return count;
        }

        public static string BuildNodeDetailText(SimulationWorld world, WorldNodeState node)
        {
            if (node == null)
                return string.Empty;
            var lines = DescribeNode(world, node);
            var here = CountPartyMembersAtNode(world, node.Id);
            lines += "\n我方在场：" + (here > 0 ? here + " 人" : "无");
            if (!string.IsNullOrEmpty(node.LocalMapId))
                lines += "\n场景：" + node.LocalMapId;
            else
                lines += "\n场景：" + WorldTravelService.PlaceholderLocalMapId + "（占位）";
            var access = CanEnterNodeLocalMap(world, node.Id);
            lines += access.IsSuccess ? "\n可进入 LocalMap" : "\n" + access.Error.Message;
            return lines;
        }

        public static Result CanEnterNodeLocalMap(SimulationWorld world, string nodeId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (string.IsNullOrEmpty(nodeId))
                return Result.Failure(ErrorCode.InvalidArgument, "nodeId required.");
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                return Result.Failure(ErrorCode.NotFound, "World node missing.", nodeId);

            if (!HasPartyMemberAtNode(world, nodeId))
                return Result.Failure(ErrorCode.InvalidOperation, "无己方角色在此节点，无法进入场景。");

            return Result.Success();
        }

        public static string DescribeNode(SimulationWorld world, WorldNodeState node)
        {
            if (node == null)
                return string.Empty;
            return string.IsNullOrEmpty(node.Name) ? node.Id : node.Name;
        }

        public static string ResolveStackTravelTarget(ArmyStack stack)
        {
            if (stack == null)
                return string.Empty;
            if (stack.IsTraveling && !string.IsNullOrEmpty(stack.DestNodeId))
                return stack.DestNodeId;
            if (stack.IsRouteAnchored && !string.IsNullOrEmpty(stack.DestNodeId))
                return stack.DestNodeId;
            return stack.NodeId ?? string.Empty;
        }

        public static string DescribeStackTravelTarget(SimulationWorld world, ArmyStack stack)
        {
            if (stack == null)
                return string.Empty;
            var stackName = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            if (stack.IsTraveling && world?.WorldGraph != null &&
                !string.IsNullOrEmpty(stack.DestNodeId) &&
                world.WorldGraph.TryGetNode(stack.DestNodeId, out var dest))
            {
                var destName = string.IsNullOrEmpty(dest.Name) ? dest.Id : dest.Name;
                return "追击「" + stackName + "」至 " + destName;
            }

            if (!string.IsNullOrEmpty(stack.NodeId) &&
                world?.WorldGraph != null &&
                world.WorldGraph.TryGetNode(stack.NodeId, out var node))
            {
                var nodeName = string.IsNullOrEmpty(node.Name) ? node.Id : node.Name;
                return "接战「" + stackName + "」@" + nodeName;
            }

            return "接战「" + stackName + "」";
        }

        public static bool CanEngageStackNow(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack) =>
            StrategicEngageRules.CanEngageStackNow(world, party, stack);

        public static bool IsAgentAtStackAnchor(
            SimulationWorld world,
            WorldAgentPresence p,
            ArmyStack stack) =>
            StrategicEngageRules.IsAgentColocatedWithStack(world, p, stack);

        public static bool IsPartyAtStackAnchor(
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
                    p == null ||
                    !StrategicEngageRules.IsAgentColocatedWithStack(world, p, stack))
                    return false;
            }

            return true;
        }

        public static void CollectPartyReadyToEngageStack(
            SimulationWorld world,
            IReadOnlyList<EntityId> party,
            ArmyStack stack,
            List<EntityId> into) =>
            StrategicEngageRules.CollectPartyReadyToEngageStack(world, party, stack, into);

        static bool IsPartyMemberAssociatedWithNode(
            SimulationWorld world,
            WorldAgentPresence p,
            string nodeId)
        {
            if (p == null || string.IsNullOrEmpty(nodeId) || !IsPlayerAgent(world, p.EntityId))
                return false;

            switch (p.Mode)
            {
                case PartyWorldPresenceMode.AtNode:
                case PartyWorldPresenceMode.InEncounter:
                    return string.Equals(p.NodeId, nodeId, StringComparison.Ordinal);
                case PartyWorldPresenceMode.RouteAnchored:
                    if (p.RouteAnchorProgress <= 0.01f &&
                        string.Equals(p.NodeId, nodeId, StringComparison.Ordinal))
                        return true;
                    if (p.RouteAnchorProgress >= 0.99f &&
                        string.Equals(p.DestNodeId, nodeId, StringComparison.Ordinal))
                        return true;
                    return false;
                default:
                    return false;
            }
        }

        static bool IsPlayerAgent(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            return (entity.Tags & EntityTag.Npc) == 0;
        }
    }
}
