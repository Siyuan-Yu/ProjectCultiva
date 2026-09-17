using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Exploration;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;
using UnityEngine.Sprites;
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
        readonly Dictionary<string, List<Mesh>> _meshesByInstance =
            new Dictionary<string, List<Mesh>>(System.StringComparer.Ordinal);
        readonly List<CompactGrassLayer> _compactGrassLayers = new List<CompactGrassLayer>();
        bool _compactGrassTemplateResolved;
        Transform _buildRoot;
        Vector2 _buildPlacementOffset;
        string _buildingInstanceKey = string.Empty;
        PlayableHostSession _session;

        sealed class CompactGrassLayer
        {
            public Material Material;
            public Texture Texture;
            public Color Color;
            public int SortingLayerId;
            public int SortingOrder;
            public readonly Vector2[] Corners = new Vector2[4];
            public readonly Vector2[] Uvs = new Vector2[4];
        }

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
            Vector2 placementOffset, bool compactGround = false)
        {
            if (string.IsNullOrWhiteSpace(instanceKey))
                throw new System.ArgumentException("A surface presentation instance key is required.", nameof(instanceKey));
            if (layout == null)
                throw new System.ArgumentNullException(nameof(layout));

            RemoveLayoutInstance(instanceKey);
            BeginInstanceBuild(instanceKey, layout, placementOffset);
            try { BuildFromLayout(layout, compactGround); }
            finally { EndInstanceBuild(); }
            return _instances[instanceKey];
        }

        /// <summary>Materialize only the loaded 50x50 partition from the published terrain cache.</summary>
        public SurfacePresentationInstance BuildContinuousSurfaceChunkInstance(
            string instanceKey, ContinuousSurfaceWorldMapDefinition terrain,
            OutdoorSurfaceCoordinateMapper mapper, SurfaceChunkCoord chunk)
        {
            if (terrain == null || mapper == null)
                throw new System.ArgumentNullException(terrain == null ? nameof(terrain) : nameof(mapper));
            RemoveLayoutInstance(instanceKey);
            BeginInstanceBuild(instanceKey, null, Vector2.zero);
            mapper.ChunkLocalToWorld(chunk, 0f, 0f, out var left, out var bottom);
            mapper.WorldToPresentation(left, bottom, out var px, out var py);
            var width = Mathf.RoundToInt(mapper.ChunkWidth / terrain.CellSize);
            var height = Mathf.RoundToInt(mapper.ChunkHeight / terrain.CellSize);
            var startX = Mathf.RoundToInt((left - terrain.OriginWorldX) / terrain.CellSize);
            var startY = Mathf.RoundToInt((bottom - terrain.OriginWorldY) / terrain.CellSize);
            var presentationCell = terrain.CellSize * mapper.PresentationUnitsPerWorldUnit;
            if (stampGrassGround && TryResolveCompactGrassTemplate())
                BuildCompactGrassGround(px, py, width, height, presentationCell);
            else
                PlaceZoneOverlay(px + width * presentationCell * .5f,
                    py + height * presentationCell * .5f, "plain_" + chunk,
                    width * presentationCell, height * presentationCell,
                    new Color(.30f, .42f, .26f, .98f));
            // Merge adjacent cells of the same authored terrain into one presentation rectangle.
            for (var y = 0; y < height; y++)
            {
                var terrainRow = terrain.BaseTerrainRows[startY + y];
                var forestRow = terrain.ForestRows[startY + y];
                var x = 0;
                while (x < width)
                {
                    var kind = terrainRow[startX + x];
                    var forest = forestRow[startX + x];
                    var end = x + 1;
                    while (end < width && terrainRow[startX + end] == kind && forestRow[startX + end] == forest)
                        end++;
                    Color color;
                    if (kind == 'M') color = new Color(.39f, .39f, .35f, .94f);
                    else if (kind == 'W') color = new Color(.13f, .39f, .66f, .86f);
                    else if (forest != '0') color = new Color(.11f, .29f, .13f, .48f + (forest - '0') * .035f);
                    else { x = end; continue; }
                    PlaceZoneOverlay(px + (x + end) * .5f * presentationCell,
                        py + (y + .5f) * presentationCell,
                        "terrain_" + chunk + "_" + y + "_" + x,
                        (end - x) * presentationCell, presentationCell, color);
                    x = end;
                }
            }
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

        /// <summary>Materializes only one loaded chunk from the complete checked-in geography grid.</summary>
        public SurfacePresentationInstance BuildOutdoorGeographyInstance(
            string instanceKey,
            OutdoorSurfaceGeographyDefinition geography,
            OutdoorSurfaceCoordinateMapper mapper,
            SurfaceChunkCoord ownerChunk)
        {
            if (geography?.Navigation == null) return null;
            RemoveLayoutInstance(instanceKey);
            BeginInstanceBuild(instanceKey, null, Vector2.zero);
            mapper.ChunkLocalToWorld(ownerChunk, 0f, 0f, out var chunkX, out var chunkY);
            var cellsX = Mathf.RoundToInt(mapper.ChunkWidth / geography.Navigation.CellSize);
            var cellsY = Mathf.RoundToInt(mapper.ChunkHeight / geography.Navigation.CellSize);
            for (var y = 0; y < cellsY; y++)
            {
                var runKind = SurfaceGroundCellKind.Ground;
                var runStart = 0;
                for (var x = 0; x <= cellsX; x++)
                {
                    var kind = SurfaceGroundCellKind.Ground;
                    if (x < cellsX)
                    {
                        var wx = chunkX + (x + .5f) * geography.Navigation.CellSize;
                        var wy = chunkY + (y + .5f) * geography.Navigation.CellSize;
                        geography.Navigation.TryGetCell(wx, wy, out kind);
                    }
                    if (x == 0) { runKind = kind; runStart = 0; continue; }
                    if (kind == runKind && x < cellsX) continue;
                    if (TryGetGeographyColor(runKind, out var color))
                    {
                        var left = chunkX + runStart * geography.Navigation.CellSize;
                        var right = chunkX + x * geography.Navigation.CellSize;
                        var bottom = chunkY + y * geography.Navigation.CellSize;
                        var top = bottom + geography.Navigation.CellSize;
                        mapper.WorldToPresentation((left + right) * .5f, (bottom + top) * .5f, out var px, out var py);
                        PlaceZoneOverlay(px, py, "geo_" + ownerChunk + "_" + y + "_" + runStart,
                            (right - left) * mapper.PresentationUnitsPerWorldUnit,
                            (top - bottom) * mapper.PresentationUnitsPerWorldUnit, color);
                    }
                    runKind = kind; runStart = x;
                }
            }
            for (var i = 0; i < geography.Landmarks.Count; i++)
            {
                var landmark = geography.Landmarks[i];
                if (mapper.WorldToChunk(landmark.WorldX, landmark.WorldY) != ownerChunk) continue;
                mapper.WorldToPresentation(landmark.WorldX, landmark.WorldY, out var px, out var py);
                var go = new GameObject(landmark.StableId ?? "W2A_Landmark");
                go.transform.SetParent(_buildRoot != null ? _buildRoot : mapRoot, false);
                go.transform.position = HostPresentationSpace.FromPresentation(px, py, HostPresentationSpace.EntityZ);
                var label = go.AddComponent<TextMesh>();
                label.text = landmark.Label ?? string.Empty; label.characterSize = .35f; label.fontSize = 32;
                label.color = new Color(.95f, .85f, .35f, 1f); label.anchor = TextAnchor.LowerCenter;
                TrackBuilt(go);
            }
            return EndInstanceBuild();
        }

        static bool TryGetGeographyColor(SurfaceGroundCellKind kind, out Color color)
        {
            if ((kind & SurfaceGroundCellKind.Solid) != 0) { color = new Color(.27f, .29f, .31f, .92f); return true; }
            if ((kind & SurfaceGroundCellKind.Bridge) != 0) { color = new Color(.55f, .36f, .16f, .94f); return true; }
            if ((kind & SurfaceGroundCellKind.Water) != 0) { color = new Color(.12f, .42f, .72f, .86f); return true; }
            if ((kind & SurfaceGroundCellKind.Road) != 0) { color = new Color(.62f, .50f, .31f, .72f); return true; }
            color = default; return false;
        }

        public static bool TryEstimateOutdoorRenderedObjectCount(
            OutdoorSurfacePlacementDefinition placement,
            out int count)
        {
            count = 0;
            if (placement == null || placement.SourceCellsW <= 0 || placement.SourceCellsH <= 0 ||
                !MapKindCatalog.TryGet(placement.Kind ?? string.Empty, out var info))
                return false;
            if (!UsesPerCellIdentity(placement, info))
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

            var stateful = OutdoorStatefulObjectSemantics.IsStatefulKind(source.Kind);
            var perCell = UsesPerCellIdentity(source, info);
            if (!stateful && info.Mode == MapKindCatalog.StampMode.ZoneOverlay)
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
            else if (!perCell)
            {
                var cx = minX + width * .5f; var cy = minY + height * .5f;
                mapper.PresentationToWorld(cx, cy, out var centerWorldX, out var centerWorldY);
                if (mapper.WorldToChunk(centerWorldX, centerWorldY) == ownerChunk)
                {
                    if (OutdoorStatefulPlacementResolver.IsDestructibleKind(info.Kind) &&
                        OutdoorStatefulPlacementResolver.IsDestroyed(
                            _session?.World?.OutdoorStatefulObjects, id))
                        return;
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
                    var cellId = OutdoorStatefulObjectId.ForCell(id, gx, gy);
                    if (OutdoorStatefulPlacementResolver.IsDestructibleKind(info.Kind) &&
                        OutdoorStatefulPlacementResolver.IsDestroyed(
                            _session?.World?.OutdoorStatefulObjects, cellId))
                        continue;
                    expectedForOwner++;
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

        static bool UsesPerCellIdentity(
            OutdoorSurfacePlacementDefinition placement,
            MapKindCatalog.KindInfo info) =>
            OutdoorStatefulObjectSemantics.IsStatefulKind(placement?.Kind)
                ? OutdoorStatefulObjectSemantics.UsesPerCellIdentity(placement.Kind)
                : info.Mode == MapKindCatalog.StampMode.PerCell;

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
            if (_meshesByInstance.TryGetValue(instanceKey, out var meshes))
                for (var i = 0; i < meshes.Count; i++)
                    DestroyBuilt(meshes[i]);
            _instances.Remove(instanceKey);
            _builtByInstance.Remove(instanceKey);
            _meshesByInstance.Remove(instanceKey);
            instance.IsLoaded = false;
            return true;
        }

        static bool TryPickLayout(PlayableHostSession session, out MapLayoutDefinition layout) =>
            MapLayoutPick.TryGet(session, out layout);

        internal bool DeferInstanceActivation { get; set; }

        internal void ActivateInstanceOwner(string ownerPrefix)
        {
            foreach (var pair in _instances)
                if (pair.Key.StartsWith(ownerPrefix, System.StringComparison.Ordinal) && pair.Value.Root != null)
                    pair.Value.Root.gameObject.SetActive(true);
        }

        void BeginInstanceBuild(string instanceKey, MapLayoutDefinition layout, Vector2 placementOffset)
        {
            EnsureRoot();
            var root = new GameObject("SurfaceInstance_" + instanceKey).transform;
            root.SetParent(mapRoot, false);
            root.gameObject.SetActive(!DeferInstanceActivation);
            _buildRoot = root;
            _buildPlacementOffset = placementOffset;
            _buildingInstanceKey = instanceKey;
            _builtByInstance[instanceKey] = new List<GameObject>();
            _meshesByInstance[instanceKey] = new List<Mesh>();
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

        void BuildFromLayout(MapLayoutDefinition layout, bool compactGround = false)
        {
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var ox = PlaceX(layout.OriginX);
            var oy = PlaceY(layout.OriginY);
            var w = layout.Width;
            var h = layout.Height;
            if (w < 1 || h < 1)
                return;

            if (stampGrassGround && compactGround)
            {
                BuildCompactGrassGround(ox, oy, w, h, cs);
            }
            else if (stampGrassGround)
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

        void BuildCompactGrassGround(float originX, float originY, int width, int height, float cellSize)
        {
            if (!TryResolveCompactGrassTemplate())
            {
                BuildPrefabGrassGround(originX, originY, width, height, cellSize);
                return;
            }

            var step = Mathf.Max(1, grassStride);
            var tileWidth = cellSize * step;
            var tileHeight = cellSize * step;
            for (var layerIndex = 0; layerIndex < _compactGrassLayers.Count; layerIndex++)
            {
                var layer = _compactGrassLayers[layerIndex];
                var vertices = new List<Vector3>();
                var colors = new List<Color>();
                var uvs = new List<Vector2>();
                var triangles = new List<int>();
                for (var gy = 0; gy < height; gy += step)
                for (var gx = 0; gx < width; gx += step)
                {
                    var centerX = originX + (gx + .5f) * cellSize;
                    var centerY = originY + (gy + .5f) * cellSize;
                    var vertexStart = vertices.Count;
                    for (var corner = 0; corner < 4; corner++)
                    {
                        vertices.Add(new Vector3(
                            centerX + layer.Corners[corner].x * tileWidth,
                            centerY + layer.Corners[corner].y * tileHeight,
                            0f));
                        colors.Add(layer.Color);
                        uvs.Add(layer.Uvs[corner]);
                    }
                    triangles.Add(vertexStart); triangles.Add(vertexStart + 1); triangles.Add(vertexStart + 2);
                    triangles.Add(vertexStart); triangles.Add(vertexStart + 2); triangles.Add(vertexStart + 3);
                }

                var go = new GameObject("ChunkGrassLayer_" + layerIndex);
                go.transform.SetParent(_buildRoot != null ? _buildRoot : mapRoot, false);
                go.transform.position = new Vector3(0f, 0f, HostPresentationSpace.GroundZ);
                var filter = go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                var mesh = new Mesh { name = "RuntimeChunkGrass_" + layerIndex };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = layer.Material;
                renderer.sortingLayerID = layer.SortingLayerId;
                renderer.sortingOrder = layer.SortingOrder;
                if (layer.Texture != null)
                {
                    var properties = new MaterialPropertyBlock();
                    properties.SetTexture("_MainTex", layer.Texture);
                    renderer.SetPropertyBlock(properties);
                }
                _meshesByInstance[_buildingInstanceKey].Add(mesh);
                TrackBuilt(go);
            }
        }

        void BuildPrefabGrassGround(float originX, float originY, int width, int height, float cellSize)
        {
            var step = Mathf.Max(1, grassStride);
            for (var gy = 0; gy < height; gy += step)
            for (var gx = 0; gx < width; gx += step)
                PlacePrefab("grass", MapKindCatalog.Grass,
                    originX + (gx + .5f) * cellSize,
                    originY + (gy + .5f) * cellSize,
                    "Grass_" + gx + "_" + gy, cellSize * step, cellSize * step,
                    new Color(.30f, .42f, .26f));
        }

        bool TryResolveCompactGrassTemplate()
        {
            if (_compactGrassTemplateResolved)
                return _compactGrassLayers.Count > 0;
            _compactGrassTemplateResolved = true;
            if (!MapLayoutPrefabResolver.TryInstantiate(
                    "grass", MapKindCatalog.Grass, out var source, warnOnMissing: false) ||
                source == null)
                return false;

            try
            {
                var renderers = source.GetComponentsInChildren<SpriteRenderer>(true);
                var totalBounds = default(Bounds);
                var hasBounds = false;
                for (var i = 0; i < renderers.Length; i++)
                {
                    var candidate = renderers[i];
                    if (candidate == null || !candidate.enabled || candidate.sprite == null ||
                        !candidate.gameObject.activeInHierarchy)
                        continue;
                    if (!hasBounds) { totalBounds = candidate.bounds; hasBounds = true; }
                    else totalBounds.Encapsulate(candidate.bounds);
                }
                if (!hasBounds || totalBounds.size.x < .0001f || totalBounds.size.y < .0001f)
                    return false;
                for (var i = 0; i < renderers.Length; i++)
                {
                    var spriteRenderer = renderers[i];
                    if (spriteRenderer == null || !spriteRenderer.enabled ||
                        spriteRenderer.sprite == null || !spriteRenderer.gameObject.activeInHierarchy)
                        continue;
                    var sprite = spriteRenderer.sprite;
                    var spriteBounds = sprite.bounds;
                    var localCorners = new[]
                    {
                        new Vector3(spriteBounds.min.x, spriteBounds.min.y),
                        new Vector3(spriteBounds.min.x, spriteBounds.max.y),
                        new Vector3(spriteBounds.max.x, spriteBounds.max.y),
                        new Vector3(spriteBounds.max.x, spriteBounds.min.y)
                    };
                    var layer = new CompactGrassLayer
                    {
                        Material = spriteRenderer.sharedMaterial,
                        Texture = sprite.texture,
                        Color = spriteRenderer.color,
                        SortingLayerId = spriteRenderer.sortingLayerID,
                        SortingOrder = spriteRenderer.sortingOrder
                    };
                    for (var corner = 0; corner < 4; corner++)
                    {
                        var point = spriteRenderer.transform.TransformPoint(localCorners[corner]);
                        layer.Corners[corner] = new Vector2(
                            (point.x - totalBounds.center.x) / totalBounds.size.x,
                            (point.y - totalBounds.center.y) / totalBounds.size.y);
                    }
                    var outer = DataUtility.GetOuterUV(sprite);
                    layer.Uvs[0] = new Vector2(outer.x, outer.y);
                    layer.Uvs[1] = new Vector2(outer.x, outer.w);
                    layer.Uvs[2] = new Vector2(outer.z, outer.w);
                    layer.Uvs[3] = new Vector2(outer.z, outer.y);
                    if (spriteRenderer.flipX)
                    {
                        Swap(layer.Uvs, 0, 3);
                        Swap(layer.Uvs, 1, 2);
                    }
                    if (spriteRenderer.flipY)
                    {
                        Swap(layer.Uvs, 0, 1);
                        Swap(layer.Uvs, 3, 2);
                    }
                    _compactGrassLayers.Add(layer);
                }
                return _compactGrassLayers.Count > 0;
            }
            finally
            {
                source.SetActive(false);
                DestroyBuilt(source);
            }
        }

        static void Swap(Vector2[] values, int a, int b)
        {
            var value = values[a];
            values[a] = values[b];
            values[b] = value;
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
            if (HostWorldObjectPickGeometry.TryGetInteractionBounds(go, out var interactionBounds))
                d.ConfigureInteractionBounds(interactionBounds);
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
            if (_session?.World?.LocalPlaces == null)
                return false;
            if (!_session.World.ContinuousOutdoorMaterialization.TryGetAnyPlace(p.BoundLocationId, out var loc) &&
                !_session.World.LocalPlaces.TryGet(p.BoundLocationId, out loc))
                return !_session.World.LocalMap.IsInInterior &&
                       string.Equals(MapKindCatalog.NormalizeKind(p.Kind), "cave", System.StringComparison.Ordinal);
            if (!OpportunityEntranceRules.IsHiddenEntrance(loc))
                return false;
            return !OpportunityEntranceRules.IsRevealedToPlayerParty(_session.World, loc);
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
            if (HostWorldObjectPickGeometry.TryGetInteractionBounds(go, out var interactionBounds))
                plot.ConfigureInteractionBounds(interactionBounds);

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

        static void DestroyBuilt(Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
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
