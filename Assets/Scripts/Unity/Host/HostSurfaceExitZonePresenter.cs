using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Navigation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Surface Exit Zone Presentation：精确覆盖 Canonical Exit Trigger Geometry。
    /// Geometry 只来自 MapLayout PlayableBounds + ExitTriggerDepth；Availability 只控制显隐。
    /// </summary>
    public sealed class HostSurfaceExitZonePresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] Color zoneColor = new Color(0.2f, 0.85f, 0.55f, 0.28f);
        [SerializeField] float overlayZ = -0.15f;
        [SerializeField] bool drawFilledOverlay = true;
        [SerializeField] bool drawTriggerOutline;

        Transform _root;
        static Mesh _quadMesh;
        static Material _sharedMaterial;
        static Material _outlineMaterial;
        readonly List<SurfaceExitVisibleZone> _zones = new List<SurfaceExitVisibleZone>(6);
        readonly List<SurfaceExitCoverageRect> _rects = new List<SurfaceExitCoverageRect>(32);

        WildernessLocalWorldProjection.WildernessLocalMapBounds _cachedBounds;
        float _cachedDepth;
        string _cachedMapId = string.Empty;

        public int VisibleZoneCount { get; private set; }
        public float CachedExitTriggerDepth => _cachedDepth;
        public string CachedMapLayoutId => _cachedMapId;

        /// <summary>
        /// 当前已通过战略可通行与本地连通性校验的出口集合。
        /// Presenter、手动出口、WASD 与自动旅行都必须消费这里的同一结果。
        /// </summary>
        public IReadOnlyList<SurfaceExitVisibleZone> UsableZones => _zones;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void Rebuild()
        {
            ClearVisualsOnly();
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
                return;

            var world = session.World;
            if (!SurfaceExitZoneCalculator.ShouldPresent(world))
                return;

            if (!TryResolvePlayableBounds(out var bounds, out var authoredDepth, out var mapId))
                return;

            var depth = SurfaceExitZoneCalculator.NormalizeDepth(authoredDepth, bounds);
            // Geometry 真源：同一 LocalMap → 同一 bounds+depth（不读角色/Entry/Hex）。
            _cachedBounds = bounds;
            _cachedDepth = depth;
            _cachedMapId = mapId ?? string.Empty;

            world.LocalMap.ExitTriggerDepth = authoredDepth > 0.0001f
                ? authoredDepth
                : SurfaceExitZoneCalculator.DefaultExitTriggerDepth;

            SurfaceExitZoneCalculator.CollectVisibleZones(world, bounds, depth, _zones);
            var strategicExitCount = _zones.Count;
            var structuralReadyCount = 0;
            var exactDuplicateCount = 0;
            var identityCounts = new Dictionary<string, int>();
            for (var i = 0; i < _zones.Count; i++)
            {
                var c = _zones[i].Connection;
                var key = c.SourceHex + ">" + c.DestinationHex + ":" + c.DirectionIndex;
                if (!identityCounts.TryGetValue(key, out var count))
                    count = 0;
                identityCounts[key] = count + 1;
            }
            foreach (var pair in identityCounts)
            {
                if (pair.Value > 1)
                    exactDuplicateCount += pair.Value - 1;
            }
            var grid = bootstrap.MoveController != null ? bootstrap.MoveController.WalkGrid : null;
            var active = session.PlayerParty != null ? session.PlayerParty.ActiveCharacterId : default;
            EntityView activeView = null;
            var hasActive = !active.IsNone && bootstrap.ViewSpawner != null &&
                            bootstrap.ViewSpawner.Registry.TryGet(active, out activeView) && activeView != null;
            for (var i = _zones.Count - 1; i >= 0; i--)
            {
                var connection = _zones[i].Connection;
                HexCell tile = null;
                if (world.HexWorld != null)
                    world.HexWorld.TryGetTile(connection.DestinationHex, out tile);
                var structural = SurfaceExitTraversalService.TryPrepareTraversal(
                    world, session.PlayerParty, connection, out _);
                var destinationValid = structural.IsSuccess;
                if (destinationValid)
                    structuralReadyCount++;
                var identity = connection.SourceHex + ">" + connection.DestinationHex + ":" + connection.DirectionIndex;
                var isExactDuplicate = identityCounts[identity] > 1;
                var px = 0f;
                var py = 0f;
                var reachable = hasActive && SurfaceExitWalkGridReachability.TryResolveReachablePointInsideExitSlot(
                    grid, activeView.transform.position.x, activeView.transform.position.y, connection, out px, out py);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[SurfaceExitAudit] ContextKind=" + world.PlayerPartyTravel.LocationKind +
                    " SiteId=" + (world.PlayerPartyTravel.SiteId ?? string.Empty) +
                    " CurrentHex=" + world.PlayerPartyTravel.CurrentHex +
                    " SourceHex=" + connection.SourceHex + " DestinationHex=" + connection.DestinationHex +
                    " DirectionIndex=" + connection.DirectionIndex + " DestinationKind=" + connection.DestinationKind +
                    " DestinationTerrain=" + (tile != null ? tile.Terrain.ToString() : "Missing") +
                    " DestinationPassable=" + (tile != null && tile.IsPassable) +
                    " StructuralReady=" + structural.IsSuccess +
                    " StructuralReason=" + (structural.IsFailure ? structural.Error.ToString() : "None") +
                    " SlotRect=" + connection.SlotRect.MinX + "," + connection.SlotRect.MinY + "," + connection.SlotRect.MaxX + "," + connection.SlotRect.MaxY +
                    " LocallyReachable=" + reachable + " ResolvedApproachPoint=" + px + "," + py);
                if (destinationValid && !reachable)
                {
                    Debug.LogWarning(
                        "[SurfaceExitTopology] Classification=STRATEGICALLY_VALID_BUT_LOCALLY_UNREACHABLE" +
                        " SourceHex=" + connection.SourceHex +
                        " DestinationHex=" + connection.DestinationHex,
                        this);
                }
#endif
                if (!destinationValid)
                    Debug.LogWarning("SurfaceExit structural preflight rejected: " + structural.Error, this);
                if (!destinationValid || !reachable || isExactDuplicate)
                    _zones.RemoveAt(i);
            }
            SurfaceExitTopologyAudit.Run(
                this, world, strategicExitCount, structuralReadyCount, _zones.Count, exactDuplicateCount);
            if (_zones.Count == 0)
                return;

            EnsureRoot();
            var fillMat = SharedMaterial();
            fillMat.color = zoneColor;
            for (var i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                _rects.Clear();
                SurfaceExitZoneCalculator.AppendConnectionCoverageRects(z.Connection, _rects);
                for (var r = 0; r < _rects.Count; r++)
                    SpawnRect(_rects[r], z.DirectionIndex, r, fillMat, filled: true);
            }

            VisibleZoneCount = _zones.Count;
        }

        public bool TryGetUsableSurfaceExitAtPoint(
            float localX,
            float localY,
            out SurfaceExitConnection connection,
            out Vector3 approachPoint)
        {
            connection = default;
            approachPoint = default;
            for (var i = 0; i < _zones.Count; i++)
            {
                var candidate = _zones[i].Connection;
                if (!SurfaceExitZoneCalculator.PointBelongsToConnection(
                        localX, localY, candidate, _cachedDepth))
                    continue;
                if (!TryResolveCurrentApproach(candidate, out approachPoint))
                    continue;
                connection = candidate;
                return true;
            }

            return false;
        }

        public bool TryGetUsableSurfaceExit(
            SurfaceExitConnection expected,
            out Vector3 approachPoint)
        {
            approachPoint = default;
            for (var i = 0; i < _zones.Count; i++)
            {
                var candidate = _zones[i].Connection;
                if (!SameIdentity(candidate, expected))
                    continue;
                return TryResolveCurrentApproach(candidate, out approachPoint);
            }

            return false;
        }

        bool TryResolveCurrentApproach(
            SurfaceExitConnection connection,
            out Vector3 approachPoint)
        {
            approachPoint = default;
            var session = bootstrap != null ? bootstrap.Session : null;
            var active = session?.PlayerParty != null
                ? session.PlayerParty.ActiveCharacterId
                : EntityId.None;
            if (active.IsNone || bootstrap?.ViewSpawner == null ||
                !bootstrap.ViewSpawner.Registry.TryGet(active, out var activeView) ||
                activeView == null)
                return false;
            var grid = bootstrap.MoveController != null ? bootstrap.MoveController.WalkGrid : null;
            if (!SurfaceExitWalkGridReachability.TryResolveReachablePointInsideExitSlot(
                    grid,
                    activeView.transform.position.x,
                    activeView.transform.position.y,
                    connection,
                    out var x,
                    out var y))
                return false;
            approachPoint = new Vector3(x, y, HostPresentationSpace.EntityZ);
            return true;
        }

        static bool SameIdentity(SurfaceExitConnection left, SurfaceExitConnection right) =>
            left.SourceHex.Equals(right.SourceHex) &&
            left.DestinationHex.Equals(right.DestinationHex) &&
            left.DirectionIndex == right.DirectionIndex;

        internal void WriteTopologyAudit(
            XianXia.Core.Simulation.SimulationWorld world,
            int currentStrategicExitCount,
            int currentStructuralReadyCount,
            int currentUsableExitCount,
            int currentExactDuplicateCount)
        {
            var sites = world?.Strategic?.Sites;
            if (sites == null || world.HexWorld == null)
                return;

            var currentSiteId = world.PlayerPartyTravel?.SiteId ?? string.Empty;
            var hexSize = world.HexWorld.HexSize > 0.0001f ? world.HexWorld.HexSize : 1f;
            var nominalBounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                0f, 0f, 1f, 16, 16);
            var connections = new List<SurfaceExitConnection>(16);
            var totalInvalid = 0;
            var totalLocallyUnavailable = 0;
            foreach (var pair in sites.Sites)
            {
                var site = pair.Value;
                if (site == null)
                    continue;
                WorldSiteFootprintExitConnectionResolver.CollectConnections(
                    world,
                    site,
                    hexSize,
                    nominalBounds,
                    SurfaceExitZoneCalculator.DefaultExitTriggerDepth,
                    SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                    connections);

                var destinationSiteCounts = new Dictionary<string, int>();
                var destinationSiteHexes = new Dictionary<string, string>();
                var uniqueDestinationHexes = new HashSet<HexCoord>();
                var uniqueDestinationSites = new HashSet<string>();
                var invalidStrategic = 0;
                for (var i = 0; i < connections.Count; i++)
                {
                    var c = connections[i];
                    uniqueDestinationHexes.Add(c.DestinationHex);
                    world.HexWorld.TryGetTile(c.DestinationHex, out var tile);
                    var valid = tile != null && tile.IsPassable &&
                                tile.Terrain != XianXia.Core.World.Hex.HexTerrainType.Water;
                    if (!valid)
                    {
                        invalidStrategic++;
                        totalInvalid++;
                        Debug.LogError(
                            "[SurfaceExitTopology] INVALID_STRATEGIC_EXIT SiteId=" + site.SiteId +
                            " SourceHex=" + c.SourceHex +
                            " DestinationHex=" + c.DestinationHex,
                            this);
                    }

                    var sharedBoundaryEdgeCount = 0;
                    foreach (var footprintHex in site.EnumerateFootprintHexes())
                    {
                        for (var direction = 0; direction < HexMath.DirectionCount; direction++)
                        {
                            if (HexMath.Neighbor(footprintHex, direction).Equals(c.DestinationHex))
                                sharedBoundaryEdgeCount++;
                        }
                    }

                    var destinationSiteId = string.Empty;
                    if (sites.TryGetAtHex(c.DestinationHex, out var destinationSite) &&
                        destinationSite != null)
                    {
                        destinationSiteId = destinationSite.SiteId;
                        uniqueDestinationSites.Add(destinationSiteId);
                        if (!destinationSiteCounts.TryGetValue(destinationSiteId, out var count))
                            count = 0;
                        destinationSiteCounts[destinationSiteId] = count + 1;
                        if (!destinationSiteHexes.TryGetValue(destinationSiteId, out var hexes))
                            hexes = string.Empty;
                        destinationSiteHexes[destinationSiteId] =
                            hexes + (hexes.Length > 0 ? "," : string.Empty) + c.DestinationHex;
                    }

                    Debug.Log(
                        "[SurfaceExitTopology.Exit] SiteId=" + site.SiteId +
                        " SourceHex=" + c.SourceHex +
                        " DestinationHex=" + c.DestinationHex +
                        " DestinationKind=" + c.DestinationKind +
                        " DestinationSiteId=" + destinationSiteId +
                        " Terrain=" + (tile != null ? tile.Terrain.ToString() : "Missing") +
                        " Passable=" + (tile != null && tile.IsPassable) +
                        " SharedBoundaryEdgeCount=" + sharedBoundaryEdgeCount,
                        this);
                }

                var sameSiteMultiExit = string.Empty;
                foreach (var destination in destinationSiteCounts)
                {
                    if (destination.Value <= 1)
                        continue;
                    if (sameSiteMultiExit.Length > 0)
                        sameSiteMultiExit += ",";
                    sameSiteMultiExit += destination.Key + "×" + destination.Value;
                    Debug.Log(
                        "[SurfaceExitTopology.Group] Classification=MULTIPLE_EXITS_TO_SAME_SITE" +
                        " SiteId=" + site.SiteId +
                        " DestinationSite=" + destination.Key +
                        " ConnectionCount=" + destination.Value +
                        " DestinationHexes=[" + destinationSiteHexes[destination.Key] + "]",
                        this);
                }

                var isCurrent = string.Equals(
                    currentSiteId, site.SiteId, System.StringComparison.Ordinal);
                var locallyUnavailable = isCurrent
                    ? System.Math.Max(0, currentStrategicExitCount - currentUsableExitCount)
                    : -1;
                if (locallyUnavailable > 0)
                    totalLocallyUnavailable += locallyUnavailable;
                var footprintHexCount = 0;
                foreach (var _ in site.EnumerateFootprintHexes())
                    footprintHexCount++;
                Debug.Log(
                    "[SurfaceExitTopology] SiteId=" + site.SiteId +
                    " DisplayName=" + site.DisplayName +
                    " FootprintHexCount=" + footprintHexCount +
                    " StrategicExits=" + connections.Count +
                    " UniqueDestinationHexCount=" + uniqueDestinationHexes.Count +
                    " UniqueDestinationSiteCount=" + uniqueDestinationSites.Count +
                    " InvalidStrategic=" + invalidStrategic +
                    " SameSiteMultiExit=" +
                    (sameSiteMultiExit.Length > 0 ? sameSiteMultiExit : "None") +
                    " LocallyUnavailable=" +
                    (locallyUnavailable >= 0 ? locallyUnavailable.ToString() : "NotLoaded"),
                    this);
            }
            Debug.Log(
                "[SurfaceExitTopology.Summary] InvalidStrategicExitCount=" + totalInvalid +
                " CurrentLoadedStrategicExitCount=" + currentStrategicExitCount +
                " CurrentLoadedStructuralReadyCount=" + currentStructuralReadyCount +
                " CurrentLoadedStructuralTransitionUnavailableCount=" +
                System.Math.Max(0, currentStrategicExitCount - currentStructuralReadyCount) +
                " CurrentLoadedLocallyReachableCount=" + currentUsableExitCount +
                " CurrentLoadedVisibleUsableCount=" + currentUsableExitCount +
                " CurrentLoadedExactDuplicateCount=" + currentExactDuplicateCount +
                " CurrentLoadedLocallyUnavailableCount=" + totalLocallyUnavailable,
                this);
        }

        public void Clear()
        {
            ClearVisualsOnly();
            _cachedMapId = string.Empty;
            _cachedDepth = 0f;
        }

        void ClearVisualsOnly()
        {
            VisibleZoneCount = 0;
            _zones.Clear();
            _rects.Clear();
            if (_root == null)
                return;
            if (Application.isPlaying)
                Destroy(_root.gameObject);
            else
                DestroyImmediate(_root.gameObject);
            _root = null;
        }

        void SpawnRect(
            SurfaceExitCoverageRect rect,
            int directionIndex,
            int rectIndex,
            Material mat,
            bool filled)
        {
            if (!drawFilledOverlay && filled)
                return;
            var w = rect.Width;
            var h = rect.Height;
            if (w < 0.001f || h < 0.001f)
                return;

            var go = new GameObject(
                "SurfaceExitZone_D" + directionIndex + "_R" + rectIndex);
            go.transform.SetParent(_root, false);
            var cx = (rect.MinX + rect.MaxX) * 0.5f;
            var cy = (rect.MinY + rect.MaxY) * 0.5f;
            go.transform.position = HostPresentationSpace.FromPresentation(cx, cy, overlayZ);
            go.transform.localScale = new Vector3(w, h, 1f);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = QuadMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = -20;

            if (drawTriggerOutline)
            {
                // 简易外框：略大一圈的半透明边（同几何，不改真源）。
                var outline = new GameObject("Outline");
                outline.transform.SetParent(go.transform, false);
                outline.transform.localPosition = new Vector3(0f, 0f, 0.01f);
                outline.transform.localScale = new Vector3(1.02f, 1.02f, 1f);
                var omf = outline.AddComponent<MeshFilter>();
                omf.sharedMesh = QuadMesh();
                var omr = outline.AddComponent<MeshRenderer>();
                var oMat = OutlineMaterial();
                oMat.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.55f);
                omr.sharedMaterial = oMat;
                omr.sortingOrder = -19;
            }
        }

        bool TryResolvePlayableBounds(
            out WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            out float authoredDepth,
            out string mapId)
        {
            bounds = default;
            authoredDepth = 0f;
            mapId = string.Empty;

            var session = bootstrap != null ? bootstrap.Session : null;
            var world = session != null ? session.World : null;
            if (world?.LocalMap != null)
                mapId = world.LocalMap.ActiveMapLayoutId ?? string.Empty;

            // 优先 MapLayout 定义（同一 LocalMap 固定），禁止依赖临时 WalkGrid fallback 尺寸。
            if (!string.IsNullOrWhiteSpace(mapId) && session?.Registry != null)
            {
                var parsed = DefinitionId.Parse(mapId.Trim());
                if (parsed.IsSuccess &&
                    session.Registry.TryGetMapLayout(parsed.Value, out var layout) &&
                    layout != null &&
                    layout.Width > 0 &&
                    layout.Height > 0)
                {
                    var cs = layout.CellSize > 0.0001f ? layout.CellSize : 1f;
                    bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                        layout.OriginX, layout.OriginY, cs, layout.Width, layout.Height);
                    authoredDepth = layout.ExitTriggerDepth > 0.0001f
                        ? layout.ExitTriggerDepth
                        : (cs * SurfaceExitZoneCalculator.DefaultExitTriggerDepth);
                    return true;
                }
            }

            var grid = bootstrap != null ? bootstrap.MoveController?.WalkGrid : null;
            if (grid == null)
                return false;
            bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                grid.OriginX, grid.OriginY, grid.CellSize, grid.Width, grid.Height);
            authoredDepth = world?.LocalMap != null && world.LocalMap.ExitTriggerDepth > 0.0001f
                ? world.LocalMap.ExitTriggerDepth
                : SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
            return true;
        }

        void EnsureRoot()
        {
            if (_root != null)
                return;
            var go = new GameObject("SurfaceExitZones");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }

        static Material SharedMaterial()
        {
            if (_sharedMaterial != null)
                return _sharedMaterial;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _sharedMaterial = new Material(shader);
            return _sharedMaterial;
        }

        static Material OutlineMaterial()
        {
            if (_outlineMaterial != null)
                return _outlineMaterial;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _outlineMaterial = new Material(shader);
            return _outlineMaterial;
        }

        static Mesh QuadMesh()
        {
            if (_quadMesh != null)
                return _quadMesh;
            _quadMesh = new Mesh
            {
                name = "SurfaceExitZoneQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f),
                },
            };
            _quadMesh.RecalculateBounds();
            return _quadMesh;
        }
    }

    /// <summary>仅开发环境运行的全 WorldSite 出口拓扑审计入口。</summary>
    static class SurfaceExitTopologyAudit
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Run(
            HostSurfaceExitZonePresenter presenter,
            XianXia.Core.Simulation.SimulationWorld world,
            int currentStrategicExitCount,
            int currentStructuralReadyCount,
            int currentUsableExitCount,
            int currentExactDuplicateCount)
        {
            presenter?.WriteTopologyAudit(
                world,
                currentStrategicExitCount,
                currentStructuralReadyCount,
                currentUsableExitCount,
                currentExactDuplicateCount);
        }
    }
}
