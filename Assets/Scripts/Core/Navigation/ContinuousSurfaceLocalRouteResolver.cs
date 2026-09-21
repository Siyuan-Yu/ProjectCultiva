using System;
using System.Collections.Generic;

namespace XianXia.Core.Navigation
{
    /// <summary>
    /// Resolves a bounded window of global Surface guidance points against the loaded Composite
    /// WalkGrid. One flood fill supplies connectivity for every candidate; movement still uses
    /// the normal GridPathfinder after a target is selected.
    /// </summary>
    public static class ContinuousSurfaceLocalRouteResolver
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
            IReadOnlyList<float> candidateXy,
            int firstRouteIndex,
            out int targetRouteIndex,
            out float subgoalX,
            out float subgoalY,
            out ContinuousWalkGridSubgoalKind kind,
            out string failureReason)
        {
            targetRouteIndex = -1;
            subgoalX = 0f;
            subgoalY = 0f;
            kind = ContinuousWalkGridSubgoalKind.ReachableFrontier;
            failureReason = string.Empty;
            if (grid == null)
            {
                failureReason = "NavigationNotReady";
                return false;
            }
            if (candidateXy == null || candidateXy.Count < 2 || (candidateXy.Count & 1) != 0)
            {
                failureReason = "SurfaceRouteWindowEmpty";
                return false;
            }
            if (!grid.TryWorldToCell(startX, startY, out var sx, out var sy) ||
                !grid.TryFindNearestWalkable(sx, sy, 8, out sx, out sy))
            {
                failureReason = "SourceOutsideNavigation";
                return false;
            }

            Flood(grid, sx, sy);

            // Preserve normal behavior when the immediate guidance point is physically reachable.
            if (TryResolveReachableCandidate(grid, candidateXy, 0, out subgoalX, out subgoalY))
            {
                targetRouteIndex = firstRouteIndex;
                kind = ContinuousWalkGridSubgoalKind.ExactLoadedGoal;
                return true;
            }

            var furthestOffset = -1;
            var firstOutsideOffset = -1;
            for (var offset = 0; offset < candidateXy.Count / 2; offset++)
            {
                var x = candidateXy[offset * 2];
                var y = candidateXy[offset * 2 + 1];
                if (!grid.TryWorldToCell(x, y, out _, out _))
                {
                    if (firstOutsideOffset < 0) firstOutsideOffset = offset;
                    continue;
                }
                if (TryResolveReachableCandidate(grid, candidateXy, offset, out _, out _))
                    furthestOffset = offset;
            }

            if (furthestOffset >= 0 &&
                TryResolveReachableCandidate(grid, candidateXy, furthestOffset,
                    out subgoalX, out subgoalY))
            {
                targetRouteIndex = firstRouteIndex + furthestOffset;
                kind = ContinuousWalkGridSubgoalKind.ExactLoadedGoal;
                return true;
            }

            if (firstOutsideOffset >= 0)
            {
                var desiredX = candidateXy[firstOutsideOffset * 2];
                var desiredY = candidateXy[firstOutsideOffset * 2 + 1];
                var startIndex = sy * grid.Width + sx;
                var best = startIndex;
                var bestDistance = DistanceToDesired(grid, sx, sy, desiredX, desiredY);
                for (var i = 0; i < Touched.Count; i++)
                {
                    var cell = Touched[i];
                    var cx = cell % grid.Width;
                    var cy = cell / grid.Width;
                    var distance = DistanceToDesired(grid, cx, cy, desiredX, desiredY);
                    if (distance < bestDistance - 0.0001f ||
                        (Math.Abs(distance - bestDistance) <= 0.0001f && cell < best))
                    {
                        best = cell;
                        bestDistance = distance;
                    }
                }
                if (best == startIndex)
                {
                    failureReason = "SurfaceLocalRouteNoProgress";
                    return false;
                }
                grid.CellToWorldCenter(best % grid.Width, best / grid.Width,
                    out subgoalX, out subgoalY);
                kind = ContinuousWalkGridSubgoalKind.ReachableFrontier;
                return true;
            }

            failureReason = "NoPhysicalRoute";
            return false;
        }

        static bool TryResolveReachableCandidate(
            WalkGrid grid, IReadOnlyList<float> xy, int offset, out float x, out float y)
        {
            x = xy[offset * 2];
            y = xy[offset * 2 + 1];
            return grid.TryWorldToCell(x, y, out var cx, out var cy) &&
                   grid.IsWalkable(cx, cy) && _visited[cy * grid.Width + cx];
        }

        static void Flood(WalkGrid grid, int sx, int sy)
        {
            EnsureCapacity(grid.Width * grid.Height);
            ResetVisited();
            Queue.Clear();
            var start = sy * grid.Width + sx;
            Visit(start);
            Queue.Enqueue(start);
            while (Queue.Count > 0)
            {
                var current = Queue.Dequeue();
                var cx = current % grid.Width;
                var cy = current / grid.Width;
                for (var i = 0; i < Dx.Length; i++)
                {
                    var nx = cx + Dx[i];
                    var ny = cy + Dy[i];
                    if (!grid.IsWalkable(nx, ny)) continue;
                    if (i >= 4 &&
                        (!grid.IsWalkable(cx + Dx[i], cy) ||
                         !grid.IsWalkable(cx, cy + Dy[i])))
                        continue;
                    var next = ny * grid.Width + nx;
                    if (_visited[next]) continue;
                    Visit(next);
                    Queue.Enqueue(next);
                }
            }
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
            if (_visited.Length >= count) return;
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
            for (var i = 0; i < Touched.Count; i++) _visited[Touched[i]] = false;
            Touched.Clear();
        }
    }
}
