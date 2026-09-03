using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>TerritoryRegion 领域真源 Board（2J §6.3）。禁止扫描全地图 Hex 猜 Region 归属。</summary>
    public sealed class TerritoryRegionBoard
    {
        readonly Dictionary<string, TerritoryRegion> _byRegionId =
            new Dictionary<string, TerritoryRegion>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _regionIdByPrimarySite =
            new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<HexCoord, string> _regionIdByHex =
            new Dictionary<HexCoord, string>();

        public IReadOnlyDictionary<string, TerritoryRegion> Regions => _byRegionId;

        public void Clear()
        {
            _byRegionId.Clear();
            _regionIdByPrimarySite.Clear();
            _regionIdByHex.Clear();
        }

        /// <summary>
        /// 注册 Region。跨 Region overlap 是 <b>硬错误</b>（2J §6.6 制作人决定：初始 Content 不允许重叠）：
        /// 同一 Hex 已属于 Region A 再注册 Region B → throw InvalidOperationException，
        /// 不自动裁决（不 nearest、不 tie-break、不 first-come）。
        /// 同 RegionId 重复注册 = 覆盖（内容重载语义，先清旧 hex 索引再重写）。
        /// </summary>
        public void Register(TerritoryRegion region)
        {
            if (region == null || string.IsNullOrEmpty(region.RegionId))
                throw new ArgumentException("TerritoryRegion requires RegionId.");

            if (_byRegionId.TryGetValue(region.RegionId, out var previous) && previous != null)
            {
                // 幂等重载：移除旧 Region 的 hex 索引后按新 Hexes 重写。
                for (var i = 0; i < previous.Hexes.Count; i++)
                {
                    if (_regionIdByHex.TryGetValue(previous.Hexes[i], out var rid) &&
                        string.Equals(rid, region.RegionId, StringComparison.Ordinal))
                        _regionIdByHex.Remove(previous.Hexes[i]);
                }
            }

            for (var i = 0; i < region.Hexes.Count; i++)
            {
                var hex = region.Hexes[i];
                if (_regionIdByHex.TryGetValue(hex, out var existing) &&
                    !string.Equals(existing, region.RegionId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Territory hex overlap: hex=(" + hex.Q + "," + hex.R +
                        ") existingRegion=" + existing + " newRegion=" + region.RegionId + ".");
                }

                _regionIdByHex[hex] = region.RegionId;
            }

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

        /// <summary>Hex → 所属 Region（O(1)）。无 Region = false（不扫全表）。</summary>
        public bool TryGetAtHex(HexCoord hex, out TerritoryRegion region)
        {
            region = null;
            if (!_regionIdByHex.TryGetValue(hex, out var regionId))
                return false;
            return TryGet(regionId, out region);
        }
    }
}
