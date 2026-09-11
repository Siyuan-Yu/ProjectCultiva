using System;
using System.Collections.Generic;

namespace XianXia.Core.Navigation
{
    public enum ContinuousWalkGridSubgoalKind
    {
        ExactLoadedGoal = 0,
        ReachableApproach = 1,
        ReachableFrontier = 2,
    }

    /// <summary>
    /// Resolves one cached Continuous Outdoor subgoal from the currently composed navigation
    /// window. Reachability uses the same eight-neighbour/corner-cut rule as grid A*.
    /// </summary>
    public static class ContinuousWalkGridSubgoalResolver
    {
        static readonly int[] Dx = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly int[] Dy = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly Queue<int> Queue = new Queue<int>(512);
        static bool[] _visited = Array.Empty<bool>();
        static readonly List<int> Touched = new List<int>(512);

        public static bool TryResolve(
            WalkGrid grid,
            float startX,
            float startY,
            float desiredX,
            float desiredY,
            out float subgoalX,
            out float subgoalY,
            out ContinuousWalkGridSubgoalKind kind,
            out string failureReason)
        {
            subgoalX = 0f;
            subgoalY = 0f;
            kind = ContinuousWalkGridSubgoalKind.ReachableFrontier;
            failureReason = string.Empty;
            if (grid == null)
            {
                failureReason = "NavigationNotReady";
                return false;
            }
            if (!grid.TryWorldToCell(startX, startY, out var sx, out var sy) ||
                !grid.TryFindNearestWalkable(sx, sy, 8, out sx, out sy))
            {
                failureReason = "SourceOutsideNavigation";
                return false;
            }

            EnsureCapacity(grid.Width * grid.Height);
            ResetVisited();
            Queue.Clear();
            var start = sy * grid.Width + sx;
            Visit(start);
            Queue.Enqueue(start);

            var desiredLoaded = grid.TryWorldToCell(desiredX, desiredY, out var gx, out var gy);
            var desiredWalkable = desiredLoaded && grid.IsWalkable(gx, gy);
            var best = start;
            var bestDistance = DistanceToDesired(grid, sx, sy, desiredX, desiredY);

            while (Queue.Count > 0)
            {
                var current = Queue.Dequeue();
                var cx = current % grid.Width;
                var cy = current / grid.Width;
                var distance = DistanceToDesired(grid, cx, cy, desiredX, desiredY);
                if (distance < bestDistance - 0.0001f ||
                    (Math.Abs(distance - bestDistance) <= 0.0001f && current < best))
                {
                    best = current;
                    bestDistance = distance;
                }

                for (var i = 0; i < Dx.Length; i++)
                {
                    var nx = cx + Dx[i];
                    var ny = cy + Dy[i];
                    if (!grid.IsWalkable(nx, ny))
                        continue;
                    if (i >= 4 &&
                        (!grid.IsWalkable(cx + Dx[i], cy) ||
                         !grid.IsWalkable(cx, cy + Dy[i])))
                        continue;
                    var next = ny * grid.Width + nx;
                    if (_visited[next])
                        continue;
                    Visit(next);
                    Queue.Enqueue(next);
                }
            }

            var desiredIndex = desiredLoaded ? gy * grid.Width + gx : -1;
            if (desiredWalkable && desiredIndex >= 0 && _visited[desiredIndex])
            {
                subgoalX = desiredX;
                subgoalY = desiredY;
                kind = ContinuousWalkGridSubgoalKind.ExactLoadedGoal;
                return true;
            }

            if (!desiredLoaded && best == start)
            {
                failureReason = "NeedsRouteData";
                return false;
            }

            var bestX = best % grid.Width;
            var bestY = best / grid.Width;
            grid.CellToWorldCenter(bestX, bestY, out subgoalX, out subgoalY);
            kind = desiredLoaded
                ? ContinuousWalkGridSubgoalKind.ReachableApproach
                : ContinuousWalkGridSubgoalKind.ReachableFrontier;
            return true;
        }

        static float DistanceToDesired(WalkGrid grid, int x, int y, float desiredX, float desiredY)
        {
            grid.CellToWorldCenter(x, y, out var wx, out var wy);
            var dx = wx - desiredX;
            var dy = wy - desiredY;
            return dx * dx + dy * dy;
        }

        static void EnsureCapacity(int count)
        {
            if (_visited.Length >= count)
                return;
            _visited = new bool[count];
            Touched.Clear();
        }

        static void Visit(int index)
        {
            _visited[index] = true;
            Touched.Add(index);
        }

        static void ResetVisited()
        {
            for (var i = 0; i < Touched.Count; i++)
                _visited[Touched[i]] = false;
            Touched.Clear();
        }
    }
}
