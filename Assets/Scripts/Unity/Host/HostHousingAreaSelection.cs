using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Left-click empty ground near a housing work area or on a control-core building → select for HostFormalHud.
    /// </summary>
    public sealed class HostHousingAreaSelection : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] Camera worldCamera;
        [SerializeField] float pickRadius = 6.5f;

        public string SelectedWorkAreaId { get; private set; } = string.Empty;

        public string SelectedControlCoreWorkAreaId { get; private set; } = string.Empty;

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

        public void Clear()
        {
            SelectedWorkAreaId = string.Empty;
            SelectedControlCoreWorkAreaId = string.Empty;
        }

        public void ClearHousing() => SelectedWorkAreaId = string.Empty;

        public void ClearControlCore() => SelectedControlCoreWorkAreaId = string.Empty;

        public void SelectControlCore(string workAreaId)
        {
            SelectedControlCoreWorkAreaId = workAreaId ?? string.Empty;
            SelectedWorkAreaId = string.Empty;
        }

        void Update()
        {
            // Keep control-core panel open while party is selected (assault／occupy).
            if (selectionController != null && selectionController.State.Count > 0)
                SelectedWorkAreaId = string.Empty;
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
                SelectedControlCoreWorkAreaId = coreId;
                SelectedWorkAreaId = string.Empty;
                return;
            }

            if (TryPickHousing(world, worldPoint, pickRadius, out var areaId))
            {
                SelectedWorkAreaId = areaId;
                SelectedControlCoreWorkAreaId = string.Empty;
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
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > radius || d >= best)
                    continue;
                best = d;
                bestId = area.Id;
            }

            if (string.IsNullOrEmpty(bestId))
                return false;
            workAreaId = bestId;
            return true;
        }

        void OnDrawGizmos()
        {
            if (bootstrap?.Session?.World == null)
                return;

            if (!string.IsNullOrEmpty(SelectedControlCoreWorkAreaId) &&
                bootstrap.Session.World.ControlCores.TryGet(SelectedControlCoreWorkAreaId, out var core))
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

            if (string.IsNullOrEmpty(SelectedWorkAreaId))
                return;
            if (!bootstrap.Session.World.TryGetWorkArea(SelectedWorkAreaId, out var area) ||
                !bootstrap.Session.World.WorldRegion.TryGet(area.LocationId, out var loc))
                return;
            var houseCenter = HostPresentationSpace.FromPresentation(
                loc.PresentationX + area.OffsetX,
                loc.PresentationZ + area.OffsetZ);
            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.85f);
            Gizmos.DrawWireSphere(houseCenter, 2.2f);
        }
    }
}
