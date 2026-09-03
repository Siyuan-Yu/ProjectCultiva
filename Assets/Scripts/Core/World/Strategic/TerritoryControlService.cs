using System;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Territory 唯一写入口（2J §6.18）。<see cref="SetRegionController"/> 同时更新
    /// Region.ControlFactionId 与其全部 Hex 的 ControlFactionId；<b>不</b>自动改 Site Owner
    /// （Capture 事务由下一轮 TransferWorldSiteAndTerritory 统一负责，避免循环依赖）。
    /// </summary>
    public static class TerritoryControlService
    {
        /// <summary>Hex 政治控制者（可能空 = None）。直接读 cell.ControlFactionId 亦可，本方法统一入口。</summary>
        public static string GetController(SimulationWorld world, HexCoord hex)
        {
            if (world?.HexWorld == null)
                return string.Empty;
            if (!world.HexWorld.TryGetCell(hex, out var cell) || cell == null)
                return string.Empty;
            return cell.ControlFactionId ?? string.Empty;
        }

        public static bool GetRegionForSite(
            SimulationWorld world,
            string siteId,
            out TerritoryRegion region)
        {
            region = null;
            if (world?.Strategic?.TerritoryRegions == null || string.IsNullOrEmpty(siteId))
                return false;
            return world.Strategic.TerritoryRegions.TryGetByPrimaryWorldSite(siteId, out region);
        }

        /// <summary>规格命名别名：优先 site.TerritoryRegionId，然后 Board.TryGet；不按 Owner / 距离推导。</summary>
        public static bool TryGetRegionForSite(
            SimulationWorld world,
            string siteId,
            out TerritoryRegion region)
        {
            region = null;
            if (world?.Strategic?.Sites == null ||
                world?.Strategic?.TerritoryRegions == null ||
                string.IsNullOrEmpty(siteId))
                return false;
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return false;
            if (string.IsNullOrEmpty(site.TerritoryRegionId))
                return false;
            return world.Strategic.TerritoryRegions.TryGet(site.TerritoryRegionId, out region);
        }

        /// <summary>Hex → 所属 TerritoryRegion（Board O(1)）。无 Region = false。</summary>
        public static bool TryGetRegionAtHex(
            SimulationWorld world,
            HexCoord hex,
            out TerritoryRegion region)
        {
            region = null;
            if (world?.Strategic?.TerritoryRegions == null)
                return false;
            return world.Strategic.TerritoryRegions.TryGetAtHex(hex, out region);
        }

        /// <summary>整块设置 Region Controller（region.ControlFactionId + 全部 Hex.ControlFactionId）。</summary>
        public static void SetRegionController(
            SimulationWorld world,
            string regionId,
            string factionId)
        {
            if (world?.Strategic?.TerritoryRegions == null || string.IsNullOrEmpty(regionId))
                return;
            if (!world.Strategic.TerritoryRegions.TryGet(regionId, out var region) || region == null)
                return;

            var controller = factionId ?? string.Empty;
            region.ControlFactionId = controller;

            for (var i = 0; i < region.Hexes.Count; i++)
            {
                var hex = region.Hexes[i];
                if (!world.HexWorld.TryGetCell(hex, out var cell) || cell == null)
                    continue;
                cell.ControlFactionId = controller;
            }
        }
    }
}
