using System;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World
{
    /// <summary>大地图移动目标：节点或道路上的进度点。</summary>
    public readonly struct WorldTravelTarget : IEquatable<WorldTravelTarget>
    {
        public bool IsRouteProgress { get; }
        public bool IsHex { get; }
        public string NodeId { get; }
        public string RouteId { get; }
        public string RouteFromNodeId { get; }
        public string RouteToNodeId { get; }
        public float RouteProgress { get; }
        public int HexQ { get; }
        public int HexR { get; }

        public HexCoord HexCoord => new HexCoord(HexQ, HexR);

        WorldTravelTarget(
            bool isRouteProgress,
            bool isHex,
            string nodeId,
            string routeId,
            string routeFromNodeId,
            string routeToNodeId,
            float routeProgress,
            int hexQ,
            int hexR)
        {
            IsRouteProgress = isRouteProgress;
            IsHex = isHex;
            NodeId = nodeId ?? string.Empty;
            RouteId = routeId ?? string.Empty;
            RouteFromNodeId = routeFromNodeId ?? string.Empty;
            RouteToNodeId = routeToNodeId ?? string.Empty;
            RouteProgress = routeProgress;
            HexQ = hexQ;
            HexR = hexR;
        }

        public static WorldTravelTarget AtNode(string nodeId) =>
            new WorldTravelTarget(false, false, nodeId, string.Empty, string.Empty, string.Empty, -1f, int.MinValue, int.MinValue);

        public static WorldTravelTarget AtHex(HexCoord coord) =>
            new WorldTravelTarget(false, true, string.Empty, string.Empty, string.Empty, string.Empty, -1f, coord.Q, coord.R);

        public static WorldTravelTarget OnRoute(
            string routeId,
            string fromNodeId,
            string toNodeId,
            float progress) =>
            new WorldTravelTarget(true, false, string.Empty, routeId, fromNodeId, toNodeId, progress, int.MinValue, int.MinValue);

        public string Describe(Simulation.SimulationWorld world)
        {
            if (IsHex)
            {
                if (world?.Strategic?.Sites != null)
                {
                    foreach (var kv in world.Strategic.Sites.Sites)
                    {
                        var site = kv.Value;
                        if (site != null && site.HexCoord == HexCoord)
                            return string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
                    }
                }

                return HexCoord.ToString();
            }

            if (!IsRouteProgress)
            {
                if (world?.Strategic?.Sites != null &&
                    world.Strategic.Sites.TryGet(NodeId, out var site) &&
                    site != null)
                    return string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName;
                return NodeId;
            }

            var pct = (int)Math.Round(RouteProgress * 100f);
            return "道路 " + pct + "%";
        }

        public bool Equals(WorldTravelTarget other) =>
            IsRouteProgress == other.IsRouteProgress &&
            IsHex == other.IsHex &&
            string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
            string.Equals(RouteId, other.RouteId, StringComparison.Ordinal) &&
            string.Equals(RouteFromNodeId, other.RouteFromNodeId, StringComparison.Ordinal) &&
            string.Equals(RouteToNodeId, other.RouteToNodeId, StringComparison.Ordinal) &&
            Math.Abs(RouteProgress - other.RouteProgress) <= 0.001f &&
            HexQ == other.HexQ &&
            HexR == other.HexR;

        public override bool Equals(object obj) => obj is WorldTravelTarget other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = IsRouteProgress.GetHashCode();
                hash = (hash * 397) ^ (NodeId != null ? NodeId.GetHashCode() : 0);
                hash = (hash * 397) ^ (RouteId != null ? RouteId.GetHashCode() : 0);
                hash = (hash * 397) ^ RouteProgress.GetHashCode();
                hash = (hash * 397) ^ HexQ;
                hash = (hash * 397) ^ HexR;
                return hash;
            }
        }
    }
}
