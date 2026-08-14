using XianXia.Core.Navigation;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Build a WalkGrid from authored mapLayout placements (blocksMovement rectangles).
    /// Policy: only placements with blocksMovement=true block; MapEditor defaults buildings on,
    /// decorations off (manual checkbox).
    /// </summary>
    public static class MapLayoutWalkGridBuilder
    {
        public static WalkGrid Create(MapLayoutDefinition layout)
        {
            if (layout == null)
                return new WalkGrid(0f, 0f, 1f, 1, 1);

            var cellSize = layout.CellSize > 0f ? layout.CellSize : 1f;
            var width = layout.Width > 0 ? layout.Width : 1;
            var height = layout.Height > 0 ? layout.Height : 1;
            var grid = new WalkGrid(layout.OriginX, layout.OriginY, cellSize, width, height);

            if (layout.Placements == null)
                return grid;

            foreach (var p in layout.Placements)
            {
                if (p == null || !p.BlocksMovement)
                    continue;
                // Zones should not block even if mis-authored.
                if (IsZoneKind(p.Kind))
                    continue;
                var w = p.W < 1 ? 1 : p.W;
                var h = p.H < 1 ? 1 : p.H;
                grid.SetBlockedRect(p.X, p.Y, p.X + w - 1, p.Y + h - 1, true);
            }

            // Keep a walkable cross at non-blocking bound locations (interaction approach).
            foreach (var p in layout.Placements)
            {
                if (p == null || string.IsNullOrEmpty(p.BoundLocationId) || p.BlocksMovement)
                    continue;
                if (IsZoneKind(p.Kind))
                    continue;
                var cx = p.X + (p.W < 1 ? 0 : p.W / 2);
                var cy = p.Y + (p.H < 1 ? 0 : p.H / 2);
                ClearCross(grid, cx, cy);
            }

            return grid;
        }

        static bool IsZoneKind(string kind)
        {
            if (string.IsNullOrEmpty(kind))
                return false;
            return kind.StartsWith("zone", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(kind, "forest", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(kind, "spring", System.StringComparison.OrdinalIgnoreCase);
        }

        static void ClearCross(WalkGrid grid, int cx, int cy)
        {
            grid.SetBlocked(cx, cy, false);
            grid.SetBlocked(cx + 1, cy, false);
            grid.SetBlocked(cx - 1, cy, false);
            grid.SetBlocked(cx, cy + 1, false);
            grid.SetBlocked(cx, cy - 1, false);
        }
    }
}
