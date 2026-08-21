using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>开战前宏观位置快照（支援结束后默认还原，防瞬移）。</summary>
    public sealed class PreBattleWorldPresence
    {
        public PartyWorldPresenceMode Mode { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        public string DestNodeId { get; set; } = string.Empty;
        public int RemainingTravelTicks { get; set; }
        public int TravelTotalTicks { get; set; }
        public float RouteAnchorProgress { get; set; } = -1f;
        public float RouteSegmentOriginProgress { get; set; } = -1f;
        public float RouteSegmentEndProgress { get; set; } = -1f;
        public string FollowStackId { get; set; } = string.Empty;
        public string CombatPursuitStackId { get; set; } = string.Empty;

        public static PreBattleWorldPresence Capture(WorldAgentPresence p)
        {
            if (p == null)
                return null;
            return new PreBattleWorldPresence
            {
                Mode = p.Mode,
                NodeId = p.NodeId ?? string.Empty,
                RouteId = p.RouteId ?? string.Empty,
                DestNodeId = p.DestNodeId ?? string.Empty,
                RemainingTravelTicks = p.RemainingTravelTicks,
                TravelTotalTicks = p.TravelTotalTicks,
                RouteAnchorProgress = p.RouteAnchorProgress,
                RouteSegmentOriginProgress = p.RouteSegmentOriginProgress,
                RouteSegmentEndProgress = p.RouteSegmentEndProgress,
                FollowStackId = p.FollowStackId ?? string.Empty,
                CombatPursuitStackId = p.CombatPursuitStackId ?? string.Empty
            };
        }

        public void ApplyTo(WorldAgentPresence p)
        {
            if (p == null)
                return;
            p.Mode = Mode;
            p.NodeId = NodeId ?? string.Empty;
            p.RouteId = RouteId ?? string.Empty;
            p.DestNodeId = DestNodeId ?? string.Empty;
            p.RemainingTravelTicks = RemainingTravelTicks;
            p.TravelTotalTicks = TravelTotalTicks;
            p.RouteAnchorProgress = RouteAnchorProgress;
            p.RouteSegmentOriginProgress = RouteSegmentOriginProgress;
            p.RouteSegmentEndProgress = RouteSegmentEndProgress;
            p.FollowStackId = FollowStackId ?? string.Empty;
            p.CombatPursuitStackId = CombatPursuitStackId ?? string.Empty;
        }
    }

    public enum BattleParticipantKind
    {
        MandatoryFriendly = 0,
        OptionalFriendly = 1,
        EnemyPrimary = 2,
        EnemyReinforcement = 3
    }

    public sealed class BattleParticipantRecord
    {
        public BattleParticipantKind Kind { get; set; }
        public EntityId EntityId { get; set; }
        public string ArmyStackId { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public int CombatPower { get; set; }
        /// <summary>可选支援：玩家是否勾选加入。</summary>
        public bool Selected { get; set; }
        public PreBattleWorldPresence PreBattle { get; set; }
    }

    /// <summary>BattleOffer 产生时的参战快照（ADR-0023 Phase B）。</summary>
    public sealed class BattleParticipantSnapshot
    {
        public string OfferId { get; set; } = string.Empty;
        public string BattleAnchorNodeId { get; set; } = string.Empty;
        public string BattleAnchorDestNodeId { get; set; } = string.Empty;
        public string BattleAnchorRouteId { get; set; } = string.Empty;
        public float BattleAnchorProgress { get; set; } = -1f;
        public string PrimaryEnemyStackId { get; set; } = string.Empty;
        public string EncounterLocalMapId { get; set; } =
            StrategicEncounterCatalog.DefaultEncounterLocalMapId;
        public string LastBattleSummary { get; set; } = string.Empty;
        public bool PlayerWon { get; set; }
        /// <summary>自动战已出结果、待确认结算弹窗（与手动战后非强制条区分）。</summary>
        public bool IsAutoSettlement { get; set; }

        readonly List<BattleParticipantRecord> _records = new List<BattleParticipantRecord>(16);

        public IReadOnlyList<BattleParticipantRecord> Records => _records;

        public void Clear()
        {
            OfferId = string.Empty;
            BattleAnchorNodeId = string.Empty;
            BattleAnchorDestNodeId = string.Empty;
            BattleAnchorRouteId = string.Empty;
            BattleAnchorProgress = -1f;
            PrimaryEnemyStackId = string.Empty;
            EncounterLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            LastBattleSummary = string.Empty;
            PlayerWon = false;
            IsAutoSettlement = false;
            _records.Clear();
        }

        public void Add(BattleParticipantRecord record)
        {
            if (record == null)
                return;
            _records.Add(record);
        }

        public List<EntityId> CollectSelectedFriendly()
        {
            var list = new List<EntityId>(_records.Count);
            for (var i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r.EntityId.IsNone)
                    continue;
                if (r.Kind == BattleParticipantKind.MandatoryFriendly ||
                    (r.Kind == BattleParticipantKind.OptionalFriendly && r.Selected))
                    list.Add(r.EntityId);
            }

            return list;
        }

        public List<string> CollectEnemyStackIds()
        {
            var list = new List<string>(4);
            for (var i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (string.IsNullOrEmpty(r.ArmyStackId))
                    continue;
                if (r.Kind != BattleParticipantKind.EnemyPrimary &&
                    r.Kind != BattleParticipantKind.EnemyReinforcement)
                    continue;
                if (!list.Contains(r.ArmyStackId))
                    list.Add(r.ArmyStackId);
            }

            return list;
        }

        public BattleParticipantRecord FindByEntity(EntityId id)
        {
            if (id.IsNone)
                return null;
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].EntityId == id)
                    return _records[i];
            }

            return null;
        }
    }

    /// <summary>
    /// 战略支援距离：大地图世界坐标近距（约 2～3 人头像宽），非相邻节点、非像素点击判定。
    /// </summary>
    public static class ReinforcementRangeService
    {
        /// <summary>遗留 TravelCost 阈值（诊断用）。</summary>
        public static int DefaultThreshold { get; set; } = 24;

        /// <summary>遗留最大跳数（诊断用）。</summary>
        public static int DefaultMaxHops { get; set; } = 1;

        /// <summary>
        /// 默认世界坐标半径。ch01 节点间距约 2～4；0.25 ≈ 贴战场极近，不含邻村。
        /// </summary>
        public static float DefaultWorldRadius { get; set; } = 0.25f;

        public static int GetThreshold(SimulationWorld world)
        {
            if (world?.Strategic != null && world.Strategic.ReinforcementTravelCostThreshold > 0)
                return world.Strategic.ReinforcementTravelCostThreshold;
            return DefaultThreshold;
        }

        public static int GetMaxHops(SimulationWorld world)
        {
            if (world?.Strategic != null && world.Strategic.ReinforcementMaxHops >= 0)
                return world.Strategic.ReinforcementMaxHops;
            return DefaultMaxHops;
        }

        public static float GetWorldRadius(SimulationWorld world)
        {
            if (world?.Strategic != null && world.Strategic.ReinforcementWorldRadius > 0f)
                return world.Strategic.ReinforcementWorldRadius;
            return DefaultWorldRadius;
        }

        public static bool TryGetStrategicTravelCost(
            SimulationWorld world,
            WorldAgentPresence from,
            string anchorNodeId,
            string anchorRouteId,
            float anchorProgress,
            out int cost)
        {
            cost = int.MaxValue;
            if (world?.WorldGraph == null || from == null)
                return false;

            var fromNode = ResolvePresenceNodeForDistance(from, out var fromRoutePartial);
            var toNode = ResolveAnchorNode(world, anchorNodeId, anchorRouteId, anchorProgress);
            if (string.IsNullOrEmpty(fromNode) || string.IsNullOrEmpty(toNode))
                return false;

            if (string.Equals(fromNode, toNode, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(from.RouteId) &&
                    string.Equals(from.RouteId, anchorRouteId, StringComparison.Ordinal) &&
                    world.WorldGraph.TryGetRoute(from.RouteId, out var sameRoute))
                {
                    var edge = sameRoute.TravelCost > 0 ? sameRoute.TravelCost : 1;
                    var dp = Math.Abs(GetPresenceProgress(from) - Clamp01(anchorProgress));
                    cost = (int)Math.Ceiling(dp * edge);
                    return true;
                }

                cost = fromRoutePartial;
                return true;
            }

            if (!TrySumPathTravelCost(world, fromNode, toNode, out cost))
                return false;
            cost += fromRoutePartial;
            return true;
        }

        public static bool IsWithinReinforcementRange(
            SimulationWorld world,
            WorldAgentPresence presence,
            string anchorNodeId,
            string anchorRouteId,
            float anchorProgress)
        {
            if (!TryGetWorldDistance(
                    world, presence, anchorNodeId, anchorRouteId, anchorProgress, out var dist))
                return false;
            return dist <= GetWorldRadius(world);
        }

        /// <summary>大地图世界坐标距离（节点／路段插值）。</summary>
        public static bool TryGetWorldDistance(
            SimulationWorld world,
            WorldAgentPresence from,
            string anchorNodeId,
            string anchorRouteId,
            float anchorProgress,
            out float distance)
        {
            distance = float.MaxValue;
            if (!TryGetPresenceWorldXY(world, from, out var fx, out var fy))
                return false;
            if (!TryGetAnchorWorldXY(world, anchorNodeId, anchorRouteId, anchorProgress, out var ax, out var ay))
                return false;
            var dx = fx - ax;
            var dy = fy - ay;
            distance = (float)Math.Sqrt(dx * dx + dy * dy);
            return true;
        }

        public static bool TryGetPresenceWorldXY(
            SimulationWorld world,
            WorldAgentPresence presence,
            out float x,
            out float y)
        {
            x = y = 0f;
            if (world == null || presence == null)
                return false;
            if (!WorldTravelService.TryResolveTravelWorldPoints(
                    world, presence, out var fx, out var fy, out var tx, out var ty))
                return false;

            if (presence.HasRoutePresentation)
            {
                var t = GetPresenceProgress(presence);
                x = fx + (tx - fx) * t;
                y = fy + (ty - fy) * t;
                return true;
            }

            x = fx;
            y = fy;
            return true;
        }

        public static bool TryGetAnchorWorldXY(
            SimulationWorld world,
            string anchorNodeId,
            string anchorRouteId,
            float anchorProgress,
            out float x,
            out float y)
        {
            x = y = 0f;
            if (world?.WorldGraph == null)
                return false;

            if (!string.IsNullOrEmpty(anchorRouteId) &&
                anchorProgress >= 0f &&
                world.WorldGraph.TryGetRoute(anchorRouteId, out var route) &&
                world.WorldGraph.TryGetNode(route.FromNodeId, out var from) &&
                world.WorldGraph.TryGetNode(route.ToNodeId, out var to))
            {
                var t = Clamp01(anchorProgress);
                x = from.WorldX + (to.WorldX - from.WorldX) * t;
                y = from.WorldY + (to.WorldY - from.WorldY) * t;
                return true;
            }

            var nodeId = ResolveAnchorNode(world, anchorNodeId, anchorRouteId, anchorProgress);
            if (string.IsNullOrEmpty(nodeId) || !world.WorldGraph.TryGetNode(nodeId, out var node))
                return false;
            x = node.WorldX;
            y = node.WorldY;
            return true;
        }

        /// <summary>节点跳数（同节点＝0；相邻＝1）。诊断用。</summary>
        public static bool TryGetHopDistance(
            SimulationWorld world,
            WorldAgentPresence from,
            string anchorNodeId,
            string anchorRouteId,
            float anchorProgress,
            out int hops)
        {
            hops = int.MaxValue;
            if (world?.WorldGraph == null || from == null)
                return false;

            var fromNode = ResolvePresenceNodeForDistance(from, out _);
            var toNode = ResolveAnchorNode(world, anchorNodeId, anchorRouteId, anchorProgress);
            if (string.IsNullOrEmpty(fromNode) || string.IsNullOrEmpty(toNode))
                return false;

            return TryCountPathHops(world, fromNode, toNode, out hops);
        }

        public static bool IsStackWithinRange(
            SimulationWorld world,
            ArmyStack stack,
            string anchorNodeId,
            string anchorRouteId,
            float anchorProgress)
        {
            if (stack == null)
                return false;
            var fake = new WorldAgentPresence
            {
                Mode = stack.IsRoutePositioned
                    ? (stack.IsTraveling ? PartyWorldPresenceMode.Traveling : PartyWorldPresenceMode.RouteAnchored)
                    : PartyWorldPresenceMode.AtNode,
                NodeId = stack.NodeId ?? string.Empty,
                DestNodeId = stack.DestNodeId ?? string.Empty,
                RouteId = stack.RouteId ?? string.Empty,
                RouteAnchorProgress = stack.IsRouteAnchored ? stack.GetRouteDisplayProgress() : -1f,
                TravelTotalTicks = stack.TravelTotalTicks,
                RemainingTravelTicks = stack.RemainingTravelTicks
            };
            return IsWithinReinforcementRange(
                world, fake, anchorNodeId, anchorRouteId, anchorProgress);
        }

        static string ResolvePresenceNodeForDistance(WorldAgentPresence p, out int partialCost)
        {
            partialCost = 0;
            if (p == null)
                return string.Empty;
            if (p.Mode == PartyWorldPresenceMode.AtNode)
                return p.NodeId ?? string.Empty;

            // 途中／路锚：取较近端，并把未走完路段折算为 partial
            if (!string.IsNullOrEmpty(p.RouteId) && !string.IsNullOrEmpty(p.NodeId))
            {
                var progress = GetPresenceProgress(p);
                if (progress <= 0.5f)
                {
                    partialCost = 0;
                    return p.NodeId;
                }

                partialCost = 0;
                return string.IsNullOrEmpty(p.DestNodeId) ? p.NodeId : p.DestNodeId;
            }

            return p.NodeId ?? string.Empty;
        }

        static string ResolveAnchorNode(
            SimulationWorld world,
            string anchorNodeId,
            string anchorRouteId,
            float anchorProgress)
        {
            if (!string.IsNullOrEmpty(anchorNodeId) &&
                (string.IsNullOrEmpty(anchorRouteId) || anchorProgress < 0f))
                return anchorNodeId;

            if (!string.IsNullOrEmpty(anchorRouteId) &&
                world.WorldGraph.TryGetRoute(anchorRouteId, out var route))
            {
                var p = Clamp01(anchorProgress);
                if (p <= 0.5f)
                    return route.FromNodeId;
                return route.ToNodeId;
            }

            return anchorNodeId ?? string.Empty;
        }

        static float GetPresenceProgress(WorldAgentPresence p)
        {
            if (p == null)
                return 0f;
            if (p.Mode == PartyWorldPresenceMode.RouteAnchored)
                return Clamp01(p.RouteAnchorProgress);
            if (p.TravelTotalTicks > 0)
                return Clamp01(p.TravelProgress);
            if (p.RouteAnchorProgress >= 0f)
                return Clamp01(p.RouteAnchorProgress);
            return 0f;
        }

        static float Clamp01(float v)
        {
            if (v < 0f)
                return 0f;
            if (v > 1f)
                return 1f;
            return v;
        }

        /// <summary>最短路径边数（BFS）。</summary>
        public static bool TryCountPathHops(
            SimulationWorld world,
            string fromNodeId,
            string toNodeId,
            out int hops)
        {
            hops = 0;
            if (world?.WorldGraph == null ||
                string.IsNullOrEmpty(fromNodeId) ||
                string.IsNullOrEmpty(toNodeId))
                return false;
            if (string.Equals(fromNodeId, toNodeId, StringComparison.Ordinal))
                return true;

            var dist = new Dictionary<string, int>(StringComparer.Ordinal);
            var q = new Queue<string>();
            dist[fromNodeId] = 0;
            q.Enqueue(fromNodeId);

            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                var curDist = dist[cur];
                if (string.Equals(cur, toNodeId, StringComparison.Ordinal))
                {
                    hops = curDist;
                    return true;
                }

                foreach (var kv in world.WorldGraph.Routes)
                {
                    var route = kv.Value;
                    if (route == null || WorldTravelService.CanTraverse(route).IsFailure)
                        continue;
                    TryEnqueueHop(q, dist, cur, route.FromNodeId, route.ToNodeId, curDist);
                    if (!route.Directed)
                        TryEnqueueHop(q, dist, cur, route.ToNodeId, route.FromNodeId, curDist);
                }
            }

            return false;
        }

        static void TryEnqueueHop(
            Queue<string> q,
            Dictionary<string, int> dist,
            string from,
            string edgeFrom,
            string edgeTo,
            int fromDist)
        {
            if (!string.Equals(from, edgeFrom, StringComparison.Ordinal) ||
                string.IsNullOrEmpty(edgeTo))
                return;
            if (dist.ContainsKey(edgeTo))
                return;
            dist[edgeTo] = fromDist + 1;
            q.Enqueue(edgeTo);
        }

        /// <summary>按边 TravelCost 累加的最短路径（Dijkstra；诊断／遗留）。</summary>
        public static bool TrySumPathTravelCost(
            SimulationWorld world,
            string fromNodeId,
            string toNodeId,
            out int totalCost)
        {
            totalCost = 0;
            if (world?.WorldGraph == null ||
                string.IsNullOrEmpty(fromNodeId) ||
                string.IsNullOrEmpty(toNodeId))
                return false;
            if (string.Equals(fromNodeId, toNodeId, StringComparison.Ordinal))
                return true;

            var dist = new Dictionary<string, int>(StringComparer.Ordinal);
            var q = new SortedSet<NodeCost>(NodeCostComparer.Instance);
            dist[fromNodeId] = 0;
            q.Add(new NodeCost(fromNodeId, 0));

            while (q.Count > 0)
            {
                var cur = q.Min;
                q.Remove(cur);
                if (string.Equals(cur.NodeId, toNodeId, StringComparison.Ordinal))
                {
                    totalCost = cur.Cost;
                    return true;
                }

                if (dist.TryGetValue(cur.NodeId, out var known) && known < cur.Cost)
                    continue;

                foreach (var kv in world.WorldGraph.Routes)
                {
                    var route = kv.Value;
                    if (route == null || WorldTravelService.CanTraverse(route).IsFailure)
                        continue;
                    TryRelax(q, dist, cur, route.FromNodeId, route.ToNodeId, route.TravelCost);
                    if (!route.Directed)
                        TryRelax(q, dist, cur, route.ToNodeId, route.FromNodeId, route.TravelCost);
                }
            }

            return false;
        }

        static void TryRelax(
            SortedSet<NodeCost> q,
            Dictionary<string, int> dist,
            NodeCost cur,
            string a,
            string b,
            int edgeCost)
        {
            string next = null;
            if (string.Equals(cur.NodeId, a, StringComparison.Ordinal))
                next = b;
            else if (string.Equals(cur.NodeId, b, StringComparison.Ordinal))
                next = a;
            if (string.IsNullOrEmpty(next))
                return;
            var w = edgeCost > 0 ? edgeCost : 1;
            var nd = cur.Cost + w;
            if (dist.TryGetValue(next, out var old) && old <= nd)
                return;
            dist[next] = nd;
            q.Add(new NodeCost(next, nd));
        }

        readonly struct NodeCost
        {
            public readonly string NodeId;
            public readonly int Cost;
            public NodeCost(string nodeId, int cost)
            {
                NodeId = nodeId;
                Cost = cost;
            }
        }

        sealed class NodeCostComparer : IComparer<NodeCost>
        {
            public static readonly NodeCostComparer Instance = new NodeCostComparer();
            public int Compare(NodeCost x, NodeCost y)
            {
                var c = x.Cost.CompareTo(y.Cost);
                if (c != 0)
                    return c;
                return string.CompareOrdinal(x.NodeId, y.NodeId);
            }
        }
    }
}
