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
                    _inspect.Kind == WorldObjectInspectKind.Bed ||
                    _inspect.Kind == WorldObjectInspectKind.Door)
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
                 _inspect.Kind == WorldObjectInspectKind.Bed ||
                 _inspect.Kind == WorldObjectInspectKind.Door) && _inspect.Plot == null)
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

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !_inspect.HasTarget ||
                selectionController != null && selectionController.State.Count > 0 ||
                worldCamera == null || !TryGetSelectionBounds(out var bounds))
                return;
            var a = worldCamera.WorldToScreenPoint(new Vector3(bounds.xMin, bounds.yMin,
                HostPresentationSpace.GroundZ));
            var b = worldCamera.WorldToScreenPoint(new Vector3(bounds.xMax, bounds.yMax,
                HostPresentationSpace.GroundZ));
            if (a.z <= 0f || b.z <= 0f) return;
            var frame = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 2f,
                Screen.height - Mathf.Max(a.y, b.y) - 2f,
                Mathf.Max(a.x, b.x) + 2f,
                Screen.height - Mathf.Min(a.y, b.y) + 2f);
            if (frame.width < 2f || frame.height < 2f) return;
            var previous = GUI.color;
            DrawFrame(frame, new Color(.07f, .13f, .14f, .95f), 4f);
            DrawFrame(frame, new Color(.32f, .92f, .94f, 1f), 2f);
            GUI.color = previous;
        }

        static void DrawFrame(Rect r, Color color, float thickness)
        {
            GUI.color = color;
            var pixel = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, thickness), pixel);
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), pixel);
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, thickness, r.height), pixel);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), pixel);
        }

        bool TryGetSelectionBounds(out Rect bounds)
        {
            bounds = default;
            if (_inspect.Plot != null && _inspect.Plot.HasInteractionBounds)
            {
                bounds = _inspect.Plot.InteractionBounds;
                if (_inspect.Kind == WorldObjectInspectKind.Room)
                {
                    var plots = HostMapObjectRegistry.AllPlots;
                    for (var i = 0; i < plots.Count; i++)
                    {
                        var piece = plots[i];
                        if (piece == null || !piece.IsRoom || piece.IsDoor ||
                            !piece.HasInteractionBounds ||
                            !string.Equals(piece.StableCellId, _inspect.Plot.StableCellId,
                                System.StringComparison.Ordinal)) continue;
                        var r = piece.InteractionBounds;
                        bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin),
                            Mathf.Min(bounds.yMin, r.yMin), Mathf.Max(bounds.xMax, r.xMax),
                            Mathf.Max(bounds.yMax, r.yMax));
                    }
                }
                return true;
            }
            if (_inspect.Destructible != null && _inspect.Destructible.HasInteractionBounds)
            {
                bounds = _inspect.Destructible.InteractionBounds;
                return true;
            }
            var world = bootstrap?.Session?.World;
            if (world == null) return false;
            if (_inspect.Kind == WorldObjectInspectKind.ControlCore &&
                world.ControlCores.TryGet(_inspect.WorkAreaId, out var core))
            {
                var tileMap = bootstrap.GetComponent<HostDemoTileMap>();
                if (tileMap != null && tileMap.TryGetCoreVisualBounds(core.LocationId, out bounds))
                    return true;
                MapLayoutDefinition layout = null;
                var continuous = bootstrap.ContinuousOutdoorSurfaceRuntime;
                if (continuous == null || !continuous.IsActive)
                    MapLayoutPick.TryGet(bootstrap.Session, out layout);
                if (HostControlCoreQuery.TryGetFootprint(world, layout, continuous, core,
                        out var coreMinX, out var coreMaxX, out var coreMinY, out var coreMaxY,
                        out _, out _))
                {
                    bounds = Rect.MinMaxRect(coreMinX, coreMinY, coreMaxX, coreMaxY);
                    return true;
                }
            }
            if (_inspect.Kind == WorldObjectInspectKind.FactionFlag &&
                world.Strategic.FactionFlags.Flags.TryGetValue(_inspect.FactionFlagId, out var flag) &&
                HostFactionFlagQuery.TryGetContinuousFootprint(flag,
                    bootstrap.ContinuousOutdoorSurfaceRuntime,
                    out var minX, out var maxX, out var minY, out var maxY))
            {
                bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
                return true;
            }
            if ((_inspect.Kind == WorldObjectInspectKind.Housing ||
                 _inspect.Kind == WorldObjectInspectKind.WorkArea) &&
                world.TryGetWorkArea(_inspect.WorkAreaId, out var area) && area != null &&
                (world.ContinuousOutdoorMaterialization.TryGetAnyPlace(area.LocationId, out var loc) ||
                 world.LocalPlaces.TryGet(area.LocationId, out loc)))
            {
                var center = HostPresentationSpace.FromPresentation(
                    loc.PresentationX + area.OffsetX, loc.PresentationZ + area.OffsetZ);
                bounds = new Rect(center.x - 2.2f, center.y - 2.2f, 4.4f, 4.4f);
                return true;
            }
            return false;
        }
    }
}
