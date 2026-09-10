using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Exploration;
using XianXia.Core.World.Surface;
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

        readonly Dictionary<string, SurfacePresentationInstance> _instances =
            new Dictionary<string, SurfacePresentationInstance>(System.StringComparer.Ordinal);
        readonly Dictionary<string, List<GameObject>> _builtByInstance =
            new Dictionary<string, List<GameObject>>(System.StringComparer.Ordinal);
        Transform _buildRoot;
        Vector2 _buildPlacementOffset;
        string _buildingInstanceKey = string.Empty;
        PlayableHostSession _session;

        public int TileCount
        {
            get
            {
                var count = 0;
                foreach (var built in _builtByInstance)
                    count += built.Value.Count;
                return count;
            }
        }

        public IReadOnlyDictionary<string, SurfacePresentationInstance> LoadedInstances => _instances;

        public int MissingPrefabCount => MapLayoutPrefabResolver.MissingCount;

        public void Rebuild() => Rebuild(null);

        public void Rebuild(PlayableHostSession session)
        {
            Clear();
            _session = session;
            MapLayoutPrefabResolver.BeginBatch();
            if (!buildOnRebuild)
                return;

            if (TryPickLayout(session, out var layout))
            {
                BuildLayoutInstance("legacy:active-localmap", layout, Vector2.zero);
                return;
            }

            // 已指定 LocalMap 却取不到定义：禁止回落荒村色带（会串景）
            if (session != null && !string.IsNullOrWhiteSpace(session.PreferredMapLayoutId))
            {
                Debug.LogError(
                    "[HostDemoTileMap] MapLayout not found: " + session.PreferredMapLayoutId +
                    " — left empty (no legacy village fallback).",
                    this);
                return;
            }

            BeginInstanceBuild("legacy:active-localmap", null, Vector2.zero);
            BuildLegacyDemoTiles();
            EndInstanceBuild();
        }

        /// <summary>
        /// Level Tester：编辑模式下 Import 预览，不依赖 PlayableHostSession。
        /// </summary>
        public void RebuildFromLayout(MapLayoutDefinition layout)
        {
            Clear();
            _session = null;
            MapLayoutPrefabResolver.BeginBatch();
            if (!buildOnRebuild || layout == null)
                return;
            BuildLayoutInstance("legacy:editor-preview", layout, Vector2.zero);
        }

        /// <summary>
        /// Incrementally builds one transient presentation instance. The key is loading ownership
        /// only: it is not a LocalMap, Domain, or Save identifier.
        /// </summary>
        public SurfacePresentationInstance BuildLayoutInstance(
            string instanceKey,
            MapLayoutDefinition layout,
            Vector2 placementOffset)
        {
            if (string.IsNullOrWhiteSpace(instanceKey))
                throw new System.ArgumentException("A surface presentation instance key is required.", nameof(instanceKey));
            if (layout == null)
                throw new System.ArgumentNullException(nameof(layout));

            RemoveLayoutInstance(instanceKey);
            BeginInstanceBuild(instanceKey, layout, placementOffset);
            BuildFromLayout(layout);
            return EndInstanceBuild();
        }

        /// <summary>
        /// Builds baked outdoor placements directly from physical rectangles plus authored-cell
        /// semantics. This path never converts physical precision into a fake MapPlacement grid.
        /// </summary>
        public SurfacePresentationInstance BuildOutdoorPlacementInstance(
            string instanceKey,
            IReadOnlyList<OutdoorSurfacePlacementDefinition> placements,
            OutdoorSurfaceCoordinateMapper mapper,
            SurfaceChunkCoord ownerChunk)
        {
            if (string.IsNullOrWhiteSpace(instanceKey))
                throw new System.ArgumentException("A surface presentation instance key is required.", nameof(instanceKey));
            if (mapper == null)
                throw new System.ArgumentNullException(nameof(mapper));
            RemoveLayoutInstance(instanceKey);
            BeginInstanceBuild(instanceKey, null, Vector2.zero);
            if (placements != null)
                for (var i = 0; i < placements.Count; i++)
                    if (placements[i] != null)
                        StampOutdoorPlacement(placements[i], mapper, ownerChunk);
            return EndInstanceBuild();
        }

        public static bool TryEstimateOutdoorRenderedObjectCount(
            OutdoorSurfacePlacementDefinition placement,
            out int count)
        {
            count = 0;
            if (placement == null || placement.SourceCellsW <= 0 || placement.SourceCellsH <= 0 ||
                !MapKindCatalog.TryGet(placement.Kind ?? string.Empty, out var info))
                return false;
            if (info.Mode != MapKindCatalog.StampMode.PerCell)
            {
                count = 1;
                return true;
            }
            try { count = checked(placement.SourceCellsW * placement.SourceCellsH); }
            catch (System.OverflowException) { return false; }
            return count > 0;
        }

        void StampOutdoorPlacement(
            OutdoorSurfacePlacementDefinition source,
            OutdoorSurfaceCoordinateMapper mapper,
            SurfaceChunkCoord ownerChunk)
        {
            if (!TryEstimateOutdoorRenderedObjectCount(source, out _) ||
                !MapKindCatalog.TryGet(source.Kind ?? string.Empty, out var info))
            {
                LogOutdoorSemanticMismatch(source, 0, 0);
                return;
            }

            var metadata = new MapPlacement
            {
                Id = source.StableId, Kind = source.Kind, BlocksMovement = source.BlocksMovement,
                BoundLocationId = source.BoundLocationId, Label = source.Label,
                LootItemId = source.LootItemId, SpawnTableId = source.SpawnTableId,
                SpawnCount = source.SpawnCount
            };
            if (ShouldHideHiddenEntrance(metadata) || ShouldHideTakenLoot(metadata))
                return;

            mapper.WorldToPresentation(source.WorldX, source.WorldY, out var left, out var bottom);
            mapper.WorldToPresentation(source.WorldX + source.WorldWidth, source.WorldY + source.WorldHeight,
                out var right, out var top);
            var minX = Mathf.Min(left, right); var minY = Mathf.Min(bottom, top);
            var width = Mathf.Abs(right - left); var height = Mathf.Abs(top - bottom);
            var id = source.StableId ?? string.Empty;
            var actual = 0; var expectedForOwner = 0;

            if (info.Mode == MapKindCatalog.StampMode.ZoneOverlay)
            {
                mapper.ChunkLocalToWorld(ownerChunk, 0f, 0f, out var chunkWorldX, out var chunkWorldY);
                var ix0 = Mathf.Max(source.WorldX, chunkWorldX);
                var iy0 = Mathf.Max(source.WorldY, chunkWorldY);
                var ix1 = Mathf.Min(source.WorldX + source.WorldWidth, chunkWorldX + mapper.ChunkWidth);
                var iy1 = Mathf.Min(source.WorldY + source.WorldHeight, chunkWorldY + mapper.ChunkHeight);
                if (ix1 > ix0 && iy1 > iy0)
                {
                    mapper.WorldToPresentation(ix0, iy0, out var clipLeft, out var clipBottom);
                    mapper.WorldToPresentation(ix1, iy1, out var clipRight, out var clipTop);
                    PlaceZoneOverlay((clipLeft + clipRight) * .5f, (clipBottom + clipTop) * .5f,
                        id + "_zone_" + ownerChunk.X + "_" + ownerChunk.Y,
                        Mathf.Abs(clipRight - clipLeft), Mathf.Abs(clipTop - clipBottom), info.FallbackColor);
                    actual = expectedForOwner = 1;
                }
            }
            else if (info.Mode == MapKindCatalog.StampMode.SingleCentered)
            {
                var cx = minX + width * .5f; var cy = minY + height * .5f;
                mapper.PresentationToWorld(cx, cy, out var centerWorldX, out var centerWorldY);
                if (mapper.WorldToChunk(centerWorldX, centerWorldY) == ownerChunk)
                {
                    var go = PlacePrefab(info.Kind, info.PrefabPath, cx, cy, id, width, height,
                        info.FallbackColor, sortingOrder: info.Kind == "controlCore" || info.Kind == "roadHub" ? -8 : -12);
                    if (info.InteractKind.HasValue)
                        AttachPlot(go, metadata, info, source.SourceGridX, source.SourceGridY, cx, cy, id);
                    AttachDestructibleIfNeeded(go, metadata, info.Kind, id);
                    actual = expectedForOwner = 1;
                }
            }
            else
            {
                var cellW = width / source.SourceCellsW;
                var cellH = height / source.SourceCellsH;
                var cellOrder = info.Kind == "wall" ? -5 : -25;
                for (var gy = 0; gy < source.SourceCellsH; gy++)
                for (var gx = 0; gx < source.SourceCellsW; gx++)
                {
                    var cx = minX + (gx + .5f) * cellW;
                    var cy = minY + (gy + .5f) * cellH;
                    mapper.PresentationToWorld(cx, cy, out var cellWorldX, out var cellWorldY);
                    if (mapper.WorldToChunk(cellWorldX, cellWorldY) != ownerChunk) continue;
                    expectedForOwner++;
                    var cellId = id + ":" + gx + ":" + gy;
                    var cellMetadata = metadata;
                    cellMetadata.Id = cellId;
                    var go = PlacePrefab(info.Kind, info.PrefabPath, cx, cy, cellId, cellW, cellH,
                        info.FallbackColor, sortingOrder: cellOrder);
                    if (info.InteractKind.HasValue || info.Plantable)
                        AttachPlot(go, cellMetadata, info, source.SourceGridX + gx, source.SourceGridY + gy, cx, cy, cellId);
                    if (string.Equals(info.Kind, "wall", System.StringComparison.OrdinalIgnoreCase))
                        AttachDestructibleIfNeeded(go, cellMetadata, info.Kind, cellId);
                    actual++;
                }
            }

            if (actual != expectedForOwner)
                LogOutdoorSemanticMismatch(source, expectedForOwner, actual);
        }

        static void LogOutdoorSemanticMismatch(OutdoorSurfacePlacementDefinition p, int expected, int actual)
        {
            Debug.LogError("OutdoorPlacementSemanticMismatch" +
                           " Chunk=" + (p != null ? p.ChunkX + "," + p.ChunkY : "?") +
                           " Site=" + (p?.SiteId ?? string.Empty) +
                           " Placement=" + (p?.StableId ?? string.Empty) +
                           " Kind=" + (p?.Kind ?? string.Empty) +
                           " CellsW/H=" + (p != null ? p.SourceCellsW + "/" + p.SourceCellsH : "0/0") +
                           " EstimatedPrefabCount=" + expected +
                           " ActualPrefabCount=" + actual);
        }

        public bool RemoveLayoutInstance(string instanceKey)
        {
            if (string.IsNullOrWhiteSpace(instanceKey) || !_instances.TryGetValue(instanceKey, out var instance))
                return false;
            HostInteractSpots.RemoveOwner(instanceKey);
            HostMapObjectRegistry.RemoveOwner(instanceKey);
            HostFarmFieldRegistry.RemoveOwner(instanceKey);
            if (instance.Root != null)
                DestroyBuilt(instance.Root.gameObject);
            _instances.Remove(instanceKey);
            _builtByInstance.Remove(instanceKey);
            instance.IsLoaded = false;
            return true;
        }

        static bool TryPickLayout(PlayableHostSession session, out MapLayoutDefinition layout) =>
            MapLayoutPick.TryGet(session, out layout);

        void BeginInstanceBuild(string instanceKey, MapLayoutDefinition layout, Vector2 placementOffset)
        {
            EnsureRoot();
            var root = new GameObject("SurfaceInstance_" + instanceKey).transform;
            root.SetParent(mapRoot, false);
            _buildRoot = root;
            _buildPlacementOffset = placementOffset;
            _buildingInstanceKey = instanceKey;
            _builtByInstance[instanceKey] = new List<GameObject>();
            _instances[instanceKey] = new SurfacePresentationInstance(instanceKey, layout, placementOffset, root);
            HostInteractSpots.BeginOwnerBuild(instanceKey);
            HostMapObjectRegistry.BeginOwnerBuild(instanceKey);
            HostFarmFieldRegistry.BeginOwnerBuild(instanceKey);
        }

        SurfacePresentationInstance EndInstanceBuild()
        {
            var key = _buildingInstanceKey;
            // 批量 owner build 收尾：三个 registry 的 flatten／index 只重建一次。
            // Continuous chunk build 会一次注册几十／上百格；逐格 rebuild 是 O(N²) 卡顿尖峰。
            HostInteractSpots.EndOwnerBuild();
            HostMapObjectRegistry.EndOwnerBuild();
            HostFarmFieldRegistry.EndOwnerBuild();
            _buildRoot = null;
            _buildPlacementOffset = Vector2.zero;
            _buildingInstanceKey = string.Empty;
            return _instances.TryGetValue(key, out var instance) ? instance : null;
        }

        float PlaceX(float x) => x + _buildPlacementOffset.x;
        float PlaceY(float y) => y + _buildPlacementOffset.y;

        void TrackBuilt(GameObject go)
        {
            if (go == null || string.IsNullOrEmpty(_buildingInstanceKey))
                return;
            _builtByInstance[_buildingInstanceKey].Add(go);
        }

        void BuildFromLayout(MapLayoutDefinition layout)
        {
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var ox = PlaceX(layout.OriginX);
            var oy = PlaceY(layout.OriginY);
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
            var ox = PlaceX(layout.OriginX);
            var oy = PlaceY(layout.OriginY);
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
                    AttachPlot(go, p, info, p.X, p.Y, cx, cy, id);
                AttachDestructibleIfNeeded(go, p, info.Kind, id);
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
                    AttachPlot(go, p, info, cellX, cellY, wx, wy, id + ":" + gx + ":" + gy);
                if (string.Equals(info.Kind, "wall", System.StringComparison.OrdinalIgnoreCase))
                    AttachDestructibleIfNeeded(go, p, info.Kind, cellName);
            }
        }

        void AttachDestructibleIfNeeded(
            GameObject go,
            MapPlacement p,
            string kind,
            string instanceId)
        {
            if (go == null || string.IsNullOrEmpty(kind))
                return;
            var isTree =
                string.Equals(kind, "treeS", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "treeM", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "treeL", System.StringComparison.OrdinalIgnoreCase);
            var isWall = string.Equals(kind, "wall", System.StringComparison.OrdinalIgnoreCase);
            if (!isTree && !isWall)
                return;

            var d = go.GetComponent<HostMapDestructible>() ?? go.AddComponent<HostMapDestructible>();
            var yield = isTree ? HostMapDestructible.DefaultWoodYield(kind) : 0;
            d.Configure(
                _session?.World,
                instanceId,
                kind,
                p?.Label,
                HostMapDestructible.DefaultMaxHp(kind),
                yield);
            if (isTree && d.ResolveWoodYield() <= 0)
                Debug.LogWarning("[MapLayout] 树产量为 0：kind=" + kind + " id=" + instanceId);
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
            if (!_session.World.ContinuousOutdoorMaterialization.TryGetAnyPlace(p.BoundLocationId, out var loc) &&
                !_session.World.WorldRegion.TryGet(p.BoundLocationId, out loc))
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
            go.transform.SetParent(_buildRoot != null ? _buildRoot : mapRoot, false);
            var intended = HostPresentationSpace.FromPresentation(x, y, HostPresentationSpace.GroundZ);
            go.transform.position = intended;
            FitToWorldSize(go, Mathf.Max(0.01f, worldW), Mathf.Max(0.01f, worldH));
            AlignBoundsCenter(go, intended);
            TrackBuilt(go);
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
            float wy,
            string stableCellId)
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
                _session?.World,
                stableCellId,
                p.BoundLocationId ?? string.Empty,
                info.InteractKind.Value,
                label,
                cellX,
                cellY,
                info.Kind,
                lootSpotId,
                lootItemId);

            // 药田／农田：只进 HostFarmFieldRegistry，点中格才交互。
            // 树：只走可破坏物砍伐，勿注册 Work 热点（否则右键会当成林区劳动、不掉树产木材）。
            if (info.Plantable)
                return;
            if (IsTreeKind(info.Kind))
                return;

            HostInteractSpots.RegisterPlot(new HostInteractSpot(
                p.BoundLocationId ?? string.Empty,
                info.InteractKind.Value,
                wx,
                wy,
                label,
                lootSpotId,
                lootItemId));
        }

        static bool IsTreeKind(string kind) =>
            string.Equals(kind, "treeS", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "treeM", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "treeL", System.StringComparison.OrdinalIgnoreCase);

        void StampMissingPlacement(MapLayoutDefinition layout, MapPlacement p, int index, string kind)
        {
            Debug.LogWarning(
                "[MapLayout] Unknown map kind '" + (kind ?? string.Empty) +
                "' in placement '" + (p?.Id ?? index.ToString()) + "'. Using MissingPrefab placeholder.");
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var ox = PlaceX(layout.OriginX);
            var oy = PlaceY(layout.OriginY);
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

                PlacePrefab(KindForLegacyPath(path), path, PlaceX(x + 0.5f), PlaceY(y + 0.5f), "Tile_" + x + "_" + y, 1f, 1f, fallback);
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
            var keys = new List<string>(_instances.Keys);
            for (var i = 0; i < keys.Count; i++)
                RemoveLayoutInstance(keys[i]);
            HostInteractSpots.ClearAll();
            HostMapObjectRegistry.ClearAll();
            HostFarmFieldRegistry.ClearAll();

            // Instance dictionary is not serialized: after domain reload／场景重载，children may remain.
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
            go.transform.SetParent(_buildRoot != null ? _buildRoot : mapRoot, false);
            go.transform.localScale = Vector3.one;
            var intended = HostPresentationSpace.FromPresentation(x, y, HostPresentationSpace.GroundZ);
            go.transform.position = intended;
            FitToWorldSize(go, Mathf.Max(0.01f, worldW), Mathf.Max(0.01f, worldH));
            AlignBoundsCenter(go, intended);
            ApplySortingOrder(go, usedMissingPlaceholder ? sortingOrder + 50 : sortingOrder);
            if (!usedMissingPlaceholder && prefabPath == MapKindCatalog.Wall)
                TintRenderers(go, new Color(0.32f, 0.32f, 0.36f, 1f));
            StripNonHostBehaviours(go);
            TrackBuilt(go);
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
