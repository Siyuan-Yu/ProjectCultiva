using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;

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
        [SerializeField] float pickRadius = 6.5f;
        [SerializeField] float plotPickRadius = 1.35f;
        [SerializeField] float destructiblePickRadius = 2.2f;

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
            // 选中己方时收起住房检视，保留主管府（突击中要看耐久）
            if (selectionController != null && selectionController.State.Count > 0)
            {
                if (_inspect.Kind == WorldObjectInspectKind.Housing ||
                    _inspect.Kind == WorldObjectInspectKind.WorkArea ||
                    _inspect.Kind == WorldObjectInspectKind.Plot)
                    _inspect.Clear();
            }

            if (_inspect.Kind == WorldObjectInspectKind.Destructible &&
                (_inspect.Destructible == null || _inspect.Destructible.IsDestroyed))
                _inspect.Clear();

            if (_inspect.Kind == WorldObjectInspectKind.Plot && _inspect.Plot == null)
                _inspect.Clear();
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

            var world = bootstrap.Session.World;
            MapLayoutPick.TryGet(bootstrap.Session, out var layout);
            if (HostControlCoreQuery.TryPickAtWorld(world, layout, worldPoint, out var coreId))
            {
                _inspect.SetControlCore(coreId);
                return;
            }

            if (HostMapObjectRegistry.TryPickDestructible(worldPoint, destructiblePickRadius, out var d))
            {
                _inspect.SetDestructible(d);
                return;
            }

            if (HostMapObjectRegistry.TryPickPlot(worldPoint, plotPickRadius, out var plot) &&
                (plot.IsPlantableField || plot.InteractKind == HostInteractSpotKind.Work ||
                 plot.InteractKind == HostInteractSpotKind.Loot ||
                 plot.InteractKind == HostInteractSpotKind.Cultivate))
            {
                _inspect.SetPlot(plot);
                return;
            }

            if (TryPickHousing(world, worldPoint, pickRadius, out var houseId))
            {
                _inspect.SetHousing(houseId);
                return;
            }

            if (TryPickWorkArea(world, worldPoint, pickRadius, out var areaId))
            {
                _inspect.SetWorkArea(areaId);
                return;
            }

            Clear();
        }

        public static bool TryPickHousing(
            SimulationWorld world,
            Vector3 worldPoint,
            float radius,
            out string workAreaId)
        {
            workAreaId = string.Empty;
            if (world == null)
                return false;

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var best = float.MaxValue;
            string bestId = null;

            foreach (var kv in world.WorkAreas)
            {
                var area = kv.Value;
                if (!HousingAssignmentService.IsHousingArea(area))
                    continue;
                if (string.IsNullOrEmpty(area.LocationId) ||
                    !world.WorldRegion.TryGet(area.LocationId, out var loc))
                    continue;

                var cx = loc.PresentationX + area.OffsetX;
                var cz = loc.PresentationZ + area.OffsetZ;
                var dx = cx - p.x;
                var dy = cz - p.y;
                var dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > radius || dist >= best)
                    continue;
                best = dist;
                bestId = area.Id;
            }

            if (string.IsNullOrEmpty(bestId))
                return false;
            workAreaId = bestId;
            return true;
        }

        public static bool TryPickWorkArea(
            SimulationWorld world,
            Vector3 worldPoint,
            float radius,
            out string workAreaId)
        {
            workAreaId = string.Empty;
            if (world == null)
                return false;

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var best = float.MaxValue;
            string bestId = null;

            foreach (var kv in world.WorkAreas)
            {
                var area = kv.Value;
                if (area == null || area.IsControlCore || HousingAssignmentService.IsHousingArea(area))
                    continue;
                if (string.IsNullOrEmpty(area.LocationId) ||
                    !world.WorldRegion.TryGet(area.LocationId, out var loc))
                    continue;

                float dist;
                if (HostFarmFieldRules.IsFarmTaggedWorkArea(area))
                {
                    // 农田／药田：只点在耕种格上才检视，勿用地点圆心大半径扫绿草。
                    if (!HostFarmFieldRegistry.TryFindPlotAt(worldPoint, out var plot) ||
                        plot == null ||
                        !string.Equals(plot.LocationId, area.LocationId, System.StringComparison.Ordinal))
                        continue;
                    dist = HostFarmFieldRules.XyDistance(plot.transform.position, worldPoint);
                }
                else
                {
                    var cx = loc.PresentationX + area.OffsetX;
                    var cz = loc.PresentationZ + area.OffsetZ;
                    var dx = cx - p.x;
                    var dy = cz - p.y;
                    dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > radius)
                        continue;
                }

                if (dist >= best)
                    continue;
                best = dist;
                bestId = area.Id;
            }

            if (string.IsNullOrEmpty(bestId))
                return false;
            workAreaId = bestId;
            return true;
        }

        void OnDrawGizmos()
        {
            if (bootstrap?.Session?.World == null || !_inspect.HasTarget)
                return;

            if (_inspect.Kind == WorldObjectInspectKind.ControlCore &&
                bootstrap.Session.World.ControlCores.TryGet(_inspect.WorkAreaId, out var core))
            {
                MapLayoutPick.TryGet(bootstrap.Session, out var layout);
                if (HostControlCoreQuery.TryGetCenter(
                        bootstrap.Session.World, layout, core, out var center))
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

            if ((_inspect.Kind == WorldObjectInspectKind.Housing ||
                 _inspect.Kind == WorldObjectInspectKind.WorkArea) &&
                bootstrap.Session.World.TryGetWorkArea(_inspect.WorkAreaId, out var area) &&
                bootstrap.Session.World.WorldRegion.TryGet(area.LocationId, out var loc))
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
