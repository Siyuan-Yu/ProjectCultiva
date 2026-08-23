using System;
using System.Collections.Generic;
using XianXia.Core.Content;

namespace XianXia.Core.World
{
    public sealed class WorldNodeState
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string LocalMapId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        /// <summary>Hex 迁移：战略空间坐标；int.MinValue 表示未绑定。</summary>
        public int HexQ { get; set; } = int.MinValue;
        public int HexR { get; set; } = int.MinValue;
        public bool HasHexCoord => HexQ != int.MinValue && HexR != int.MinValue;
        /// <summary>战略政治归属 FactionId（2A OwnerFactionId；字段名暂保留 OwnerId）。</summary>
        public string OwnerId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public List<string> Tags { get; } = new List<string>();
    }

    public sealed class WorldRouteState
    {
        public string Id { get; set; } = string.Empty;
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public int TravelCost { get; set; }
        public float Danger { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public bool Directed { get; set; }
        public List<ContentCondition> TraversalRequirements { get; } = new List<ContentCondition>();
        public string EncounterPoolId { get; set; } = string.Empty;
    }

    /// <summary>会话内宏观图（由 Data Bootstrap 从 worldGraph 灌入）。</summary>
    public sealed class WorldGraphBoard
    {
        readonly Dictionary<string, WorldNodeState> _nodes =
            new Dictionary<string, WorldNodeState>(StringComparer.Ordinal);
        readonly Dictionary<string, WorldRouteState> _routes =
            new Dictionary<string, WorldRouteState>(StringComparer.Ordinal);

        public string GraphId { get; set; } = string.Empty;
        public string GraphName { get; set; } = string.Empty;
        public string StartNodeId { get; set; } = string.Empty;

        public IReadOnlyDictionary<string, WorldNodeState> Nodes => _nodes;
        public IReadOnlyDictionary<string, WorldRouteState> Routes => _routes;

        public bool HasGraph => !string.IsNullOrEmpty(GraphId) && _nodes.Count > 0;

        public void Clear()
        {
            GraphId = string.Empty;
            GraphName = string.Empty;
            StartNodeId = string.Empty;
            _nodes.Clear();
            _routes.Clear();
        }

        public void RegisterNode(WorldNodeState node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id))
                throw new ArgumentException("WorldNodeState requires Id.");
            _nodes[node.Id] = node;
        }

        public void RegisterRoute(WorldRouteState route)
        {
            if (route == null || string.IsNullOrEmpty(route.Id))
                throw new ArgumentException("WorldRouteState requires Id.");
            _routes[route.Id] = route;
        }

        public bool TryGetNode(string id, out WorldNodeState node)
        {
            node = null;
            return !string.IsNullOrEmpty(id) && _nodes.TryGetValue(id, out node);
        }

        public bool TryGetRoute(string id, out WorldRouteState route)
        {
            route = null;
            return !string.IsNullOrEmpty(id) && _routes.TryGetValue(id, out route);
        }

        public bool TryFindRoute(string fromNodeId, string toNodeId, out WorldRouteState route)
        {
            route = null;
            foreach (var kv in _routes)
            {
                var r = kv.Value;
                if (string.Equals(r.FromNodeId, fromNodeId, StringComparison.Ordinal) &&
                    string.Equals(r.ToNodeId, toNodeId, StringComparison.Ordinal))
                {
                    route = r;
                    return true;
                }

                if (!r.Directed &&
                    string.Equals(r.FromNodeId, toNodeId, StringComparison.Ordinal) &&
                    string.Equals(r.ToNodeId, fromNodeId, StringComparison.Ordinal))
                {
                    route = r;
                    return true;
                }
            }

            return false;
        }
    }
}
