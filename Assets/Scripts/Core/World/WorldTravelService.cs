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
            if (p.Mode == PartyWorldPresenceMode.InEncounter)
            {
                if (!StrategicEncounterSpawner.IsFieldCleared(world))
                    return Result.Failure(ErrorCode.InvalidOperation, "战斗未结束，无法离开战场。", id.Value.ToString());
                StrategicEncounterSpawner.ReleaseEngagedForMacroTravel(world, id);
                if (!world.WorldPresence.TryGet(id, out p) || p == null)
                    return Result.Failure(ErrorCode.NotFound, "Traveler has no world node.", id.Value.ToString());
            }

            if (p.Mode == PartyWorldPresenceMode.RouteAnchored)
                return StartTravelFromRouteAnchor(world, id, p, toNodeId);
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
            p.RouteAnchorProgress = -1f;
            p.ClearRouteSegment();
            return Result.Success();
        }

        static Result StartTravelFromRouteAnchor(
            SimulationWorld world,
            EntityId id,
            WorldAgentPresence p,
            string toNodeId)
        {
            if (string.IsNullOrEmpty(p.RouteId) || !world.WorldGraph.TryGetRoute(p.RouteId, out var route))
                return Result.Failure(ErrorCode.InvalidOperation, "Traveler is not on a macro route.");

            var atOrigin = string.Equals(toNodeId, p.NodeId, StringComparison.Ordinal);
            var atDest = string.Equals(toNodeId, p.DestNodeId, StringComparison.Ordinal);
            if (!atOrigin && !atDest)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "金丹前只能沿当前道路前往两端节点。");
            }

            if (atOrigin && p.RouteAnchorProgress <= 0.001f)
                return Result.Failure(ErrorCode.InvalidArgument, "Already at route origin.");
            if (atDest && p.RouteAnchorProgress >= 0.999f)
                return Result.Failure(ErrorCode.InvalidArgument, "Already at route destination.");

            var gate = CanTraverse(route);
            if (gate.IsFailure)
                return gate;

            var cost = route.TravelCost > 0 ? route.TravelCost : 1;
            var fullTicks = ComputeTravelTicks(world, id, cost);
            var startProgress = p.RouteAnchorProgress;
            float endProgress;
            float fraction;
            if (atDest)
            {
                endProgress = 1f;
                fraction = 1f - startProgress;
            }
            else
            {
                endProgress = 0f;
                fraction = startProgress;
            }

            if (fraction <= 0.001f)
            {
                ArriveAtRouteEndpoint(world, p, atDest ? p.DestNodeId : p.NodeId);
                return Result.Success();
            }

            // 至少走一段可见路程，避免清场后短距离被当成瞬移
            var ticks = Math.Max(8, (int)Math.Round(fullTicks * fraction));
            p.Mode = PartyWorldPresenceMode.Traveling;
            p.RouteAnchorProgress = -1f;
            p.RouteSegmentOriginProgress = startProgress;
            p.RouteSegmentEndProgress = endProgress;
            p.TravelTotalTicks = ticks;
            p.RemainingTravelTicks = ticks;
            return Result.Success();
        }

        /// <summary>从路线锚点前往同一路线上的任意进度（金丹前宏观道路）。</summary>
        public static Result StartTravelToRouteProgress(
            SimulationWorld world,
            EntityId id,
            float targetProgress)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (id.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid traveler.");
            if (!world.WorldPresence.TryGet(id, out var p) || p == null)
                return Result.Failure(ErrorCode.NotFound, "Traveler presence missing.", id.Value.ToString());
            if (p.Mode != PartyWorldPresenceMode.RouteAnchored)
                return Result.Failure(ErrorCode.InvalidOperation, "Traveler is not anchored on a route.");
            if (string.IsNullOrEmpty(p.RouteId) || !world.WorldGraph.TryGetRoute(p.RouteId, out var route))
                return Result.Failure(ErrorCode.InvalidOperation, "Traveler is not on a macro route.");

            var gate = CanTraverse(route);
            if (gate.IsFailure)
                return gate;

            targetProgress = Math.Max(0f, Math.Min(1f, targetProgress));
            if (Math.Abs(p.RouteAnchorProgress - targetProgress) <= 0.001f)
            {
                p.AnchorOnRoute(targetProgress);
                return Result.Success();
            }

            return StartTravelRouteSegment(
                world,
                id,
                p,
                p.RouteId,
                p.NodeId,
                p.DestNodeId,
                p.RouteAnchorProgress,
                targetProgress);
        }

        public static Result StartTravelPartyToRouteProgress(
            SimulationWorld world,
            IReadOnlyList<EntityId> agents,
            float targetProgress)
        {
            if (world == null || agents == null || agents.Count == 0)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid route progress travel request.");

            var started = 0;
            string lastFail = null;
            for (var i = 0; i < agents.Count; i++)
            {
                var result = StartTravelToRouteProgress(world, agents[i], targetProgress);
                if (result.IsSuccess)
                    started++;
                else
                    lastFail = result.Error.Message;
            }

            return started > 0
                ? Result.Success()
                : Result.Failure(ErrorCode.InvalidOperation, lastFail ?? "No travelers started.");
        }

        static void ArriveAtRouteEndpoint(SimulationWorld world, WorldAgentPresence p, string nodeId)
        {
            if (p == null)
                return;
            p.NodeId = nodeId ?? string.Empty;
            p.ClearTravel();
        }

        public static Result StartTravelPartyToStackAnchor(
            SimulationWorld world,
            IReadOnlyList<EntityId> agents,
            ArmyStack stack)
        {
            if (world == null || agents == null || agents.Count == 0 || stack == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid pursuit travel request.");

            var started = 0;
            string lastFail = null;
            for (var i = 0; i < agents.Count; i++)
            {
                var one = StartTravelToStackAnchor(world, agents[i], stack);
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
                    string.IsNullOrEmpty(lastFail) ? "No traveler could pursue stack." : lastFail);
            }

            SyncPartyFocus(world);
            return Result.Success();
        }

        public static Result StartTravelToStackAnchor(
            SimulationWorld world,
            EntityId id,
            ArmyStack stack)
        {
            if (id.IsNone || stack == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid pursuit traveler.");
            if (!world.WorldPresence.TryGet(id, out var p) || p == null)
                return Result.Failure(ErrorCode.NotFound, "Traveler presence missing.");

            // 非道路驻军／行军：追到目标节点（或 Dest）
            if (!stack.IsRoutePositioned || string.IsNullOrEmpty(stack.RouteId))
            {
                if (p.Mode == PartyWorldPresenceMode.Traveling && p.HasRoutePresentation)
                    p.AnchorOnRoute(p.TravelProgress);
                return StartTravelOne(world, id, StrategicNodeAccessService.ResolveStackTravelTarget(stack));
            }

            if (StrategicNodeAccessService.IsAgentAtStackAnchor(world, p, stack))
                return Result.Success();

            if (p.Mode == PartyWorldPresenceMode.InEncounter)
            {
                if (!StrategicEncounterSpawner.IsFieldCleared(world))
                    return Result.Failure(ErrorCode.InvalidOperation, "战斗未结束，无法离开战场。");
                StrategicEncounterSpawner.ReleaseEngagedForMacroTravel(world, id);
                if (!world.WorldPresence.TryGet(id, out p) || p == null)
                    return Result.Failure(ErrorCode.NotFound, "Traveler presence missing.");
            }

            var target = stack.GetRouteDisplayProgress();
            if (p.Mode == PartyWorldPresenceMode.RouteAnchored)
            {
                return StartTravelRouteSegment(
                    world,
                    id,
                    p,
                    stack.RouteId,
                    stack.NodeId,
                    stack.DestNodeId,
                    p.RouteAnchorProgress,
                    target);
            }

            if (p.Mode == PartyWorldPresenceMode.Traveling &&
                string.Equals(p.RouteId, stack.RouteId, StringComparison.Ordinal))
            {
                return RetargetTravelToRouteProgress(world, id, p, stack.NodeId, stack.DestNodeId, target);
            }

            // 正在别的路上：先钉在当前进度，再改去追目标栈
            if (p.Mode == PartyWorldPresenceMode.Traveling && p.HasRoutePresentation)
            {
                p.AnchorOnRoute(p.TravelProgress);
                return StartTravelToStackAnchor(world, id, stack);
            }

            if (p.Mode == PartyWorldPresenceMode.AtNode)
            {
                if (string.Equals(p.NodeId, stack.NodeId, StringComparison.Ordinal))
                {
                    return StartTravelRouteSegment(
                        world, id, p, stack.RouteId, stack.NodeId, stack.DestNodeId, 0f, target);
                }

                if (string.Equals(p.NodeId, stack.DestNodeId, StringComparison.Ordinal))
                {
                    return StartTravelRouteSegment(
                        world, id, p, stack.RouteId, stack.NodeId, stack.DestNodeId, 1f, target);
                }

                var hop = StartTravelOne(world, id, stack.NodeId);
                if (hop.IsFailure)
                    hop = StartTravelOne(world, id, stack.DestNodeId);
                if (hop.IsFailure)
                    return hop;
                if (!world.WorldPresence.TryGet(id, out p) || p == null)
                    return Result.Failure(ErrorCode.NotFound, "Traveler presence missing.");
                return RetargetTravelToRouteProgress(world, id, p, stack.NodeId, stack.DestNodeId, target);
            }

            return Result.Failure(ErrorCode.InvalidOperation, "Traveler cannot pursue this stack.");
        }

        public static void ClampPursuitTravelToStackAnchor(
            SimulationWorld world,
            EntityId id,
            ArmyStack stack)
        {
            if (world == null || stack == null || !stack.IsRoutePositioned || id.IsNone)
                return;
            if (!world.WorldPresence.TryGet(id, out var p) || p == null)
                return;
            if (p.Mode != PartyWorldPresenceMode.Traveling ||
                !string.Equals(p.RouteId, stack.RouteId, StringComparison.Ordinal))
                return;
            if (StrategicNodeAccessService.IsAgentAtStackAnchor(world, p, stack))
                return;

            RetargetTravelToRouteProgress(
                world, id, p, stack.NodeId, stack.DestNodeId, stack.GetRouteDisplayProgress());
        }

        static Result RetargetTravelToRouteProgress(
            SimulationWorld world,
            EntityId id,
            WorldAgentPresence p,
            string originNodeId,
            string destNodeId,
            float targetProgress)
        {
            if (p == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Presence missing.");

            var current = p.TravelProgress;
            if (current + 0.02f >= targetProgress)
            {
                p.AnchorOnRoute(targetProgress);
                return Result.Success();
            }

            return StartTravelRouteSegment(
                world,
                id,
                p,
                p.RouteId,
                originNodeId,
                destNodeId,
                current,
                targetProgress);
        }

        static Result StartTravelRouteSegment(
            SimulationWorld world,
            EntityId id,
            WorldAgentPresence p,
            string routeId,
            string originNodeId,
            string destNodeId,
            float startProgress,
            float endProgress)
        {
            if (p == null || string.IsNullOrEmpty(routeId))
                return Result.Failure(ErrorCode.InvalidArgument, "Route segment invalid.");
            if (!world.WorldGraph.TryGetRoute(routeId, out var route))
                return Result.Failure(ErrorCode.NotFound, "Route missing.", routeId);

            var gate = CanTraverse(route);
            if (gate.IsFailure)
                return gate;

            startProgress = Math.Max(0f, Math.Min(1f, startProgress));
            endProgress = Math.Max(0f, Math.Min(1f, endProgress));
            if (Math.Abs(startProgress - endProgress) <= 0.001f)
            {
                p.NodeId = originNodeId ?? string.Empty;
                p.DestNodeId = destNodeId ?? string.Empty;
                p.RouteId = routeId;
                p.AnchorOnRoute(endProgress);
                return Result.Success();
            }

            var cost = route.TravelCost > 0 ? route.TravelCost : 1;
            var fullTicks = ComputeTravelTicks(world, id, cost);
            var fraction = Math.Abs(endProgress - startProgress);
            var ticks = Math.Max(4, (int)Math.Round(fullTicks * fraction));
            p.Mode = PartyWorldPresenceMode.Traveling;
            p.NodeId = originNodeId ?? string.Empty;
            p.DestNodeId = destNodeId ?? string.Empty;
            p.RouteId = routeId;
            p.RouteAnchorProgress = -1f;
            p.RouteSegmentOriginProgress = startProgress;
            p.RouteSegmentEndProgress = endProgress;
            p.TravelTotalTicks = ticks;
            p.RemainingTravelTicks = ticks;
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

                if (p.RouteSegmentEndProgress >= 0f)
                {
                    if (p.RouteSegmentEndProgress <= 0.01f)
                        ArriveAtRouteEndpoint(world, p, p.NodeId);
                    else if (p.RouteSegmentEndProgress >= 0.99f)
                        ArriveAtRouteEndpoint(world, p, p.DestNodeId);
                    else
                        p.AnchorOnRoute(p.RouteSegmentEndProgress);
                    arrived = true;
                    arrivedOut?.Add(p.EntityId);
                    WorldTravelPathService.TryContinueQueuedTravel(world, p.EntityId);
                    continue;
                }

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
                WorldTravelPathService.TryContinueQueuedTravel(world, p.EntityId);
            }

            if (arrived || arrivedOut == null)
                SyncPartyFocus(world);

            return Result.Success();
        }

        public static bool CanReceiveTravelOrder(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.WorldPresence.TryGet(id, out var p) || p == null)
                return false;
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return false;
            if (p.Mode == PartyWorldPresenceMode.AtNode ||
                p.Mode == PartyWorldPresenceMode.RouteAnchored ||
                p.Mode == PartyWorldPresenceMode.Traveling)
                return true;
            // 中途打架不可下令离开；敌清空后可在大地图下令（画面可仍留在战场 LocalMap）
            // ADR-0023：Modal／PostBattle 已在上方拦截；非 Modal 的旧 FieldCleared 路径逐步淘汰
            return p.Mode == PartyWorldPresenceMode.InEncounter &&
                   StrategicEncounterSpawner.IsFieldCleared(world);
        }

        public static bool CanReachNodeFromPresence(
            SimulationWorld world,
            WorldAgentPresence p,
            string toNodeId)
        {
            if (world == null || p == null || string.IsNullOrEmpty(toNodeId))
                return false;
            if (p.Mode == PartyWorldPresenceMode.RouteAnchored)
            {
                return string.Equals(toNodeId, p.NodeId, StringComparison.Ordinal) ||
                       string.Equals(toNodeId, p.DestNodeId, StringComparison.Ordinal);
            }

            if (p.Mode != PartyWorldPresenceMode.AtNode || string.IsNullOrEmpty(p.NodeId))
                return false;
            return world.WorldGraph.TryFindRoute(p.NodeId, toNodeId, out _);
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
            world.PartyWorld.LocalMapId = BattleOfferService.HasActiveManualEncounter(world)
                ? BattleOfferService.ResolveActiveEncounterLocalMapId(world)
                : ResolveLocalMapId(node);
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
            if (!presence.HasRoutePresentation ||
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
