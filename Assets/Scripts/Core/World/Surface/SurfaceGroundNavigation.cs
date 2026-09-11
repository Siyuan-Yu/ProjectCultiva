using System;
using System.Collections.Generic;
using XianXia.Core.Navigation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Hex;

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

    /// <summary>
    /// Checked-in Surface ground authority. The complete regional grid is independent of loaded
    /// chunk GameObjects. Water/solid block; bridge is walkable only because the bake validated
    /// that it overlays its declared river. Roads do not clear blockers.
    /// </summary>
    public sealed class SurfaceGroundNavigation
    {
        readonly SurfaceGroundCellKind[] _cells;
        readonly WalkGrid _walkGrid;
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
            IReadOnlyList<SurfaceGroundCellKind> cells)
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
            _cells = new SurfaceGroundCellKind[cells.Count];
            _walkGrid = new WalkGrid(originX, originY, cellSize, width, height);
            for (var i = 0; i < cells.Count; i++)
            {
                _cells[i] = cells[i];
                var kind = cells[i];
                var blocked = (kind & SurfaceGroundCellKind.Solid) != 0 ||
                              ((kind & SurfaceGroundCellKind.Water) != 0 &&
                               (kind & SurfaceGroundCellKind.Bridge) == 0);
                if (blocked) _walkGrid.SetBlocked(i % width, i / width, true);
            }
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
            List<WorldVec2> routeOut)
        {
            if (routeOut == null) throw new ArgumentNullException(nameof(routeOut));
            routeOut.Clear();
            if (!Contains(start.X, start.Y) || !Contains(goal.X, goal.Y))
                return SurfaceGroundRouteStatus.OutsideCoverage;
            if (!IsWalkable(goal.X, goal.Y))
                return SurfaceGroundRouteStatus.BlockedGoal;
            _pathScratch.Clear();
            if (!_workspace.TryFindWorldPath(
                    _walkGrid, start.X, start.Y, goal.X, goal.Y,
                    _pathScratch, startSnapRadius: 8, goalSnapRadius: 0))
                return SurfaceGroundRouteStatus.NoRouteWithinCoverage;
            routeOut.Add(start);
            for (var i = 0; i + 1 < _pathScratch.Count; i += 2)
                routeOut.Add(new WorldVec2(_pathScratch[i], _pathScratch[i + 1]));
            if (routeOut.Count == 0 || !routeOut[routeOut.Count - 1].Equals(goal))
                routeOut.Add(goal);
            return SurfaceGroundRouteStatus.Found;
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

        public bool TryOverrideHexCompatibility(WorldVec2 from, WorldVec2 to, out bool walkable)
        {
            walkable = false;
            if (TryResolveShared(from, to, out var navigation))
            {
                walkable = navigation.IsSegmentWalkable(from.X, from.Y, to.X, to.Y);
                return true;
            }
            return false;
        }
    }
}
