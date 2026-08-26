using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Background Travel 到达瞬间的 Ingress 上下文（非 WorldLocation 真源，不持久化）。
    /// </summary>
    public readonly struct BackgroundTravelArrivalContext
    {
        public BackgroundTravelArrivalContext(
            HexCoord ingressOutsideHex,
            HexCoord enteringHex,
            HexCoord destinationHex,
            string destinationSiteId)
        {
            IngressOutsideHex = ingressOutsideHex;
            EnteringHex = enteringHex;
            DestinationHex = destinationHex;
            DestinationSiteId = destinationSiteId ?? string.Empty;
        }

        /// <summary>从该 Outside Hex 跨入 Destination（WorldSite Footprint 或 Wilderness Hex）。</summary>
        public HexCoord IngressOutsideHex { get; }

        /// <summary>跨入后的 Hex（Path 末段或 Footprint 代表格）。</summary>
        public HexCoord EnteringHex { get; }

        public HexCoord DestinationHex { get; }
        public string DestinationSiteId { get; }

        public bool HasWorldSiteDestination => !string.IsNullOrEmpty(DestinationSiteId);

        public static bool TryFromMotion(
            SimulationWorld world,
            BackgroundCharacterTravelMotion motion,
            out BackgroundTravelArrivalContext context)
        {
            context = default;
            if (world == null || motion == null || motion.HexPathCount < 1)
                return false;

            var path = motion.HexPath;
            var enteringHex = path[path.Count - 1];
            var destinationHex = motion.DestinationHex;
            var destinationSiteId = motion.DestinationSiteId ?? string.Empty;

            WorldSite destinationSite = null;
            if (string.IsNullOrEmpty(destinationSiteId) &&
                world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(destinationHex, out var footprintSite))
            {
                destinationSiteId = footprintSite.SiteId;
                destinationSite = footprintSite;
            }
            else if (!string.IsNullOrEmpty(destinationSiteId))
            {
                world.Strategic?.Sites?.TryGet(destinationSiteId, out destinationSite);
            }

            if (!TryResolveIngressOutsideHex(
                    world,
                    path,
                    enteringHex,
                    destinationSite,
                    out var ingressOutsideHex))
                return false;

            context = new BackgroundTravelArrivalContext(
                ingressOutsideHex,
                enteringHex,
                destinationHex,
                destinationSiteId);
            return true;
        }

        static bool TryResolveIngressOutsideHex(
            SimulationWorld world,
            System.Collections.Generic.IReadOnlyList<HexCoord> path,
            HexCoord enteringHex,
            WorldSite destinationSite,
            out HexCoord ingressOutsideHex)
        {
            ingressOutsideHex = default;
            if (path == null || path.Count < 1)
                return false;

            if (path.Count >= 2)
            {
                var previous = path[path.Count - 2];
                if (destinationSite == null || !destinationSite.OccupiesHex(previous))
                {
                    ingressOutsideHex = previous;
                    return true;
                }
            }

            if (destinationSite != null)
            {
                HexCoord best = default;
                var hasBest = false;
                var preferred = path.Count >= 2 ? path[path.Count - 2] : enteringHex;
                for (var dir = 0; dir < 6; dir++)
                {
                    var neighbor = HexMath.Neighbor(enteringHex, dir);
                    if (destinationSite.OccupiesHex(neighbor))
                        continue;
                    if (!world.HexWorld.TryGetTile(neighbor, out var tile) ||
                        tile == null ||
                        !tile.IsPassable)
                        continue;

                    if (!hasBest || neighbor == preferred)
                    {
                        best = neighbor;
                        hasBest = true;
                        if (neighbor == preferred)
                            break;
                    }
                }

                if (hasBest)
                {
                    ingressOutsideHex = best;
                    return true;
                }

                return false;
            }

            if (path.Count >= 2)
            {
                ingressOutsideHex = path[path.Count - 2];
                return true;
            }

            return false;
        }
    }
}
