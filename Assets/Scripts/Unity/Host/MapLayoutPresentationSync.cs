using XianXia.Core.Exploration;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Align worldRegion location presentation with mapLayout placement centers
    /// (via placement.boundLocationId) so MapEditor layout matches Host.
    /// </summary>
    public static class MapLayoutPresentationSync
    {
        public static int Apply(PlayableHostSession session)
        {
            if (session?.Registry?.MapLayouts == null ||
                session.World?.WorldRegion?.Locations == null)
                return 0;

            MapLayoutDefinition layout = null;
            foreach (var kv in session.Registry.MapLayouts)
            {
                layout = kv.Value;
                if (!string.IsNullOrEmpty(kv.Value.WorldRegionId) &&
                    kv.Value.WorldRegionId.IndexOf("ch01", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    break;
            }

            if (layout?.Placements == null)
                return 0;

            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var applied = 0;
            foreach (var p in layout.Placements)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.BoundLocationId))
                    continue;
                if (!session.World.WorldRegion.Locations.TryGetValue(p.BoundLocationId, out var loc) ||
                    loc == null)
                    continue;

                var pw = p.W < 1 ? 1 : p.W;
                var ph = p.H < 1 ? 1 : p.H;
                loc.PresentationX = layout.OriginX + (p.X + pw * 0.5f) * cs;
                loc.PresentationZ = layout.OriginY + (p.Y + ph * 0.5f) * cs;
                applied++;
            }

            return applied;
        }

        public static bool TryGetLayout(PlayableHostSession session, out MapLayoutDefinition layout)
        {
            layout = null;
            if (session?.Registry?.MapLayouts == null || session.Registry.MapLayouts.Count == 0)
                return false;
            foreach (var kv in session.Registry.MapLayouts)
            {
                layout = kv.Value;
                if (!string.IsNullOrEmpty(kv.Value.WorldRegionId) &&
                    kv.Value.WorldRegionId.IndexOf("ch01", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return layout != null;
        }
    }
}
