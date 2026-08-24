using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World.Hex
{
    /// <summary>H5：Hex 地图编辑写入口（Host/Dev 工具调用）。</summary>
    public static class HexMapEditorService
    {
        public static Result PaintTerrain(
            SimulationWorld world,
            HexCoord hex,
            HexTerrainType terrain,
            bool passable = true)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Hex strategic map is not active.");
            if (world?.HexWorld == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Hex grid missing.");

            var tile = world.HexWorld.GetOrCreate(hex);
            tile.Terrain = terrain;
            tile.IsPassable = passable;
            return Result.Success();
        }

        public static Result SetRoad(SimulationWorld world, HexCoord hex, bool isRoad)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Hex strategic map is not active.");
            if (world?.HexWorld == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Hex grid missing.");

            var tile = world.HexWorld.GetOrCreate(hex);
            tile.IsRoad = isRoad;
            if (isRoad && tile.Terrain == HexTerrainType.Plain)
                tile.Terrain = HexTerrainType.Road;
            return Result.Success();
        }

        public static Result PlaceSite(SimulationWorld world, WorldSite site)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Hex strategic map is not active.");
            if (site == null || string.IsNullOrEmpty(site.SiteId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid site.");

            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            return Result.Success();
        }
    }
}
