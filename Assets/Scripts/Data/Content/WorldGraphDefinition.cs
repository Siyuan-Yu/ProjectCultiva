using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// 宏观世界图（[113]）：节点＋道路边；实体玩法仍在 LocalMap（mapLayout）。
    /// </summary>
    public sealed class WorldGraphDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string StartNodeId { get; set; }
        public List<WorldNodeEntry> Nodes { get; set; } = new List<WorldNodeEntry>();
        public List<WorldRouteEntry> Routes { get; set; } = new List<WorldRouteEntry>();
    }

    public sealed class WorldNodeEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        /// <summary>Town／Village／Sect／Mine／Forest／SpiritLand／Pass／Ruin／Ferry／Other…</summary>
        public string Kind { get; set; }
        /// <summary>有则进入时加载该 mapLayout；空＝宏观可达但无实体图。</summary>
        public string LocalMapId { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public string OwnerId { get; set; }
        /// <summary>Visible／Abandoned／Fallen… 空＝默认可见。</summary>
        public string State { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class WorldRouteEntry
    {
        public string Id { get; set; }
        public string FromNodeId { get; set; }
        public string ToNodeId { get; set; }
        /// <summary>Road／Trail／Bridge／RiverCrossing／MountainPass…</summary>
        public string Kind { get; set; }
        /// <summary>旅行时间代价（世界 tick／单位，B 阶段消费）。</summary>
        public int TravelCost { get; set; }
        /// <summary>遭遇权重／危险度（0＝安全）。</summary>
        public float Danger { get; set; }
        public string OwnerId { get; set; }
        /// <summary>Open／Damaged／Blocked／UnderConstruction…</summary>
        public string State { get; set; }
        /// <summary>true＝仅 From→To；默认双向。</summary>
        public bool Directed { get; set; }
        public List<ContentCondition> TraversalRequirements { get; set; } = new List<ContentCondition>();
        /// <summary>可选；路上触发临时 Encounter LocalMap 的池 id（E 阶段）。</summary>
        public string EncounterPoolId { get; set; }
    }
}
