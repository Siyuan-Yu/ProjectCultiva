using UnityEngine;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>Wilderness LocalMap 阵营旗建筑表现与鼠标落点模式。</summary>
    public sealed class HostFactionFlagPresenter : MonoBehaviour
    {
        PlayableHostBootstrap _bootstrap;
        GameObject _visual;
        GameObject _preview;
        TextMesh _label;
        string _shownFlagId = string.Empty;
        string _status = string.Empty;
        bool _placing;
        bool _previewLegal;
        float _previewX;
        float _previewZ;

        void Awake() => _bootstrap = GetComponent<PlayableHostBootstrap>();
        void OnDestroy() { DestroyVisual(); DestroyPreview(); }

        void Update()
        {
            var world = _bootstrap?.Session?.World;
            if (!TryGetWildernessContext(world, out var context))
            {
                CancelPlacement(); DestroyVisual(); return;
            }

            if (world.Strategic.FactionFlags.TryGetAt(context.WildernessHex, out var flag) && flag != null)
            {
                CancelPlacement();
                EnsureVisual(flag);
                if (_label != null)
                    _label.text = StrategicFactionCatalog.DisplayName(flag.FactionId) +
                                  "\nHP " + flag.CurrentHp + "/" + flag.MaxHp;
            }
            else
            {
                DestroyVisual();
                if (_placing)
                    UpdatePlacementPreview(context.WildernessHex);
            }
        }

        void OnGUI()
        {
            var world = _bootstrap?.Session?.World;
            if (!TryGetWildernessContext(world, out var context))
                return;

            var rect = new Rect(Screen.width - 190f, Screen.height - 104f, 178f, 90f);
            HostUiHitTest.Block(rect);
            GUI.Box(rect, "阵营旗");
            var gate = FactionFlagPlacementAuthorization.CanBeginPlacement(
                world, world.Strategic.PlayerFactionId, context.WildernessHex, out var gain);
            GUI.enabled = gate.IsSuccess;
            if (GUI.Button(new Rect(rect.x + 8f, rect.y + 25f, rect.width - 16f, 26f),
                    _placing ? "取消放置" : "选择立旗位置"))
            {
                if (_placing) CancelPlacement(); else BeginPlacement();
            }
            GUI.enabled = true;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 55f, rect.width - 16f, 30f),
                gate.IsFailure
                    ? gate.Error.Message
                    : (string.IsNullOrEmpty(_status) ? "可新增无主格：" + gain : _status));
        }

        void BeginPlacement()
        {
            _placing = true;
            _status = "移动鼠标预览；左键确认，Esc 取消";
            HostInputGate.BlockWorldInteraction = true;
        }

        void CancelPlacement()
        {
            if (_placing)
                HostInputGate.BlockWorldInteraction = false;
            _placing = false;
            DestroyPreview();
        }

        void UpdatePlacementPreview(XianXia.Core.World.Hex.HexCoord anchor)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelPlacement(); return; }
            if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                return;
            var camera = Camera.main;
            if (camera == null || !HostPresentationSpace.TryRaycastPlane(camera, Input.mousePosition, out var wp) ||
                !MapLayoutPick.TryGet(_bootstrap.Session, out var layout) || layout == null)
                return;
            var p = HostPresentationSpace.ToPresentation(wp);
            var baseGrid = MapLayoutWalkGridBuilder.Create(layout);
            _previewLegal = HostFactionFlagQuery.TryResolveLegalCenterAt(
                layout, baseGrid, p.x, p.y, out _previewX, out _previewZ);
            EnsurePreview();
            PositionBuilding(_preview, _previewX, _previewZ, layout);
            Tint(_preview, _previewLegal ? new Color(.35f, 1f, .45f, .55f) : new Color(1f, .25f, .2f, .55f));
            if (Input.GetMouseButtonDown(0) && _previewLegal)
                PlaceFlag(anchor, _previewX, _previewZ);
        }

        void PlaceFlag(XianXia.Core.World.Hex.HexCoord anchor, float x, float z)
        {
            var world = _bootstrap.Session.World;
            var flagId = "flag:player:" + world.Tick.Value + ":" + anchor.Q + ":" + anchor.R;
            var result = FactionFlagService.TryPlace(
                world, flagId, world.Strategic.PlayerFactionId, anchor,
                FactionFlagService.NextEstablishedOrder(world), x, z, true);
            _status = result.IsSuccess ? "立旗成功" : result.Error.Message;
            if (result.IsSuccess)
            {
                _status = string.Empty;
                CancelPlacement();
                _bootstrap.RefreshFactionFlagWalkGrid();
            }
        }

        bool TryGetWildernessContext(XianXia.Core.Simulation.SimulationWorld world,
            out LoadedLocalMapBelongingQuery.LoadedLocalMapContext context)
        {
            context = default;
            return world != null && _bootstrap.Session.IsInitialized &&
                   (_bootstrap.WorldMapPanel == null || !_bootstrap.WorldMapPanel.IsOpen) &&
                   LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out context) &&
                   context.Kind == LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex;
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
        void DestroyPreview() { if (_preview != null) Destroy(_preview); _preview = null; }
    }
}
