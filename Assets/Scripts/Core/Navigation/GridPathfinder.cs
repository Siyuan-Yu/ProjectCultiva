using System;
using System.Collections.Generic;

namespace XianXia.Core.Navigation
{
    /// <summary>8-neighbour A* on <see cref="WalkGrid"/> with corner-cut guards and LOS string-pull. Pure Core.</summary>
    public static class GridPathfinder
    {
        // N, S, E, W, NE, NW, SE, SW
        static readonly int[] Dx = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly int[] Dy = { 1, -1, 0, 0, 1, 1, -1, -1 };
        const int CardinalCost = 10;
        const int DiagonalCost = 14;

        public static bool TryFindPath(
            WalkGrid grid,
            int startX,
            int startY,
            int goalX,
            int goalY,
            List<GridCoord> pathOut)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (pathOut == null)
                throw new ArgumentNullException(nameof(pathOut));
            pathOut.Clear();

            if (!grid.IsWalkable(startX, startY) || !grid.IsWalkable(goalX, goalY))
                return false;
            if (startX == goalX && startY == goalY)
            {
                pathOut.Add(new GridCoord(startX, startY));
                return true;
            }

            var w = grid.Width;
            var h = grid.Height;
            var len = w * h;
            var gScore = new int[len];
            var fScore = new int[len];
            var cameFrom = new int[len];
            var closed = new bool[len];
            for (var i = 0; i < len; i++)
            {
                gScore[i] = int.MaxValue;
                fScore[i] = int.MaxValue;
                cameFrom[i] = -1;
            }

            var start = Index(startX, startY, w);
            var goal = Index(goalX, goalY, w);
            gScore[start] = 0;
            fScore[start] = Heuristic(startX, startY, goalX, goalY);

            var open = new List<int>(64) { start };

            while (open.Count > 0)
            {
                var bestI = 0;
                var bestF = fScore[open[0]];
                for (var i = 1; i < open.Count; i++)
                {
                    var f = fScore[open[i]];
                    if (f >= bestF)
                        continue;
                    bestF = f;
                    bestI = i;
                }

                var current = open[bestI];
                open.RemoveAt(bestI);
                if (current == goal)
                {
                    Reconstruct(cameFrom, goal, w, pathOut);
                    return true;
                }

                if (closed[current])
                    continue;
                closed[current] = true;

                var cx = current % w;
                var cy = current / w;
                for (var n = 0; n < 8; n++)
                {
                    var nx = cx + Dx[n];
                    var ny = cy + Dy[n];
                    if (!grid.IsWalkable(nx, ny))
                        continue;

                    var diagonal = n >= 4;
                    if (diagonal && !CanStepDiagonal(grid, cx, cy, nx, ny))
                        continue;

                    var ni = Index(nx, ny, w);
                    if (closed[ni])
                        continue;

                    var step = diagonal ? DiagonalCost : CardinalCost;
                    var tentative = gScore[current] + step;
                    if (tentative >= gScore[ni])
                        continue;

                    cameFrom[ni] = current;
                    gScore[ni] = tentative;
                    fScore[ni] = tentative + Heuristic(nx, ny, goalX, goalY);
                    if (!open.Contains(ni))
                        open.Add(ni);
                }
            }

            return false;
        }

        /// <summary>World-space path (cell centres, string-pulled). Snaps start within 8, goal within 4.</summary>
        public static bool TryFindWorldPath(
            WalkGrid grid,
            float startX,
            float startY,
            float goalX,
            float goalY,
            List<float> pathXyOut) =>
            TryFindWorldPath(grid, startX, startY, goalX, goalY, pathXyOut, 8, 4);

        public static bool TryFindWorldPath(
            WalkGrid grid,
            float startX,
            float startY,
            float goalX,
            float goalY,
            List<float> pathXyOut,
            int startSnapRadius,
            int goalSnapRadius)
        {
            if (pathXyOut == null)
                throw new ArgumentNullException(nameof(pathXyOut));
            pathXyOut.Clear();

            if (startSnapRadius < 0)
                startSnapRadius = 0;
            if (goalSnapRadius < 0)
                goalSnapRadius = 0;

            if (!grid.TryWorldToCell(startX, startY, out var sx, out var sy) ||
                !grid.TryFindNearestWalkable(sx, sy, startSnapRadius, out sx, out sy))
                return false;
            if (!grid.TryWorldToCell(goalX, goalY, out var gx, out var gy) ||
                !grid.TryFindNearestWalkable(gx, gy, goalSnapRadius, out gx, out gy))
                return false;

            var cells = new List<GridCoord>(32);
            if (!TryFindPath(grid, sx, sy, gx, gy, cells))
                return false;

            SimplifyCells(grid, cells);

            for (var i = 0; i < cells.Count; i++)
            {
                grid.CellToWorldCenter(cells[i].X, cells[i].Y, out var wx, out var wy);
                pathXyOut.Add(wx);
                pathXyOut.Add(wy);
            }

            // Exact goal only if last segment does not cut through blocked cells.
            if (cells.Count > 0)
            {
                grid.CellToWorldCenter(gx, gy, out var cx, out var cy);
                var useExactGoal = grid.TryWorldToCell(goalX, goalY, out var ogx, out var ogy) &&
                                   grid.IsWalkable(ogx, ogy) &&
                                   IsWorldSegmentWalkable(grid, cx, cy, goalX, goalY);
                pathXyOut[pathXyOut.Count - 2] = useExactGoal ? goalX : cx;
                pathXyOut[pathXyOut.Count - 1] = useExactGoal ? goalY : cy;
            }

            // Also allow string-pull from first cell center toward exact start if useful —
            // movement already starts from unit pos; keep first waypoint as first simplified cell.

            return pathXyOut.Count >= 2 || (pathXyOut.Count == 0 && sx == gx && sy == gy);
        }

        /// <summary>
        /// Drop intermediate cells when a straight segment between kept points stays on walkable cells.
        /// Produces true diagonal legs instead of axis-aligned staircases.
        /// </summary>
        public static void SimplifyCells(WalkGrid grid, List<GridCoord> cells)
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

        /// <summary>Diagonal step allowed only if both adjacent cardinals are walkable (no corner cut).</summary>
        static bool CanStepDiagonal(WalkGrid grid, int cx, int cy, int nx, int ny) =>
            grid.IsWalkable(nx, cy) && grid.IsWalkable(cx, ny);

        static void Reconstruct(int[] cameFrom, int goal, int w, List<GridCoord> pathOut)
        {
            var stack = new List<int>(32);
            for (var cur = goal; cur >= 0; cur = cameFrom[cur])
            {
                stack.Add(cur);
                if (cameFrom[cur] < 0)
                    break;
            }

            for (var i = stack.Count - 1; i >= 0; i--)
            {
                var idx = stack[i];
                pathOut.Add(new GridCoord(idx % w, idx / w));
            }
        }

        /// <summary>Octile distance scaled to cardinal=10 / diagonal=14.</summary>
        static int Heuristic(int ax, int ay, int bx, int by)
        {
            var dx = Math.Abs(ax - bx);
            var dy = Math.Abs(ay - by);
            return CardinalCost * (dx + dy) + (DiagonalCost - 2 * CardinalCost) * Math.Min(dx, dy);
        }

        static int Index(int x, int y, int w) => y * w + x;
    }
}
