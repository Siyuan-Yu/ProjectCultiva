using System.Collections.Generic;
using UnityEngine;
using XianXia.Data.Content;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 按 mapLayout 生成对应 Environment prefab：药田／农田／路等 1×1 铺格；房子 20×20 居中一个。
    /// </summary>
    public sealed class HostDemoTileMap : MonoBehaviour
    {
        const int LegacyMinX = -40;
        const int LegacyMinY = -25;
        const int LegacyWidth = 80;
        const int LegacyHeight = 50;

        [SerializeField] Transform mapRoot;
        [SerializeField] bool buildOnRebuild = true;
        [SerializeField] bool stampGrassGround = true;
        [SerializeField] int grassStride = 2;

        readonly List<GameObject> _built = new List<GameObject>();

        public int TileCount => _built.Count;

        public void Rebuild() => Rebuild(null);

        public void Rebuild(PlayableHostSession session)
        {
            Clear();
            HostInteractSpots.BeginLayoutRebuild();
            if (!buildOnRebuild)
                return;
            EnsureRoot();

            if (TryPickLayout(session, out var layout))
            {
                BuildFromLayout(layout);
                return;
            }

            BuildLegacyDemoTiles();
        }

        static bool TryPickLayout(PlayableHostSession session, out MapLayoutDefinition layout)
        {
            layout = null;
            if (session?.Registry?.MapLayouts == null || session.Registry.MapLayouts.Count == 0)
                return false;

            foreach (var kv in session.Registry.MapLayouts)
            {
                layout = kv.Value;
                if (!string.IsNullOrEmpty(kv.Value.WorldRegionId) &&
                    kv.Value.WorldRegionId.IndexOf("ch01", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return layout != null;
        }

        void BuildFromLayout(MapLayoutDefinition layout)
        {
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var ox = layout.OriginX;
            var oy = layout.OriginY;
            var w = layout.Width;
            var h = layout.Height;
            if (w < 1 || h < 1)
                return;

            if (stampGrassGround)
            {
                var step = Mathf.Max(1, grassStride);
                for (var gy = 0; gy < h; gy += step)
                for (var gx = 0; gx < w; gx += step)
                {
                    var wx = ox + (gx + 0.5f) * cs;
                    var wy = oy + (gy + 0.5f) * cs;
                    PlacePrefab(MapKindCatalog.Grass, wx, wy, "Grass_" + gx + "_" + gy, cs * step, cs * step,
                        new Color(0.30f, 0.42f, 0.26f));
                }
            }

            if (layout.Placements == null)
                return;

            for (var i = 0; i < layout.Placements.Count; i++)
            {
                var p = layout.Placements[i];
                if (p == null)
                    continue;
                StampPlacement(layout, p, i);
            }
        }

        void StampPlacement(MapLayoutDefinition layout, MapPlacement p, int index)
        {
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var ox = layout.OriginX;
            var oy = layout.OriginY;
            var pw = p.W < 1 ? 1 : p.W;
            var ph = p.H < 1 ? 1 : p.H;
            var kind = p.Kind ?? string.Empty;
            var id = string.IsNullOrEmpty(p.Id) ? "p" + index : p.Id;

            if (!MapKindCatalog.TryGet(kind, out var info))
            {
                info = new MapKindCatalog.KindInfo(
                    kind,
                    MapKindCatalog.Road,
                    MapKindCatalog.StampMode.PerCell,
                    pw,
                    ph,
                    false,
                    null,
                    new Color(0.5f, 0.5f, 0.5f));
            }

            if (info.Mode == MapKindCatalog.StampMode.SingleCentered)
            {
                var cx = ox + (p.X + pw * 0.5f) * cs;
                var cy = oy + (p.Y + ph * 0.5f) * cs;
                var path = info.PrefabPath;
                if (kind == "house")
                    path = ResolveHousePath();
                var sizeW = pw * cs;
                var sizeH = ph * cs;
                if (kind == "house")
                {
                    sizeW = MapKindCatalog.HouseFootprint * cs;
                    sizeH = MapKindCatalog.HouseFootprint * cs;
                }

                var go = PlacePrefab(path, cx, cy, id, sizeW, sizeH, info.FallbackColor);
                if (info.InteractKind.HasValue)
                    AttachPlot(go, p, info, p.X, p.Y, cx, cy);
                return;
            }

            // PerCell：一格一个 prefab
            for (var gy = 0; gy < ph; gy++)
            for (var gx = 0; gx < pw; gx++)
            {
                var cellX = p.X + gx;
                var cellY = p.Y + gy;
                var wx = ox + (cellX + 0.5f) * cs;
                var wy = oy + (cellY + 0.5f) * cs;
                var cellName = id + "_" + gx + "_" + gy;
                var go = PlacePrefab(info.PrefabPath, wx, wy, cellName, cs, cs, info.FallbackColor);
                if (info.InteractKind.HasValue || info.Plantable)
                    AttachPlot(go, p, info, cellX, cellY, wx, wy);
            }
        }

        static string ResolveHousePath()
        {
#if UNITY_EDITOR
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MapKindCatalog.House) != null)
                return MapKindCatalog.House;
#endif
            return MapKindCatalog.HouseFallback;
        }

        void AttachPlot(
            GameObject go,
            MapPlacement p,
            MapKindCatalog.KindInfo info,
            int cellX,
            int cellY,
            float wx,
            float wy)
        {
            if (go == null || !info.InteractKind.HasValue)
                return;

            var plot = go.GetComponent<HostMapPlotCell>() ?? go.AddComponent<HostMapPlotCell>();
            var label = string.IsNullOrWhiteSpace(p.Label)
                ? info.Kind + "(" + cellX + "," + cellY + ")"
                : p.Label + "(" + cellX + "," + cellY + ")";
            plot.Configure(
                p.BoundLocationId ?? string.Empty,
                info.InteractKind.Value,
                label,
                cellX,
                cellY,
                info.Kind);

            HostInteractSpots.RegisterPlot(new HostInteractSpot(
                p.BoundLocationId ?? string.Empty,
                info.InteractKind.Value,
                wx,
                wy,
                label));
        }

        void BuildLegacyDemoTiles()
        {
            for (var y = LegacyMinY; y < LegacyMinY + LegacyHeight; y++)
            for (var x = LegacyMinX; x < LegacyMinX + LegacyWidth; x++)
            {
                // 旧关卡硬编码色带
                string path;
                Color fallback;
                if (x >= 24 && y <= -10)
                {
                    path = MapKindCatalog.Spirit;
                    fallback = new Color(0.35f, 0.7f, 0.85f);
                }
                else if (x <= -28)
                {
                    path = MapKindCatalog.Forest;
                    fallback = new Color(0.25f, 0.48f, 0.28f);
                }
                else if (x >= -10 && x <= 3 && y >= -20 && y <= -11)
                {
                    path = MapKindCatalog.Herb;
                    fallback = new Color(0.35f, 0.65f, 0.40f);
                }
                else if (x >= 8 && x <= 32 && y >= -20 && y <= -4)
                {
                    path = MapKindCatalog.Farm;
                    fallback = new Color(0.70f, 0.62f, 0.30f);
                }
                else if (Mathf.Abs(y) <= 1 || Mathf.Abs(x) <= 1)
                {
                    path = MapKindCatalog.Road;
                    fallback = new Color(0.55f, 0.45f, 0.32f);
                }
                else
                {
                    path = MapKindCatalog.Grass;
                    fallback = new Color(0.30f, 0.42f, 0.26f);
                }

                PlacePrefab(path, x + 0.5f, y + 0.5f, "Tile_" + x + "_" + y, 1f, 1f, fallback);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _built.Count; i++)
            {
                if (_built[i] == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(_built[i]);
                else
                    DestroyImmediate(_built[i]);
            }

            _built.Clear();
        }

        void EnsureRoot()
        {
            if (mapRoot != null)
                return;
            var go = new GameObject("DemoTileMap");
            go.transform.SetParent(transform, false);
            mapRoot = go.transform;
        }

        GameObject PlacePrefab(
            string prefabPath,
            float x,
            float y,
            string name,
            float worldW,
            float worldH,
            Color fallbackColor)
        {
            GameObject go = null;
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null && prefabPath == MapKindCatalog.House)
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapKindCatalog.HouseFallback);
            if (prefab == null && (prefabPath == MapKindCatalog.Wall || prefabPath == MapKindCatalog.Rock))
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapKindCatalog.Road);
            if (prefab != null)
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
#endif
            if (go == null)
            {
                go = new GameObject(name);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = HostSpriteFactory.TileSprite();
                sr.color = fallbackColor;
                sr.sortingOrder = -30;
            }

            go.name = name;
            go.transform.SetParent(mapRoot, false);
            go.transform.localScale = Vector3.one;
            go.transform.position = HostPresentationSpace.FromPresentation(x, y, HostPresentationSpace.GroundZ);
            FitToWorldSize(go, Mathf.Max(0.01f, worldW), Mathf.Max(0.01f, worldH));
            StripNonHostBehaviours(go);
            _built.Add(go);
            return go;
        }

        static void FitToWorldSize(GameObject go, float worldW, float worldH)
        {
            go.transform.localScale = Vector3.one;
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.x < 0.0001f || bounds.size.y < 0.0001f)
                return;

            var sx = worldW / bounds.size.x;
            var sy = worldH / bounds.size.y;
            go.transform.localScale = new Vector3(sx, sy, 1f);
        }

        static void StripNonHostBehaviours(GameObject go)
        {
            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null)
                    continue;
                var ns = mb.GetType().Namespace ?? string.Empty;
                if (ns.StartsWith("XianXia.Unity.Host", System.StringComparison.Ordinal))
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(mb);
                else
                    Object.DestroyImmediate(mb);
            }
        }
    }
}
