using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Exploration;
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
        PlayableHostSession _session;

        public int TileCount => _built.Count;

        public int MissingPrefabCount => MapLayoutPrefabResolver.MissingCount;

        public void Rebuild() => Rebuild(null);

        public void Rebuild(PlayableHostSession session)
        {
            Clear();
            _session = session;
            MapLayoutPrefabResolver.BeginBatch();
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

        /// <summary>
        /// Level Tester：编辑模式下 Import 预览，不依赖 PlayableHostSession。
        /// </summary>
        public void RebuildFromLayout(MapLayoutDefinition layout)
        {
            Clear();
            _session = null;
            MapLayoutPrefabResolver.BeginBatch();
            HostInteractSpots.BeginLayoutRebuild();
            if (!buildOnRebuild || layout == null)
                return;
            EnsureRoot();
            BuildFromLayout(layout);
        }

        static bool TryPickLayout(PlayableHostSession session, out MapLayoutDefinition layout) =>
            MapLayoutPick.TryGet(session, out layout);

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
                    PlacePrefab("grass", MapKindCatalog.Grass, wx, wy, "Grass_" + gx + "_" + gy, cs * step, cs * step,
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
                StampMissingPlacement(layout, p, index, kind);
                return;
            }

            // 未勘查显形的洞口：不刷外观／交互（坐标仍由 MapLayoutPresentationSync 对齐）。
            if (ShouldHideHiddenEntrance(p))
                return;
            // 已拾取的地上物：不刷。
            if (ShouldHideTakenLoot(p))
                return;

            if (info.Mode == MapKindCatalog.StampMode.ZoneOverlay)
            {
                var cx = ox + (p.X + pw * 0.5f) * cs;
                var cy = oy + (p.Y + ph * 0.5f) * cs;
                PlaceZoneOverlay(cx, cy, id + "_zone", pw * cs, ph * cs, info.FallbackColor);
                return;
            }

            if (info.Mode == MapKindCatalog.StampMode.SingleCentered)
            {
                var cx = ox + (p.X + pw * 0.5f) * cs;
                var cy = oy + (p.Y + ph * 0.5f) * cs;
                var path = info.PrefabPath;
                var go = PlacePrefab(kind, path, cx, cy, id, pw * cs, ph * cs, info.FallbackColor,
                    sortingOrder: kind == "controlCore" || kind == "roadHub" ? -8 : -12);
                if (info.InteractKind.HasValue)
                    AttachPlot(go, p, info, p.X, p.Y, cx, cy);
                return;
            }

            // PerCell：一格一个 prefab
            var cellOrder = kind == "wall" ? -5 : -25;
            for (var gy = 0; gy < ph; gy++)
            for (var gx = 0; gx < pw; gx++)
            {
                var cellX = p.X + gx;
                var cellY = p.Y + gy;
                var wx = ox + (cellX + 0.5f) * cs;
                var wy = oy + (cellY + 0.5f) * cs;
                var cellName = id + "_" + gx + "_" + gy;
                var go = PlacePrefab(kind, info.PrefabPath, wx, wy, cellName, cs, cs, info.FallbackColor,
                    sortingOrder: cellOrder);
                if (info.InteractKind.HasValue || info.Plantable)
                    AttachPlot(go, p, info, cellX, cellY, wx, wy);
            }
        }

        bool ShouldHideTakenLoot(MapPlacement p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.LootItemId))
                return false;
            if (_session?.World == null)
                return false;
            var spotId = string.IsNullOrWhiteSpace(p.Id) ? p.LootItemId : p.Id;
            return XianXia.Core.Content.WorldLootPickupService.IsTaken(_session.World, spotId);
        }

        bool ShouldHideHiddenEntrance(MapPlacement p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.BoundLocationId))
                return false;
            if (_session?.World?.WorldRegion == null)
                return false;
            if (!_session.World.WorldRegion.TryGet(p.BoundLocationId, out var loc))
                return false;
            if (!OpportunityEntranceRules.IsHiddenEntrance(loc))
                return false;
            return !OpportunityEntranceRules.IsRevealed(_session.World, loc);
        }

        GameObject PlaceZoneOverlay(float x, float y, string name, float worldW, float worldH, Color color)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = HostSpriteFactory.TileSprite();
            var c = color;
            if (c.a >= 0.99f)
                c.a = 0.32f;
            sr.color = c;
            sr.sortingOrder = -20;
            go.transform.SetParent(mapRoot, false);
            var intended = HostPresentationSpace.FromPresentation(x, y, HostPresentationSpace.GroundZ);
            go.transform.position = intended;
            FitToWorldSize(go, Mathf.Max(0.01f, worldW), Mathf.Max(0.01f, worldH));
            AlignBoundsCenter(go, intended);
            _built.Add(go);
            return go;
        }

        static string ResolveHousePath()
        {
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
                : p.Label;
            var lootSpotId = string.IsNullOrWhiteSpace(p.Id) ? string.Empty : p.Id;
            var lootItemId = p.LootItemId ?? string.Empty;
            plot.Configure(
                p.BoundLocationId ?? string.Empty,
                info.InteractKind.Value,
                label,
                cellX,
                cellY,
                info.Kind,
                lootSpotId,
                lootItemId);

            HostInteractSpots.RegisterPlot(new HostInteractSpot(
                p.BoundLocationId ?? string.Empty,
                info.InteractKind.Value,
                wx,
                wy,
                label,
                lootSpotId,
                lootItemId));
        }

        void StampMissingPlacement(MapLayoutDefinition layout, MapPlacement p, int index, string kind)
        {
            Debug.LogWarning(
                "[MapLayout] Unknown map kind '" + (kind ?? string.Empty) +
                "' in placement '" + (p?.Id ?? index.ToString()) + "'. Using MissingPrefab placeholder.");
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var ox = layout.OriginX;
            var oy = layout.OriginY;
            var pw = p.W < 1 ? 1 : p.W;
            var ph = p.H < 1 ? 1 : p.H;
            var id = string.IsNullOrEmpty(p.Id) ? "missing_" + index : p.Id;
            var cx = ox + (p.X + pw * 0.5f) * cs;
            var cy = oy + (p.Y + ph * 0.5f) * cs;
            PlacePrefab(kind, MapKindCatalog.MissingPrefab, cx, cy, id + "_missing", pw * cs, ph * cs,
                Color.white, sortingOrder: 100);
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

                PlacePrefab(KindForLegacyPath(path), path, x + 0.5f, y + 0.5f, "Tile_" + x + "_" + y, 1f, 1f, fallback);
            }
        }

        static string KindForLegacyPath(string path)
        {
            if (path == MapKindCatalog.Spirit) return "spring";
            if (path == MapKindCatalog.Forest) return "forest";
            if (path == MapKindCatalog.Herb) return "herbField";
            if (path == MapKindCatalog.Farm) return "grainField";
            if (path == MapKindCatalog.Road) return "road";
            return "grass";
        }

        public void Clear()
        {
            for (var i = 0; i < _built.Count; i++)
                DestroyBuilt(_built[i]);
            _built.Clear();

            // _built is not serialized: after domain reload／场景重载，子物体仍在但列表为空。
            // Always wipe mapRoot children so Import 不会叠出旧位置＋新位置。
            if (mapRoot != null)
            {
                for (var i = mapRoot.childCount - 1; i >= 0; i--)
                    DestroyBuilt(mapRoot.GetChild(i).gameObject);
            }

            // Lost mapRoot ref could leave orphan DemoTileMap roots from earlier Imports.
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null || child == mapRoot)
                    continue;
                if (child.name == "DemoTileMap")
                    DestroyBuilt(child.gameObject);
            }
        }

        static void DestroyBuilt(GameObject go)
        {
            if (go == null)
                return;
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
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
            string kind,
            string prefabPath,
            float x,
            float y,
            string name,
            float worldW,
            float worldH,
            Color fallbackColor,
            int sortingOrder = -30)
        {
            GameObject go = null;
            var usedMissingPlaceholder = false;
            if (MapLayoutPrefabResolver.TryInstantiate(kind, prefabPath, out go))
            {
                // resolved prefab
            }
            else if (MapLayoutPrefabResolver.TryInstantiate(kind, MapKindCatalog.MissingPrefab, out go, warnOnMissing: false))
            {
                usedMissingPlaceholder = true;
            }
            else
            {
                go = new GameObject(name);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = HostSpriteFactory.MissingPrefabSprite();
                sr.color = Color.white;
                sr.sortingOrder = sortingOrder + 50;
                usedMissingPlaceholder = true;
            }

            go.name = usedMissingPlaceholder ? name + "_MissingPrefab" : name;
            go.transform.SetParent(mapRoot, false);
            go.transform.localScale = Vector3.one;
            var intended = HostPresentationSpace.FromPresentation(x, y, HostPresentationSpace.GroundZ);
            go.transform.position = intended;
            FitToWorldSize(go, Mathf.Max(0.01f, worldW), Mathf.Max(0.01f, worldH));
            AlignBoundsCenter(go, intended);
            ApplySortingOrder(go, usedMissingPlaceholder ? sortingOrder + 50 : sortingOrder);
            if (!usedMissingPlaceholder && prefabPath == MapKindCatalog.Wall)
                TintRenderers(go, new Color(0.32f, 0.32f, 0.36f, 1f));
            StripNonHostBehaviours(go);
            _built.Add(go);
            return go;
        }

        static void FitToWorldSize(GameObject go, float worldW, float worldH)
        {
            go.transform.localScale = Vector3.one;
            if (!TryGetRendererBounds(go, out var bounds))
                return;
            if (bounds.size.x < 0.0001f || bounds.size.y < 0.0001f)
                return;

            var sx = worldW / bounds.size.x;
            var sy = worldH / bounds.size.y;
            go.transform.localScale = new Vector3(sx, sy, 1f);
        }

        /// <summary>
        /// 缩放绕 transform 原点，精灵 pivot 若不在中心会导致色块／地砖相对逻辑格偏移。
        /// 缩放后再把渲染包围盒中心对齐到目标点。
        /// </summary>
        static void AlignBoundsCenter(GameObject go, Vector3 intendedCenter)
        {
            if (!TryGetRendererBounds(go, out var bounds))
                return;
            var delta = intendedCenter - bounds.center;
            go.transform.position += delta;
        }

        static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0)
                return false;
            var any = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null || !sr.enabled || sr.sprite == null)
                    continue;
                if (!any)
                {
                    bounds = sr.bounds;
                    any = true;
                }
                else
                    bounds.Encapsulate(sr.bounds);
            }

            return any;
        }

        static void ApplySortingOrder(GameObject go, int order)
        {
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].sortingOrder = order;
            }
        }

        static void TintRenderers(GameObject go, Color color)
        {
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].color = color;
            }
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
