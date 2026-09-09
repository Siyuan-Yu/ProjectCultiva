using System.Collections.Generic;
using XianXia.Core.World.Surface;

namespace XianXia.Data.Content
{
    /// <summary>Pure W1C coverage query. Overlapping authored surfaces are an explicit error.</summary>
    public static class OutdoorSurfaceCoverageResolver
    {
        public static bool TryResolveAtWorldPosition(DefinitionRegistry registry, float worldX, float worldY, out OutdoorWorldSurfaceDefinition surface)
        {
            surface = null;
            if (registry == null) return false;
            foreach (var entry in registry.OutdoorSurfaces)
            {
                if (!ContainsWorldPosition(entry.Value, worldX, worldY)) continue;
                if (surface != null) { surface = null; return false; }
                surface = entry.Value;
            }
            return surface != null;
        }

        public static bool ContainsWorldPosition(OutdoorWorldSurfaceDefinition surface, float worldX, float worldY)
        {
            if (surface == null) return false;
            var mapper = new OutdoorSurfaceCoordinateMapper(surface.ChunkWidth, surface.ChunkHeight, surface.CellSize, originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            var coord = mapper.WorldToChunk(worldX, worldY);
            for (var i = 0; i < surface.Chunks.Count; i++) if (surface.Chunks[i].Coord == coord) return true;
            return false;
        }
    }
}
