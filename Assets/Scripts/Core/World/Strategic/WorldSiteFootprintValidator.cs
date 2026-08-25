using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>WorldSite Footprint 内容/Runtime 校验（与 Terrain 无关）。</summary>
    public static class WorldSiteFootprintValidator
    {
        public static bool IsAnchorInFootprint(WorldSite site)
        {
            if (site == null)
                return false;
            return site.OccupiesHex(site.AnchorHex);
        }

        public static bool IsPresenceInFootprint(WorldSite site)
        {
            if (site == null)
                return false;
            return site.OccupiesHex(site.PresenceHex);
        }

        public static bool IsFootprintConnected(IReadOnlyList<HexCoord> footprint)
        {
            if (footprint == null || footprint.Count <= 1)
                return footprint != null && footprint.Count == 1;

            var set = new HashSet<HexCoord>(footprint);
            var visited = new HashSet<HexCoord>();
            var stack = new Stack<HexCoord>();
            stack.Push(footprint[0]);
            visited.Add(footprint[0]);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                for (var d = 0; d < HexMath.AxialDirections.Length; d++)
                {
                    var neighbor = HexMath.Neighbor(cur, d);
                    if (!set.Contains(neighbor) || !visited.Add(neighbor))
                        continue;
                    stack.Push(neighbor);
                }
            }

            return visited.Count == set.Count;
        }

        public static int CountFootprintHexes(WorldSite site)
        {
            if (site == null)
                return 0;
            var count = 0;
            foreach (var _ in site.EnumerateFootprintHexes())
                count++;
            return count;
        }
    }
}
