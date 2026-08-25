using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// WorldMap 输入解析：永远只有 Hex / WorldSite 级目标。
    /// 禁止 PreciseWorldDestination / 点击像素目的地。
    /// </summary>
    public static class WorldMapPartyTravelCommand
    {
        public struct Resolved
        {
            /// <summary>点击命中的 Hex 身份（与点击像素无关）。</summary>
            public HexCoord TargetHex;

            /// <summary>若点击落在 Site Footprint，则为该 Site；否则空。</summary>
            public string TargetSiteId;

            /// <summary>V1 路径终点 Hex：普通格=TargetHex；Site=确定性 approach footprint。</summary>
            public HexCoord DestinationHex;

            /// <summary>V1 连续落点：DestinationHex 的 canonical center（永不等于点击像素）。</summary>
            public WorldVec2 CanonicalDestinationWorld;
        }

        /// <summary>
        /// 仅根据 clickedHex 解析。任何 clickWorld 参数不得影响目标语义。
        /// </summary>
        public static bool TryResolve(
            SimulationWorld world,
            HexCoord clickedHex,
            out Resolved resolved) =>
            TryResolve(world, clickedHex, 0f, 0f, out resolved);

        /// <summary>
        /// clickWorldX/Y 仅用于 API 对称测试：证明不同像素仍得到同一 Resolved。
        /// </summary>
        public static bool TryResolve(
            SimulationWorld world,
            HexCoord clickedHex,
            float clickWorldX,
            float clickWorldY,
            out Resolved resolved)
        {
            resolved = default;
            // 刻意忽略 clickWorldX/Y —— WorldMap 不是连续坐标点击界面。
            _ = clickWorldX;
            _ = clickWorldY;

            if (world?.HexWorld == null || !world.HexWorld.HasGrid)
                return false;
            if (!world.HexWorld.TryGetTile(clickedHex, out var tile) || tile == null)
                return false;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            resolved.TargetHex = clickedHex;
            resolved.TargetSiteId = string.Empty;
            resolved.DestinationHex = clickedHex;

            if (world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(clickedHex, out var site) &&
                site != null)
            {
                resolved.TargetSiteId = site.SiteId;
                resolved.DestinationHex = ResolveDeterministicApproachHex(world, site, clickedHex);
            }

            HexMath.ToWorldPosition(
                resolved.DestinationHex,
                hexSize,
                out var dx,
                out var dy);
            resolved.CanonicalDestinationWorld = new WorldVec2(dx, dy);
            return true;
        }

        static HexCoord ResolveDeterministicApproachHex(
            SimulationWorld world,
            WorldSite site,
            HexCoord fallback)
        {
            HexCoord best = site.PresenceHex;
            var bestDist = int.MaxValue;
            var from = fallback;
            if (world.PlayerPartyTravel != null && world.PlayerPartyTravel.HasPosition)
                from = world.PlayerPartyTravel.CurrentHex;

            foreach (var hex in site.EnumerateFootprintHexes())
            {
                if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null || !tile.IsPassable)
                    continue;
                var d = HexMath.Distance(from, hex);
                if (d < bestDist ||
                    (d == bestDist && (hex.Q < best.Q || (hex.Q == best.Q && hex.R < best.R))))
                {
                    bestDist = d;
                    best = hex;
                }
            }

            return best;
        }
    }
}
