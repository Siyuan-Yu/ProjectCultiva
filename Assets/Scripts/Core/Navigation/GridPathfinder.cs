using System;
using System.Collections.Generic;

namespace XianXia.Core.Navigation
{
    /// <summary>4-neighbour A* on <see cref="WalkGrid"/>. Pure Core; no Unity.</summary>
    public static class GridPathfinder
    {
        static readonly int[] Dx = { 1, -1, 0, 0 };
        static readonly int[] Dy = { 0, 0, 1, -1 };

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

            // Binary-heap-ish via sorted list of open indices (small grids OK).
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
                for (var n = 0; n < 4; n++)
                {
                    var nx = cx + Dx[n];
                    var ny = cy + Dy[n];
                    if (!grid.IsWalkable(nx, ny))
                        continue;
                    var ni = Index(nx, ny, w);
                    if (closed[ni])
                        continue;

                    var tentative = gScore[current] + 1;
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

        /// <summary>World-space path (cell centres). Snaps ends to nearest walkable within 8 cells.</summary>
        public static bool TryFindWorldPath(
            WalkGrid grid,
            float startX,
            float startY,
            float goalX,
            float goalY,
            List<float> pathXyOut)
        {
            if (pathXyOut == null)
                throw new ArgumentNullException(nameof(pathXyOut));
            pathXyOut.Clear();

            if (!grid.TryWorldToCell(startX, startY, out var sx, out var sy) ||
                !grid.TryFindNearestWalkable(sx, sy, 8, out sx, out sy))
                return false;
            if (!grid.TryWorldToCell(goalX, goalY, out var gx, out var gy) ||
                !grid.TryFindNearestWalkable(gx, gy, 8, out gx, out gy))
                return false;

            var cells = new List<GridCoord>(32);
            if (!TryFindPath(grid, sx, sy, gx, gy, cells))
                return false;

            for (var i = 0; i < cells.Count; i++)
            {
                grid.CellToWorldCenter(cells[i].X, cells[i].Y, out var wx, out var wy);
                pathXyOut.Add(wx);
                pathXyOut.Add(wy);
            }

            // Exact goal if walkable cell centre already close; append requested goal on last cell.
            if (cells.Count > 0)
            {
                pathXyOut[pathXyOut.Count - 2] = goalX;
                pathXyOut[pathXyOut.Count - 1] = goalY;
                // Keep goal on walkable: snap goal world into last cell centre if goal cell was snapped.
                grid.CellToWorldCenter(gx, gy, out var cx, out var cy);
                if (!grid.TryWorldToCell(goalX, goalY, out var ogx, out var ogy) ||
                    !grid.IsWalkable(ogx, ogy))
                {
                    pathXyOut[pathXyOut.Count - 2] = cx;
                    pathXyOut[pathXyOut.Count - 1] = cy;
                }
            }

            return pathXyOut.Count >= 2;
        }

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

        static int Heuristic(int ax, int ay, int bx, int by) =>
            Math.Abs(ax - bx) + Math.Abs(ay - by);

        static int Index(int x, int y, int w) => y * w + x;
    }
}
