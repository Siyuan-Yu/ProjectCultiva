using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>宏观节点 LocalMap 准入（138：未抵达／敌占不可进）。</summary>
    public static class StrategicNodeAccessService
    {
        public static bool HasPartyMemberAtNode(SimulationWorld world, string nodeId)
        {
            if (world == null || string.IsNullOrEmpty(nodeId))
                return false;

            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null || p.Mode != PartyWorldPresenceMode.AtNode)
                    continue;
                if (string.Equals(p.NodeId, nodeId, StringComparison.Ordinal) && IsPlayerAgent(world, p.EntityId))
                    return true;
            }

            return false;
        }

        public static bool IsOwnerHostileToPlayer(SimulationWorld world, string ownerId)
        {
            if (world?.Strategic == null || string.IsNullOrEmpty(ownerId))
                return false;
            if (string.Equals(ownerId, world.Strategic.PlayerFactionId, StringComparison.Ordinal))
                return false;
            return world.Strategic.Diplomacy.IsHostile(
                world.Strategic.PlayerFactionId,
                ownerId);
        }

        public static int CountPartyMembersAtNode(SimulationWorld world, string nodeId)
        {
            if (world == null || string.IsNullOrEmpty(nodeId))
                return 0;
            var count = 0;
            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null || p.Mode != PartyWorldPresenceMode.AtNode)
                    continue;
                if (string.Equals(p.NodeId, nodeId, StringComparison.Ordinal) && IsPlayerAgent(world, p.EntityId))
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

            if (!string.IsNullOrEmpty(node.OwnerId) &&
                IsOwnerHostileToPlayer(world, node.OwnerId))
            {
                var ownerName = StrategicFactionCatalog.DisplayName(node.OwnerId);
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "敌占节点（" + ownerName + "），需接战后方可进入。");
            }

            return Result.Success();
        }

        public static string DescribeNode(SimulationWorld world, WorldNodeState node)
        {
            if (node == null)
                return string.Empty;
            var name = string.IsNullOrEmpty(node.Name) ? node.Id : node.Name;
            if (string.IsNullOrEmpty(node.OwnerId))
                return name + " · 无归属";
            var owner = StrategicFactionCatalog.DisplayName(node.OwnerId);
            if (world?.Strategic == null)
                return name + " · " + owner;
            var hostile = IsOwnerHostileToPlayer(world, node.OwnerId);
            return name + " · " + owner + (hostile ? " · 敌对" : "");
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
            ArmyStack stack)
        {
            if (world == null || stack == null || party == null || party.Count == 0)
                return false;

            if (!stack.IsTraveling && !stack.IsRouteAnchored && !string.IsNullOrEmpty(stack.NodeId))
            {
                for (var i = 0; i < party.Count; i++)
                {
                    if (!world.WorldPresence.TryGet(party[i], out var p) || p == null)
                        continue;
                    if (p.Mode == PartyWorldPresenceMode.AtNode &&
                        string.Equals(p.NodeId, stack.NodeId, StringComparison.Ordinal))
                        return true;
                }
            }

            if (stack.IsRouteAnchored && !string.IsNullOrEmpty(stack.RouteId))
            {
                for (var i = 0; i < party.Count; i++)
                {
                    if (!world.WorldPresence.TryGet(party[i], out var p) || p == null)
                        continue;
                    if (p.Mode == PartyWorldPresenceMode.Traveling &&
                        string.Equals(p.RouteId, stack.RouteId, StringComparison.Ordinal) &&
                        p.TravelProgress + 0.02f >= stack.RouteAnchorProgress)
                        return true;
                    if (p.Mode == PartyWorldPresenceMode.AtNode &&
                        !string.IsNullOrEmpty(stack.DestNodeId) &&
                        string.Equals(p.NodeId, stack.DestNodeId, StringComparison.Ordinal))
                        return true;
                }
            }

            if (stack.IsTraveling && !string.IsNullOrEmpty(stack.RouteId))
            {
                for (var i = 0; i < party.Count; i++)
                {
                    if (!world.WorldPresence.TryGet(party[i], out var p) || p == null)
                        continue;
                    if (p.Mode == PartyWorldPresenceMode.Traveling &&
                        string.Equals(p.RouteId, stack.RouteId, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        static bool IsPlayerAgent(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            return (entity.Tags & EntityTag.Npc) == 0;
        }
    }
}
