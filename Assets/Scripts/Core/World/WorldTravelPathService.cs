using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

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
            if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                return Result.Failure(ErrorCode.NotFound, "Traveler presence missing.", id.Value.ToString());
            if (!WorldTravelService.CanReceiveTravelOrder(world, id))
                return Result.Failure(ErrorCode.InvalidOperation, "Traveler cannot receive orders now.", id.Value.ToString());

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
            }

            var anchor = ResolveAnchorNodeId(presence);
            if (string.IsNullOrEmpty(anchor))
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot resolve travel origin.");

            var path = new List<string>(16);
            if (!TryFindNodePath(world, anchor, nodeId, path) || path.Count < 2)
                return Result.Failure(ErrorCode.InvalidOperation, "No macro route to destination.");

            var queue = GetOrCreateQueue(id);
            for (var i = 2; i < path.Count; i++)
            {
                queue.Enqueue(new PendingTravelLeg { NodeId = path[i] });
            }

            return StartTravelOneIgnoringQueue(world, id, path[1]);
        }

        static Result BeginRouteProgressTarget(
            SimulationWorld world,
            EntityId id,
            WorldAgentPresence presence,
            WorldTravelTarget target)
        {
            if (presence.HasRoutePresentation &&
                string.Equals(presence.RouteId, target.RouteId, StringComparison.Ordinal))
            {
                return WorldTravelService.StartTravelToRouteProgress(world, id, target.RouteProgress);
            }

            var anchor = ResolveAnchorNodeId(presence);
            if (string.IsNullOrEmpty(anchor))
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot resolve travel origin.");

            if (string.Equals(anchor, target.RouteFromNodeId, StringComparison.Ordinal) ||
                string.Equals(anchor, target.RouteToNodeId, StringComparison.Ordinal))
            {
                var start = string.Equals(anchor, target.RouteFromNodeId, StringComparison.Ordinal) ? 0f : 1f;
                presence.NodeId = target.RouteFromNodeId;
                presence.DestNodeId = target.RouteToNodeId;
                presence.RouteId = target.RouteId;
                if (presence.Mode != PartyWorldPresenceMode.RouteAnchored)
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

            var queue = GetOrCreateQueue(id);
            for (var i = 2; i < path.Count; i++)
                queue.Enqueue(new PendingTravelLeg { NodeId = path[i] });
            queue.Enqueue(new PendingTravelLeg
            {
                IsRoute = true,
                RouteId = target.RouteId,
                RouteFromNodeId = target.RouteFromNodeId,
                RouteToNodeId = target.RouteToNodeId,
                RouteProgress = target.RouteProgress
            });

            return StartTravelOneIgnoringQueue(world, id, path[1]);
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

            if (presence.Mode == PartyWorldPresenceMode.RouteAnchored ||
                (presence.Mode == PartyWorldPresenceMode.Traveling && presence.HasRoutePresentation))
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
