using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Construction
{
    /// <summary>
    /// Single authority for binding a StorageRoom footprint to one active, owned WorldSite.
    /// Presentation may use this for read-only preview; ConstructionService re-runs it at commit.
    /// </summary>
    public static class WorldSiteStorageRoomPlacementService
    {
        public static Result ResolveSiteForFootprint(
            SimulationWorld world, string factionId, string surfaceId, float worldX, float worldY,
            int cellsW, int cellsH, float cellSize, out string siteId)
        {
            siteId = string.Empty;
            if (world == null || string.IsNullOrWhiteSpace(factionId) || string.IsNullOrWhiteSpace(surfaceId) ||
                cellsW <= 0 || cellsH <= 0 || cellSize <= 0f)
                return Result.Failure(ErrorCode.InvalidArgument, "储藏室位置或据点信息无效。");

            for (var y = 0; y < cellsH; y++)
            for (var x = 0; x < cellsW; x++)
            {
                if (!WorldSiteAdministrativeControlResolver.TryResolve(world, surfaceId,
                        worldX + (x + .5f) * cellSize, worldY + (y + .5f) * cellSize,
                        out var site, out _) || site == null || !site.IsCoreActive)
                    return Result.Failure(ErrorCode.InvalidOperation, "储藏室必须完整建在同一己方有效据点内。");
                if (!string.Equals(site.OwnerFactionId, factionId, StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.InvalidOperation, "此处属于其他势力的实际行政控制范围。");
                if (siteId.Length == 0) siteId = site.SiteId;
                else if (!string.Equals(siteId, site.SiteId, StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.InvalidOperation, "储藏室必须完整建在同一据点内。");
            }

            if (siteId.Length == 0)
                return Result.Failure(ErrorCode.InvalidOperation, "储藏室必须完整建在同一据点内。");
            if (world.SiteStorageRooms.TryGetBySite(siteId, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "该据点已有储藏室。");
            return Result.Success();
        }
    }
}
