using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    /// <summary>宏观路径规划：多节点 BFS、道路进度、队列续走。</summary>
    public static class WorldTravelPathService
    {
        sealed class PendingTravelLeg
        {
            public bool IsRoute;
            public string NodeId = string.Empty;
            public string RouteId = string.Empty;
            public string RouteFromNodeId = string.Empty;
            public string RouteToNodeId = string.Empty;
            public float RouteProgress;
        }

        static readonly Dictionary<ulong, Queue<PendingTravelLeg>> Queues =
            new Dictionary<ulong, Queue<PendingTravelLeg>>();

        public static void ClearAllQueues() => Queues.Clear();

        public static bool HasPendingLegs(EntityId id) =>
            !id.IsNone && Queues.TryGetValue(id.Value, out var q) && q.Count > 0;

        public static bool TryFindNodePath(
            SimulationWorld world,
            string fromNodeId,
            string toNodeId,
            List<string> pathOut)
        {
            pathOut?.Clear();
            if (world?.WorldGraph == null || pathOut == null ||
                string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(toNodeId))
                return false;
            if (string.Equals(fromNodeId, toNodeId, StringComparison.Ordinal))
                return false;
            if (!world.WorldGraph.TryGetNode(fromNodeId, out _) ||
                !world.WorldGraph.TryGetNode(toNodeId, out _))
                return false;

            var prev = new Dictionary<string, string>(StringComparer.Ordinal);
            var q = new Queue<string>();
            q.Enqueue(fromNodeId);
            prev[fromNodeId] = fromNodeId;

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (string.Equals(cur, toNodeId, StringComparison.Ordinal))
                {
                    ReconstructPath(prev, fromNodeId, toNodeId, pathOut);
                    return pathOut.Count > 1;
                }

                foreach (var kv in world.WorldGraph.Routes)
                {
                    var route = kv.Value;
                    if (route == null)
                        continue;
                    if (WorldTravelService.CanTraverse(route).IsFailure)
                        continue;

                    TryEnqueueNeighbor(q, prev, cur, route.FromNodeId, route.ToNodeId);
                    if (!route.Directed)
                        TryEnqueueNeighbor(q, prev, cur, route.ToNodeId, route.FromNodeId);
                }
            }

            return false;
        }

        static void TryEnqueueNeighbor(
            Queue<string> q,
            Dictionary<string, string> prev,
            string cur,
            string a,
            string b)
        {
            string next = null;
            if (string.Equals(cur, a, StringComparison.Ordinal))
                next = b;
            else if (string.Equals(cur, b, StringComparison.Ordinal))
                next = a;
            if (string.IsNullOrEmpty(next) || prev.ContainsKey(next))
                return;
            prev[next] = cur;
            q.Enqueue(next);
        }

        static void ReconstructPath(
            Dictionary<string, string> prev,
            string from,
            string to,
            List<string> pathOut)
        {
            pathOut.Clear();
            var cur = to;
            while (!string.Equals(cur, from, StringComparison.Ordinal))
            {
                pathOut.Add(cur);
                cur = prev[cur];
            }

            pathOut.Add(from);
            pathOut.Reverse();
        }

        public static bool TryGetNextHopTowardNode(
            SimulationWorld world,
            string fromNodeId,
            string toNodeId,
            out string nextHopNodeId)
        {
            nextHopNodeId = string.Empty;
            if (world == null || string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(toNodeId))
                return false;
            if (string.Equals(fromNodeId, toNodeId, StringComparison.Ordinal))
                return false;

            var path = new List<string>(16);
            if (!TryFindNodePath(world, fromNodeId, toNodeId, path) || path.Count < 2)
                return false;
            nextHopNodeId = path[1];
            return !string.IsNullOrEmpty(nextHopNodeId);
        }

        public static bool CanAgentReachTarget(
            SimulationWorld world,
            WorldAgentPresence presence,
            WorldTravelTarget target)
        {
            if (world == null || presence == null || !WorldTravelService.CanReceiveTravelOrder(world, presence.EntityId))
                return false;

            if (target.IsRouteProgress)
                return CanReachRouteProgress(world, presence, target);

            if (string.IsNullOrEmpty(target.NodeId))
                return false;
            if (presence.Mode == PartyWorldPresenceMode.AtNode &&
                string.Equals(presence.NodeId, target.NodeId, StringComparison.Ordinal))
                return false;

            // 路中／清场后仍 InEncounter：当前道路两端可直达（勿用「较近端」当 BFS 起点，否则回原端会判不可达）
            if (presence.HasRoutePresentation &&
                (presence.Mode == PartyWorldPresenceMode.RouteAnchored ||
                 presence.Mode == PartyWorldPresenceMode.Traveling ||
                 presence.Mode == PartyWorldPresenceMode.InEncounter))
            {
                if (string.Equals(target.NodeId, presence.NodeId, StringComparison.Ordinal))
                    return presence.TravelProgress > 0.01f;
                if (string.Equals(target.NodeId, presence.DestNodeId, StringComparison.Ordinal))
                    return presence.TravelProgress < 0.99f;
            }

            var anchor = ResolveAnchorNodeId(presence);
            if (string.IsNullOrEmpty(anchor))
                return false;
            if (string.Equals(anchor, target.NodeId, StringComparison.Ordinal) &&
                presence.Mode == PartyWorldPresenceMode.AtNode)
                return false;

            var scratch = new List<string>(16);
            return TryFindNodePath(world, anchor, target.NodeId, scratch);
        }

        static bool CanReachRouteProgress(
            SimulationWorld world,
            WorldAgentPresence presence,
            WorldTravelTarget target)
        {
            if (string.IsNullOrEmpty(target.RouteId))
                return false;

            if (presence.HasRoutePresentation &&
                string.Equals(presence.RouteId, target.RouteId, StringComparison.Ordinal))
            {
                return Math.Abs(presence.TravelProgress - target.RouteProgress) > 0.01f;
            }

            var anchor = ResolveAnchorNodeId(presence);
            if (string.IsNullOrEmpty(anchor))
                return false;

            var scratch = new List<string>(16);
            return TryFindNodePath(world, anchor, target.RouteFromNodeId, scratch) ||
                   TryFindNodePath(world, anchor, target.RouteToNodeId, scratch);
        }

        public static Result StartAgentTravelToTarget(
            SimulationWorld world,
            EntityId id,
            WorldTravelTarget target)
        {
            if (world == null || id.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid travel order.");
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "手动遭遇进行中：禁止战略出行（逃跑须走撤退结算）。",
                    id.Value.ToString());
            if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                return Result.Failure(ErrorCode.NotFound, "Traveler presence missing.", id.Value.ToString());
            if (!WorldTravelService.CanReceiveTravelOrder(world, id))
                return Result.Failure(ErrorCode.InvalidOperation, "Traveler cannot receive orders now.", id.Value.ToString());

            if (presence.Mode == PartyWorldPresenceMode.InEncounter)
            {
                StrategicEncounterSpawner.ReleaseEngagedForMacroTravel(world, id);
                if (!world.WorldPresence.TryGet(id, out presence) || presence == null)
                    return Result.Failure(ErrorCode.NotFound, "Traveler presence missing.", id.Value.ToString());
            }

            ClearQueue(id);
            NormalizePresenceForRetarget(presence);
            presence.ClearFollow();

            if (target.IsRouteProgress)
                return BeginRouteProgressTarget(world, id, presence, target);

            return BeginNodeTarget(world, id, presence, target.NodeId);
        }

        public static void TryContinueQueuedTravel(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !Queues.TryGetValue(id.Value, out var queue) || queue.Count == 0)
                return;
            if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                return;

            var leg = queue.Dequeue();
            if (queue.Count == 0)
                Queues.Remove(id.Value);

            if (leg.IsRoute)
            {
                var target = WorldTravelTarget.OnRoute(
                    leg.RouteId,
                    leg.RouteFromNodeId,
                    leg.RouteToNodeId,
                    leg.RouteProgress);
                BeginRouteProgressTarget(world, id, presence, target);
                return;
            }

            if (!string.IsNullOrEmpty(leg.NodeId))
                StartTravelOneIgnoringQueue(world, id, leg.NodeId);
        }

        static Result BeginNodeTarget(
            SimulationWorld world,
            EntityId id,
            WorldAgentPresence presence,
            string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return Result.Failure(ErrorCode.InvalidArgument, "Node target required.");

            if (presence.Mode == PartyWorldPresenceMode.RouteAnchored)
            {
                if (string.Equals(nodeId, presence.NodeId, StringComparison.Ordinal) &&
                    presence.RouteAnchorProgress <= 0.01f)
                    return Result.Failure(ErrorCode.InvalidArgument, "Already at route origin.");
                if (string.Equals(nodeId, presence.DestNodeId, StringComparison.Ordinal) &&
                    presence.RouteAnchorProgress >= 0.99f)
                    return Result.Failure(ErrorCode.InvalidArgument, "Already at route destination.");
                if (string.Equals(nodeId, presence.NodeId, StringComparison.Ordinal) ||
                    string.Equals(nodeId, presence.DestNodeId, StringComparison.Ordinal))
                    return WorldTravelService.StartTravel(world, id, nodeId);

                // 金丹前：先沿当前道路走到较近端点，再续走后续节点（禁止路中瞬移切路）
                var exit = ResolveAnchorNodeId(presence);
                if (string.IsNullOrEmpty(exit))
                    return Result.Failure(ErrorCode.InvalidOperation, "Cannot resolve travel origin.");

                var path = new List<string>(16);
                if (!TryFindNodePath(world, exit, nodeId, path) || path.Count < 1)
                    return Result.Failure(ErrorCode.InvalidOperation, "No macro route to destination.");

                var queue = GetOrCreateQueue(id);
                for (var i = 1; i < path.Count; i++)
                    queue.Enqueue(new PendingTravelLeg { NodeId = path[i] });

                return WorldTravelService.StartTravel(world, id, exit);
            }

            var anchor = ResolveAnchorNodeId(presence);
            if (string.IsNullOrEmpty(anchor))
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot resolve travel origin.");

            var pathFromAnchor = new List<string>(16);
            if (!TryFindNodePath(world, anchor, nodeId, pathFromAnchor) || pathFromAnchor.Count < 2)
                return Result.Failure(ErrorCode.InvalidOperation, "No macro route to destination.");

            var q = GetOrCreateQueue(id);
            for (var i = 2; i < pathFromAnchor.Count; i++)
                q.Enqueue(new PendingTravelLeg { NodeId = pathFromAnchor[i] });

            return StartTravelOneIgnoringQueue(world, id, pathFromAnchor[1]);
        }

        static Result BeginRouteProgressTarget(
            SimulationWorld world,
            EntityId id,
            WorldAgentPresence presence,
            WorldTravelTarget target)
        {
            if (string.IsNullOrEmpty(target.RouteId))
                return Result.Failure(ErrorCode.InvalidArgument, "Route target required.");

            // 已在同一道路：对齐 Node→Dest 与点选 From→To，再沿进度走（禁止跳变）
            if (IsOnSameRoute(presence, target))
            {
                AlignPresenceToRouteTarget(presence, target);
                if (presence.Mode == PartyWorldPresenceMode.Traveling)
                    presence.AnchorOnRoute(presence.TravelProgress);
                return WorldTravelService.StartTravelToRouteProgress(world, id, target.RouteProgress);
            }

            var anchor = ResolveAnchorNodeId(presence);
            if (string.IsNullOrEmpty(anchor))
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot resolve travel origin.");

            // 仅当人已真正站在目标路的端点时，才允许挂上该路再走进度。
            // 禁止：人在另一条路中段、只因「较近端」等于目标路端点，就改 RouteId 保留旧进度 → 瞬移。
            if (CanMountRouteAtSharedEndpoint(presence, target, anchor))
            {
                var start = string.Equals(anchor, target.RouteFromNodeId, StringComparison.Ordinal)
                    ? 0f
                    : 1f;
                presence.NodeId = target.RouteFromNodeId;
                presence.DestNodeId = target.RouteToNodeId;
                presence.RouteId = target.RouteId;
                presence.AnchorOnRoute(start);
                return WorldTravelService.StartTravelToRouteProgress(world, id, target.RouteProgress);
            }

            var pathToFrom = new List<string>(16);
            var pathToTo = new List<string>(16);
            var canFrom = TryFindNodePath(world, anchor, target.RouteFromNodeId, pathToFrom);
            var canTo = TryFindNodePath(world, anchor, target.RouteToNodeId, pathToTo);
            if (!canFrom && !canTo)
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot reach target road.");

            var useFrom = canFrom && (!canTo || pathToFrom.Count <= pathToTo.Count);
            var path = useFrom ? pathToFrom : pathToTo;
            if (path.Count < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot reach target road.");

            var routeLeg = new PendingTravelLeg
            {
                IsRoute = true,
                RouteId = target.RouteId,
                RouteFromNodeId = target.RouteFromNodeId,
                RouteToNodeId = target.RouteToNodeId,
                RouteProgress = target.RouteProgress
            };

            // 仍在别的道路中段：先走到较近端，再按节点路径＋目标路进度续走
            if (presence.Mode == PartyWorldPresenceMode.RouteAnchored &&
                presence.HasRoutePresentation &&
                !string.Equals(presence.RouteId, target.RouteId, StringComparison.Ordinal))
            {
                var exit = ResolveAnchorNodeId(presence);
                if (string.IsNullOrEmpty(exit))
                    return Result.Failure(ErrorCode.InvalidOperation, "Cannot resolve travel origin.");

                var queue = GetOrCreateQueue(id);
                // path[0] 应为 exit／anchor；其后节点入队，最后目标路进度
                for (var i = 1; i < path.Count; i++)
                    queue.Enqueue(new PendingTravelLeg { NodeId = path[i] });
                queue.Enqueue(routeLeg);
                return WorldTravelService.StartTravel(world, id, exit);
            }

            if (path.Count < 2)
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot reach target road.");

            var q = GetOrCreateQueue(id);
            for (var i = 2; i < path.Count; i++)
                q.Enqueue(new PendingTravelLeg { NodeId = path[i] });
            q.Enqueue(routeLeg);
            return StartTravelOneIgnoringQueue(world, id, path[1]);
        }

        static bool IsOnSameRoute(WorldAgentPresence presence, WorldTravelTarget target)
        {
            if (presence == null || !target.IsRouteProgress || string.IsNullOrEmpty(target.RouteId))
                return false;
            if (!string.Equals(presence.RouteId, target.RouteId, StringComparison.Ordinal))
                return false;
            if (presence.HasRoutePresentation)
                return true;
            return presence.Mode == PartyWorldPresenceMode.RouteAnchored &&
                   presence.RouteAnchorProgress >= 0f;
        }

        /// <summary>
        /// 人已在目标路共享端点上（AtNode，或当前路锚贴在该端），才可挂路。
        /// </summary>
        static bool CanMountRouteAtSharedEndpoint(
            WorldAgentPresence presence,
            WorldTravelTarget target,
            string anchor)
        {
            if (presence == null || string.IsNullOrEmpty(anchor))
                return false;
            if (!string.Equals(anchor, target.RouteFromNodeId, StringComparison.Ordinal) &&
                !string.Equals(anchor, target.RouteToNodeId, StringComparison.Ordinal))
                return false;

            if (presence.Mode == PartyWorldPresenceMode.AtNode &&
                string.Equals(presence.NodeId, anchor, StringComparison.Ordinal))
                return true;

            if (presence.Mode != PartyWorldPresenceMode.RouteAnchored)
                return false;

            if (string.Equals(presence.RouteId, target.RouteId, StringComparison.Ordinal))
                return true;

            if (!presence.HasRoutePresentation)
                return false;

            if (string.Equals(anchor, presence.NodeId, StringComparison.Ordinal) &&
                presence.RouteAnchorProgress <= 0.02f)
                return true;
            if (string.Equals(anchor, presence.DestNodeId, StringComparison.Ordinal) &&
                presence.RouteAnchorProgress >= 0.98f)
                return true;

            return false;
        }

        /// <summary>把 Presence 的 Node→Dest 对齐到点选目标的 From→To，进度同步翻转。</summary>
        static void AlignPresenceToRouteTarget(WorldAgentPresence presence, WorldTravelTarget target)
        {
            if (presence == null || !target.IsRouteProgress)
                return;

            if (string.Equals(presence.NodeId, target.RouteFromNodeId, StringComparison.Ordinal) &&
                string.Equals(presence.DestNodeId, target.RouteToNodeId, StringComparison.Ordinal))
                return;

            if (string.Equals(presence.NodeId, target.RouteToNodeId, StringComparison.Ordinal) &&
                string.Equals(presence.DestNodeId, target.RouteFromNodeId, StringComparison.Ordinal))
            {
                presence.NodeId = target.RouteFromNodeId;
                presence.DestNodeId = target.RouteToNodeId;
                if (presence.Mode == PartyWorldPresenceMode.RouteAnchored &&
                    presence.RouteAnchorProgress >= 0f)
                    presence.RouteAnchorProgress = 1f - Clamp01(presence.RouteAnchorProgress);
                if (presence.Mode == PartyWorldPresenceMode.Traveling &&
                    presence.RouteSegmentOriginProgress >= 0f &&
                    presence.RouteSegmentEndProgress >= 0f)
                {
                    var o = presence.RouteSegmentOriginProgress;
                    var e = presence.RouteSegmentEndProgress;
                    presence.RouteSegmentOriginProgress = 1f - e;
                    presence.RouteSegmentEndProgress = 1f - o;
                }

                return;
            }

            // Dest 缺失或端点不匹配：用目标路端点补齐，尽量保留当前进度
            var keep = presence.RouteAnchorProgress >= 0f
                ? presence.RouteAnchorProgress
                : presence.TravelProgress;
            presence.NodeId = string.IsNullOrEmpty(target.RouteFromNodeId)
                ? presence.NodeId
                : target.RouteFromNodeId;
            presence.DestNodeId = target.RouteToNodeId ?? string.Empty;
            presence.RouteId = target.RouteId;
            if (presence.Mode == PartyWorldPresenceMode.RouteAnchored)
                presence.RouteAnchorProgress = Clamp01(keep);
        }

        static float Clamp01(float v)
        {
            if (v < 0f)
                return 0f;
            if (v > 1f)
                return 1f;
            return v;
        }

        static Result StartTravelOneIgnoringQueue(SimulationWorld world, EntityId id, string nodeId)
        {
            var result = WorldTravelService.StartTravel(world, id, nodeId);
            if (result.IsSuccess)
                WorldTravelService.SyncPartyFocus(world);
            return result;
        }

        static void NormalizePresenceForRetarget(WorldAgentPresence presence)
        {
            if (presence == null)
                return;
            if (presence.Mode == PartyWorldPresenceMode.Traveling)
                presence.AnchorOnRoute(presence.TravelProgress);
        }

        public static string ResolveAnchorNodeId(WorldAgentPresence presence)
        {
            if (presence == null || string.IsNullOrEmpty(presence.NodeId))
                return string.Empty;

            // 含清场后仍 InEncounter、尚未 Release 上路的人
            if (presence.HasRoutePresentation &&
                (presence.Mode == PartyWorldPresenceMode.RouteAnchored ||
                 presence.Mode == PartyWorldPresenceMode.Traveling ||
                 presence.Mode == PartyWorldPresenceMode.InEncounter))
            {
                if (presence.TravelProgress >= 0.5f && !string.IsNullOrEmpty(presence.DestNodeId))
                    return presence.DestNodeId;
                return presence.NodeId;
            }

            return presence.NodeId;
        }

        static Queue<PendingTravelLeg> GetOrCreateQueue(EntityId id)
        {
            if (!Queues.TryGetValue(id.Value, out var queue))
            {
                queue = new Queue<PendingTravelLeg>();
                Queues[id.Value] = queue;
            }

            return queue;
        }

        static void ClearQueue(EntityId id)
        {
            if (!id.IsNone)
                Queues.Remove(id.Value);
        }

        public static bool TryPickRouteTarget(
            WorldGraphBoard graph,
            float worldX,
            float worldY,
            float maxWorldDistance,
            out WorldTravelTarget target)
        {
            target = default;
            if (graph == null)
                return false;

            var bestDistSq = maxWorldDistance * maxWorldDistance;
            var found = false;
            foreach (var kv in graph.Routes)
            {
                var route = kv.Value;
                if (route == null ||
                    !graph.TryGetNode(route.FromNodeId, out var from) ||
                    !graph.TryGetNode(route.ToNodeId, out var to))
                    continue;

                var dx = to.WorldX - from.WorldX;
                var dy = to.WorldY - from.WorldY;
                var lenSq = dx * dx + dy * dy;
                if (lenSq <= 0.0001f)
                    continue;

                var t = ((worldX - from.WorldX) * dx + (worldY - from.WorldY) * dy) / lenSq;
                t = Math.Max(0f, Math.Min(1f, t));
                var px = from.WorldX + dx * t;
                var py = from.WorldY + dy * t;
                var distSq = (worldX - px) * (worldX - px) + (worldY - py) * (worldY - py);
                if (distSq > bestDistSq)
                    continue;

                bestDistSq = distSq;
                target = WorldTravelTarget.OnRoute(route.Id, route.FromNodeId, route.ToNodeId, t);
                found = true;
            }

            return found;
        }
    }
}
