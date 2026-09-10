using System;

namespace XianXia.Core.Navigation
{
    /// <summary>
    /// 8-neighbour A* on <see cref="WalkGrid"/> with corner-cut guards and LOS string-pull. Pure Core.
    ///
    /// The algorithm itself lives in <see cref="GridPathfinderWorkspace"/> so repeated requests
    /// (NPC schedule repaths on a Continuous surface) reuse their buffers instead of allocating
    /// g/f/cameFrom/closed per call. These static entry points keep the original API and share one
    /// workspace; use a dedicated workspace if you need an isolated one (tests).
    /// </summary>
    public static class GridPathfinder
    {
        static readonly GridPathfinderWorkspace Shared = new GridPathfinderWorkspace();

        public static bool TryFindPath(
            WalkGrid grid,
            int startX,
            int startY,
            int goalX,
            int goalY,
            System.Collections.Generic.List<GridCoord> pathOut) =>
            Shared.TryFindPath(grid, startX, startY, goalX, goalY, pathOut);

        /// <summary>World-space path (cell centres, string-pulled). Snaps start within 8, goal within 4.</summary>
        public static bool TryFindWorldPath(
            WalkGrid grid,
            float startX,
            float startY,
            float goalX,
            float goalY,
            System.Collections.Generic.List<float> pathXyOut) =>
            Shared.TryFindWorldPath(grid, startX, startY, goalX, goalY, pathXyOut, 8, 4);

        public static bool TryFindWorldPath(
            WalkGrid grid,
            float startX,
            float startY,
            float goalX,
            float goalY,
            System.Collections.Generic.List<float> pathXyOut,
            int startSnapRadius,
            int goalSnapRadius) =>
            Shared.TryFindWorldPath(grid, startX, startY, goalX, goalY, pathXyOut, startSnapRadius, goalSnapRadius);

        /// <summary>
        /// Drop intermediate cells when a straight segment between kept points stays on walkable cells.
        /// Produces true diagonal legs instead of axis-aligned staircases.
        /// </summary>
        public static void SimplifyCells(WalkGrid grid, System.Collections.Generic.List<GridCoord> cells)
        {
            if (grid == null || cells == null || cells.Count <= 2)
                return;

            var write = 1;
            var anchor = 0;
            for (var i = 1; i < cells.Count; i++)
            {
                var canSkipToHere = i + 1 < cells.Count &&
                                    IsCellSegmentWalkable(grid, cells[anchor], cells[i + 1]);
                if (canSkipToHere)
                    continue;

                if (write != i)
                    cells[write] = cells[i];
                write++;
                anchor = i;
            }

            if (write < cells.Count)
                cells.RemoveRange(write, cells.Count - write);
        }

        /// <summary>Sample the segment; false if any sample lands on a blocked／OOB cell.</summary>
        public static bool IsWorldSegmentWalkable(
            WalkGrid grid, float x0, float y0, float x1, float y1)
        {
            if (grid == null)
                return false;
            var dx = x1 - x0;
            var dy = y1 - y0;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1e-4)
            {
                if (!grid.TryWorldToCell(x0, y0, out var cx, out var cy))
                    return false;
                return grid.IsWalkable(cx, cy);
            }

            var step = Math.Max(0.25f, grid.CellSize * 0.35f);
            var n = Math.Max(1, (int)Math.Ceiling(dist / step));
            for (var i = 0; i <= n; i++)
            {
                var t = i / (float)n;
                var x = x0 + dx * t;
                var y = y0 + dy * t;
                if (!grid.TryWorldToCell(x, y, out var cx, out var cy) || !grid.IsWalkable(cx, cy))
                    return false;
            }

            return true;
        }

        static bool IsCellSegmentWalkable(WalkGrid grid, GridCoord a, GridCoord b)
        {
            grid.CellToWorldCenter(a.X, a.Y, out var ax, out var ay);
            grid.CellToWorldCenter(b.X, b.Y, out var bx, out var by);
            return IsWorldSegmentWalkable(grid, ax, ay, bx, by);
        }
    }
}
