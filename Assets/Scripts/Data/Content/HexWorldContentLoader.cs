using System;
using System.Collections.Generic;
using System.Text;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Data.Content
{
    /// <summary>正式 Hex World Content JSON → Runtime Domain 的唯一加载入口。</summary>
    public static class HexWorldContentLoader
    {
        public const int SupportedSchemaVersion = 1;

        public static Result Apply(SimulationWorld world, HexWorldContentDefinition definition)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "world null.");
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "HexWorldContentDefinition null.");
            if (definition.Width < 1 || definition.Height < 1)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid hex world size.");

            var grid = world.HexWorld;
            grid.Clear();
            grid.MapId = definition.Id.ToString();
            grid.MapName = definition.Name ?? string.Empty;
            grid.HexSize = definition.HexSize > 0f ? definition.HexSize : HexWorldScale.DefaultHexOuterRadius;
            grid.FillRectangle(definition.Width, definition.Height, ParseTerrain(definition.DefaultTerrain));

            ApplyDefaultPassability(grid, definition.DefaultTerrain, definition.DefaultPassable);

            if (definition.Cells != null)
            {
                for (var i = 0; i < definition.Cells.Count; i++)
                {
                    var src = definition.Cells[i];
                    if (src == null || !grid.IsInBounds(src.Q, src.R))
                        continue;
                    if (!grid.TryGetCell(new HexCoord(src.Q, src.R), out var cell) || cell == null)
                        continue;
                    cell.Terrain = ParseTerrain(src.Terrain);
                    cell.IsRoad = src.IsRoad;
                    cell.IsPassable = src.Passable ?? ResolvePassable(cell.Terrain, cell.IsRoad);
                }
            }

            world.Strategic.Sites.Clear();
            world.Strategic.TerritoryRegions.Clear();
            if (definition.Sites != null)
            {
                for (var i = 0; i < definition.Sites.Count; i++)
                {
                    var src = definition.Sites[i];
                    if (src == null || string.IsNullOrWhiteSpace(src.SiteId))
                        continue;
                    RegisterSite(world, src);
                }
            }

            // §12 加载顺序：清空 ControlFactionId（grid.Clear 已保证新 cell 为 ""）
            // → apply standaloneTerritoryHexes → apply TerritoryRegions。
            // standalone 与 Region 同含同一 Hex = Content ERROR（不静默覆盖）。
            var standaloneHexes = new Dictionary<HexCoord, string>();
            var standaloneResult = ApplyStandaloneHexControls(world, definition, standaloneHexes);
            if (standaloneResult.IsFailure)
                return standaloneResult;

            if (definition.TerritoryRegions != null)
            {
                for (var i = 0; i < definition.TerritoryRegions.Count; i++)
                {
                    var src = definition.TerritoryRegions[i];
                    if (src == null || string.IsNullOrWhiteSpace(src.RegionId))
                        continue;
                    var applied = RegisterTerritoryRegion(world, src, standaloneHexes);
                    if (applied.IsFailure)
                        return applied;
                }
            }

            var errors = TerritoryInvariantValidator.Validate(world);
            if (errors.Count > 0)
            {
                var sb = new StringBuilder(512);
                for (var i = 0; i < errors.Count; i++)
                {
                    if (i > 0)
                        sb.Append("\n");
                    sb.Append(errors[i]);
                }

                return Result.Failure(ErrorCode.ContentLoadFailed, "Territory content error: " + sb);
            }

            return Result.Success();
        }

        /// <summary>
        /// 荒野单格控制权（不属于任何 WorldSite Region 的 Hex）。在 TerritoryRegions 前 apply；
        /// 与任何 Region hex 重叠 → Content ERROR。
        /// </summary>
        static Result ApplyStandaloneHexControls(
            SimulationWorld world,
            HexWorldContentDefinition definition,
            Dictionary<HexCoord, string> standaloneHexes)
        {
            if (definition.StandaloneTerritoryHexes == null)
                return Result.Success();

            for (var i = 0; i < definition.StandaloneTerritoryHexes.Count; i++)
            {
                var src = definition.StandaloneTerritoryHexes[i];
                if (src == null)
                    continue;
                var hex = new HexCoord(src.Q, src.R);
                if (!world.HexWorld.IsInBounds(hex.Q, hex.R))
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "standaloneTerritoryHexes[" + i + "] out of bounds: " + hex + ".");
                if (standaloneHexes.ContainsKey(hex))
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "standaloneTerritoryHexes duplicate hex: " + hex + ".");
                if (!world.HexWorld.TryGetCell(hex, out var cell) || cell == null)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "standaloneTerritoryHexes hex missing in grid: " + hex + ".");
                var controller = src.ControlFactionId ?? string.Empty;
                standaloneHexes[hex] = controller;
                cell.ControlFactionId = controller;
            }

            return Result.Success();
        }

        static Result RegisterTerritoryRegion(
            SimulationWorld world,
            TerritoryRegionContentDefinition src,
            Dictionary<HexCoord, string> standaloneHexes)
        {
            var region = new TerritoryRegion
            {
                RegionId = src.RegionId ?? string.Empty,
                PrimaryWorldSiteId = src.PrimaryWorldSiteId ?? string.Empty,
                ControlFactionId = src.ControlFactionId ?? string.Empty,
            };
            if (src.Hexes != null)
            {
                var coords = new HexCoord[src.Hexes.Count];
                for (var i = 0; i < src.Hexes.Count; i++)
                    coords[i] = new HexCoord(src.Hexes[i].Q, src.Hexes[i].R);
                region.SetHexes(coords);
            }

            if (!string.IsNullOrEmpty(region.PrimaryWorldSiteId))
            {
                if (!world.Strategic.Sites.TryGet(region.PrimaryWorldSiteId, out var site) || site == null)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "TerritoryRegion '" + region.RegionId + "' PrimaryWorldSiteId '" +
                        region.PrimaryWorldSiteId + "' missing.");
                if (!string.Equals(site.TerritoryRegionId, region.RegionId, StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "WorldSite '" + site.SiteId + "'.TerritoryRegionId '" + site.TerritoryRegionId +
                        "' != TerritoryRegion '" + region.RegionId + "'.");
            }

            try
            {
                world.Strategic.TerritoryRegions.Register(region);
            }
            catch (System.InvalidOperationException ex)
            {
                // Register 的跨 Region overlap 是硬错误（2J §6.6）；转 Result 使 Apply 契约不被异常击穿。
                return Result.Failure(ErrorCode.ContentLoadFailed, ex.Message);
            }

            for (var i = 0; i < region.Hexes.Count; i++)
            {
                var hex = region.Hexes[i];
                if (standaloneHexes.ContainsKey(hex))
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Hex " + hex + " belongs to both standaloneTerritoryHexes and TerritoryRegion '" +
                        region.RegionId + "' (standalone controller='" + standaloneHexes[hex] + "').");
                if (!world.HexWorld.TryGetCell(hex, out var cell) || cell == null)
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "TerritoryRegion '" + region.RegionId + "' hex " + hex + " missing in grid.");
                cell.ControlFactionId = region.ControlFactionId;
            }

            return Result.Success();
        }

        static void ApplyDefaultPassability(HexWorld grid, string defaultTerrain, bool defaultPassable)
        {
            if (!grid.UsesCompactStorage)
                return;
            for (var r = 0; r < grid.Height; r++)
            {
                for (var q = 0; q < grid.Width; q++)
                {
                    if (!grid.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                        continue;
                    cell.IsPassable = defaultPassable;
                    cell.IsRoad = string.Equals(defaultTerrain, "Road", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        static void RegisterSite(SimulationWorld world, HexWorldSiteDefinition src)
        {
            var anchor = new HexCoord(src.AnchorQ, src.AnchorR);
            var loadedPresence = new HexCoord(
                src.PresenceQ ?? src.AnchorQ,
                src.PresenceR ?? src.AnchorR);
            var site = new WorldSite
            {
                SiteId = src.SiteId,
                DisplayName = src.DisplayName ?? src.SiteId,
                SiteType = src.SiteType ?? "Site",
                AnchorHex = anchor,
                PresenceHex = anchor,
                LocalMapId = src.LocalMapId ?? string.Empty,
                OwnerFactionId = src.OwnerFactionId ?? string.Empty,
                TerritoryRegionId = src.TerritoryRegionId ?? string.Empty,
            };

            if (src.Footprint != null && src.Footprint.Count > 0)
            {
                var footprint = new HexCoord[src.Footprint.Count];
                for (var i = 0; i < src.Footprint.Count; i++)
                    footprint[i] = new HexCoord(src.Footprint[i].Q, src.Footprint[i].R);
                site.SetFootprint(footprint);
            }
            else
            {
                site.SetFootprint(new[] { anchor });
            }

            if (site.HasPresenceAnchorMismatch(loadedPresence))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[HexWorldContentLoader] PresenceHex != AnchorHex for site '" + src.SiteId +
                    "': loaded (" + loadedPresence.Q + "," + loadedPresence.R + ") -> anchor (" +
                    anchor.Q + "," + anchor.R + ").");
            }

            site.EnsurePresenceHexValid();
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
        }

        static HexTerrainType ParseTerrain(string terrain)
        {
            if (string.IsNullOrWhiteSpace(terrain))
                return HexTerrainType.Plain;
            switch (terrain.Trim())
            {
                case "Forest":
                    return HexTerrainType.Forest;
                case "Mountain":
                case "Rock":
                    return HexTerrainType.Mountain;
                case "Water":
                    return HexTerrainType.Water;
                case "Road":
                    return HexTerrainType.Road;
                default:
                    return HexTerrainType.Plain;
            }
        }

        static bool ResolvePassable(HexTerrainType terrain, bool isRoad)
        {
            if (isRoad || terrain == HexTerrainType.Road)
                return true;
            return HexTerrainCatalog.IsPassableByDefault(terrain);
        }
    }
}
