using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Data.Content
{
    /// <summary>Runtime HexWorld → HexWorldContentDefinition（Editor 导出 / 迁移用）。</summary>
    public static class HexWorldContentExporter
    {
        public static HexWorldContentDefinition Export(SimulationWorld world)
        {
            var grid = world.HexWorld;
            var definition = new HexWorldContentDefinition
            {
                Id = DefinitionId.Parse(
                    string.IsNullOrEmpty(grid.MapId) ? "base:hex_world_export" : grid.MapId).Value,
                Name = grid.MapName ?? string.Empty,
                Width = grid.Width,
                Height = grid.Height,
                HexSize = grid.HexSize,
                DefaultTerrain = "Mountain",
                DefaultPassable = false,
            };

            for (var r = 0; r < grid.Height; r++)
            {
                for (var q = 0; q < grid.Width; q++)
                {
                    if (!grid.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                        continue;
                    definition.Cells.Add(new HexWorldCellDefinition
                    {
                        Q = q,
                        R = r,
                        Terrain = TerrainToString(cell.Terrain),
                        Passable = cell.IsPassable,
                        IsRoad = cell.IsRoad,
                    });
                }
            }

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;
                var dto = new HexWorldSiteDefinition
                {
                    SiteId = site.SiteId,
                    DisplayName = site.DisplayName,
                    SiteType = site.SiteType,
                    AnchorQ = site.AnchorHex.Q,
                    AnchorR = site.AnchorHex.R,
                    PresenceQ = site.AnchorHex.Q,
                    PresenceR = site.AnchorHex.R,
                    LocalMapId = site.LocalMapId,
                    OwnerFactionId = site.OwnerFactionId,
                };
                foreach (var hex in site.EnumerateFootprintHexes())
                    dto.Footprint.Add(new HexWorldCoordDefinition { Q = hex.Q, R = hex.R });
                definition.Sites.Add(dto);
            }

            definition.Sites.Sort((a, b) => string.CompareOrdinal(a.SiteId, b.SiteId));
            return definition;
        }

        static string TerrainToString(HexTerrainType terrain) =>
            terrain switch
            {
                HexTerrainType.Forest => "Forest",
                HexTerrainType.Mountain => "Mountain",
                HexTerrainType.Water => "Water",
                HexTerrainType.Road => "Road",
                _ => "Plain",
            };
    }
}
