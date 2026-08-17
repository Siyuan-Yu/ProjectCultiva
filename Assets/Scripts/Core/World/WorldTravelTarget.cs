using System;

namespace XianXia.Core.World
{
    /// <summary>大地图移动目标：节点或道路上的进度点。</summary>
    public readonly struct WorldTravelTarget : IEquatable<WorldTravelTarget>
    {
        public bool IsRouteProgress { get; }
        public string NodeId { get; }
        public string RouteId { get; }
        public string RouteFromNodeId { get; }
        public string RouteToNodeId { get; }
        public float RouteProgress { get; }

        WorldTravelTarget(
            bool isRouteProgress,
            string nodeId,
            string routeId,
            string routeFromNodeId,
            string routeToNodeId,
            float routeProgress)
        {
            IsRouteProgress = isRouteProgress;
            NodeId = nodeId ?? string.Empty;
            RouteId = routeId ?? string.Empty;
            RouteFromNodeId = routeFromNodeId ?? string.Empty;
            RouteToNodeId = routeToNodeId ?? string.Empty;
            RouteProgress = routeProgress;
        }

        public static WorldTravelTarget AtNode(string nodeId) =>
            new WorldTravelTarget(false, nodeId, string.Empty, string.Empty, string.Empty, -1f);

        public static WorldTravelTarget OnRoute(
            string routeId,
            string fromNodeId,
            string toNodeId,
            float progress) =>
            new WorldTravelTarget(true, string.Empty, routeId, fromNodeId, toNodeId, progress);

        public string Describe(WorldGraphBoard graph)
        {
            if (!IsRouteProgress)
            {
                if (graph != null && graph.TryGetNode(NodeId, out var node) && node != null)
                    return string.IsNullOrEmpty(node.Name) ? node.Id : node.Name;
                return NodeId;
            }

            var pct = (int)Math.Round(RouteProgress * 100f);
            if (graph != null &&
                graph.TryGetNode(RouteFromNodeId, out var from) &&
                graph.TryGetNode(RouteToNodeId, out var to))
            {
                var a = string.IsNullOrEmpty(from.Name) ? from.Id : from.Name;
                var b = string.IsNullOrEmpty(to.Name) ? to.Id : to.Name;
                return a + " ↔ " + b + "（" + pct + "%）";
            }

            return "道路 " + pct + "%";
        }

        public bool Equals(WorldTravelTarget other) =>
            IsRouteProgress == other.IsRouteProgress &&
            string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
            string.Equals(RouteId, other.RouteId, StringComparison.Ordinal) &&
            string.Equals(RouteFromNodeId, other.RouteFromNodeId, StringComparison.Ordinal) &&
            string.Equals(RouteToNodeId, other.RouteToNodeId, StringComparison.Ordinal) &&
            Math.Abs(RouteProgress - other.RouteProgress) <= 0.001f;

        public override bool Equals(object obj) => obj is WorldTravelTarget other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = IsRouteProgress.GetHashCode();
                hash = (hash * 397) ^ (NodeId != null ? NodeId.GetHashCode() : 0);
                hash = (hash * 397) ^ (RouteId != null ? RouteId.GetHashCode() : 0);
                hash = (hash * 397) ^ RouteProgress.GetHashCode();
                return hash;
            }
        }
    }
}
