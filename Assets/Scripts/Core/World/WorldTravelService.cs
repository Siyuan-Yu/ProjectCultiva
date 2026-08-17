using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    /// <summary>WorldGraph 旅行：按角色下令移动；路上按 Tick 推进；无通行令门槛。</summary>
    public static class WorldTravelService
    {
        /// <summary>路程时长基准：ticks ≈ TravelCost * 此值 / Speed（Speed 缺省按 8）。</summary>
        public const int TravelTicksPerCostAtSpeed8 = 24;

        public static Result StartTravel(
            SimulationWorld world,
            IReadOnlyList<EntityId> agents,
            string toNodeId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (agents == null || agents.Count == 0)
                return Result.Failure(ErrorCode.InvalidArgument, "Travel party empty.");
            if (string.IsNullOrWhiteSpace(toNodeId))
                return Result.Failure(ErrorCode.InvalidArgument, "toNodeId required.");
            if (!world.WorldGraph.HasGraph)
                return Result.Failure(ErrorCode.InvalidOperation, "WorldGraph not loaded.");
            if (!world.WorldGraph.TryGetNode(toNodeId, out _))
                return Result.Failure(ErrorCode.NotFound, "Destination world node missing.", toNodeId);

            var started = 0;
            string lastFail = null;
            for (var i = 0; i < agents.Count; i++)
            {
                var one = StartTravelOne(world, agents[i], toNodeId);
                if (one.IsSuccess)
                {
                    started++;
                    continue;
                }

                lastFail = one.Error.Message +
                           (string.IsNullOrEmpty(one.Error.Detail) ? "" : " · " + one.Error.Detail);
            }

            if (started == 0)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    string.IsNullOrEmpty(lastFail) ? "No traveler could move." : lastFail);
            }

            SyncPartyFocus(world);
            return Result.Success();
        }

        public static Result StartTravel(SimulationWorld world, EntityId subject, string toNodeId)
        {
            if (subject.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Travel subject required.");
            return StartTravel(world, new[] { subject }, toNodeId);
        }

        static Result StartTravelOne(SimulationWorld world, EntityId id, string toNodeId)
        {
            if (id.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid traveler.");
            if (!world.WorldPresence.TryGet(id, out var p) || string.IsNullOrEmpty(p.NodeId))
                return Result.Failure(ErrorCode.NotFound, "Traveler has no world node.", id.Value.ToString());
            if (p.Mode == PartyWorldPresenceMode.Traveling)
                return Result.Failure(ErrorCode.InvalidOperation, "Already traveling.", id.Value.ToString());
            if (p.Mode == PartyWorldPresenceMode.DepartingLocalMap)
                return Result.Failure(ErrorCode.InvalidOperation, "Already departing local map.", id.Value.ToString());
            if (p.Mode == PartyWorldPresenceMode.InEncounter)
                return Result.Failure(ErrorCode.InvalidOperation, "In encounter.", id.Value.ToString());
            if (string.Equals(p.NodeId, toNodeId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidArgument, "Already at destination node.");
            if (!world.WorldGraph.TryFindRoute(p.NodeId, toNodeId, out var route))
                return Result.Failure(ErrorCode.InvalidArgument, "No direct route between nodes.");

            var gate = CanTraverse(route);
            if (gate.IsFailure)
                return gate;

            var cost = route.TravelCost > 0 ? route.TravelCost : 1;
            var ticks = ComputeTravelTicks(world, id, cost);
            p.Mode = PartyWorldPresenceMode.Traveling;
            p.RouteId = route.Id;
            p.DestNodeId = toNodeId;
            p.TravelTotalTicks = ticks;
            p.RemainingTravelTicks = ticks;
            return Result.Success();
        }

        /// <summary>确认出行后：标记正在 LocalMap 走向边缘（尚未上宏观路）。</summary>
        public static Result MarkDepartingLocalMap(SimulationWorld world, EntityId id, string toNodeId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (id.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid traveler.");
            if (string.IsNullOrWhiteSpace(toNodeId))
                return Result.Failure(ErrorCode.InvalidArgument, "toNodeId required.");
            if (!world.WorldPresence.TryGet(id, out var p) || string.IsNullOrEmpty(p.NodeId))
                return Result.Failure(ErrorCode.NotFound, "Traveler has no world node.", id.Value.ToString());
            if (p.Mode == PartyWorldPresenceMode.Traveling)
                return Result.Failure(ErrorCode.InvalidOperation, "Already traveling.");
            if (p.Mode == PartyWorldPresenceMode.InEncounter)
                return Result.Failure(ErrorCode.InvalidOperation, "In encounter.");
            if (!world.WorldGraph.TryFindRoute(p.NodeId, toNodeId, out var route))
                return Result.Failure(ErrorCode.InvalidArgument, "No direct route between nodes.");
            var gate = CanTraverse(route);
            if (gate.IsFailure)
                return gate;

            p.Mode = PartyWorldPresenceMode.DepartingLocalMap;
            p.DestNodeId = toNodeId;
            p.RouteId = string.Empty;
            p.RemainingTravelTicks = 0;
            p.TravelTotalTicks = 0;
            return Result.Success();
        }

        /// <summary>走到 LocalMap 边缘后：转入宏观 Traveling。</summary>
        public static Result CommitTravelAfterLocalExit(SimulationWorld world, EntityId id)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.WorldPresence.TryGet(id, out var p) || p == null)
                return Result.Failure(ErrorCode.NotFound, "Traveler presence missing.");
            if (p.Mode != PartyWorldPresenceMode.DepartingLocalMap)
                return Result.Failure(ErrorCode.InvalidOperation, "Not departing local map.");
            var dest = p.DestNodeId;
            if (string.IsNullOrEmpty(dest))
                return Result.Failure(ErrorCode.InvalidOperation, "Departure destination missing.");

            // 临时回到 AtNode 以便 StartTravelOne 校验
            p.Mode = PartyWorldPresenceMode.AtNode;
            p.DestNodeId = string.Empty;
            var started = StartTravelOne(world, id, dest);
            if (started.IsFailure)
            {
                p.Mode = PartyWorldPresenceMode.DepartingLocalMap;
                p.DestNodeId = dest;
                return started;
            }

            SyncPartyFocus(world);
            return Result.Success();
        }

        public static int ComputeTravelTicks(SimulationWorld world, EntityId id, int travelCost)
        {
            var cost = travelCost > 0 ? travelCost : 1;
            var speed = 8;
            if (world != null &&
                !id.IsNone &&
                world.Entities.TryGet(id, out var ent) &&
                ent != null &&
                ent.TryGet<AttributesComponent>(out var attrs) &&
                attrs != null)
            {
                var s = attrs.GetFinal(AttributeId.Speed);
                if (s > 0)
                    speed = s;
            }

            // Speed 8 → TravelCost * 24；更高更快。至少 4 tick，避免瞬移感。
            var raw = (int)Math.Round(cost * (double)TravelTicksPerCostAtSpeed8 * 8.0 / Math.Max(1, speed));
            return Math.Max(4, raw);
        }

        public static Result CanTraverse(WorldRouteState route)
        {
            if (route == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Route is null.");

            var state = route.State ?? string.Empty;
            if (state.Equals("Blocked", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("Damaged", StringComparison.OrdinalIgnoreCase) ||
                state.Equals("UnderConstruction", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(ErrorCode.InvalidOperation, "Route blocked.", route.Id + ":" + state);
            }

            return Result.Success();
        }

        public static Result AdvanceTravel(
            SimulationWorld world,
            int ticks = 1,
            List<EntityId> arrivedOut = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (ticks < 1)
                return Result.Success();

            var arrived = false;
            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null || p.Mode != PartyWorldPresenceMode.Traveling)
                    continue;

                p.RemainingTravelTicks -= ticks;
                if (p.RemainingTravelTicks > 0)
                    continue;

                if (!world.WorldGraph.TryGetRoute(p.RouteId, out var route))
                {
                    p.ClearTravel();
                    continue;
                }

                var from = p.NodeId;
                var to = !string.IsNullOrEmpty(p.DestNodeId)
                    ? p.DestNodeId
                    : (string.Equals(route.FromNodeId, from, StringComparison.Ordinal)
                        ? route.ToNodeId
                        : route.FromNodeId);
                p.NodeId = to;
                p.ClearTravel();
                arrived = true;
                arrivedOut?.Add(p.EntityId);
            }

            if (arrived || arrivedOut == null)
                SyncPartyFocus(world);

            return Result.Success();
        }

        public static Result PlaceAgentsAtNode(
            SimulationWorld world,
            IReadOnlyList<EntityId> agents,
            string nodeId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.WorldGraph.TryGetNode(nodeId, out _))
                return Result.Failure(ErrorCode.NotFound, "World node missing.", nodeId);

            if (agents != null)
            {
                for (var i = 0; i < agents.Count; i++)
                {
                    if (agents[i].IsNone)
                        continue;
                    world.WorldPresence.SetAtNode(agents[i], nodeId);
                }
            }

            SyncPartyFocus(world);
            ApplyLocalMapSessionFromFocus(world);
            return Result.Success();
        }

        public static Result EnterNode(SimulationWorld world, string nodeId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.WorldGraph.TryGetNode(nodeId, out var node))
                return Result.Failure(ErrorCode.NotFound, "World node missing.", nodeId);

            world.PartyWorld.Mode = PartyWorldPresenceMode.AtNode;
            world.PartyWorld.NodeId = nodeId;
            world.PartyWorld.ClearTravel();
            world.PartyWorld.LocalMapId = node.LocalMapId ?? string.Empty;
            world.PartyWorld.EncounterId = string.Empty;
            ApplyLocalMapSessionFromFocus(world);
            return Result.Success();
        }

        public static void SyncPartyFocus(SimulationWorld world)
        {
            if (world == null)
                return;

            string bestWithMap = null;
            string bestAny = null;
            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null || string.IsNullOrEmpty(p.NodeId))
                    continue;
                if (p.Mode == PartyWorldPresenceMode.Traveling)
                {
                    bestAny = bestAny ?? p.NodeId;
                    continue;
                }

                bestAny = p.NodeId;
                if (world.WorldGraph.TryGetNode(p.NodeId, out var n) &&
                    !string.IsNullOrWhiteSpace(n.LocalMapId))
                    bestWithMap = p.NodeId;
            }

            var focus = bestWithMap ?? bestAny ?? world.PartyWorld.NodeId;
            if (string.IsNullOrEmpty(focus) || !world.WorldGraph.TryGetNode(focus, out var node))
                return;

            var anyTraveling = false;
            foreach (var kv in world.WorldPresence.All)
            {
                if (kv.Value != null &&
                    kv.Value.Mode == PartyWorldPresenceMode.Traveling)
                {
                    anyTraveling = true;
                    break;
                }
            }

            world.PartyWorld.NodeId = focus;
            world.PartyWorld.LocalMapId = ResolveLocalMapId(node);
            world.PartyWorld.Mode = anyTraveling
                ? PartyWorldPresenceMode.Traveling
                : PartyWorldPresenceMode.AtNode;
            if (!anyTraveling)
                world.PartyWorld.ClearTravel();
        }

        public static void ApplyLocalMapSessionFromFocus(SimulationWorld world)
        {
            var presence = world.PartyWorld;
            world.LocalMap.ClearOccupants();
            world.LocalMap.ReturnLocationId = string.Empty;
            if (string.IsNullOrWhiteSpace(presence.LocalMapId))
            {
                world.LocalMap.ActiveMapLayoutId = string.Empty;
                world.LocalMap.OverworldMapLayoutId = string.Empty;
            }
            else
            {
                world.LocalMap.ActiveMapLayoutId = presence.LocalMapId;
                world.LocalMap.OverworldMapLayoutId = presence.LocalMapId;
            }
        }

        /// <summary>无专属 LocalMap 的宏观节点共用保底灰盒场景。</summary>
        public const string PlaceholderLocalMapId = "base:map_world_node_stub";

        public static string ResolveLocalMapId(WorldNodeState node)
        {
            if (node != null && !string.IsNullOrWhiteSpace(node.LocalMapId))
                return node.LocalMapId;
            return PlaceholderLocalMapId;
        }

        /// <summary>玩家从大地图进入某节点场景（无 LocalMap 时用保底图）。</summary>
        public static Result EnterNodeScene(SimulationWorld world, string nodeId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.WorldGraph.TryGetNode(nodeId, out var node))
                return Result.Failure(ErrorCode.NotFound, "World node missing.", nodeId);

            var access = StrategicNodeAccessService.CanEnterNodeLocalMap(world, nodeId);
            if (access.IsFailure)
                return access;

            world.PartyWorld.NodeId = nodeId;
            world.PartyWorld.LocalMapId = ResolveLocalMapId(node);
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtNode;
            world.PartyWorld.ClearTravel();
            ApplyLocalMapSessionFromFocus(world);
            return Result.Success();
        }

        public static Result FocusNode(SimulationWorld world, string nodeId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.WorldGraph.TryGetNode(nodeId, out var node))
                return Result.Failure(ErrorCode.NotFound, "World node missing.", nodeId);

            world.PartyWorld.NodeId = nodeId;
            world.PartyWorld.LocalMapId = ResolveLocalMapId(node);
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtNode;
            world.PartyWorld.ClearTravel();
            ApplyLocalMapSessionFromFocus(world);
            return Result.Success();
        }

        public static bool TryResolveTravelWorldPoints(
            SimulationWorld world,
            WorldAgentPresence presence,
            out float fromX,
            out float fromY,
            out float toX,
            out float toY)
        {
            fromX = fromY = toX = toY = 0f;
            if (world == null || presence == null || string.IsNullOrEmpty(presence.NodeId))
                return false;
            if (!world.WorldGraph.TryGetNode(presence.NodeId, out var from))
                return false;
            fromX = from.WorldX;
            fromY = from.WorldY;
            if (presence.Mode != PartyWorldPresenceMode.Traveling ||
                string.IsNullOrEmpty(presence.DestNodeId) ||
                !world.WorldGraph.TryGetNode(presence.DestNodeId, out var to))
            {
                toX = fromX;
                toY = fromY;
                return true;
            }

            toX = to.WorldX;
            toY = to.WorldY;
            return true;
        }
    }
}
