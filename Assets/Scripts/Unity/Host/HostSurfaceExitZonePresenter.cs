using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
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
            if (_zones.Count == 0)
                return;

            EnsureRoot();
            var fillMat = SharedMaterial();
            fillMat.color = zoneColor;
            for (var i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                _rects.Clear();
                SurfaceExitZoneCalculator.AppendCoverageRects(
                    bounds, depth, z.DirectionIndex, _rects);
                for (var r = 0; r < _rects.Count; r++)
                    SpawnRect(_rects[r], z.DirectionIndex, r, fillMat, filled: true);
            }

            VisibleZoneCount = _zones.Count;
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
}
