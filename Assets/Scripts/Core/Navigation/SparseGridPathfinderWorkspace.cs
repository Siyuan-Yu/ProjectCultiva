using System;
using System.Collections.Generic;
using System.Threading;

namespace XianXia.Core.Navigation
{
    /// <summary>A* for world-sized grids; retains node objects without preallocating the whole map.</summary>
    internal sealed class SparseGridPathfinderWorkspace
    {
        static readonly int[] Dx = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly int[] Dy = { 1, -1, 0, 0, 1, 1, -1, -1 };
        readonly Dictionary<int, Node> _nodes = new Dictionary<int, Node>();
        readonly List<Node> _pool = new List<Node>();
        readonly List<Node> _heap = new List<Node>();
        readonly List<int> _stack = new List<int>();
        int _used;
        int _nextSequence;

        sealed class Node
        {
            public int Index, G, F, CameFrom, Sequence, HeapPosition;
            public bool Closed;
        }

        public bool TryFindPath(WalkGrid grid, int startX, int startY, int goalX, int goalY,
            List<GridCoord> output, CancellationToken cancellationToken = default)
        {
            var span = Math.Max(Math.Abs(goalX - startX), Math.Abs(goalY - startY));
            // A blocked straight ray usually needs a local detour, not a search over the
            // whole continent. Widen only if the route leaves the first corridor.
            if (span > 256)
            {
                if (Search(grid, startX, startY, goalX, goalY, output, 96, cancellationToken))
                    return true;
                if (cancellationToken.IsCancellationRequested) return false;
                if (Search(grid, startX, startY, goalX, goalY, output, 384, cancellationToken))
                    return true;
                if (cancellationToken.IsCancellationRequested) return false;
            }
            return Search(grid, startX, startY, goalX, goalY, output, 0, cancellationToken);
        }

        bool Search(WalkGrid grid, int startX, int startY, int goalX, int goalY,
            List<GridCoord> output, int corridorRadius, CancellationToken cancellationToken)
        {
            output.Clear();
            _nodes.Clear();
            _heap.Clear();
            _used = 0;
            _nextSequence = 0;
            var width = grid.Width;
            var start = Get(startX + startY * width);
            start.G = 0;
            start.F = Heuristic(startX, startY, goalX, goalY);
            Push(start);
            var goalIndex = goalX + goalY * width;
            var expansions = 0;
            while (_heap.Count > 0)
            {
                if ((++expansions & 255) == 0 && cancellationToken.IsCancellationRequested)
                    return false;
                var current = Pop();
                if (current.Index == goalIndex)
                {
                    _stack.Clear();
                    for (var index = current.Index; index >= 0; index = _nodes[index].CameFrom)
                        _stack.Add(index);
                    for (var i = _stack.Count - 1; i >= 0; i--)
                        output.Add(new GridCoord(_stack[i] % width, _stack[i] / width));
                    return true;
                }
                if (current.Closed) continue;
                current.Closed = true;
                var cx = current.Index % width;
                var cy = current.Index / width;
                for (var n = 0; n < 8; n++)
                {
                    var nx = cx + Dx[n];
                    var ny = cy + Dy[n];
                    if (corridorRadius > 0 && !InsideCorridor(nx, ny,
                            startX, startY, goalX, goalY, corridorRadius))
                        continue;
                    if (!grid.IsWalkable(nx, ny) ||
                        (n >= 4 && (!grid.IsWalkable(nx, cy) || !grid.IsWalkable(cx, ny))))
                        continue;
                    var next = Get(nx + ny * width);
                    if (next.Closed) continue;
                    var tentative = current.G + (n >= 4 ? 14 : 10);
                    if (tentative >= next.G) continue;
                    next.CameFrom = current.Index;
                    next.G = tentative;
                    next.F = tentative + Heuristic(nx, ny, goalX, goalY);
                    if (next.HeapPosition >= 0) SiftUp(next.HeapPosition);
                    else Push(next);
                }
            }
            return false;
        }

        static bool InsideCorridor(int x, int y, int startX, int startY,
            int goalX, int goalY, int radius)
        {
            if (x < Math.Min(startX, goalX) - radius || x > Math.Max(startX, goalX) + radius ||
                y < Math.Min(startY, goalY) - radius || y > Math.Max(startY, goalY) + radius)
                return false;
            var dx = (long)goalX - startX;
            var dy = (long)goalY - startY;
            var cross = Math.Abs(((long)x - startX) * dy - ((long)y - startY) * dx);
            return cross <= (long)radius * Math.Max(Math.Abs(dx), Math.Abs(dy));
        }

        Node Get(int index)
        {
            if (_nodes.TryGetValue(index, out var node)) return node;
            if (_used == _pool.Count) _pool.Add(new Node());
            node = _pool[_used++];
            node.Index = index;
            node.G = int.MaxValue;
            node.F = int.MaxValue;
            node.CameFrom = -1;
            node.Sequence = -1;
            node.HeapPosition = -1;
            node.Closed = false;
            _nodes.Add(index, node);
            return node;
        }

        void Push(Node node)
        {
            node.Sequence = _nextSequence++;
            node.HeapPosition = _heap.Count;
            _heap.Add(node);
            SiftUp(node.HeapPosition);
        }

        Node Pop()
        {
            var root = _heap[0];
            var last = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);
            root.HeapPosition = -1;
            if (_heap.Count == 0) return root;
            var pos = 0;
            while (true)
            {
                var left = pos * 2 + 1;
                if (left >= _heap.Count) break;
                var right = left + 1;
                var child = right < _heap.Count && Compare(_heap[right], _heap[left]) < 0 ? right : left;
                if (Compare(_heap[child], last) >= 0) break;
                _heap[pos] = _heap[child];
                _heap[pos].HeapPosition = pos;
                pos = child;
            }
            _heap[pos] = last;
            last.HeapPosition = pos;
            return root;
        }

        void SiftUp(int pos)
        {
            var node = _heap[pos];
            while (pos > 0)
            {
                var parent = (pos - 1) / 2;
                if (Compare(_heap[parent], node) <= 0) break;
                _heap[pos] = _heap[parent];
                _heap[pos].HeapPosition = pos;
                pos = parent;
            }
            _heap[pos] = node;
            node.HeapPosition = pos;
        }

        static int Compare(Node a, Node b) =>
            a.F != b.F ? a.F.CompareTo(b.F) : a.Sequence.CompareTo(b.Sequence);

        static int Heuristic(int ax, int ay, int bx, int by)
        {
            var dx = Math.Abs(ax - bx);
            var dy = Math.Abs(ay - by);
            // Global routes prioritize reaching the destination over exact shortest distance.
            // With 100M+ cells, an admissible heuristic explores a very broad fan around a
            // blocked long ray; 1.5x octile keeps the search directed while every final step
            // still obeys the same walkability/corner rules.
            return (10 * (dx + dy) - 6 * Math.Min(dx, dy)) * 3 / 2;
        }
    }
}
