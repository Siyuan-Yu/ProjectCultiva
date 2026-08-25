using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Hex
{
    public static class HexPathfinder
    {
        sealed class Node : IComparable<Node>
        {
            public HexCoord Coord;
            public float G;
            public float F;
            public HexCoord CameFrom;
            public bool HasCameFrom;

            public int CompareTo(Node other)
            {
                var cmp = F.CompareTo(other.F);
                if (cmp != 0)
                    return cmp;
                return G.CompareTo(other.G);
            }
        }

        static readonly List<HexCoord> NeighborScratch = new List<HexCoord>(6);

        public static bool TryFindPath(
            HexWorld grid,
            HexCoord start,
            HexCoord goal,
            List<HexCoord> pathOut) =>
            TryFindPath(grid, start, goal, pathOut, HexTravelMode.Ground);

        /// <summary>
        /// Hex A*。TravelMode 预留；V1 Ground 与旧行为一致（不绑定 FormalArmy）。
        /// </summary>
        public static bool TryFindPath(
            HexWorld grid,
            HexCoord start,
            HexCoord goal,
            List<HexCoord> pathOut,
            HexTravelMode travelMode)
        {
            pathOut?.Clear();
            if (grid == null || pathOut == null)
                return false;
            if (travelMode != HexTravelMode.Ground)
                return false;
            if (!grid.TryGetTile(start, out _) || !grid.TryGetTile(goal, out _))
                return false;
            if (start == goal)
            {
                pathOut.Add(start);
                return true;
            }

            var open = new List<Node>(64);
            var best = new Dictionary<HexCoord, Node>();
            var closed = new HashSet<HexCoord>();

            var startNode = new Node
            {
                Coord = start,
                G = 0f,
                F = HexMath.Distance(start, goal),
            };
            open.Add(startNode);
            best[start] = startNode;

            while (open.Count > 0)
            {
                open.Sort();
                var current = open[0];
                open.RemoveAt(0);

                if (closed.Contains(current.Coord))
                    continue;

                if (current.Coord == goal)
                {
                    ReconstructPath(current, best, pathOut);
                    return pathOut.Count > 0;
                }

                closed.Add(current.Coord);
                HexMath.CollectNeighbors(current.Coord, NeighborScratch);
                for (var i = 0; i < NeighborScratch.Count; i++)
                {
                    var neighborCoord = NeighborScratch[i];
                    if (!grid.TryGetTile(neighborCoord, out var neighborTile) || neighborTile == null)
                        continue;
                    if (!neighborTile.IsPassable)
                        continue;

                    var stepCost = neighborTile.ResolveMovementCost();
                    if (!float.IsFinite(stepCost))
                        continue;

                    var tentativeG = current.G + stepCost;
                    if (best.TryGetValue(neighborCoord, out var existing) && tentativeG >= existing.G)
                        continue;

                    var next = new Node
                    {
                        Coord = neighborCoord,
                        G = tentativeG,
                        F = tentativeG + HexMath.Distance(neighborCoord, goal),
                        CameFrom = current.Coord,
                        HasCameFrom = true,
                    };
                    best[neighborCoord] = next;
                    open.Add(next);
                }
            }

            return false;
        }

        static void ReconstructPath(Node goalNode, Dictionary<HexCoord, Node> best, List<HexCoord> pathOut)
        {
            pathOut.Clear();
            var current = goalNode.Coord;
            while (true)
            {
                pathOut.Add(current);
                if (!best.TryGetValue(current, out var node) || !node.HasCameFrom)
                    break;
                current = node.CameFrom;
            }

            pathOut.Reverse();
        }
    }
}
