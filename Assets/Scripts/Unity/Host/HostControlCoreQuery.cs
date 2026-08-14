using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Control-core pick／melee range from mapLayout footprint (any point on the building),
    /// not a tiny location center or interact spot.
    /// </summary>
    public static class HostControlCoreQuery
    {
        /// <summary>Extra reach outside the building AABB for melee／occupy.</summary>
        public const float MeleeMargin = 2f;

        public static bool TryPickAtMouse(
            Camera camera,
            SimulationWorld world,
            MapLayoutDefinition layout,
            out string workAreaId)
        {
            workAreaId = string.Empty;
            if (camera == null ||
                !HostPresentationSpace.TryRaycastPlane(camera, Input.mousePosition, out var worldPoint))
                return false;
            return TryPickAtWorld(world, layout, worldPoint, out workAreaId);
        }

        public static bool TryPickAtWorld(
            SimulationWorld world,
            MapLayoutDefinition layout,
            Vector3 worldPoint,
            out string workAreaId)
        {
            workAreaId = string.Empty;
            if (world == null)
                return false;

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var bestArea = float.MaxValue;
            string bestId = null;

            foreach (var kv in world.ControlCores.All)
            {
                var core = kv.Value;
                if (core == null)
                    continue;
                if (!TryGetFootprint(world, layout, core, out var minX, out var maxX, out var minZ, out var maxZ,
                        out _, out _))
                    continue;

                // Click anywhere on the building (no margin required for pick).
                if (p.x < minX || p.x > maxX || p.y < minZ || p.y > maxZ)
                    continue;

                var area = (maxX - minX) * (maxZ - minZ);
                if (area >= bestArea)
                    continue;
                bestArea = area;
                bestId = core.WorkAreaId;
            }

            if (string.IsNullOrEmpty(bestId))
                return false;
            workAreaId = bestId;
            return true;
        }

        public static bool IsAnyPointNear(
            SimulationWorld world,
            MapLayoutDefinition layout,
            ControlCoreState core,
            IReadOnlyList<(float X, float Z)> presentationPoints,
            float margin = MeleeMargin)
        {
            if (world == null || core == null || presentationPoints == null || presentationPoints.Count == 0)
                return false;
            if (!TryGetFootprint(world, layout, core, out var minX, out var maxX, out var minZ, out var maxZ,
                    out _, out _))
                return false;

            minX -= margin;
            maxX += margin;
            minZ -= margin;
            maxZ += margin;

            for (var i = 0; i < presentationPoints.Count; i++)
            {
                var x = presentationPoints[i].X;
                var z = presentationPoints[i].Z;
                if (x >= minX && x <= maxX && z >= minZ && z <= maxZ)
                    return true;
            }

            return false;
        }

        public static bool TryGetCenter(
            SimulationWorld world,
            MapLayoutDefinition layout,
            ControlCoreState core,
            out Vector3 worldCenter)
        {
            worldCenter = default;
            if (!TryGetFootprint(world, layout, core, out _, out _, out _, out _, out var cx, out var cz))
                return false;
            worldCenter = HostPresentationSpace.FromPresentation(cx, cz);
            return true;
        }

        /// <summary>
        /// Stand point just outside the building (south edge) so pathfinding is not stuck in blocksMovement.
        /// </summary>
        public static bool TryGetApproachPoint(
            SimulationWorld world,
            MapLayoutDefinition layout,
            ControlCoreState core,
            out Vector3 worldPoint)
        {
            worldPoint = default;
            if (!TryGetFootprint(world, layout, core, out var minX, out var maxX, out var minZ, out _,
                    out _, out _))
                return false;
            var ax = (minX + maxX) * 0.5f;
            var az = minZ - 1.25f;
            worldPoint = HostPresentationSpace.FromPresentation(ax, az);
            return true;
        }

        public static bool TryGetFootprint(
            SimulationWorld world,
            MapLayoutDefinition layout,
            ControlCoreState core,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ,
            out float centerX,
            out float centerZ)
        {
            minX = maxX = minZ = maxZ = centerX = centerZ = 0f;
            if (world == null || core == null || string.IsNullOrEmpty(core.LocationId))
                return false;

            if (layout?.Placements != null)
            {
                for (var i = 0; i < layout.Placements.Count; i++)
                {
                    var p = layout.Placements[i];
                    if (p == null)
                        continue;
                    var kind = MapKindCatalog.NormalizeKind(p.Kind);
                    if (kind != "controlCore")
                        continue;
                    if (!string.Equals(p.BoundLocationId, core.LocationId, System.StringComparison.Ordinal))
                        continue;

                    var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
                    var pw = p.W < 1 ? 1 : p.W;
                    var ph = p.H < 1 ? 1 : p.H;
                    minX = layout.OriginX + p.X * cs;
                    maxX = layout.OriginX + (p.X + pw) * cs;
                    minZ = layout.OriginY + p.Y * cs;
                    maxZ = layout.OriginY + (p.Y + ph) * cs;
                    centerX = (minX + maxX) * 0.5f;
                    centerZ = (minZ + maxZ) * 0.5f;
                    return true;
                }
            }

            // Fallback: location presentation + soft radius (legacy／no placement).
            if (!world.WorldRegion.TryGet(core.LocationId, out var loc))
                return false;
            var r = ControlCoreService.DefaultStandRadius;
            if (world.TryGetWorkArea(core.WorkAreaId, out var area))
            {
                centerX = loc.PresentationX + area.OffsetX;
                centerZ = loc.PresentationZ + area.OffsetZ;
            }
            else
            {
                centerX = loc.PresentationX;
                centerZ = loc.PresentationZ;
            }

            minX = centerX - r;
            maxX = centerX + r;
            minZ = centerZ - r;
            maxZ = centerZ + r;
            return true;
        }
    }
}
