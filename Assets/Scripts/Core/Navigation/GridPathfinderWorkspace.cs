using System;
using System.Collections.Generic;

namespace XianXia.Core.Navigation
{
    /// <summary>
    /// Reusable A* workspace for <see cref="GridPathfinder"/>.
    ///
    /// Continuous surfaces path on a 3x3 chunk neighborhood (≈150x150 = 22,500 cells). The previous
    /// implementation allocated g/f/cameFrom/closed plus an open list **per request**, i.e. hundreds
    /// of KB of garbage for every NPC repath. Buffers are now kept across calls and only the cells
    /// actually touched by the previous run are reset (no O(cells) re-init), so repeated finds
    /// allocate nothing.
    ///
    /// Selection order is deliberately identical to the previous linear-scan open list
    /// (minimum f; ties resolved by earliest insertion, which is preserved through decrease-key),
    /// so path results do not change.
    ///
    /// Not thread-safe: Unity main thread only (same as before).
    /// </summary>
    public sealed class GridPathfinderWorkspace
    {
        const int CardinalCost = 10;
        const int DiagonalCost = 14;
        // N, S, E, W, NE, NW, SE, SW
        static readonly int[] Dx = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly int[] Dy = { 1, -1, 0, 0, 1, 1, -1, -1 };

        int[] _g = Array.Empty<int>();
        int[] _f = Array.Empty<int>();
        int[] _cameFrom = Array.Empty<int>();
        int[] _seq = Array.Empty<int>();
        int[] _heapPos = Array.Empty<int>();
        bool[] _closed = Array.Empty<bool>();
        int[] _heap = Array.Empty<int>();
        readonly List<int> _touched = new List<int>(256);
        readonly List<GridCoord> _cells = new List<GridCoord>(64);
        readonly List<int> _stack = new List<int>(64);
        int _capacity;
        int _heapCount;
        int _nextSeq;

        /// <summary>8-neighbour A* with corner-cut guards. Same semantics as before, no allocation.</summary>
        public bool TryFindPath(
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
            EnsureCapacity(w * grid.Height);
            ResetTouched();

            var start = Index(startX, startY, w);
            var goal = Index(goalX, goalY, w);
            MarkTouched(start);
            _g[start] = 0;
            _f[start] = Heuristic(startX, startY, goalX, goalY);
            _heapCount = 0;
            _nextSeq = 0;
            Push(start);

            var found = false;
            while (_heapCount > 0)
            {
                var current = Pop();
                if (current == goal)
                {
                    found = true;
                    break;
                }

                if (_closed[current])
                    continue;
                _closed[current] = true;

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
                    if (_closed[ni])
                        continue;

                    var step = diagonal ? DiagonalCost : CardinalCost;
                    var tentative = _g[current] + step;
                    if (tentative >= _g[ni])
                        continue;

                    MarkTouched(ni);
                    _cameFrom[ni] = current;
                    _g[ni] = tentative;
                    _f[ni] = tentative + Heuristic(nx, ny, goalX, goalY);
                    if (_heapPos[ni] >= 0)
                        SiftUp(_heapPos[ni]);
                    else
                        Push(ni);
                }
            }

            if (found)
                Reconstruct(goal, w, pathOut);
            return found;
        }

        /// <summary>World-space path (cell centres, string-pulled). Snaps start within 8, goal within 4.</summary>
        public bool TryFindWorldPath(
            WalkGrid grid,
            float startX,
            float startY,
            float goalX,
            float goalY,
            List<float> pathXyOut) =>
            TryFindWorldPath(grid, startX, startY, goalX, goalY, pathXyOut, 8, 4);

        public bool TryFindWorldPath(
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

            if (!TryFindPath(grid, sx, sy, gx, gy, _cells))
                return false;

            GridPathfinder.SimplifyCells(grid, _cells);

            for (var i = 0; i < _cells.Count; i++)
            {
                grid.CellToWorldCenter(_cells[i].X, _cells[i].Y, out var wx, out var wy);
                pathXyOut.Add(wx);
                pathXyOut.Add(wy);
            }

            // Exact goal only if last segment does not cut through blocked cells.
            if (_cells.Count > 0)
            {
                grid.CellToWorldCenter(gx, gy, out var cx, out var cy);
                var useExactGoal = grid.TryWorldToCell(goalX, goalY, out var ogx, out var ogy) &&
                                   grid.IsWalkable(ogx, ogy) &&
                                   GridPathfinder.IsWorldSegmentWalkable(grid, cx, cy, goalX, goalY);
                pathXyOut[pathXyOut.Count - 2] = useExactGoal ? goalX : cx;
                pathXyOut[pathXyOut.Count - 1] = useExactGoal ? goalY : cy;
            }

            return pathXyOut.Count >= 2 || (pathXyOut.Count == 0 && sx == gx && sy == gy);
        }

        void EnsureCapacity(int len)
        {
            var needed = len < 16 ? 16 : len;
            if (needed <= _capacity)
                return;
            _capacity = needed;
            _g = new int[needed];
            _f = new int[needed];
            _cameFrom = new int[needed];
            _seq = new int[needed];
            _heapPos = new int[needed];
            _closed = new bool[needed];
            _heap = new int[needed];
            _touched.Clear();
            for (var i = 0; i < needed; i++)
            {
                _g[i] = int.MaxValue;
                _f[i] = int.MaxValue;
                _cameFrom[i] = -1;
                _heapPos[i] = -1;
            }
        }

        /// <summary>Reset only the cells the previous run wrote (O(touched), not O(cells)).</summary>
        void ResetTouched()
        {
            for (var i = 0; i < _touched.Count; i++)
            {
                var index = _touched[i];
                _g[index] = int.MaxValue;
                _f[index] = int.MaxValue;
                _cameFrom[index] = -1;
                _closed[index] = false;
                _heapPos[index] = -1;
            }
            _touched.Clear();
        }

        void MarkTouched(int index)
        {
            // 首次被本 run 触碰（复位后 g==MaxValue 即未触碰）→ 登记，下次 run 开头精确复位。
            if (_g[index] != int.MaxValue)
                return;
            _touched.Add(index);
        }

        void Push(int index)
        {
            _heap[_heapCount] = index;
            _heapPos[index] = _heapCount;
            _seq[index] = _nextSeq++;
            SiftUp(_heapCount);
            _heapCount++;
        }

        int Pop()
        {
            var root = _heap[0];
            _heapPos[root] = -1;
            _heapCount--;
            if (_heapCount > 0)
            {
                var last = _heap[_heapCount];
                var pos = 0;
                while (true)
                {
                    var left = pos * 2 + 1;
                    if (left >= _heapCount)
                        break;
                    var right = left + 1;
                    var child = right < _heapCount && Compare(_heap[right], _heap[left]) < 0 ? right : left;
                    if (Compare(_heap[child], last) >= 0)
                        break;
                    _heap[pos] = _heap[child];
                    _heapPos[_heap[pos]] = pos;
                    pos = child;
                }

                _heap[pos] = last;
                _heapPos[last] = pos;
            }

            return root;
        }

        void SiftUp(int pos)
        {
            var item = _heap[pos];
            while (pos > 0)
            {
                var parent = (pos - 1) / 2;
                var parentItem = _heap[parent];
                if (Compare(parentItem, item) <= 0)
                    break;
                _heap[pos] = parentItem;
                _heapPos[parentItem] = pos;
                pos = parent;
            }

            _heap[pos] = item;
            _heapPos[item] = pos;
        }

        /// <summary>Minimum f, ties by earliest insertion — identical to the legacy linear scan.</summary>
        int Compare(int a, int b)
        {
            var fa = _f[a];
            var fb = _f[b];
            if (fa != fb)
                return fa < fb ? -1 : 1;
            var sa = _seq[a];
            var sb = _seq[b];
            return sa == sb ? 0 : (sa < sb ? -1 : 1);
        }

        void Reconstruct(int goal, int w, List<GridCoord> pathOut)
        {
            _stack.Clear();
            for (var cur = goal; cur >= 0; cur = _cameFrom[cur])
            {
                _stack.Add(cur);
                if (_cameFrom[cur] < 0)
                    break;
            }

            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                var idx = _stack[i];
                pathOut.Add(new GridCoord(idx % w, idx / w));
            }
        }

        /// <summary>Diagonal step allowed only if both adjacent cardinals are walkable (no corner cut).</summary>
        static bool CanStepDiagonal(WalkGrid grid, int cx, int cy, int nx, int ny) =>
            grid.IsWalkable(nx, cy) && grid.IsWalkable(cx, ny);

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
