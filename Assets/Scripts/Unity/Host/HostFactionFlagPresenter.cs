using UnityEngine;
using XianXia.Core.Construction;
using XianXia.Core.Results;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>FactionFlag visual, footprint geometry and construction placement interaction.</summary>
    public sealed class HostFactionFlagPresenter : MonoBehaviour
    {
        PlayableHostBootstrap _bootstrap;
        GameObject _visual;
        GameObject _preview;
        TextMesh _label;
        string _shownFlagId = string.Empty;
        string _buildingId = string.Empty;
        string _status = string.Empty;
        bool _placing;
        bool _geometryLegal;
        bool _domainLegal;
        bool _overallLegal;
        float _previewX;
        float _previewZ;

        void Awake() => _bootstrap = GetComponent<PlayableHostBootstrap>();
        void OnDestroy() { DestroyVisual(); DestroyPreview(); }

        public Result BeginConstructionPlacement(string buildingId)
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || !world.ConstructionCatalog.TryGet(buildingId, out var spec) || spec == null)
                return Result.Failure(ErrorCode.NotFound, "建筑定义不存在。", buildingId);
            if (spec.PlacementKind != ConstructionPlacementKind.FactionFlag)
                return Result.Failure(ErrorCode.InvalidOperation, "此放置器不支持该建筑。", buildingId);
            _buildingId = buildingId;
            _placing = true;
            _status = "移动鼠标选择位置；左键建造，Esc／右键取消。";
            HostInputGate.BlockWorldInteraction = true;
            return Result.Success();
        }

        void Update()
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || !_bootstrap.Session.IsInitialized)
            {
                CancelPlacement();
                DestroyVisual();
                return;
            }

            var hasContext = LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var context);
            var wilderness = hasContext &&
                context.Kind == LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex;
            if (wilderness && world.Strategic.FactionFlags.TryGetAt(context.WildernessHex, out var flag) && flag != null)
            {
                EnsureVisual(flag);
                if (_label != null)
                    _label.text = StrategicFactionCatalog.DisplayName(flag.FactionId) +
                                  "\nHP " + flag.CurrentHp + "/" + flag.MaxHp;
            }
            else
                DestroyVisual();

            if (_placing)
                UpdatePlacementPreview(wilderness, wilderness ? context.WildernessHex : default);
            else
                DestroyPreview();
        }

        void OnGUI()
        {
            if (!_placing)
                return;
            var rect = new Rect(Screen.width - 286f, Screen.height - 126f, 274f, 112f);
            HostUiHitTest.Block(rect);
            GUI.Box(rect, "势力控制建筑");
            GUI.Label(new Rect(rect.x + 10f, rect.y + 25f, rect.width - 20f, 40f),
                "移动鼠标选择位置\n左键：建造　Esc／右键：取消");
            GUI.color = _overallLegal ? new Color(.45f, 1f, .55f) : new Color(1f, .45f, .4f);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 68f, rect.width - 20f, 38f),
                (_overallLegal ? "✓ " : "✕ ") + _status);
            GUI.color = Color.white;
        }

        public void CancelPlacement()
        {
            if (_placing)
                HostInputGate.BlockWorldInteraction = false;
            _placing = false;
            _buildingId = string.Empty;
            _status = string.Empty;
            _geometryLegal = _domainLegal = _overallLegal = false;
            DestroyPreview();
        }

        void UpdatePlacementPreview(bool wilderness, HexCoord anchor)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            var world = _bootstrap.Session.World;
            _domainLegal = false;
            var domainReason = "此建筑只能建造在野外 LocalMap。";
            if (wilderness)
            {
                var domain = FactionFlagService.ValidatePlacement(
                    world, world.Strategic.PlayerFactionId, anchor, out _);
                _domainLegal = domain.IsSuccess;
                domainReason = domain.IsSuccess ? string.Empty : domain.Error.Message;
            }

            _geometryLegal = false;
            if (!HostUiHitTest.ContainsScreenPoint(Input.mousePosition) &&
                Camera.main != null &&
                HostPresentationSpace.TryRaycastPlane(Camera.main, Input.mousePosition, out var wp) &&
                MapLayoutPick.TryGet(_bootstrap.Session, out var layout) && layout != null)
            {
                var p = HostPresentationSpace.ToPresentation(wp);
                var baseGrid = MapLayoutWalkGridBuilder.Create(layout);
                _geometryLegal = HostFactionFlagQuery.TryResolveLegalCenterAt(
                    layout, baseGrid, p.x, p.y, out _previewX, out _previewZ);
                EnsurePreview();
                PositionBuilding(_preview, _previewX, _previewZ, layout);
                _overallLegal = _geometryLegal && _domainLegal;
                Tint(_preview, _overallLegal
                    ? new Color(.35f, 1f, .45f, .55f)
                    : new Color(1f, .25f, .2f, .55f));
            }
            else
            {
                _overallLegal = false;
                DestroyPreview();
            }

            _status = !_domainLegal
                ? domainReason
                : (!_geometryLegal ? "此处有障碍或会阻挡出口。" : "此处可以建造。");

            if (Input.GetMouseButtonDown(0) && _overallLegal &&
                !HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                PlaceFlag(anchor, _previewX, _previewZ);
        }

        void PlaceFlag(HexCoord anchor, float x, float z)
        {
            var world = _bootstrap.Session.World;
            var result = ConstructionService.TryConstructFactionFlag(
                world, _buildingId, world.Strategic.PlayerFactionId, anchor, x, z, out _);
            _status = result.IsSuccess ? "建造成功。" : result.Error.Message;
            if (!result.IsSuccess)
                return;
            CancelPlacement();
            _bootstrap.RefreshFactionFlagWalkGrid();
        }

        void EnsureVisual(FactionFlagState flag)
        {
            if (_visual == null || _shownFlagId != flag.FlagId)
            {
                DestroyVisual();
                _shownFlagId = flag.FlagId;
                _visual = InstantiateBuilding("FactionControlPost_" + flag.FlagId);
                _visual.transform.SetParent(transform, false);
                _visual.AddComponent<HostFactionFlagView>().Bind(flag.FlagId);
                var labelObject = new GameObject("FlagStatus");
                labelObject.transform.SetParent(_visual.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, 2.4f, -.1f);
                _label = labelObject.AddComponent<TextMesh>();
                _label.characterSize = .1f; _label.fontSize = 25;
                _label.anchor = TextAnchor.LowerCenter; _label.alignment = TextAlignment.Center;
                _label.color = Color.white;
                var mr = labelObject.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 722;
            }
            if (!MapLayoutPick.TryGet(_bootstrap.Session, out var layout) || layout == null)
                return;
            var baseGrid = MapLayoutWalkGridBuilder.Create(layout);
            if (HostFactionFlagQuery.TryResolvePosition(flag, layout, baseGrid, out var x, out var z))
                PositionBuilding(_visual, x, z, layout);
        }

        void EnsurePreview()
        {
            if (_preview != null) return;
            _preview = InstantiateBuilding("FactionControlPost_Preview");
            _preview.transform.SetParent(transform, false);
        }

        static GameObject InstantiateBuilding(string name)
        {
            if (!MapKindCatalog.TryGet("factionControlPost", out var info) ||
                !MapLayoutPrefabResolver.TryInstantiate(info.Kind, info.PrefabPath, out var go))
            {
                go = new GameObject(name);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = HostSpriteFactory.MissingPrefabSprite();
                sr.sortingOrder = 710;
            }
            go.name = name;
            return go;
        }

        static void PositionBuilding(GameObject go, float x, float z, MapLayoutDefinition layout)
        {
            if (go == null) return;
            var cs = layout != null && layout.CellSize > 0f ? layout.CellSize : 1f;
            var intended = HostPresentationSpace.FromPresentation(x, z, HostPresentationSpace.BuildingZ);
            go.transform.localScale = Vector3.one;
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                if (bounds.size.x > .001f && bounds.size.y > .001f)
                    go.transform.localScale = new Vector3(
                        HostFactionFlagQuery.FootprintCells * cs / bounds.size.x,
                        HostFactionFlagQuery.FootprintCells * cs / bounds.size.y, 1f);
            }
            go.transform.position = intended;
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                go.transform.position += intended - bounds.center;
            }
        }

        static void Tint(GameObject go, Color color)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < rs.Length; i++) rs[i].color = color;
        }

        void DestroyVisual()
        {
            if (_visual != null) Destroy(_visual);
            _visual = null; _label = null; _shownFlagId = string.Empty;
        }

        void DestroyPreview()
        {
            if (_preview != null) Destroy(_preview);
            _preview = null;
        }
    }
}
