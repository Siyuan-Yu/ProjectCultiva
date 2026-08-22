using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.World
{
    /// <summary>单个可控角色在宏观图上的位置。</summary>
    public sealed class WorldAgentPresence
    {
        public EntityId EntityId { get; set; }
        public PartyWorldPresenceMode Mode { get; set; } = PartyWorldPresenceMode.AtNode;
        /// <summary>停留时＝当前 Node；途中＝出发 Node。</summary>
        public string NodeId { get; set; } = string.Empty;
        public string RouteId { get; set; } = string.Empty;
        public string DestNodeId { get; set; } = string.Empty;
        public int RemainingTravelTicks { get; set; }
        public int TravelTotalTicks { get; set; }
        /// <summary>RouteAnchored 时 0..1；-1 表示未锚定。</summary>
        public float RouteAnchorProgress { get; set; } = -1f;
        /// <summary>从锚点/中途出发的区段起点进度（Traveling 时用）。</summary>
        public float RouteSegmentOriginProgress { get; set; } = -1f;
        /// <summary>从锚点/中途出发的区段终点进度（Traveling 时用）。</summary>
        public float RouteSegmentEndProgress { get; set; } = -1f;
        /// <summary>宏观 RTS：跟随的 ArmyStack id；空表示未跟随。</summary>
        public string FollowStackId { get; set; } = string.Empty;
        /// <summary>攻击／追击目标栈 id；有值则到站不弹「是否查看」，只走接战。</summary>
        public string CombatPursuitStackId { get; set; } = string.Empty;

        /// <summary>
        /// 已因接战弹窗消费过本次抵达：撤退／关窗后勿再弹「是否查看」。
        /// 新的宏观出行下令时清除。
        /// </summary>
        public bool SuppressArrivalNotice { get; set; }

        public bool IsFollowingStack => !string.IsNullOrEmpty(FollowStackId);

        public bool IsCombatPursuing => !string.IsNullOrEmpty(CombatPursuitStackId);

        public bool IsRouteAnchored =>
            Mode == PartyWorldPresenceMode.RouteAnchored && RouteAnchorProgress >= 0f;

        /// <summary>0＝刚出发，1＝即将到站。</summary>
        public float TravelProgress
        {
            get
            {
                if (Mode == PartyWorldPresenceMode.RouteAnchored)
                    return RouteAnchorProgress;

                var onRoute = Mode == PartyWorldPresenceMode.Traveling ||
                              Mode == PartyWorldPresenceMode.InEncounter;
                if (Mode == PartyWorldPresenceMode.InEncounter &&
                    TravelTotalTicks <= 0 &&
                    RouteAnchorProgress >= 0f)
                    return RouteAnchorProgress;
                if (!onRoute || TravelTotalTicks <= 0)
                    return RouteAnchorProgress >= 0f ? RouteAnchorProgress : 0f;

                var done = TravelTotalTicks - RemainingTravelTicks;
                var leg = TravelTotalTicks <= 0 ? 1f : (float)done / TravelTotalTicks;
                if (RouteSegmentOriginProgress >= 0f && RouteSegmentEndProgress >= 0f)
                {
                    return RouteSegmentOriginProgress +
                           (RouteSegmentEndProgress - RouteSegmentOriginProgress) * leg;
                }

                if (done <= 0)
                    return 0f;
                if (done >= TravelTotalTicks)
                    return 1f;
                return leg;
            }
        }

        public bool HasRoutePresentation =>
            !string.IsNullOrEmpty(RouteId) &&
            !string.IsNullOrEmpty(DestNodeId) &&
            !string.Equals(NodeId, DestNodeId, StringComparison.Ordinal) &&
            (Mode == PartyWorldPresenceMode.Traveling ||
             Mode == PartyWorldPresenceMode.RouteAnchored ||
             (Mode == PartyWorldPresenceMode.InEncounter &&
              (TravelTotalTicks > 0 || RouteAnchorProgress >= 0f)));

        public void ClearRouteSegment()
        {
            RouteSegmentOriginProgress = -1f;
            RouteSegmentEndProgress = -1f;
        }

        public void AnchorOnRoute(float progress)
        {
            Mode = PartyWorldPresenceMode.RouteAnchored;
            RouteAnchorProgress = Math.Max(0f, Math.Min(1f, progress));
            RemainingTravelTicks = 0;
            TravelTotalTicks = 0;
            ClearRouteSegment();
        }

        public void ClearFollow() => FollowStackId = string.Empty;

        public void ClearCombatPursuit() => CombatPursuitStackId = string.Empty;

        public void ClearTravel()
        {
            Mode = PartyWorldPresenceMode.AtNode;
            RouteId = string.Empty;
            DestNodeId = string.Empty;
            RemainingTravelTicks = 0;
            TravelTotalTicks = 0;
            RouteAnchorProgress = -1f;
            ClearRouteSegment();
            // 注意：不清 CombatPursuitStackId——追击到站后仍要弹接战而非到站查看
        }
    }

    /// <summary>全员宏观位置；PartyWorld 作「当前镜头／焦点 Node」摘要。</summary>
    public sealed class WorldPresenceBoard
    {
        readonly Dictionary<ulong, WorldAgentPresence> _byEntity =
            new Dictionary<ulong, WorldAgentPresence>();

        public IReadOnlyDictionary<ulong, WorldAgentPresence> All => _byEntity;

        public void Clear() => _byEntity.Clear();

        public WorldAgentPresence GetOrCreate(EntityId id)
        {
            if (id.IsNone)
                throw new ArgumentException("EntityId required.");
            if (_byEntity.TryGetValue(id.Value, out var existing))
                return existing;
            var p = new WorldAgentPresence { EntityId = id };
            _byEntity[id.Value] = p;
            return p;
        }

        public bool TryGet(EntityId id, out WorldAgentPresence presence)
        {
            presence = null;
            if (id.IsNone)
                return false;
            return _byEntity.TryGetValue(id.Value, out presence);
        }

        /// <summary>尸体腐烂／实体移除后从大地图抹掉位置（不再参与任何节点／路上演算）。</summary>
        public bool Remove(EntityId id)
        {
            if (id.IsNone)
                return false;
            return _byEntity.Remove(id.Value);
        }

        public void SetAtNode(EntityId id, string nodeId)
        {
            var p = GetOrCreate(id);
            p.NodeId = nodeId ?? string.Empty;
            p.ClearTravel();
        }

        public void CollectAtNode(string nodeId, List<EntityId> into)
        {
            if (into == null || string.IsNullOrEmpty(nodeId))
                return;
            foreach (var kv in _byEntity)
            {
                var p = kv.Value;
                if (p == null || p.Mode != PartyWorldPresenceMode.AtNode)
                    continue;
                if (string.Equals(p.NodeId, nodeId, StringComparison.Ordinal))
                    into.Add(p.EntityId);
            }
        }
    }
}
