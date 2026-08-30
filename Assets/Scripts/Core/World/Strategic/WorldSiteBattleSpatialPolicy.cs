using System;
using System.Collections.Generic;
using System.Linq;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>WorldSite 战斗空间唯一几何权威；不包含单位、阵营或战斗规则。</summary>
    public static class WorldSiteBattleSpatialPolicy
    {
        public static IReadOnlyCollection<HexCoord> CollectBattleArea(WorldSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            return new HashSet<HexCoord>(site.OccupiedHexes);
        }

        public static IReadOnlyCollection<HexCoord> CollectSupportRing(WorldSite site, HexWorld world = null, int radius = 1)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius));
            var area = new HashSet<HexCoord>(site.OccupiedHexes);
            var ring = new HashSet<HexCoord>();
            var frontier = new HashSet<HexCoord>(area);
            for (var step = 0; step < radius; step++)
            {
                var next = new HashSet<HexCoord>();
                foreach (var hex in frontier)
                    for (var d = 0; d < 6; d++)
                        next.Add(HexMath.Neighbor(hex, d));
                foreach (var hex in next)
                    if (!area.Contains(hex) && (world == null || world.Contains(hex))) ring.Add(hex);
                frontier = next;
            }
            return ring;
        }

        public static bool ContainsBattleHex(WorldSite site, HexCoord hex) =>
            site != null && site.OccupiesHex(hex);

        public static bool ContainsSupportHex(WorldSite site, HexCoord hex, HexWorld world = null, int radius = 1) =>
            CollectSupportRing(site, world, radius).Contains(hex);
    }
}
