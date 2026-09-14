using System;
using UnityEngine;
using XianXia.Core.Construction;
using XianXia.Core.Results;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Farm placement input and geometry. Domain validation and payment belong to Core.</summary>
    public sealed class HostFarmFieldConstructionPresenter : MonoBehaviour
    {
        const string InputOwner = "FarmFieldPlacement";
        PlayableHostBootstrap _bootstrap;
        XianXia.Core.Simulation.SimulationWorld _world;
        BuildingConstructionSpec _spec;
        GameObject _preview;
        Sprite _sprite;
        SpriteRenderer _renderer;
        bool _placing, _legal;
        int _beganFrame;
        int _releaseInputFrame = -1;
        string _status = string.Empty;
        OutdoorConstructedAssetState _candidate;

        void Awake() => _bootstrap = GetComponent<PlayableHostBootstrap>();
        void OnDisable() => CancelPlacement();
        void OnDestroy() { CancelPlacement(); if (_sprite != null) Destroy(_sprite); }

        public Result BeginConstructionPlacement(string buildingId)
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || !world.ConstructionCatalog.TryGet(buildingId, out var spec) ||
                spec.PlacementKind != ConstructionPlacementKind.FarmField)
                return Result.Failure(ErrorCode.InvalidArgument, "农田建筑定义无效。");
            CancelPlacement();
            _bootstrap.GetComponent<HostFactionFlagPresenter>()?.CancelPlacement();
            _world = world;
            _spec = spec;
            _placing = true;
            _beganFrame = Time.frameCount;
            _status = "移动鼠标选择位置；左键建造，Esc／右键取消。";
            HostInputGate.Acquire(InputOwner);
            return Result.Success();
        }

        public void CancelPlacement()
        {
            HostInputGate.Release(InputOwner);
            _releaseInputFrame = -1;
            _placing = false;
            _legal = false;
            _candidate = null;
            if (_preview != null) Destroy(_preview);
            _preview = null;
            _renderer = null;
        }

        void FinishInputGesture()
        {
            CancelPlacement();
            // Consume this frame's confirm/cancel gesture before returning input to movement.
            HostInputGate.Acquire(InputOwner);
            _releaseInputFrame = Time.frameCount;
        }

        void Update()
        {
            if (_releaseInputFrame >= 0 && Time.frameCount > _releaseInputFrame)
            { HostInputGate.Release(InputOwner); _releaseInputFrame = -1; }
            if (!_placing) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            { FinishInputGesture(); return; }
            if (_world != _bootstrap?.Session?.World ||
                _bootstrap.ConstructionPanel?.IsOpen == true || _bootstrap.WorldMapPanel?.IsOpen == true ||
                _bootstrap.InventoryPanel?.IsOpen == true || _world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.None)
            { CancelPlacement(); return; }
            if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition) ||
                !HostPresentationSpace.TryRaycastPlane(Camera.main, Input.mousePosition, out var point))
            { _legal = false; if (_preview != null) _preview.SetActive(false); return; }
            _legal = Prepare(point, out _candidate, out _status);
            ShowPreview();
            if (Time.frameCount <= _beganFrame || !Input.GetMouseButtonDown(0) || !_legal) return;
            // Recompute both geometry and authority at confirmation time.
            if (!Prepare(point, out _candidate, out _status)) { _legal = false; return; }
            var result = ConstructionService.TryConstructFarmField(_world, _spec.BuildingId,
                _world.Strategic.PlayerFactionId, _candidate.SurfaceId, _candidate.WorldX, _candidate.WorldY, out _);
            if (result.IsFailure) { _status = result.Error.Message; _legal = false; return; }
            _bootstrap.ContinuousOutdoorSurfaceRuntime.RefreshRuntimeConstructedPlacementsForLoadedChunks();
            FinishInputGesture();
        }

        bool Prepare(Vector3 point, out OutdoorConstructedAssetState candidate, out string reason)
        {
            candidate = null;
            reason = "当前空间或战斗阶段不允许建造农田。";
            var continuous = _bootstrap.ContinuousOutdoorSurfaceRuntime;
            if (continuous == null || !continuous.IsActive || _world.LocalMap.IsInInterior ||
                _world.PlayerPartyTravel.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                _world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.None) return false;
            if (!_world.SurfaceSpatial.TryGet(continuous.ActiveSurfaceId, out var metric)) return false;
            continuous.Mapper.PresentationToWorld(point.x, point.y, out var wx, out var wy);
            var x = metric.OriginWorldX + (float)Math.Floor((wx - metric.OriginWorldX) / metric.CellSize - _spec.FootprintCellsW * .5f + .5f) * metric.CellSize;
            var y = metric.OriginWorldY + (float)Math.Floor((wy - metric.OriginWorldY) / metric.CellSize - _spec.FootprintCellsH * .5f + .5f) * metric.CellSize;
            candidate = new OutdoorConstructedAssetState {
                StableAssetId = _world.OutdoorConstructedAssets.NextId, BuildingId = _spec.BuildingId,
                Kind = _spec.OutdoorKind, SurfaceId = continuous.ActiveSurfaceId,
                WorldX = x, WorldY = y, WorldWidth = _spec.FootprintCellsW * metric.CellSize,
                WorldHeight = _spec.FootprintCellsH * metric.CellSize,
                CellsW = _spec.FootprintCellsW, CellsH = _spec.FootprintCellsH,
                BoundLocationId = "location:runtime:farm:" + _world.OutdoorConstructedAssets.NextId
            };
            var permission = OutdoorAdministrativeConstructionAuthorizationService.Validate(
                _world, _world.Strategic.PlayerFactionId, candidate);
            if (permission.IsFailure) { reason = permission.Error.Message; return false; }
            if (!ConstructionService.HasRequiredMaterials(_world, _spec, out _))
            { reason = "建造材料不足。"; return false; }
            if (!continuous.TryGetCompositeWalkGrid(out var grid))
            { reason = "超出当前已加载区域。"; return false; }
            foreach (var cell in candidate.CellAnchors())
            {
                if (!continuous.IsWorldPositionLoaded(candidate.SurfaceId, cell.WorldX, cell.WorldY))
                { reason = "超出当前已加载区域。"; return false; }
                continuous.Mapper.WorldToPresentation(cell.WorldX, cell.WorldY, out var px, out var py);
                if (!grid.TryWorldToCell(px, py, out var gx, out var gy) || !grid.IsWalkable(gx, gy))
                { reason = "此处有障碍或不可通行地形。"; return false; }
            }
            continuous.Mapper.WorldToPresentation(x, y, out var left, out var bottom);
            continuous.Mapper.WorldToPresentation(x + candidate.WorldWidth, y + candidate.WorldHeight, out var right, out var top);
            var rect = Rect.MinMaxRect(left, bottom, right, top);
            var party = _bootstrap.Session.PlayerParty;
            if (party == null || !party.HasActive || _bootstrap.ViewSpawner?.Registry == null ||
                !_bootstrap.ViewSpawner.Registry.TryGet(party.ActiveCharacterId, out var active) || active == null)
            { reason = "当前没有可执行建造的主控角色。"; return false; }
            if (Vector2.Distance(HostPresentationSpace.ToPresentation(active.transform.position), rect.center) > 12f)
            { reason = "落点距离主控过远。"; return false; }
            foreach (var plot in HostMapObjectRegistry.AllPlots)
                if (plot != null && Overlaps(rect, plot.gameObject))
                { reason = "此处已有农田或世界对象。"; return false; }
            foreach (var obj in HostMapObjectRegistry.AllDestructibles)
                if (obj != null && !obj.IsDestroyed && Overlaps(rect, obj.gameObject))
                { reason = "此处有树木、墙或控制核心。"; return false; }
            foreach (var existing in _world.OutdoorConstructedAssets.Assets.Values)
                if (OutdoorConstructedAssetBoard.Overlaps(existing, candidate))
                { reason = "此处已有农田。"; return false; }
            // Flag footprints also contribute to the composite grid above.
            reason = "此处可以建造农田。";
            return true;
        }

        static bool Overlaps(Rect rect, GameObject go)
        {
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>())
            {
                var b = renderer.bounds;
                if (rect.Overlaps(Rect.MinMaxRect(b.min.x + .001f, b.min.y + .001f, b.max.x - .001f, b.max.y - .001f))) return true;
            }
            return rect.Contains(HostPresentationSpace.ToPresentation(go.transform.position));
        }

        void ShowPreview()
        {
            if (_candidate == null) { if (_preview != null) _preview.SetActive(false); return; }
            if (_preview == null)
            {
                if (_sprite == null) _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
                _preview = new GameObject("FarmPlacementPreview");
                _preview.transform.SetParent(transform, false);
                _renderer = _preview.AddComponent<SpriteRenderer>();
                _renderer.sprite = _sprite;
                _renderer.sortingOrder = 710;
            }
            _preview.SetActive(true);
            var mapper = _bootstrap.ContinuousOutdoorSurfaceRuntime.Mapper;
            mapper.WorldToPresentation(_candidate.WorldX, _candidate.WorldY, out var x, out var y);
            mapper.WorldToPresentation(_candidate.WorldX + _candidate.WorldWidth, _candidate.WorldY + _candidate.WorldHeight, out var r, out var t);
            _preview.transform.position = HostPresentationSpace.FromPresentation((x + r) * .5f, (y + t) * .5f, HostPresentationSpace.BuildingZ);
            _preview.transform.localScale = new Vector3(r - x, t - y, 1f);
            _renderer.color = _legal ? new Color(.25f, 1f, .35f, .5f) : new Color(1f, .2f, .2f, .5f);
        }

        void OnGUI()
        {
            if (!_placing) return;
            var rect = new Rect(Screen.width - 350f, Screen.height - 150f, 338f, 136f);
            HostUiHitTest.Block(rect);
            GUI.Box(rect, "农田 · " + _spec.FootprintCellsW + " × " + _spec.FootprintCellsH);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 28f, rect.width - 24f, 38f), "左键建造　Esc／右键取消");
            GUI.color = _legal ? Color.green : new Color(1f, .45f, .4f);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 65f, rect.width - 24f, 60f), (_legal ? "✓ " : "✕ ") + _status);
            GUI.color = Color.white;
        }
    }
}
