using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 2B Minimal Wilderness Fallback：多 Hex 共享少量 LocalMap 模板（非每格一 Scene）。
    /// </summary>
    public static class WildernessLocalMapFallback
    {
        /// <summary>通用陆地保底（遭遇 stub 图复用）。</summary>
        public const string GenericWildernessLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;

        /// <summary>森林地形模板（复用林间 Site LocalMap）。</summary>
        public const string ForestWildernessLocalMapId = "base:map_site_linjian";

        /// <summary>山地地形模板（复用矿山地 LocalMap）。</summary>
        public const string MountainWildernessLocalMapId = "base:map_site_kuangshan";

        /// <summary>平原／道路／其他陆地（复用荒原甲 Site LocalMap）。</summary>
        public const string PlainsWildernessLocalMapId = "base:map_site_a";

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
                case HexTerrainType.Plain:
                case HexTerrainType.Road:
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
            return TryResolve(tile.Terrain, out localMapId);
        }
    }
}
