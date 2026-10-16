using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 左键点空：优先检视主管府／可破坏物／耕种格／住房／其它工区。
    /// 框选仍只选己方（SelectionController）；本组件只响应点选落空。
    /// </summary>
    public sealed class HostHousingAreaSelection : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] Camera worldCamera;

        readonly WorldObjectInspectSelection _inspect = new WorldObjectInspectSelection();

        public WorldObjectInspectSelection Inspect => _inspect;

        public string SelectedWorkAreaId =>
            _inspect.Kind == WorldObjectInspectKind.Housing ? _inspect.WorkAreaId : string.Empty;

        public string SelectedControlCoreWorkAreaId =>
            _inspect.Kind == WorldObjectInspectKind.ControlCore ? _inspect.WorkAreaId : string.Empty;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection, Camera camera)
        {
            bootstrap = host;
            selectionController = selection;
            worldCamera = camera != null ? camera : Camera.main;
            WireMiss();
        }

        void OnEnable() => WireMiss();

        void OnDisable()
        {
            if (selectionController != null)
                selectionController.OnPointSelectMiss -= OnMiss;
        }

        void WireMiss()
        {
            if (selectionController == null)
                return;
            selectionController.OnPointSelectMiss -= OnMiss;
            selectionController.OnPointSelectMiss += OnMiss;
        }

        public void Clear() => _inspect.Clear();

        public void ClearHousing()
        {
            if (_inspect.Kind == WorldObjectInspectKind.Housing)
                _inspect.Clear();
        }

        public void ClearControlCore()
        {
            if (_inspect.Kind == WorldObjectInspectKind.ControlCore)
                _inspect.Clear();
        }

        public void SelectControlCore(string workAreaId) =>
            _inspect.SetControlCore(workAreaId);

        public void SelectDestructible(HostMapDestructible d) =>
            _inspect.SetDestructible(d);

        void Update()
        {
            if (_inspect.HasTarget && !IsInspectTargetLocallyVisible())
                _inspect.Clear();

            // 选中己方时收起住房检视，保留主管府（突击中要看耐久）
            if (selectionController != null && selectionController.State.Count > 0)
            {
                if (_inspect.Kind == WorldObjectInspectKind.Housing ||
                    _inspect.Kind == WorldObjectInspectKind.WorkArea ||
                    _inspect.Kind == WorldObjectInspectKind.Plot ||
                    _inspect.Kind == WorldObjectInspectKind.RecoverySpot ||
                    _inspect.Kind == WorldObjectInspectKind.PrisonDropOff ||
                    _inspect.Kind == WorldObjectInspectKind.Room ||
                    _inspect.Kind == WorldObjectInspectKind.Bed)
                    _inspect.Clear();
            }

            if (_inspect.Kind == WorldObjectInspectKind.Destructible &&
                (_inspect.Destructible == null || _inspect.Destructible.IsDestroyed))
                _inspect.Clear();

            if ((_inspect.Kind == WorldObjectInspectKind.Plot ||
                 _inspect.Kind == WorldObjectInspectKind.RecoverySpot ||
                 _inspect.Kind == WorldObjectInspectKind.StorageRoom ||
                 _inspect.Kind == WorldObjectInspectKind.PrisonDropOff ||
                 _inspect.Kind == WorldObjectInspectKind.Room ||
                 _inspect.Kind == WorldObjectInspectKind.Bed) && _inspect.Plot == null)
                _inspect.Clear();
        }

        bool IsInspectTargetLocallyVisible()
        {
            var continuous = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            var world = bootstrap?.Session?.World;
            if (continuous == null || !continuous.IsActive || world == null) return true;
            Vector3 point;
            if (_inspect.Plot != null) point = _inspect.Plot.transform.position;
            else if (_inspect.Destructible != null) point = _inspect.Destructible.transform.position;
            else if (_inspect.Kind == WorldObjectInspectKind.ControlCore &&
                     world.ControlCores.TryGet(_inspect.WorkAreaId, out var core) &&
                     HostControlCoreQuery.TryGetCenter(world, null, continuous, core, out point)) { }
            else if (_inspect.Kind == WorldObjectInspectKind.FactionFlag &&
                     world.Strategic.FactionFlags.Flags.TryGetValue(_inspect.FactionFlagId, out var flag) &&
                     flag != null && flag.HasWorldPosition)
                return continuous.IsWorldPositionLoaded(flag.SurfaceId, flag.WorldX, flag.WorldY);
            else if ((_inspect.Kind == WorldObjectInspectKind.Housing ||
                      _inspect.Kind == WorldObjectInspectKind.WorkArea) &&
                     world.TryGetWorkArea(_inspect.WorkAreaId, out var area) && area != null &&
                     world.ContinuousOutdoorMaterialization.TryGetAnyPlace(area.LocationId, out var loc))
                point = HostPresentationSpace.FromPresentation(
                    loc.PresentationX + area.OffsetX, loc.PresentationZ + area.OffsetZ);
            else return false;
            var presentation = HostPresentationSpace.ToPresentation(point);
            return continuous.IsPresentationPositionLoaded(presentation.x, presentation.y);
        }

        void OnMiss(Vector2 screenPoint)
        {
            if (bootstrap?.Session?.World == null)
            {
                Clear();
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null ||
                !HostPresentationSpace.TryRaycastPlane(worldCamera, screenPoint, out var worldPoint))
            {
                Clear();
                return;
            }

            if (HostWorldObjectPicker.TryPickAtWorldPoint(bootstrap, worldPoint, out var target))
            {
                _inspect.Set(target);
                if (target.Kind == WorldObjectTargetKind.PrisonDropOff && target.Plot != null)
                {
                    var active = bootstrap.Session.PlayerParty?.ActiveCharacterId ?? XianXia.Core.Domain.Ids.EntityId.None;
                    var prisonId = string.Empty;
                    foreach (var area in bootstrap.Session.World.WorkAreas)
                        if (area.Value != null && XianXia.Core.Npc.HousingAssignmentService.IsDedicatedPrison(area.Value) &&
                            string.Equals(area.Value.LocationId, target.Plot.LocationId, System.StringComparison.Ordinal))
                        { prisonId = area.Key; break; }
                    foreach (var pair in bootstrap.Session.World.Civilians.All)
                        if (pair.Value != null && pair.Value.Disposition == XianXia.Core.Npc.CivilianDisposition.CapturedEscorted &&
                            pair.Value.CarrierId == active &&
                            XianXia.Core.Npc.MortalCivilianService.CanDetain(bootstrap.Session.World,
                                pair.Key, prisonId).IsFailure)
                        {
                            bootstrap.MoveController?.OrderEntityToWorldPointPublic(active,
                                target.Plot.transform.position, null, exactGoal: true);
                            break;
                        }
                }
            }
            else Clear();
        }

        void OnDrawGizmos()
        {
            if (bootstrap?.Session?.World == null || !_inspect.HasTarget)
                return;

            if (_inspect.Kind == WorldObjectInspectKind.ControlCore &&
                bootstrap.Session.World.ControlCores.TryGet(_inspect.WorkAreaId, out var core))
            {
                MapLayoutDefinition layout = null;
                if (bootstrap.ContinuousOutdoorSurfaceRuntime == null ||
                    !bootstrap.ContinuousOutdoorSurfaceRuntime.IsActive)
                    MapLayoutPick.TryGet(bootstrap.Session, out layout);
                if (HostControlCoreQuery.TryGetCenter(
                        bootstrap.Session.World, layout, bootstrap.ContinuousOutdoorSurfaceRuntime, core, out var center))
                {
                    Gizmos.color = new Color(0.95f, 0.35f, 0.3f, 0.9f);
                    Gizmos.DrawWireSphere(center, 2.4f);
                }

                return;
            }

            if (_inspect.Kind == WorldObjectInspectKind.Destructible && _inspect.Destructible != null)
            {
                Gizmos.color = new Color(0.35f, 0.85f, 0.45f, 0.9f);
                Gizmos.DrawWireSphere(_inspect.Destructible.transform.position, 1.6f);
                return;
            }

            if (_inspect.Kind == WorldObjectInspectKind.Plot && _inspect.Plot != null)
            {
                Gizmos.color = new Color(0.85f, 0.75f, 0.25f, 0.9f);
                Gizmos.DrawWireSphere(_inspect.Plot.transform.position, 0.9f);
                return;
            }

            if ((_inspect.Kind == WorldObjectInspectKind.Room || _inspect.Kind == WorldObjectInspectKind.Bed) &&
                _inspect.Plot != null)
            {
                Gizmos.color = _inspect.Kind == WorldObjectInspectKind.Room
                    ? new Color(0.35f, 0.75f, 1f, 0.9f) : new Color(0.75f, 0.55f, 1f, 0.9f);
                Gizmos.DrawWireSphere(_inspect.Plot.transform.position, 0.9f);
                return;
            }

            if ((_inspect.Kind == WorldObjectInspectKind.Housing ||
                 _inspect.Kind == WorldObjectInspectKind.WorkArea) &&
                bootstrap.Session.World.TryGetWorkArea(_inspect.WorkAreaId, out var area) &&
                (bootstrap.Session.World.ContinuousOutdoorMaterialization.TryGetAnyPlace(area.LocationId, out var loc) ||
                 bootstrap.Session.World.LocalPlaces.TryGet(area.LocationId, out loc)))
            {
                var houseCenter = HostPresentationSpace.FromPresentation(
                    loc.PresentationX + area.OffsetX,
                    loc.PresentationZ + area.OffsetZ);
                Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.85f);
                Gizmos.DrawWireSphere(houseCenter, 2.2f);
            }
        }
    }
}
