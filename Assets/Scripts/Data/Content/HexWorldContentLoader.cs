using System;
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
            var site = new WorldSite
            {
                SiteId = src.SiteId,
                DisplayName = src.DisplayName ?? src.SiteId,
                SiteType = src.SiteType ?? "Site",
                AnchorHex = anchor,
                LocalMapId = src.LocalMapId ?? string.Empty,
                OwnerFactionId = src.OwnerFactionId ?? string.Empty,
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
