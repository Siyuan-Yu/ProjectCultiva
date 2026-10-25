using System;
using System.Collections.Generic;
using System.Threading;
using XianXia.Core.Navigation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World.Surface
{
    [Flags]
    public enum SurfaceGroundCellKind : byte
    {
        Ground = 0,
        Water = 1,
        Road = 2,
        Bridge = 4,
        Solid = 8
    }

    public enum SurfaceGroundRouteStatus
    {
        Found = 0,
        OutsideCoverage = 1,
        NotReady = 2,
        NoRouteWithinCoverage = 3,
        BlockedGoal = 4
    }

    /// <summary>Exact horizontal road run derived from the immutable ground cells, for map display.</summary>
    public readonly struct SurfaceGroundRoadSpan
    {
        public SurfaceGroundRoadSpan(int x, int y, int length)
        { X = x; Y = y; Length = length; }
        public int X { get; }
        public int Y { get; }
        public int Length { get; }
    }

    /// <summary>
    /// Checked-in Surface ground authority. The complete regional grid is independent of loaded
    /// chunk GameObjects. Water/solid block; bridge is walkable only because the bake validated
    /// that it overlays its declared river. Roads do not clear blockers.
    /// </summary>
    public sealed class SurfaceGroundNavigation
    {
        readonly SurfaceGroundCellKind[] _cells;
        readonly WalkGrid _walkGrid;
        readonly List<SurfaceGroundRoadSpan> _roadSpans = new List<SurfaceGroundRoadSpan>();
        readonly List<BridgePassage> _bridgePassages = new List<BridgePassage>();
        readonly GridPathfinderWorkspace _workspace = new GridPathfinderWorkspace();
        readonly List<float> _pathScratch = new List<float>(512);

        public SurfaceGroundNavigation(
            string surfaceId,
            string sourceRevision,
            string sourceHash,
            float originX,
            float originY,
            float cellSize,
            int width,
            int height,
            IReadOnlyList<SurfaceGroundCellKind> cells,
            bool takeOwnership = false)
        {
            if (string.IsNullOrWhiteSpace(surfaceId)) throw new ArgumentException("surfaceId required.", nameof(surfaceId));
            if (cellSize <= 0f || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (cells == null || cells.Count != width * height) throw new ArgumentException("Surface ground cell count mismatch.", nameof(cells));
            SurfaceId = surfaceId;
            SourceRevision = sourceRevision ?? string.Empty;
            SourceHash = sourceHash ?? string.Empty;
            OriginX = originX;
            OriginY = originY;
            CellSize = cellSize;
            Width = width;
            Height = height;
            // The content loader may hand over its freshly decoded array to avoid a second
            // full-world allocation. Other callers retain the original copy semantics.
            _cells = takeOwnership && cells is SurfaceGroundCellKind[] owned
                ? owned : new SurfaceGroundCellKind[cells.Count];
            var copyCells = !ReferenceEquals(_cells, cells);
            _walkGrid = new WalkGrid(originX, originY, cellSize, width, height);
            var index = 0;
            for (var y = 0; y < height; y++)
            {
                var roadStart = -1;
                var bridgeStart = -1;
                for (var x = 0; x < width; x++, index++)
                {
                    var kind = cells[index];
                    if (copyCells) _cells[index] = kind;
                    var blocked = (kind & SurfaceGroundCellKind.Solid) != 0 ||
                                  ((kind & SurfaceGroundCellKind.Water) != 0 &&
                                   (kind & SurfaceGroundCellKind.Bridge) == 0);
                    if (blocked) _walkGrid.SetBlocked(x, y, true);
                    if (!blocked && (kind & SurfaceGroundCellKind.Bridge) != 0)
                    {
                        if (bridgeStart < 0) bridgeStart = x;
                    }
                    else if (bridgeStart >= 0)
                    {
                        AddBridgePassage(bridgeStart, x - 1, y);
                        bridgeStart = -1;
                    }
                    var visibleRoad = (kind & SurfaceGroundCellKind.Road) != 0 &&
                                      (kind & (SurfaceGroundCellKind.Water |
                                               SurfaceGroundCellKind.Bridge |
                                               SurfaceGroundCellKind.Solid)) == 0;
                    if (visibleRoad)
                    {
                        if (roadStart < 0) roadStart = x;
                    }
                    else if (roadStart >= 0)
                    {
                        _roadSpans.Add(new SurfaceGroundRoadSpan(roadStart, y, x - roadStart));
                        roadStart = -1;
                    }
                }
                if (roadStart >= 0)
                    _roadSpans.Add(new SurfaceGroundRoadSpan(roadStart, y, width - roadStart));
                if (bridgeStart >= 0)
                    AddBridgePassage(bridgeStart, width - 1, y);
            }
        }

        readonly struct BridgePassage
        {
            public readonly WorldVec2 Left;
            public readonly WorldVec2 Right;
            public BridgePassage(WorldVec2 left, WorldVec2 right)
            { Left = left; Right = right; }
        }

        void AddBridgePassage(int leftX, int rightX, int y)
        {
            _walkGrid.CellToWorldCenter(leftX, y, out var lx, out var ly);
            _walkGrid.CellToWorldCenter(rightX, y, out var rx, out var ry);
            _bridgePassages.Add(new BridgePassage(
                new WorldVec2(lx, ly), new WorldVec2(rx, ry)));
        }

        public string SurfaceId { get; }
        public string SourceRevision { get; }
        public string SourceHash { get; }
        public float OriginX { get; }
        public float OriginY { get; }
        public float CellSize { get; }
        public int Width { get; }
        public int Height { get; }
        public float MaxX => OriginX + Width * CellSize;
        public float MaxY => OriginY + Height * CellSize;
        public WalkGrid WalkGrid => _walkGrid;
        /// <summary>Display-only projection; routing and walkability continue reading the original cells.</summary>
        public IReadOnlyList<SurfaceGroundRoadSpan> RoadSpans => _roadSpans;

        public bool Contains(float x, float y) =>
            x >= OriginX && y >= OriginY && x < MaxX && y < MaxY;

        public bool TryGetCell(float x, float y, out SurfaceGroundCellKind kind)
        {
            kind = SurfaceGroundCellKind.Ground;
            if (!_walkGrid.TryWorldToCell(x, y, out var cx, out var cy)) return false;
            kind = _cells[cx + cy * Width];
            return true;
        }

        public bool IsWalkable(float x, float y) =>
            _walkGrid.TryWorldToCell(x, y, out var cx, out var cy) && _walkGrid.IsWalkable(cx, cy);

        public bool IsSegmentWalkable(float x0, float y0, float x1, float y1) =>
            Contains(x0, y0) && Contains(x1, y1) &&
            GridPathfinder.IsWorldSegmentWalkable(_walkGrid, x0, y0, x1, y1);

        public SurfaceGroundRouteStatus TryFindRoute(
            WorldVec2 start,
            WorldVec2 goal,
            List<WorldVec2> routeOut) =>
            TryFindRoute(start, goal, routeOut, _workspace, _pathScratch, default);

        /// <summary>
        /// Independent read-only planning workspace for a background WorldMap request. The
        /// checked-in ground grid is immutable after construction; only these scratch buffers
        /// belong to the caller, so NPC/main-thread route requests never share mutable state.
        /// </summary>
        public SurfaceGroundRouteStatus TryFindRouteIsolated(
            WorldVec2 start,
            WorldVec2 goal,
            List<WorldVec2> routeOut,
            CancellationToken cancellationToken = default) =>
            TryFindRoute(start, goal, routeOut,
                new GridPathfinderWorkspace(), new List<float>(512), cancellationToken);

        SurfaceGroundRouteStatus TryFindRoute(
            WorldVec2 start,
            WorldVec2 goal,
            List<WorldVec2> routeOut,
            GridPathfinderWorkspace workspace,
            List<float> pathScratch,
            CancellationToken cancellationToken)
        {
            if (routeOut == null) throw new ArgumentNullException(nameof(routeOut));
            routeOut.Clear();
            if (!Contains(start.X, start.Y) || !Contains(goal.X, goal.Y))
                return SurfaceGroundRouteStatus.OutsideCoverage;
            if (!IsWalkable(goal.X, goal.Y))
                return SurfaceGroundRouteStatus.BlockedGoal;
            // Most travel over the expanded plain has direct line of sight. Avoid allocating
            // a full-surface A* workspace for an already clear segment.
            if (IsSegmentWalkable(start.X, start.Y, goal.X, goal.Y))
            {
                routeOut.Add(start);
                if (!start.Equals(goal)) routeOut.Add(goal);
                return SurfaceGroundRouteStatus.Found;
            }
            // A river crossing may lie far outside the start-goal corridor. Try the authored
            // walkable bridge cells as exact, visibility-checked detours before continent A*.
            if (TryBridgeShortcut(start, goal, routeOut, cancellationToken))
                return SurfaceGroundRouteStatus.Found;
            pathScratch.Clear();
            if (!workspace.TryFindWorldPath(
                    _walkGrid, start.X, start.Y, goal.X, goal.Y,
                    pathScratch, startSnapRadius: 8, goalSnapRadius: 0,
                    cancellationToken: cancellationToken))
                return SurfaceGroundRouteStatus.NoRouteWithinCoverage;
            routeOut.Add(start);
            for (var i = 0; i + 1 < pathScratch.Count; i += 2)
                routeOut.Add(new WorldVec2(pathScratch[i], pathScratch[i + 1]));
            if (routeOut.Count == 0 || !routeOut[routeOut.Count - 1].Equals(goal))
                routeOut.Add(goal);
            return SurfaceGroundRouteStatus.Found;
        }

        bool TryBridgeShortcut(WorldVec2 start, WorldVec2 goal,
            List<WorldVec2> routeOut, CancellationToken cancellationToken)
        {
            var best = -1;
            var bestReverse = false;
            var bestLength = float.MaxValue;
            for (var i = 0; i < _bridgePassages.Count; i++)
            {
                if ((i & 31) == 0 && cancellationToken.IsCancellationRequested) return false;
                var passage = _bridgePassages[i];
                for (var direction = 0; direction < 2; direction++)
                {
                    var entry = direction == 0 ? passage.Left : passage.Right;
                    var exit = direction == 0 ? passage.Right : passage.Left;
                    var ax = entry.X - start.X; var ay = entry.Y - start.Y;
                    var bx = exit.X - entry.X; var by = exit.Y - entry.Y;
                    var cx = goal.X - exit.X; var cy = goal.Y - exit.Y;
                    var length = (float)(Math.Sqrt(ax * ax + ay * ay) +
                        Math.Sqrt(bx * bx + by * by) + Math.Sqrt(cx * cx + cy * cy));
                    if (length >= bestLength ||
                        !IsSegmentWalkable(start.X, start.Y, entry.X, entry.Y) ||
                        !IsSegmentWalkable(entry.X, entry.Y, exit.X, exit.Y) ||
                        !IsSegmentWalkable(exit.X, exit.Y, goal.X, goal.Y)) continue;
                    best = i;
                    bestReverse = direction != 0;
                    bestLength = length;
                }
            }
            if (best < 0) return false;
            var selected = _bridgePassages[best];
            var first = bestReverse ? selected.Right : selected.Left;
            var second = bestReverse ? selected.Left : selected.Right;
            routeOut.Add(start);
            if (!start.Equals(first)) routeOut.Add(first);
            if (!second.Equals(routeOut[routeOut.Count - 1])) routeOut.Add(second);
            if (!goal.Equals(routeOut[routeOut.Count - 1])) routeOut.Add(goal);
            return true;
        }
    }

    /// <summary>
    /// Session binding for checked-in Surface geography. Registered navigation is world/session
    /// data and survives presentation unload; Active remains the currently presented Surface for
    /// PlayerParty compatibility.
    /// </summary>
    public sealed class SurfaceGroundAuthority
    {
        readonly Dictionary<string, SurfaceGroundNavigation> _registered =
            new Dictionary<string, SurfaceGroundNavigation>(StringComparer.Ordinal);
        readonly Dictionary<string, KeyValuePair<string, WorldVec2>> _siteArrivals =
            new Dictionary<string, KeyValuePair<string, WorldVec2>>(StringComparer.Ordinal);

        public SurfaceGroundNavigation Active { get; private set; }
        public bool IsReady => Active != null || _registered.Count > 0;
        public IReadOnlyDictionary<string, SurfaceGroundNavigation> Registered => _registered;

        public void Register(SurfaceGroundNavigation navigation)
        {
            if (navigation == null || string.IsNullOrWhiteSpace(navigation.SurfaceId)) return;
            _registered[navigation.SurfaceId] = navigation;
        }

        public void Activate(SurfaceGroundNavigation navigation)
        {
            Register(navigation);
            Active = navigation;
        }

        /// <summary>Clears only the presentation-facing active pointer.</summary>
        public void Clear() => Active = null;

        public void ClearRegistered()
        {
            Active = null;
            _registered.Clear();
            _siteArrivals.Clear();
        }

        public void RegisterSiteArrival(string surfaceId, string siteId, WorldVec2 position)
        {
            if (string.IsNullOrWhiteSpace(surfaceId) || string.IsNullOrWhiteSpace(siteId)) return;
            _siteArrivals[siteId] = new KeyValuePair<string, WorldVec2>(surfaceId, position);
        }

        public bool TryResolveSiteArrival(
            string siteId,
            out string surfaceId,
            out WorldVec2 position)
        {
            if (_siteArrivals.TryGetValue(siteId ?? string.Empty, out var entry))
            {
                surfaceId = entry.Key;
                position = entry.Value;
                return true;
            }
            surfaceId = string.Empty;
            position = default;
            return false;
        }

        public bool TryGet(string surfaceId, out SurfaceGroundNavigation navigation) =>
            _registered.TryGetValue(surfaceId ?? string.Empty, out navigation);

        public bool TryResolveContaining(WorldVec2 position, out SurfaceGroundNavigation navigation)
        {
            foreach (var pair in _registered)
            {
                var candidate = pair.Value;
                if (candidate != null && candidate.Contains(position.X, position.Y))
                {
                    navigation = candidate;
                    return true;
                }
            }

            navigation = null;
            return false;
        }

        public bool TryResolveShared(
            WorldVec2 start,
            WorldVec2 goal,
            out SurfaceGroundNavigation navigation)
        {
            foreach (var pair in _registered)
            {
                var candidate = pair.Value;
                if (candidate != null && candidate.Contains(start.X, start.Y) &&
                    candidate.Contains(goal.X, goal.Y))
                {
                    navigation = candidate;
                    return true;
                }
            }

            navigation = null;
            return false;
        }

    }
}
