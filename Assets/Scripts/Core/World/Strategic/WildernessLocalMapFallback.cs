using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 2C：普通 Hex → Wilderness LocalMap Template（不得指向任何 Fixed WorldSite 专属图）。
    /// </summary>
    public static class WildernessLocalMapFallback
    {
        /// <summary>道路 Hex 专用 Fallback（非 Site）。</summary>
        public const string RoadWildernessLocalMapId = "base:map_wilderness_road_fallback";

        /// <summary>平原／通用陆地 Fallback（非 Site）。</summary>
        public const string PlainsWildernessLocalMapId = "base:map_wilderness_plain_fallback";

        /// <summary>森林 Fallback（非 Site；不再复用林间 Site LocalMap）。</summary>
        public const string ForestWildernessLocalMapId = "base:map_wilderness_forest_fallback";

        /// <summary>山地 Fallback（非 Site；不再复用矿山 Site LocalMap）。</summary>
        public const string MountainWildernessLocalMapId = "base:map_wilderness_mountain_fallback";

        /// <summary>遗留：遭遇 stub，仅作最后兜底（非普通 Hex 主路径）。</summary>
        public const string GenericWildernessLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;

        /// <summary>禁止再作为普通 Hex Fallback 的 Fixed Site LocalMap。</summary>
        public const string ForbiddenHuangyuanSiteLocalMapId = "base:map_site_a";

        public static bool TryResolve(HexTerrainType terrain, out string localMapId)
        {
            localMapId = string.Empty;
            switch (terrain)
            {
                case HexTerrainType.Water:
                    return false;
                case HexTerrainType.Forest:
                    localMapId = ForestWildernessLocalMapId;
                    return true;
                case HexTerrainType.Mountain:
                    localMapId = MountainWildernessLocalMapId;
                    return true;
                case HexTerrainType.Road:
                    localMapId = RoadWildernessLocalMapId;
                    return true;
                case HexTerrainType.Plain:
                default:
                    localMapId = PlainsWildernessLocalMapId;
                    return true;
            }
        }

        public static bool TryResolve(SimulationWorld world, HexCoord hex, out string localMapId)
        {
            localMapId = string.Empty;
            if (world?.HexWorld == null ||
                !world.HexWorld.TryGetTile(hex, out var tile) ||
                tile == null ||
                !tile.IsPassable)
                return false;
            if (!TryResolve(tile.Terrain, out localMapId))
                return false;
            // 硬护栏：普通 Hex 不得落到荒原甲 Site 图。
            if (string.Equals(localMapId, ForbiddenHuangyuanSiteLocalMapId, System.StringComparison.Ordinal))
            {
                localMapId = PlainsWildernessLocalMapId;
                return true;
            }

            return true;
        }

        public static bool IsForbiddenSiteLocalMapReuse(string localMapId)
        {
            if (string.IsNullOrEmpty(localMapId))
                return false;
            return string.Equals(localMapId, ForbiddenHuangyuanSiteLocalMapId, System.StringComparison.Ordinal) ||
                   string.Equals(localMapId, "base:map_site_linjian", System.StringComparison.Ordinal) ||
                   string.Equals(localMapId, "base:map_site_kuangshan", System.StringComparison.Ordinal) ||
                   string.Equals(localMapId, "base:map_site_b", System.StringComparison.Ordinal);
        }
    }
}
