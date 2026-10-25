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
        // The global Surface can span thousands of cells. A string-pull lookahead must stay
        // bounded: rechecking an ever longer ray from one anchor makes long routes quadratic.
        const int MaxSimplifyLookaheadCells = 48;

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
                                    i + 1 - anchor <= MaxSimplifyLookaheadCells &&
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

        /// <summary>
        /// Exact grid traversal for a world-space segment. Every crossed cell is checked and a
        /// vertex crossing additionally checks both adjacent cardinal cells, matching A*'s
        /// no-corner-cut rule.
        /// </summary>
        public static bool IsWorldSegmentWalkable(
            WalkGrid grid, float x0, float y0, float x1, float y1)
        {
            if (grid == null)
                return false;
            if (!grid.TryWorldToCell(x0, y0, out var cx, out var cy) ||
                !grid.TryWorldToCell(x1, y1, out var goalX, out var goalY) ||
                !grid.IsWalkable(cx, cy) || !grid.IsWalkable(goalX, goalY))
                return false;
            if (cx == goalX && cy == goalY)
                return true;

            var gx0 = (x0 - grid.OriginX) / grid.CellSize;
            var gy0 = (y0 - grid.OriginY) / grid.CellSize;
            var gx1 = (x1 - grid.OriginX) / grid.CellSize;
            var gy1 = (y1 - grid.OriginY) / grid.CellSize;
            const double boundaryEpsilon = 1e-7;
            var onVerticalBoundary = Math.Abs(gx0 - Math.Round(gx0)) <= boundaryEpsilon &&
                                     Math.Abs(gx1 - Math.Round(gx1)) <= boundaryEpsilon;
            var onHorizontalBoundary = Math.Abs(gy0 - Math.Round(gy0)) <= boundaryEpsilon &&
                                       Math.Abs(gy1 - Math.Round(gy1)) <= boundaryEpsilon;
            if (!BoundaryCompanionsWalkable(
                    grid, cx, cy, onVerticalBoundary, onHorizontalBoundary))
                return false;
            var dx = gx1 - gx0;
            var dy = gy1 - gy0;
            var stepX = dx > 0d ? 1 : dx < 0d ? -1 : 0;
            var stepY = dy > 0d ? 1 : dy < 0d ? -1 : 0;
            var deltaX = stepX == 0 ? double.PositiveInfinity : Math.Abs(1d / dx);
            var deltaY = stepY == 0 ? double.PositiveInfinity : Math.Abs(1d / dy);
            var nextX = stepX > 0 ? cx + 1d : cx;
            var nextY = stepY > 0 ? cy + 1d : cy;
            var maxX = stepX == 0 ? double.PositiveInfinity : (nextX - gx0) / dx;
            var maxY = stepY == 0 ? double.PositiveInfinity : (nextY - gy0) / dy;
            const double tieEpsilon = 1e-10;

            while (cx != goalX || cy != goalY)
            {
                if (maxX + tieEpsilon < maxY)
                {
                    cx += stepX;
                    maxX += deltaX;
                    if (!grid.IsWalkable(cx, cy) || !BoundaryCompanionsWalkable(
                            grid, cx, cy, onVerticalBoundary, onHorizontalBoundary)) return false;
                }
                else if (maxY + tieEpsilon < maxX)
                {
                    cy += stepY;
                    maxY += deltaY;
                    if (!grid.IsWalkable(cx, cy) || !BoundaryCompanionsWalkable(
                            grid, cx, cy, onVerticalBoundary, onHorizontalBoundary)) return false;
                }
                else
                {
                    if (!grid.IsWalkable(cx + stepX, cy) ||
                        !grid.IsWalkable(cx, cy + stepY) ||
                        !grid.IsWalkable(cx + stepX, cy + stepY))
                        return false;
                    cx += stepX;
                    cy += stepY;
                    maxX += deltaX;
                    maxY += deltaY;
                    if (!BoundaryCompanionsWalkable(
                            grid, cx, cy, onVerticalBoundary, onHorizontalBoundary)) return false;
                }
            }
            return true;
        }

        static bool BoundaryCompanionsWalkable(
            WalkGrid grid, int cellX, int cellY, bool verticalBoundary, bool horizontalBoundary)
        {
            if (verticalBoundary && !grid.IsWalkable(cellX - 1, cellY)) return false;
            if (horizontalBoundary && !grid.IsWalkable(cellX, cellY - 1)) return false;
            if (verticalBoundary && horizontalBoundary &&
                !grid.IsWalkable(cellX - 1, cellY - 1)) return false;
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
