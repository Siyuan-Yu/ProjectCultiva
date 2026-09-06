using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Fixed WorldSite 政治易主的唯一事务入口（2J §6.19）：Site Owner 与绑定 TerritoryRegion
    /// Controller 必须一次一起变，不允许出现「Owner 已改、Region 未改」的中间态。
    ///
    /// 行为：
    ///  1. 找 WorldSite；
    ///  2. 经 site.TerritoryRegionId 找 TerritoryRegion；
    ///  3. 验证 Region.PrimaryWorldSiteId == site.SiteId（双向绑定，不一致 = 数据错误不静默修）；
    ///  4. site.OwnerFactionId = newFactionId；
    ///  5. TerritoryControlService.SetRegionController（同步 Region.ControlFactionId + 全部 Hex.ControlFactionId）；
    ///  6. 后置校验（Development-only assert）。
    ///
    /// 无 TerritoryRegion 的 legacy / dynamic Site（TerritoryRegionId == ""）允许 fallback 到
    /// WorldSiteOwnershipService.SetOwner —— 未来 Dynamic Site 本来就没有 Region，不强制所有
    /// Runtime Site 都必须有 Region。
    /// 低层单点写（WorldSiteOwnershipService.SetOwner）保留用于 dynamic / legacy setup / 底层 restore，
    /// 正式 Fixed Site Capture 一律走本 service。
    /// </summary>
    public static class WorldSiteTerritoryTransferService
    {
        public static Result Transfer(
            SimulationWorld world,
            string siteId,
            string newFactionId)
        {
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "WorldSiteTerritoryTransfer requires world + siteId.");

            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite not found.", siteId);

            var faction = newFactionId ?? string.Empty;

            if (string.IsNullOrEmpty(site.TerritoryRegionId))
            {
                // legacy / dynamic Site：无 Region，仅 Owner（低层语义，不产生 Region 副作用）。
                WorldSiteOwnershipService.SetOwner(world, siteId, faction);
                StrategicTerritoryCoverageResolver.Rebuild(world);
                return Result.Success();
            }

            var regionId = site.TerritoryRegionId;
            if (world.Strategic.TerritoryRegions == null ||
                !world.Strategic.TerritoryRegions.TryGet(regionId, out var region) ||
                region == null)
            {
                return Result.Failure(
                    ErrorCode.ContentLoadFailed,
                    "WorldSite '" + siteId + "' TerritoryRegionId '" + regionId + "' missing.");
            }

            if (!string.Equals(region.PrimaryWorldSiteId, siteId, StringComparison.Ordinal))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "TerritoryRegion '" + regionId + "' PrimaryWorldSiteId='" +
                    region.PrimaryWorldSiteId + "' != WorldSite '" + siteId + "'.");
            }

            // 4. Site Owner
            site.OwnerFactionId = faction;
            // Region/Hex 是 resolver 的 compatibility projection；Site Owner 才是政治 cause。
            StrategicTerritoryCoverageResolver.Rebuild(world);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Debug.Assert(
                string.Equals(site.OwnerFactionId ?? string.Empty, faction, StringComparison.Ordinal),
                "WorldSiteTerritoryTransfer post-condition: Owner write failed.");
            System.Diagnostics.Debug.Assert(
                string.Equals(region.ControlFactionId ?? string.Empty, faction, StringComparison.Ordinal),
                "WorldSiteTerritoryTransfer post-condition: Region controller write failed.");
            for (var i = 0; i < region.Hexes.Count; i++)
            {
                var hex = region.Hexes[i];
                if (world.HexWorld != null && world.HexWorld.TryGetCell(hex, out var cell) && cell != null)
                {
                    System.Diagnostics.Debug.Assert(
                        string.Equals(cell.ControlFactionId ?? string.Empty, faction, StringComparison.Ordinal),
                        "WorldSiteTerritoryTransfer post-condition: hex " + hex + " controller write failed.");
                }
            }
#endif

            return Result.Success();
        }
    }
}
