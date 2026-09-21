using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Fixed WorldSite 政治易主的唯一事务入口。
    /// WorldSite.OwnerFactionId 是政治 cause，TerritoryClaim 是历史空间 authority；
    /// 现代行政控制只由 Site Owner + TerritoryClaim + Actual Control resolver 表达。
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
            return Result.Success();
        }
    }
}
