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

        public static bool TryResolveDepartureHex(
            SimulationWorld world,
            WorldSite site,
            HexCoord destinationHex,
            IReadOnlyCollection<HexCoord> allowedOutsideHexes,
            out HexCoord exitHex)
        {
            exitHex = default;
            if (allowedOutsideHexes == null)
                return TryResolveDepartureHex(world, site, destinationHex, out exitHex);
            CollectTraversableOutsideNeighbors(world, site, OutsideScratch);
            var bestDist = int.MaxValue;
            var found = false;
            for (var i = 0; i < OutsideScratch.Count; i++)
            {
                var outside = OutsideScratch[i];
                if (!Contains(allowedOutsideHexes, outside) ||
                    !HexPathfinder.TryFindPath(world.HexWorld, outside, destinationHex, PathScratch) ||
                    PathScratch.Count < 1)
                    continue;
                var dist = PathScratch.Count;
                if (dist < bestDist || (dist == bestDist && CompareHex(outside, exitHex) < 0))
                {
                    bestDist = dist;
                    exitHex = outside;
                    found = true;
                }
            }
            return found;
        }

        static bool Contains(IReadOnlyCollection<HexCoord> set, HexCoord value)
        {
            foreach (var item in set)
                if (item.Equals(value)) return true;
            return false;
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

        /// <summary>
        /// 离开 Site 时使用的 Outside Exit Hex 所邻接的 Footprint 源格（Boundary Connection 内侧）。
        /// </summary>
        public static bool TryResolveDepartureFootprintHex(
            WorldSite site,
            HexCoord outsideExitHex,
            out HexCoord footprintHex)
        {
            footprintHex = default;
            if (site == null)
                return false;

            var found = false;
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                for (var dir = 0; dir < 6; dir++)
                {
                    if (!HexMath.Neighbor(hex, dir).Equals(outsideExitHex))
                        continue;
                    if (!found || CompareHex(hex, footprintHex) < 0)
                    {
                        footprintHex = hex;
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Footprint 内侧 → Outside Hex 边界上的世界坐标 Entry（Domain-only）。
        /// </summary>
        public static bool TryResolveDepartureBoundaryEntryWorldPosition(
            HexCoord footprintHex,
            HexCoord outsideExitHex,
            float hexSize,
            out WorldVec2 entryWorldPos)
        {
            entryWorldPos = default;
            var size = hexSize > 0f ? hexSize : 1f;
            HexMath.ToWorldPosition(footprintHex, size, out var fx, out var fy);
            HexMath.ToWorldPosition(outsideExitHex, size, out var ox, out var oy);
            entryWorldPos = new WorldVec2((fx + ox) * 0.5f, (fy + oy) * 0.5f);
            return true;
        }

        public static int ResolveDirectionBetween(HexCoord fromHex, HexCoord toHex)
        {
            for (var i = 0; i < 6; i++)
            {
                if (HexMath.Neighbor(fromHex, i).Equals(toHex))
                    return i;
            }

            return 0;
        }
    }
}
