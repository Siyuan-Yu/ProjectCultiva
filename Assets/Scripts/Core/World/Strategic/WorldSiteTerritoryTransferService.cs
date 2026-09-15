using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Fixed WorldSite 政治易主的唯一事务入口。
    /// WorldSite.OwnerFactionId 是政治 cause，TerritoryClaim 是历史空间 authority；
    /// TerritoryRegion／Hex 仅由 coverage rebuild 生成 compatibility projection，不是 Capture 前置条件。
    /// </summary>
    public static class WorldSiteTerritoryTransferService
    {
        public static Result Transfer(
            SimulationWorld world,
            string siteId,
            string newFactionId)
        {
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(siteId) || string.IsNullOrWhiteSpace(newFactionId))
                return Result.Failure(ErrorCode.InvalidArgument, "WorldSiteTerritoryTransfer requires world + siteId + newFactionId.");

            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite not found.", siteId);

            WorldSiteOwnershipService.SetOwner(world, siteId, newFactionId);
            StrategicTerritoryCoverageResolver.Rebuild(world);
            return Result.Success();
        }
    }
}
