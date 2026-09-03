using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>TerritoryRegion 领域真源 Board（2J §6.3）。禁止扫描全地图 Hex 猜 Region 归属。</summary>
    public sealed class TerritoryRegionBoard
    {
        readonly Dictionary<string, TerritoryRegion> _byRegionId =
            new Dictionary<string, TerritoryRegion>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _regionIdByPrimarySite =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, TerritoryRegion> Regions => _byRegionId;

        public void Clear()
        {
            _byRegionId.Clear();
            _regionIdByPrimarySite.Clear();
        }

        /// <summary>注册 Region；同 RegionId 重复注册 = 覆盖（内容重载语义）。</summary>
        public void Register(TerritoryRegion region)
        {
            if (region == null || string.IsNullOrEmpty(region.RegionId))
                throw new ArgumentException("TerritoryRegion requires RegionId.");

            _byRegionId[region.RegionId] = region;
            if (!string.IsNullOrEmpty(region.PrimaryWorldSiteId))
                _regionIdByPrimarySite[region.PrimaryWorldSiteId] = region.RegionId;
        }

        public bool TryGet(string regionId, out TerritoryRegion region) =>
            _byRegionId.TryGetValue(regionId, out region) && region != null;

        public bool TryGetByPrimaryWorldSite(string siteId, out TerritoryRegion region)
        {
            region = null;
            if (string.IsNullOrEmpty(siteId))
                return false;
            return _regionIdByPrimarySite.TryGetValue(siteId, out var regionId) &&
                   TryGet(regionId, out region);
        }
    }
}
