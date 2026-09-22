using XianXia.Core.World;
using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Construction;
using XianXia.Core.Results;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>FactionFlag visual, footprint geometry and construction placement interaction.</summary>
    public sealed class HostFactionFlagPresenter : MonoBehaviour
    {
        PlayableHostBootstrap _bootstrap;
        sealed class FlagVisual
        {
            public GameObject Root;
            public TextMesh Label;
        }
        readonly Dictionary<string, FlagVisual> _visuals =
            new Dictionary<string, FlagVisual>(StringComparer.Ordinal);
        GameObject _preview;
        string _buildingId = string.Empty;
        string _status = string.Empty;
        bool _placing;
        bool _geometryLegal;
        bool _domainLegal;
        bool _overallLegal;
        float _previewX;
        float _previewZ;

        void Awake() => _bootstrap = GetComponent<PlayableHostBootstrap>();
        void OnDestroy() { DestroyAllVisuals(); DestroyPreview(); }

        public Result BeginConstructionPlacement(string buildingId)
        {
            var world = _bootstrap?.Session?.World;
            if (world == null || !world.ConstructionCatalog.TryGet(buildingId, out var spec) || spec == null)
                return Result.Failure(ErrorCode.NotFound, "建筑定义不存在。", buildingId);
            if (spec.PlacementKind != ConstructionPlacementKind.FactionFlag)
                return Result.Failure(ErrorCode.InvalidOperation, "此放置器不支持该建筑。", buildingId);
            _bootstrap.GetComponent<HostFarmFieldConstructionPresenter>()?.CancelPlacement();
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
                DestroyAllVisuals();
                return;
            }

            var continuous = _bootstrap.ContinuousOutdoorSurfaceRuntime != null &&
                             _bootstrap.ContinuousOutdoorSurfaceRuntime.IsActive &&
                             world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldPosition;
            SyncVisuals(world, continuous);

            if (_placing)
                UpdatePlacementPreview();
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

        void UpdatePlacementPreview()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            var world = _bootstrap.Session.World;
            var continuous = _bootstrap.ContinuousOutdoorSurfaceRuntime != null &&
                             _bootstrap.ContinuousOutdoorSurfaceRuntime.IsActive &&
                             world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldPosition;
            _domainLegal = false;
            var domainReason = continuous
                ? "请选择合法的 Continuous Outdoor 落点。"
                : "新据点核心只能建造在 Continuous Outdoor。";

            _geometryLegal = false;
            if (!HostUiHitTest.ContainsScreenPoint(Input.mousePosition) &&
                Camera.main != null &&
                HostPresentationSpace.TryRaycastPlane(Camera.main, Input.mousePosition, out var wp))
            {
                var p = HostPresentationSpace.ToPresentation(wp);
                FactionFlagSitePlacementRequest request = null;
                var requestFailure = string.Empty;
                _previewX = p.x;
                _previewZ = p.y;
                if (continuous)
                {
                    var prepared = TryPrepareFactionFlagSitePlacement(
                        p.x, p.y, out request, out _previewX, out _previewZ,
                        out _geometryLegal, out requestFailure);
                    if (prepared &&
                        world.ConstructionCatalog.TryGet(_buildingId, out var spec) && spec != null)
                    {
                        var domain = FactionFlagService.ValidateSiteCorePlacement(
                            world, world.Strategic.PlayerFactionId, request,
                            spec.InitialSiteLevel);
                        _domainLegal = domain.IsSuccess;
                        domainReason = domain.IsSuccess ? string.Empty : domain.Error.Message;
                    }
                    else
                        domainReason = requestFailure;
                }
                EnsurePreview();
                PositionBuilding(_preview, _previewX, _previewZ, 1f);
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
                PlaceFlag(_previewX, _previewZ);
        }

        void PlaceFlag(float x, float z)
        {
            var world = _bootstrap.Session.World;
            if (!TryPrepareFactionFlagSitePlacement(
                    x, z, out var request, out _, out _, out _, out var failure))
            {
                _status = failure;
                return;
            }
            var result = ConstructionService.TryConstructFactionFlagSite(
                world, _buildingId, world.Strategic.PlayerFactionId, request,
                out _, out var siteId);
            _status = result.IsSuccess ? "建造成功。" : result.Error.Message;
            if (!result.IsSuccess)
                return;
            CancelPlacement();
            _bootstrap.RefreshFactionFlagWalkGrid();
            Debug.Log("[CW03SiteCreated] SiteId=" + siteId + " Surface=" + request.SurfaceId +
                      " World=(" + request.WorldPosition.X.ToString("0.###") + "," +
                      request.WorldPosition.Y.ToString("0.###") + ")", this);
        }

        bool TryPrepareFactionFlagSitePlacement(
            float targetPresentationX,
            float targetPresentationZ,
            out FactionFlagSitePlacementRequest request,
            out float presentationX,
            out float presentationZ,
            out bool geometryLegal,
            out string failure)
        {
            request = null;
            presentationX = targetPresentationX;
            presentationZ = targetPresentationZ;
            geometryLegal = false;
            failure = string.Empty;
            var session = _bootstrap?.Session;
            var world = session?.World;
            var continuous = _bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (world == null || continuous == null || !continuous.IsActive ||
                world.LocalMap.IsInInterior ||
                world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.None)
            {
                failure = "当前空间或战斗阶段不允许建站。";
                return false;
            }

            if (!continuous.TryGetCompositeWalkGrid(out var composite) || composite == null)
            {
                failure = "超出当前loaded buildable area。";
                return false;
            }
            if (!HostFactionFlagQuery.TryResolveLegalCenterAtContinuous(
                    composite, targetPresentationX, targetPresentationZ,
                    out var resolvedX, out var resolvedZ))
            {
                failure = composite.TryWorldToCell(targetPresentationX, targetPresentationZ, out _, out _)
                    ? "此处建筑占地不可通行/有障碍。"
                    : "超出当前loaded buildable area。";
                return false;
            }
            geometryLegal = true;
            presentationX = resolvedX;
            presentationZ = resolvedZ;
            if (!continuous.PresentationToWorld(
                    presentationX, presentationZ, out var worldX, out var worldY) ||
                world.SurfaceSpatial == null ||
                !world.SurfaceSpatial.TryGet(continuous.ActiveSurfaceId, out var surface) ||
                surface == null || !surface.ContainsWorldPosition(worldX, worldY))
            {
                failure = "当前位置不属于有效Continuous Surface。";
                return false;
            }
            if (!continuous.IsWorldPositionLoaded(continuous.ActiveSurfaceId, worldX, worldY))
            {
                failure = "超出当前loaded buildable area。";
                return false;
            }
            if (session.PlayerParty == null || !session.PlayerParty.HasActive ||
                _bootstrap.ViewSpawner?.Registry == null ||
                !_bootstrap.ViewSpawner.Registry.TryGet(session.PlayerParty.ActiveCharacterId, out var active) ||
                active == null)
            {
                failure = "当前没有可执行建造的主控角色。";
                return false;
            }
            var activePoint = HostPresentationSpace.ToPresentation(active.transform.position);
            var dx = activePoint.x - presentationX;
            var dz = activePoint.y - presentationZ;
            const float maxPlacementDistance = 12f;
            if (dx * dx + dz * dz > maxPlacementDistance * maxPlacementDistance)
            {
                failure = "落点距离主控过远。";
                return false;
            }
            request = new FactionFlagSitePlacementRequest
            {
                SurfaceId = continuous.ActiveSurfaceId,
                WorldPosition = new WorldVec2(worldX, worldY),
                PresentationX = presentationX,
                PresentationZ = presentationZ
            };
            return true;
        }

        void SyncVisuals(
            XianXia.Core.Simulation.SimulationWorld world,
            bool continuous)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            if (continuous)
            {
                foreach (var pair in world.Strategic.FactionFlags.Flags)
                {
                    var flag = pair.Value;
                    if (flag == null) continue;
                    if (!IsContinuousFlagLoaded(flag)) continue;
                    EnsureVisual(flag);
                    keep.Add(flag.FlagId);
                }
            }
            var remove = new List<string>();
            foreach (var pair in _visuals)
                if (!keep.Contains(pair.Key)) remove.Add(pair.Key);
            for (var i = 0; i < remove.Count; i++) DestroyVisual(remove[i]);
        }

        bool IsContinuousFlagLoaded(FactionFlagState flag)
        {
            var continuous = _bootstrap.ContinuousOutdoorSurfaceRuntime;
            if (flag == null || continuous == null || !continuous.IsActive) return false;
            if (!flag.HasWorldPosition ||
                !string.Equals(flag.SurfaceId, continuous.ActiveSurfaceId, StringComparison.Ordinal))
                return false;
            return continuous.IsWorldPositionLoaded(
                continuous.ActiveSurfaceId, flag.WorldX, flag.WorldY);
        }

        void EnsureVisual(FactionFlagState flag)
        {
            if (!_visuals.TryGetValue(flag.FlagId, out var visual) || visual?.Root == null)
            {
                var root = InstantiateBuilding("FactionControlPost_" + flag.FlagId);
                root.transform.SetParent(transform, false);
                root.AddComponent<HostFactionFlagView>().Bind(flag.FlagId);
                var labelObject = new GameObject("FlagStatus");
                labelObject.transform.SetParent(root.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, 2.4f, -.1f);
                var label = labelObject.AddComponent<TextMesh>();
                label.characterSize = .1f; label.fontSize = 25;
                label.anchor = TextAnchor.LowerCenter; label.alignment = TextAlignment.Center;
                label.color = Color.white;
                var mr = labelObject.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 722;
                visual = new FlagVisual { Root = root, Label = label };
                _visuals[flag.FlagId] = visual;
            }
            visual.Label.text = BuildLabel(flag);
            var continuous = _bootstrap.ContinuousOutdoorSurfaceRuntime;
            if (continuous != null && continuous.IsActive &&
                HostFactionFlagQuery.TryGetCenter(flag, continuous, out var center))
            {
                var p = HostPresentationSpace.ToPresentation(center);
                PositionBuilding(visual.Root, p.x, p.y, 1f);
            }
        }

        string BuildLabel(FactionFlagState flag)
        {
            var world = _bootstrap?.Session?.World;
            if (!string.IsNullOrEmpty(flag.SiteId) && world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGet(flag.SiteId, out var site) && site != null)
                return site.DisplayName + " · Lv." + site.CoreLevel +
                       "\n" + StrategicFactionCatalog.DisplayName(site.OwnerFactionId) +
                       " · " + (site.IsCoreActive ? "核心有效" : "核心失效") +
                       " · " + site.CoreRangeWidth.ToString("0.#") + "×" +
                       site.CoreRangeHeight.ToString("0.#") +
                       "\nHP " + flag.CurrentHp + "/" + flag.MaxHp;
            return StrategicFactionCatalog.DisplayName(flag.FactionId) +
                   "\nHP " + flag.CurrentHp + "/" + flag.MaxHp;
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

        static void PositionBuilding(GameObject go, float x, float z, float cellSize)
        {
            if (go == null) return;
            var cs = cellSize > 0f ? cellSize : 1f;
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

        void DestroyVisual(string flagId)
        {
            if (!_visuals.TryGetValue(flagId ?? string.Empty, out var visual)) return;
            if (visual?.Root != null) Destroy(visual.Root);
            _visuals.Remove(flagId);
        }

        void DestroyAllVisuals()
        {
            var ids = new List<string>(_visuals.Keys);
            for (var i = 0; i < ids.Count; i++) DestroyVisual(ids[i]);
        }

        void DestroyPreview()
        {
            if (_preview != null) Destroy(_preview);
            _preview = null;
        }
    }
}
