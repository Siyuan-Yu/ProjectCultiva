using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Formal Army 战略移动：路中锚点出发须带 RouteSegment，禁止 SyncFromArmy 把单位拉回路线起点。
    /// </summary>
    public static class ArmyTravelCommandService
    {
        static readonly Dictionary<string, Queue<string>> PendingNodeHops =
            new Dictionary<string, Queue<string>>(StringComparer.Ordinal);

        static readonly List<string> PathScratch = new List<string>(16);

        public static void ClearArmyTravelQueue(string armyId)
        {
            if (string.IsNullOrEmpty(armyId))
                return;
            PendingNodeHops.Remove(armyId);
        }

        public static bool HasPendingLegs(string armyId) =>
            !string.IsNullOrEmpty(armyId) &&
            PendingNodeHops.TryGetValue(armyId, out var queue) &&
            queue != null &&
            queue.Count > 0;

        public static bool TryContinueQueuedTravel(SimulationWorld world, string armyId)
        {
            if (world == null || string.IsNullOrEmpty(armyId))
                return false;
            if (!PendingNodeHops.TryGetValue(armyId, out var queue) || queue == null || queue.Count == 0)
            {
                PendingNodeHops.Remove(armyId);
                return false;
            }

            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
            {
                PendingNodeHops.Remove(armyId);
                return false;
            }

            var nextLeg = queue.Dequeue();
            if (queue.Count == 0)
                PendingNodeHops.Remove(armyId);

            if (ArmyPursuitTargetService.TryConsumePursuitRouteLeg(nextLeg, out var pursuitRouteId) &&
                world.WorldGraph.TryGetRoute(pursuitRouteId, out var pursuitRoute) &&
                pursuitRoute != null)
            {
                ArmyPursuitTargetService.TryResolveTargetArmy(world, out var pursuitTarget);
                var dynamicProgress = ArmyPursuitTargetService.ResolveTargetRouteProgressForLeg(
                    world,
                    pursuitTarget,
                    pursuitRoute);
                var arrivedNode = army.NodeId ?? string.Empty;
                NormalizeFormalArmyRouteEndpoints(world, army, pursuitRoute);
                var entryProgress = string.Equals(arrivedNode, pursuitRoute.ToNodeId, StringComparison.Ordinal)
                    ? 1f
                    : 0f;
                army.State = FormalArmyState.AtNode;
                army.RouteAnchorProgress = entryProgress;
                army.ClearRouteSegment();
                return StartArmyTravelToRouteProgress(world, army, pursuitRoute, dynamicProgress).IsSuccess;
            }

            if (TryConsumeRouteProgressLeg(nextLeg, out var routeId, out var progress) &&
                world.WorldGraph.TryGetRoute(routeId, out var route) &&
                route != null)
            {
                var arrivedNode = army.NodeId ?? string.Empty;
                NormalizeFormalArmyRouteEndpoints(world, army, route);
                var entryProgress = string.Equals(arrivedNode, route.ToNodeId, StringComparison.Ordinal)
                    ? 1f
                    : 0f;
                army.State = FormalArmyState.AtNode;
                army.RouteAnchorProgress = entryProgress;
                army.ClearRouteSegment();
                return StartArmyTravelToRouteProgress(world, army, route, progress).IsSuccess;
            }

            return MoveArmyDirectHop(world, army, nextLeg).IsSuccess;
        }

        public static Result MoveArmyToNode(SimulationWorld world, string armyId, string toNodeId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (string.IsNullOrWhiteSpace(armyId))
                return Result.Failure(ErrorCode.InvalidArgument, "ArmyId required.");
            if (string.IsNullOrWhiteSpace(toNodeId))
                return Result.Failure(ErrorCode.InvalidArgument, "toNodeId required.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);
            if (army.State == FormalArmyState.Garrisoned)
                return Result.Failure(ErrorCode.InvalidOperation, "Garrisoned army cannot travel.", armyId);
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Modal encounter blocks army travel.");
            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no living members.", armyId);

            ClearArmyTravelQueue(armyId);
            ClearArmyMemberPathQueues(world, army);
            ClearArmyPursuitMarks(world, army);
            PrepareArmyMacroTravel(world, army);
            return ExecuteMoveArmyToNode(world, army, toNodeId);
        }

        public static Result MoveArmyToRouteProgress(
            SimulationWorld world,
            string armyId,
            string routeId,
            float progress)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (string.IsNullOrWhiteSpace(armyId))
                return Result.Failure(ErrorCode.InvalidArgument, "ArmyId required.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);
            if (army.State == FormalArmyState.Garrisoned)
                return Result.Failure(ErrorCode.InvalidOperation, "Garrisoned army cannot travel.", armyId);
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Modal encounter blocks army travel.");
            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no living members.", armyId);
            if (string.IsNullOrEmpty(routeId) || !world.WorldGraph.TryGetRoute(routeId, out var route) || route == null)
                return Result.Failure(ErrorCode.NotFound, "Route missing.", routeId ?? string.Empty);

            ClearArmyTravelQueue(armyId);
            ClearArmyMemberPathQueues(world, army);
            ClearArmyPursuitMarks(world, army);
            PrepareArmyMacroTravel(world, army);

            progress = Math.Max(0f, Math.Min(1f, progress));
            return BeginArmyRouteProgressTarget(world, army, route, progress, false);
        }

        /// <summary>追击专用：不清 Pursuit 标记，FormalArmy 为真源。移动核心与 MoveArmyToNode/RouteProgress 相同。</summary>
        public static Result MoveArmyToStackAnchor(
            SimulationWorld world,
            string armyId,
            ArmyStack stack)
        {
            if (world == null || string.IsNullOrWhiteSpace(armyId) || stack == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid pursuit order.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);
            if (army.State == FormalArmyState.Garrisoned)
                return Result.Failure(ErrorCode.InvalidOperation, "Garrisoned army cannot travel.", armyId);
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Modal encounter blocks army travel.");
            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no living members.", armyId);

            ClearArmyTravelQueue(armyId);
            ClearArmyMemberPathQueues(world, army);
            PrepareArmyMacroTravel(world, army);

            var leaderId = army.LeaderCharacterId;
            if (!leaderId.IsNone &&
                world.WorldPresence.TryGet(leaderId, out var leaderWp) &&
                leaderWp != null &&
                StrategicEngageRules.IsAgentColocatedWithStack(world, leaderWp, stack))
                return Result.Success();

            if (!stack.IsRoutePositioned || string.IsNullOrEmpty(stack.RouteId))
            {
                var node = StrategicNodeAccessService.ResolveStackTravelTarget(stack);
                if (string.IsNullOrEmpty(node))
                    return Result.Failure(ErrorCode.InvalidOperation, "Stack has no travel target.");
                return ExecuteMoveArmyToNode(world, army, node);
            }

            if (!world.WorldGraph.TryGetRoute(stack.RouteId, out var stackRoute) || stackRoute == null)
                return Result.Failure(ErrorCode.NotFound, "Stack route missing.", stack.RouteId);

            return BeginArmyRouteProgressTarget(world, army, stackRoute, stack.GetRouteDisplayProgress(), false);
        }

        /// <summary>追击专用：以 Target FormalArmy 为真源，不清 Pursuit 标记。</summary>
        public static Result MoveArmyToTargetArmy(
            SimulationWorld world,
            string armyId,
            string targetArmyId)
        {
            if (world == null || string.IsNullOrWhiteSpace(armyId) || string.IsNullOrWhiteSpace(targetArmyId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid pursuit order.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);
            if (!world.Strategic.FormalArmies.TryGet(targetArmyId, out var target) || target == null)
                return Result.Failure(ErrorCode.NotFound, "Target army not found.", targetArmyId);
            if (army.State == FormalArmyState.Garrisoned)
                return Result.Failure(ErrorCode.InvalidOperation, "Garrisoned army cannot travel.", armyId);
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Modal encounter blocks army travel.");
            if (!ArmyPostBattleSyncService.HasMacroOrderLivingMember(world, army))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no living members.", armyId);

            ClearArmyTravelQueue(armyId);
            ClearArmyMemberPathQueues(world, army);
            PrepareArmyMacroTravel(world, army);

            if (target.State == FormalArmyState.Garrisoned ||
                (target.State == FormalArmyState.AtNode &&
                 string.IsNullOrEmpty(target.RouteId) &&
                 !string.IsNullOrEmpty(target.NodeId)))
            {
                if (string.IsNullOrEmpty(target.NodeId))
                    return Result.Failure(ErrorCode.InvalidOperation, "Target has no node.");
                return ExecuteMoveArmyToNode(world, army, target.NodeId);
            }

            if (string.IsNullOrEmpty(target.RouteId) ||
                !world.WorldGraph.TryGetRoute(target.RouteId, out var route) ||
                route == null)
            {
                if (!string.IsNullOrEmpty(target.NodeId))
                    return ExecuteMoveArmyToNode(world, army, target.NodeId);
                return Result.Failure(ErrorCode.InvalidOperation, "Target has no travel position.");
            }

            if (ArmyPursuitTargetService.IsStaticRouteTarget(target))
            {
                var staticProgress = target.GetRouteDisplayProgress();
                if (string.Equals(army.RouteId, route.Id, StringComparison.Ordinal) &&
                    (army.IsTraveling || army.IsRouteAnchored))
                {
                    NormalizeFormalArmyRouteEndpoints(world, army, route);
                    return StartArmyTravelToRouteProgress(world, army, route, staticProgress);
                }

                return BeginArmyRouteProgressTarget(world, army, route, staticProgress, false);
            }

            var chaseEnd = ArmyPursuitTargetService.ResolveChaseEndpoint(target);
            if (string.Equals(army.RouteId, route.Id, StringComparison.Ordinal) &&
                (army.IsTraveling || army.IsRouteAnchored))
            {
                NormalizeFormalArmyRouteEndpoints(world, army, route);
                return StartArmyTravelToRouteProgress(world, army, route, chaseEnd);
            }

            return BeginArmyRouteProgressTarget(world, army, route, chaseEnd, true);
        }

        public static void ClampArmyPursuitToStackAnchor(
            SimulationWorld world,
            FormalArmy army,
            ArmyStack stack)
        {
            if (world == null || army == null || stack == null)
                return;
            if (!ArmyStackAdapter.TryGetFormalArmy(world, stack, out var targetArmy) || targetArmy == null)
                return;
            ArmyPursuitTargetService.TryEnsurePursuitTravel(world, army, targetArmy);
        }

        static Result BeginArmyRouteProgressTarget(
            SimulationWorld world,
            FormalArmy army,
            WorldRouteState route,
            float progress,
            bool pursuitDynamicLeg)
        {
            if (army.IsRouteAnchored &&
                string.Equals(army.RouteId, route.Id, StringComparison.Ordinal))
            {
                NormalizeFormalArmyRouteEndpoints(world, army, route);
                return StartArmyTravelToRouteProgress(world, army, route, progress);
            }

            var anchor = ResolveArmyAnchorNodeId(army);
            if (string.IsNullOrEmpty(anchor))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no travel origin.");

            if (CanMountArmyAtSharedEndpoint(world, army, route, anchor))
            {
                var entryProgress = string.Equals(anchor, route.ToNodeId, StringComparison.Ordinal) ? 1f : 0f;
                army.State = FormalArmyState.AtNode;
                army.RouteId = route.Id;
                NormalizeFormalArmyRouteEndpoints(world, army, route);
                army.RouteAnchorProgress = entryProgress;
                army.ClearRouteSegment();
                return StartArmyTravelToRouteProgress(world, army, route, progress);
            }

            if (!WorldTravelPathService.TryChooseRouteEntryForRoute(
                    world, anchor, route, progress, out var pathToEntry, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot reach target road.");

            var routeLeg = pursuitDynamicLeg
                ? ArmyPursuitTargetService.FormatPursuitRouteLeg(route.Id)
                : FormatRouteProgressLeg(route.Id, progress);

            if (army.IsRouteAnchored &&
                !string.Equals(army.RouteId, route.Id, StringComparison.Ordinal) &&
                world.WorldGraph.TryGetRoute(army.RouteId, out var currentRoute) &&
                currentRoute != null)
            {
                var exitNode = ResolveArmyAnchorNodeId(army);
                if (string.IsNullOrEmpty(exitNode))
                    return Result.Failure(ErrorCode.InvalidOperation, "Cannot resolve travel origin.");

                var crossQueue = new Queue<string>(Math.Max(1, pathToEntry.Count));
                for (var i = 1; i < pathToEntry.Count; i++)
                    crossQueue.Enqueue(pathToEntry[i]);
                crossQueue.Enqueue(routeLeg);
                PendingNodeHops[army.ArmyId] = crossQueue;

                NormalizeFormalArmyRouteEndpoints(world, army, currentRoute);
                if (IsRouteGraphEndpoint(currentRoute, exitNode))
                    return StartFromRouteAnchor(world, army, exitNode);

                return BeginArmyNodeTargetFromRouteAnchor(world, army, currentRoute, exitNode);
            }

            if (pathToEntry.Count < 2)
            {
                if (CanMountArmyAtSharedEndpoint(world, army, route, anchor))
                {
                    var entryProgress = string.Equals(anchor, route.ToNodeId, StringComparison.Ordinal) ? 1f : 0f;
                    army.State = FormalArmyState.AtNode;
                    army.RouteId = route.Id;
                    NormalizeFormalArmyRouteEndpoints(world, army, route);
                    army.RouteAnchorProgress = entryProgress;
                    army.ClearRouteSegment();
                    return StartArmyTravelToRouteProgress(world, army, route, progress);
                }

                return Result.Failure(ErrorCode.InvalidOperation, "Cannot reach target road.");
            }

            var queue = new Queue<string>(Math.Max(1, pathToEntry.Count));
            for (var i = 2; i < pathToEntry.Count; i++)
                queue.Enqueue(pathToEntry[i]);
            queue.Enqueue(routeLeg);
            PendingNodeHops[army.ArmyId] = queue;
            return MoveArmyDirectHop(world, army, pathToEntry[1]);
        }

        static Result ExecuteMoveArmyToNode(SimulationWorld world, FormalArmy army, string toNodeId)
        {
            if (string.IsNullOrEmpty(army.NodeId))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no origin node.");
            if (string.Equals(ResolveArmyAnchorNodeId(army), toNodeId, StringComparison.Ordinal) &&
                !army.IsTraveling &&
                !army.IsRouteAnchored)
                return Result.Failure(ErrorCode.InvalidArgument, "Already at destination node.");

            if (army.IsRouteAnchored)
            {
                if (!world.WorldGraph.TryGetRoute(army.RouteId, out var route) || route == null)
                    return Result.Failure(ErrorCode.InvalidOperation, "Army is not on a macro route.");

                NormalizeFormalArmyRouteEndpoints(world, army, route);
                if (IsRouteGraphEndpoint(route, toNodeId))
                    return StartFromRouteAnchor(world, army, toNodeId);

                return BeginArmyNodeTargetFromRouteAnchor(world, army, route, toNodeId);
            }

            return BeginArmyNodeTarget(world, army, toNodeId);
        }

        static void PrepareArmyMacroTravel(SimulationWorld world, FormalArmy army)
        {
            ReleaseLivingMembersForMacroTravel(world, army);
            ReconcileArmyWithLivingMembers(world, army);
            NormalizeArmyForRetarget(world, army);
            if (army.IsRouteAnchored &&
                world?.WorldGraph != null &&
                world.WorldGraph.TryGetRoute(army.RouteId, out var route) &&
                route != null)
            {
                NormalizeFormalArmyRouteEndpoints(world, army, route);
            }
        }

        static void ReleaseLivingMembersForMacroTravel(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone ||
                    !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id) ||
                    !world.WorldPresence.TryGet(id, out var wp) ||
                    wp == null ||
                    wp.Mode != PartyWorldPresenceMode.InEncounter)
                    continue;

                if (!StrategicEncounterSpawner.IsFieldCleared(world))
                    continue;

                StrategicEncounterSpawner.ReleaseEngagedForMacroTravel(world, id);
            }
        }

        static bool CanMountArmyAtSharedEndpoint(
            SimulationWorld world,
            FormalArmy army,
            WorldRouteState route,
            string anchorNodeId)
        {
            if (army == null || route == null || string.IsNullOrEmpty(anchorNodeId))
                return false;
            if (!string.Equals(anchorNodeId, route.FromNodeId, StringComparison.Ordinal) &&
                !string.Equals(anchorNodeId, route.ToNodeId, StringComparison.Ordinal))
                return false;

            if (!army.IsRouteAnchored && !army.IsTraveling &&
                string.Equals(army.NodeId, anchorNodeId, StringComparison.Ordinal))
                return true;

            if (!army.IsRouteAnchored)
                return false;

            if (string.Equals(army.RouteId, route.Id, StringComparison.Ordinal))
                return true;

            if (string.Equals(anchorNodeId, army.NodeId, StringComparison.Ordinal) &&
                army.RouteAnchorProgress <= 0.02f)
                return true;
            if (string.Equals(anchorNodeId, army.DestNodeId, StringComparison.Ordinal) &&
                army.RouteAnchorProgress >= 0.98f)
                return true;

            return false;
        }

        static string FormatRouteProgressLeg(string routeId, float progress) =>
            "__route_progress__:" + routeId + ":" + progress.ToString("R");

        /// <summary>
        /// 战后／投影漂移：以 living 成员 WorldPresence 对齐 FormalArmy，避免从错误 Node 瞬移开拔。
        /// </summary>
        public static void ReconcileArmyWithLivingMembers(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null || army.IsTraveling)
                return;

            if (!TryResolveLivingLeaderPresence(world, army, out _, out var wp) || wp == null)
                return;

            if (TrySyncArmyRouteFromMemberPresence(world, army, wp))
                return;

            if (wp.Mode == PartyWorldPresenceMode.AtNode && !string.IsNullOrEmpty(wp.NodeId))
            {
                army.State = FormalArmyState.AtNode;
                army.NodeId = wp.NodeId;
                army.RouteId = string.Empty;
                army.DestNodeId = string.Empty;
                army.RouteAnchorProgress = -1f;
                army.ClearRouteSegment();
            }
        }

        /// <summary>大地图预览：从 living 成员推断路径起点，不修改 FormalArmy。</summary>
        public static string ResolvePreviewAnchorNodeId(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return string.Empty;

            if (TryResolveLivingLeaderPresence(world, army, out _, out var wp) && wp != null)
            {
                if (wp.Mode == PartyWorldPresenceMode.AtNode && !string.IsNullOrEmpty(wp.NodeId))
                    return wp.NodeId;

                if (!string.IsNullOrEmpty(wp.RouteId) &&
                    world.WorldGraph.TryGetRoute(wp.RouteId, out var route) &&
                    route != null &&
                    (wp.HasRoutePresentation ||
                     wp.Mode == PartyWorldPresenceMode.Traveling ||
                     wp.Mode == PartyWorldPresenceMode.RouteAnchored ||
                     wp.Mode == PartyWorldPresenceMode.InEncounter))
                {
                    var progress = ToGraphRouteProgress(route, wp);
                    if (progress >= 0.5f && !string.IsNullOrEmpty(route.ToNodeId))
                        return route.ToNodeId;
                    if (!string.IsNullOrEmpty(route.FromNodeId))
                        return route.FromNodeId;
                }

                if (!string.IsNullOrEmpty(wp.NodeId))
                    return wp.NodeId;
            }

            return ResolveArmyAnchorNodeId(army);
        }

        public static bool TryBuildPathPreviewToNode(
            SimulationWorld world,
            FormalArmy army,
            string toNodeId,
            List<string> pathNodes)
        {
            pathNodes?.Clear();
            if (world == null || army == null || pathNodes == null || string.IsNullOrEmpty(toNodeId))
                return false;

            var anchor = ResolvePreviewAnchorNodeId(world, army);
            if (string.IsNullOrEmpty(anchor))
                return false;

            return WorldTravelPathService.TryFindNodePath(world, anchor, toNodeId, pathNodes) &&
                   pathNodes.Count >= 2;
        }

        public static bool TryBuildPathPreviewToStack(
            SimulationWorld world,
            FormalArmy army,
            ArmyStack stack,
            List<string> pathNodes,
            out string targetRouteId,
            out float targetRouteProgress)
        {
            targetRouteId = string.Empty;
            targetRouteProgress = -1f;
            pathNodes?.Clear();
            if (world == null || army == null || stack == null || pathNodes == null)
                return false;

            if (stack.IsRoutePositioned && !string.IsNullOrEmpty(stack.RouteId))
            {
                targetRouteId = stack.RouteId;
                targetRouteProgress = stack.GetRouteDisplayProgress();
            }

            var toNode = StrategicNodeAccessService.ResolveStackTravelTarget(stack);
            if (string.IsNullOrEmpty(toNode))
                return false;

            return TryBuildPathPreviewToNode(world, army, toNode, pathNodes);
        }

        /// <summary>将敌军栈转为与宏观移动相同的 WorldTravelTarget。</summary>
        public static WorldTravelTarget ResolveStackWorldTravelTarget(
            SimulationWorld world,
            ArmyStack stack)
        {
            if (stack == null)
                return default;

            if (stack.IsRoutePositioned &&
                !string.IsNullOrEmpty(stack.RouteId) &&
                world?.WorldGraph != null &&
                world.WorldGraph.TryGetRoute(stack.RouteId, out var route) &&
                route != null)
            {
                var from = route.FromNodeId ?? stack.NodeId ?? string.Empty;
                var to = route.ToNodeId ?? stack.DestNodeId ?? string.Empty;
                return WorldTravelTarget.OnRoute(
                    stack.RouteId,
                    from,
                    to,
                    stack.GetRouteDisplayProgress());
            }

            var node = StrategicNodeAccessService.ResolveStackTravelTarget(stack);
            return WorldTravelTarget.AtNode(node ?? string.Empty);
        }

        static bool TrySyncArmyRouteFromMemberPresence(
            SimulationWorld world,
            FormalArmy army,
            WorldAgentPresence wp)
        {
            if (world == null || army == null || wp == null || string.IsNullOrEmpty(wp.RouteId))
                return false;
            if (wp.Mode != PartyWorldPresenceMode.RouteAnchored &&
                wp.Mode != PartyWorldPresenceMode.InEncounter &&
                wp.Mode != PartyWorldPresenceMode.Traveling)
                return false;
            if (wp.Mode == PartyWorldPresenceMode.Traveling && !wp.HasRoutePresentation)
                return false;

            army.State = FormalArmyState.AtNode;
            army.RouteId = wp.RouteId;
            army.RemainingTravelTicks = 0;
            army.TravelTotalTicks = 0;
            army.ClearRouteSegment();
            if (world.WorldGraph.TryGetRoute(army.RouteId, out var route) && route != null)
            {
                NormalizeFormalArmyRouteEndpoints(world, army, route);
                army.RouteAnchorProgress = ToGraphRouteProgress(route, wp);
            }
            else
            {
                army.NodeId = wp.NodeId ?? string.Empty;
                army.DestNodeId = wp.DestNodeId ?? string.Empty;
                army.RouteAnchorProgress = wp.RouteAnchorProgress;
            }

            return true;
        }

        internal static void NormalizeFormalArmyRouteEndpoints(
            SimulationWorld world,
            FormalArmy army,
            WorldRouteState route)
        {
            if (world == null || army == null || route == null || string.IsNullOrEmpty(route.Id))
                return;

            army.RouteId = route.Id;
            army.NodeId = route.FromNodeId ?? string.Empty;
            army.DestNodeId = route.ToNodeId ?? string.Empty;
        }

        static bool TryResolveLivingLeaderPresence(
            SimulationWorld world,
            FormalArmy army,
            out EntityId leaderId,
            out WorldAgentPresence presence)
        {
            leaderId = EntityId.None;
            presence = null;
            if (world == null || army == null)
                return false;

            leaderId = army.LeaderCharacterId;
            if (!leaderId.IsNone &&
                LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, leaderId) &&
                world.WorldPresence.TryGet(leaderId, out presence) &&
                presence != null)
                return true;

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone || !LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out presence) || presence == null)
                    continue;
                leaderId = id;
                return true;
            }

            return false;
        }

        static Result BeginArmyNodeTarget(SimulationWorld world, FormalArmy army, string toNodeId)
        {
            var anchor = ResolveArmyAnchorNodeId(army);
            if (string.IsNullOrEmpty(anchor))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no travel origin.");

            if (string.Equals(anchor, toNodeId, StringComparison.Ordinal) &&
                !army.IsRouteAnchored &&
                !army.IsTraveling)
                return Result.Failure(ErrorCode.InvalidArgument, "Already at destination node.");

            PathScratch.Clear();
            if (!WorldTravelPathService.TryFindNodePath(world, anchor, toNodeId, PathScratch) ||
                PathScratch.Count < 2)
                return Result.Failure(ErrorCode.InvalidArgument, "No macro route to destination.");

            if (PathScratch.Count == 2)
                return MoveArmyDirectHop(world, army, toNodeId);

            var queue = new Queue<string>(PathScratch.Count - 2);
            for (var i = 2; i < PathScratch.Count; i++)
                queue.Enqueue(PathScratch[i]);
            PendingNodeHops[army.ArmyId] = queue;
            return MoveArmyDirectHop(world, army, PathScratch[1]);
        }

        static Result BeginArmyNodeTargetFromRouteAnchor(
            SimulationWorld world,
            FormalArmy army,
            WorldRouteState route,
            string toNodeId)
        {
            NormalizeFormalArmyRouteEndpoints(world, army, route);

            var progress = Math.Max(0f, Math.Min(1f, army.RouteAnchorProgress));
            PathScratch.Clear();
            var canFrom = WorldTravelPathService.TryFindNodePath(world, route.FromNodeId, toNodeId, PathScratch);
            var fromPath = new List<string>(PathScratch);
            var fromLen = canFrom ? Math.Max(0, fromPath.Count - 1) : int.MaxValue;
            PathScratch.Clear();
            var canTo = WorldTravelPathService.TryFindNodePath(world, route.ToNodeId, toNodeId, PathScratch);
            var toPath = new List<string>(PathScratch);
            var toLen = canTo ? Math.Max(0, toPath.Count - 1) : int.MaxValue;

            if (fromLen == int.MaxValue && toLen == int.MaxValue)
                return Result.Failure(ErrorCode.InvalidArgument, "No macro route to destination.");

            var fromCost = progress + fromLen;
            var toCost = (1f - progress) + toLen;

            string exitNode;
            List<string> pathFromExit;
            float exitProgress;
            if (toCost <= fromCost)
            {
                exitNode = route.ToNodeId;
                exitProgress = 1f;
                pathFromExit = toPath;
            }
            else
            {
                exitNode = route.FromNodeId;
                exitProgress = 0f;
                pathFromExit = fromPath;
            }

            if (pathFromExit == null || pathFromExit.Count < 1)
                return Result.Failure(ErrorCode.InvalidArgument, "No macro route to destination.");

            if (pathFromExit.Count > 1)
            {
                var queue = new Queue<string>(pathFromExit.Count - 1);
                for (var i = 1; i < pathFromExit.Count; i++)
                    queue.Enqueue(pathFromExit[i]);
                PendingNodeHops[army.ArmyId] = queue;
            }

            if (Math.Abs(progress - exitProgress) <= 0.001f)
            {
                army.State = FormalArmyState.AtNode;
                army.NodeId = exitNode;
                army.RouteId = string.Empty;
                army.DestNodeId = string.Empty;
                army.RouteAnchorProgress = -1f;
                army.ClearRouteSegment();
                ArmyPresenceAdapter.SyncFromArmy(world, army);
                return TryContinueQueuedTravel(world, army.ArmyId)
                    ? Result.Success()
                    : Result.Failure(ErrorCode.InvalidOperation, "Cannot start travel from route anchor.");
            }

            return StartFromRouteAnchor(world, army, exitNode);
        }

        static Result MoveArmyDirectHop(SimulationWorld world, FormalArmy army, string toNodeId)
        {
            var fromNodeId = army.NodeId;
            if (string.IsNullOrEmpty(fromNodeId))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no origin node.");

            if (army.IsRouteAnchored)
                return StartFromRouteAnchor(world, army, toNodeId);

            if (!world.WorldGraph.TryFindRoute(fromNodeId, toNodeId, out var route) || route == null)
                return Result.Failure(ErrorCode.InvalidArgument, "No direct route between nodes.");

            var gate = WorldTravelService.CanTraverse(route);
            if (gate.IsFailure)
                return gate;

            var fullTicks = WorldTravelService.ComputeTravelTicks(
                world,
                army.LeaderCharacterId,
                route.TravelCost > 0 ? route.TravelCost : 1);
            fullTicks = Math.Max(8, fullTicks);

            NormalizeFormalArmyRouteEndpoints(world, army, route);
            ResolveDirectHopRouteSegment(route, fromNodeId, toNodeId, out var startProgress, out var endProgress);

            var fraction = Math.Abs(endProgress - startProgress);
            if (fraction <= 0.001f)
            {
                army.State = FormalArmyState.AtNode;
                army.NodeId = toNodeId;
                army.RouteId = string.Empty;
                army.DestNodeId = string.Empty;
                army.RouteAnchorProgress = -1f;
                army.ClearRouteSegment();
                ArmyPresenceAdapter.SyncFromArmy(world, army);
                WorldTravelService.SyncPartyFocus(world);
                return Result.Success();
            }

            var ticks = Math.Max(8, (int)Math.Round(fullTicks * Math.Max(fraction, 0.001f)));
            army.State = FormalArmyState.OnRoute;
            army.TravelTotalTicks = ticks;
            army.RemainingTravelTicks = ticks;
            army.RouteAnchorProgress = -1f;
            army.RouteSegmentOriginProgress = startProgress;
            army.RouteSegmentEndProgress = endProgress;

            ArmyPresenceAdapter.SyncFromArmy(world, army);
            WorldTravelService.SyncPartyFocus(world);
            return Result.Success();
        }

        /// <summary>
        /// 路网 From→To 进度；若实际行军方向与路网相反（如荒村→青云路），须走 1→0 区段。
        /// </summary>
        internal static void ResolveDirectHopRouteSegment(
            WorldRouteState route,
            string fromNodeId,
            string toNodeId,
            out float startProgress,
            out float endProgress)
        {
            startProgress = 0f;
            endProgress = 1f;
            if (route == null)
                return;

            if (string.Equals(fromNodeId, route.FromNodeId, StringComparison.Ordinal) &&
                string.Equals(toNodeId, route.ToNodeId, StringComparison.Ordinal))
            {
                startProgress = 0f;
                endProgress = 1f;
                return;
            }

            if (string.Equals(fromNodeId, route.ToNodeId, StringComparison.Ordinal) &&
                string.Equals(toNodeId, route.FromNodeId, StringComparison.Ordinal))
            {
                startProgress = 1f;
                endProgress = 0f;
            }
        }

        /// <summary>
        /// 把 Presence／接战快照里沿 Node→Dest 的进度换算为路网 From→To 进度。
        /// </summary>
        internal static float ToGraphRouteProgress(
            WorldRouteState route,
            string travelOriginNodeId,
            string travelDestNodeId,
            float rawProgress)
        {
            if (route == null)
                return Clamp01(rawProgress);

            rawProgress = Clamp01(rawProgress);
            if (string.Equals(travelOriginNodeId, route.FromNodeId, StringComparison.Ordinal) &&
                string.Equals(travelDestNodeId, route.ToNodeId, StringComparison.Ordinal))
                return rawProgress;

            if (string.Equals(travelOriginNodeId, route.ToNodeId, StringComparison.Ordinal) &&
                string.Equals(travelDestNodeId, route.FromNodeId, StringComparison.Ordinal))
                return Clamp01(1f - rawProgress);

            if (string.Equals(travelOriginNodeId, route.ToNodeId, StringComparison.Ordinal))
                return Clamp01(1f - rawProgress);
            if (string.Equals(travelOriginNodeId, route.FromNodeId, StringComparison.Ordinal))
                return rawProgress;

            return rawProgress;
        }

        internal static float ToGraphRouteProgress(WorldRouteState route, WorldAgentPresence presence)
        {
            if (route == null || presence == null)
                return 0f;

            var raw = presence.RouteAnchorProgress >= 0f
                ? presence.RouteAnchorProgress
                : presence.TravelProgress;
            return ToGraphRouteProgress(route, presence.NodeId, presence.DestNodeId, raw);
        }

        static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > 1f)
                return 1f;
            return value;
        }

        static Result StartFromRouteAnchor(SimulationWorld world, FormalArmy army, string toNodeId)
        {
            if (string.IsNullOrEmpty(army.RouteId) ||
                !world.WorldGraph.TryGetRoute(army.RouteId, out var route) ||
                route == null)
                return Result.Failure(ErrorCode.InvalidOperation, "Army is not on a macro route.");

            NormalizeFormalArmyRouteEndpoints(world, army, route);

            var targetIsRouteTo = string.Equals(toNodeId, route.ToNodeId, StringComparison.Ordinal);
            var targetIsRouteFrom = string.Equals(toNodeId, route.FromNodeId, StringComparison.Ordinal);
            if (!targetIsRouteTo && !targetIsRouteFrom)
                return Result.Failure(ErrorCode.InvalidArgument, "From route anchor only origin/dest allowed.");

            var startProgress = army.RouteAnchorProgress;
            if (startProgress < 0f)
                startProgress = 0f;
            startProgress = Math.Max(0f, Math.Min(1f, startProgress));

            var endProgress = targetIsRouteTo ? 1f : 0f;
            var fraction = Math.Abs(endProgress - startProgress);
            if (fraction <= 0.001f)
            {
                army.State = FormalArmyState.AtNode;
                army.NodeId = toNodeId;
                army.RouteId = string.Empty;
                army.DestNodeId = string.Empty;
                army.RouteAnchorProgress = -1f;
                army.ClearRouteSegment();
                ArmyPresenceAdapter.SyncFromArmy(world, army);
                WorldTravelService.SyncPartyFocus(world);
                return Result.Success();
            }

            var fullTicks = WorldTravelService.ComputeTravelTicks(
                world,
                army.LeaderCharacterId,
                route.TravelCost > 0 ? route.TravelCost : 1);
            var ticks = Math.Max(8, (int)Math.Round(fullTicks * Math.Max(fraction, 0.001f)));

            army.State = FormalArmyState.OnRoute;
            army.TravelTotalTicks = ticks;
            army.RemainingTravelTicks = ticks;
            army.RouteAnchorProgress = -1f;
            army.RouteSegmentOriginProgress = startProgress;
            army.RouteSegmentEndProgress = endProgress;

            ArmyPresenceAdapter.SyncFromArmy(world, army);
            WorldTravelService.SyncPartyFocus(world);
            return Result.Success();
        }

        static Result StartArmyTravelToRouteProgress(
            SimulationWorld world,
            FormalArmy army,
            WorldRouteState route,
            float targetProgress)
        {
            // 追击改道时必须从「当前显示进度」续跑；若在 Traveling 时误用 0，
            // 每 tick Clamp 都会把军团拽回路网 FromNode（青石荒村）并重置 ticks。
            var startProgress = ResolveArmyRouteStartProgress(army);
            NormalizeFormalArmyRouteEndpoints(world, army, route);

            startProgress = Math.Max(0f, Math.Min(1f, startProgress));
            targetProgress = Math.Max(0f, Math.Min(1f, targetProgress));

            if (Math.Abs(startProgress - targetProgress) <= 0.001f)
            {
                army.State = FormalArmyState.AtNode;
                army.RouteAnchorProgress = targetProgress;
                army.RemainingTravelTicks = 0;
                army.TravelTotalTicks = 0;
                army.ClearRouteSegment();
                ArmyPresenceAdapter.SyncFromArmy(world, army);
                WorldTravelService.SyncPartyFocus(world);
                return Result.Success();
            }

            var gate = WorldTravelService.CanTraverse(route);
            if (gate.IsFailure)
                return gate;

            var fullTicks = WorldTravelService.ComputeTravelTicks(
                world,
                army.LeaderCharacterId,
                route.TravelCost > 0 ? route.TravelCost : 1);
            var fraction = Math.Abs(targetProgress - startProgress);
            var ticks = Math.Max(8, (int)Math.Round(fullTicks * Math.Max(fraction, 0.001f)));

            army.State = FormalArmyState.OnRoute;
            army.TravelTotalTicks = ticks;
            army.RemainingTravelTicks = ticks;
            army.RouteSegmentOriginProgress = startProgress;
            army.RouteSegmentEndProgress = targetProgress;
            army.RouteAnchorProgress = -1f;

            ArmyPresenceAdapter.SyncFromArmy(world, army);
            WorldTravelService.SyncPartyFocus(world);
            return Result.Success();
        }

        static float ResolveArmyRouteStartProgress(FormalArmy army)
        {
            if (army == null)
                return 0f;
            if (army.IsTraveling)
                return army.GetRouteDisplayProgress();
            if (army.IsRouteAnchored)
                return army.RouteAnchorProgress;
            return 0f;
        }

        internal static bool TryConsumeRouteProgressLeg(string legToken, out string routeId, out float progress)
        {
            routeId = string.Empty;
            progress = 0f;
            if (string.IsNullOrEmpty(legToken) ||
                !legToken.StartsWith("__route_progress__:", StringComparison.Ordinal))
                return false;

            var payload = legToken.Substring("__route_progress__:".Length);
            var split = payload.LastIndexOf(':');
            if (split <= 0 || split >= payload.Length - 1)
                return false;
            routeId = payload.Substring(0, split);
            return float.TryParse(payload.Substring(split + 1), out progress);
        }

        static bool IsRouteGraphEndpoint(WorldRouteState route, string nodeId)
        {
            if (route == null || string.IsNullOrEmpty(nodeId))
                return false;
            return string.Equals(nodeId, route.FromNodeId, StringComparison.Ordinal) ||
                   string.Equals(nodeId, route.ToNodeId, StringComparison.Ordinal);
        }

        static void NormalizeArmyForRetarget(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null || !army.IsTraveling || string.IsNullOrEmpty(army.RouteId))
                return;

            army.RouteAnchorProgress = army.GetRouteDisplayProgress();
            army.State = FormalArmyState.AtNode;
            army.RemainingTravelTicks = 0;
            army.TravelTotalTicks = 0;
            army.ClearRouteSegment();
            ArmyPresenceAdapter.SyncFromArmy(world, army);
        }

        static string ResolveArmyAnchorNodeId(FormalArmy army)
        {
            if (army == null)
                return string.Empty;
            if (army.IsRouteAnchored)
            {
                if (army.RouteAnchorProgress >= 0.5f && !string.IsNullOrEmpty(army.DestNodeId))
                    return army.DestNodeId;
                return army.NodeId ?? string.Empty;
            }

            return army.NodeId ?? string.Empty;
        }

        static void ClearArmyMemberPathQueues(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone)
                    continue;
                WorldTravelPathService.ClearAgentQueue(id);
            }
        }

        static void ClearArmyPursuitMarks(SimulationWorld world, FormalArmy army)
        {
            if (world?.Strategic == null || army == null)
                return;

            var rt = world.Strategic.Encounter;
            if (rt != null &&
                string.Equals(rt.PursueAttackerArmyId, army.ArmyId, StringComparison.Ordinal))
                StrategicPursuitService.ClearPursuit(world);

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone || !world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                wp.ClearCombatPursuit();
            }
        }
    }
}
