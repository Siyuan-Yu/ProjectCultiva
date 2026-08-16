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

        /// <summary>0＝刚出发，1＝即将到站。</summary>
        public float TravelProgress
        {
            get
            {
                if (Mode != PartyWorldPresenceMode.Traveling || TravelTotalTicks <= 0)
                    return 0f;
                var done = TravelTotalTicks - RemainingTravelTicks;
                if (done <= 0)
                    return 0f;
                if (done >= TravelTotalTicks)
                    return 1f;
                return (float)done / TravelTotalTicks;
            }
        }

        public void ClearTravel()
        {
            Mode = PartyWorldPresenceMode.AtNode;
            RouteId = string.Empty;
            DestNodeId = string.Empty;
            RemainingTravelTicks = 0;
            TravelTotalTicks = 0;
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
                if (p == null ||
                    (p.Mode != PartyWorldPresenceMode.AtNode &&
                     p.Mode != PartyWorldPresenceMode.DepartingLocalMap))
                    continue;
                if (string.Equals(p.NodeId, nodeId, StringComparison.Ordinal))
                    into.Add(p.EntityId);
            }
        }
    }
}
