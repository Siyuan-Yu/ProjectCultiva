using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// WorldSite 旧 Hex footprint 输入的内容/Runtime 校验（与 Terrain 无关）。
    /// 这些形状规则不适用于 Continuous Surface 或 Actual Administrative Control。
    /// </summary>
    public static class WorldSiteHexFootprintValidator
    {
        /// <summary>
        /// Phase 5R-B3C3：单一 WorldSite footprint 的正式内容约束（WorldGraph 编辑期/加载期调用）：
        ///  A. LegacyOccupiedHexes 非空；
        ///  B. 6-neighbor connectivity（单一连续物理区域）；
        ///  C. V2 spatial kernel non-empty（star-shaped，可被
        ///     <see cref="HexFootprintSpatialGeometry.TryBuild"/> 径向映射覆盖）。
        /// 返回错误列表（空 = 通过）。每条含 SiteId + LegacyOccupiedHexes + failure reason。
        /// kernel 判定<b>复用</b> <see cref="HexFootprintSpatialGeometry.TryBuild"/>，不复制第二套算法。
        /// </summary>
        public static List<string> ValidateFootprint(WorldSite site, float hexSize = 1f)
        {
            var errors = new List<string>();
            if (site == null)
            {
                errors.Add("WorldSiteFootprint.Empty site=null");
                return errors;
            }

            if (site.LegacyOccupiedHexes.Count == 0)
            {
                errors.Add("WorldSiteFootprint.Empty site=" + site.SiteId);
                return errors;
            }

            if (!IsFootprintConnected(site.LegacyOccupiedHexes))
            {
                errors.Add(
                    "WorldSiteFootprint.Disconnected site=" + site.SiteId +
                    " footprint=[" + FormatHexes(site.LegacyOccupiedHexes) + "]");
            }

            if (!HexFootprintSpatialGeometry.TryBuild(site.LegacyOccupiedHexes, hexSize, out var geometry) ||
                !geometry.HasKernel)
            {
                errors.Add(
                    "WorldSiteFootprint.NoSpatialKernel site=" + site.SiteId +
                    " footprint=[" + FormatHexes(site.LegacyOccupiedHexes) + "]");
            }

            return errors;
        }

        static string FormatHexes(IReadOnlyList<HexCoord> footprint)
        {
            var s = string.Empty;
            for (var i = 0; i < footprint.Count; i++)
            {
                s += (i == 0 ? "" : " ") + "(" + footprint[i].Q + "," + footprint[i].R + ")";
            }

            return s;
        }

        public static bool IsAnchorInFootprint(WorldSite site)
        {
            if (site == null)
                return false;
            return site.OccupiesLegacyHex(site.LegacyAnchorHex);
        }

        public static bool IsPresenceMatchesAnchor(WorldSite site)
        {
            if (site == null)
                return false;
            return site.LegacyPresenceHex == site.LegacyAnchorHex;
        }

        public static bool IsPresenceInFootprint(WorldSite site)
        {
            if (site == null)
                return false;
            return site.OccupiesLegacyHex(site.LegacyPresenceHex);
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
            foreach (var _ in site.EnumerateLegacyFootprintHexes())
                count++;
            return count;
        }
    }
}
