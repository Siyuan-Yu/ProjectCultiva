using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// WorldSite 全 Footprint 边界出口选择（Domain-only；不依赖 LocalMap Presentation）。
    /// </summary>
    public static class BackgroundCharacterSiteDepartureResolver
    {
        static readonly List<HexCoord> OutsideScratch = new List<HexCoord>(16);
        static readonly List<HexCoord> PathScratch = new List<HexCoord>(64);

        public static bool TryResolveDepartureHex(
            SimulationWorld world,
            WorldSite site,
            HexCoord destinationHex,
            out HexCoord exitHex)
        {
            exitHex = default;
            if (world?.HexWorld == null || site == null)
                return false;

            CollectTraversableOutsideNeighbors(world, site, OutsideScratch);
            if (OutsideScratch.Count == 0)
                return false;

            var bestDist = int.MaxValue;
            var found = false;
            for (var i = 0; i < OutsideScratch.Count; i++)
            {
                var outside = OutsideScratch[i];
                if (!HexPathfinder.TryFindPath(world.HexWorld, outside, destinationHex, PathScratch) ||
                    PathScratch.Count < 1)
                    continue;

                var dist = PathScratch.Count;
                if (dist < bestDist ||
                    (dist == bestDist && CompareHex(outside, exitHex) < 0))
                {
                    bestDist = dist;
                    exitHex = outside;
                    found = true;
                }
            }

            return found;
        }

        public static void CollectTraversableOutsideNeighbors(
            SimulationWorld world,
            WorldSite site,
            List<HexCoord> into)
        {
            into.Clear();
            if (world?.HexWorld == null || site == null)
                return;

            var seen = new HashSet<HexCoord>();
            foreach (var footprintHex in site.EnumerateFootprintHexes())
            {
                for (var dir = 0; dir < 6; dir++)
                {
                    var neighbor = HexMath.Neighbor(footprintHex, dir);
                    if (site.OccupiesHex(neighbor))
                        continue;
                    if (!world.HexWorld.TryGetTile(neighbor, out var tile) || tile == null || !tile.IsPassable)
                        continue;
                    if (seen.Add(neighbor))
                        into.Add(neighbor);
                }
            }
        }

        static int CompareHex(HexCoord a, HexCoord b)
        {
            var cq = a.Q.CompareTo(b.Q);
            return cq != 0 ? cq : a.R.CompareTo(b.R);
        }
    }
}
