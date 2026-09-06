using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Navigation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>阵营旗建筑占地、落点、拾取与接近点的 Host 几何真源。</summary>
    public static class HostFactionFlagQuery
    {
        public const int FootprintCells = 4;
        public const float MeleeMargin = 2f;

        public static bool TryResolvePosition(
            FactionFlagState flag, MapLayoutDefinition layout, WalkGrid baseGrid,
            out float centerX, out float centerZ)
        {
            centerX = centerZ = 0f;
            if (flag == null || layout == null || baseGrid == null)
                return false;
            if (flag.HasLocalPosition)
            {
                centerX = flag.LocalX;
                centerZ = flag.LocalZ;
                return true;
            }
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var targetX = layout.OriginX + layout.Width * cs * .5f;
            var targetZ = layout.OriginY + layout.Height * cs * .5f;
            return TryResolveLegalCenterNear(layout, baseGrid, targetX, targetZ, out centerX, out centerZ);
        }

        public static bool TryResolveLegalCenterNear(
            MapLayoutDefinition layout, WalkGrid baseGrid, float targetX, float targetZ,
            out float centerX, out float centerZ)
        {
            centerX = centerZ = 0f;
            if (layout == null || baseGrid == null)
                return false;
            baseGrid.TryWorldToCell(targetX, targetZ, out var tcx, out var tcy);
            tcx = Mathf.Clamp(tcx, 0, baseGrid.Width - 1);
            tcy = Mathf.Clamp(tcy, 0, baseGrid.Height - 1);
            var maxR = Math.Max(baseGrid.Width, baseGrid.Height);
            for (var r = 0; r <= maxR; r++)
            for (var dy = -r; dy <= r; dy++)
            for (var dx = -r; dx <= r; dx++)
            {
                if (r > 0 && Math.Abs(dx) != r && Math.Abs(dy) != r)
                    continue;
                var minCx = tcx + dx - FootprintCells / 2;
                var minCy = tcy + dy - FootprintCells / 2;
                if (!IsLegalFootprint(layout, baseGrid, minCx, minCy))
                    continue;
                var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
                centerX = layout.OriginX + (minCx + FootprintCells * .5f) * cs;
                centerZ = layout.OriginY + (minCy + FootprintCells * .5f) * cs;
                return true;
            }
            return false;
        }

        public static bool TryResolveLegalCenterAt(
            MapLayoutDefinition layout, WalkGrid baseGrid, float targetX, float targetZ,
            out float centerX, out float centerZ)
        {
            centerX = centerZ = 0f;
            if (layout == null || baseGrid == null ||
                !baseGrid.TryWorldToCell(targetX, targetZ, out var tcx, out var tcy))
                return false;
            var minCx = tcx - FootprintCells / 2;
            var minCy = tcy - FootprintCells / 2;
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            centerX = layout.OriginX + (minCx + FootprintCells * .5f) * cs;
            centerZ = layout.OriginY + (minCy + FootprintCells * .5f) * cs;
            return IsLegalFootprint(layout, baseGrid, minCx, minCy);
        }

        static bool IsLegalFootprint(MapLayoutDefinition layout, WalkGrid grid, int minCx, int minCy)
        {
            var maxCx = minCx + FootprintCells - 1;
            var maxCy = minCy + FootprintCells - 1;
            if (!grid.InBounds(minCx, minCy) || !grid.InBounds(maxCx, maxCy))
                return false;
            for (var y = minCy; y <= maxCy; y++)
            for (var x = minCx; x <= maxCx; x++)
                if (!grid.IsWalkable(x, y))
                    return false;

            // 整个建筑必须处于 SafeInterior，不能占据 Surface Exit / 近缘。
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                layout.OriginX, layout.OriginY, cs, layout.Width, layout.Height);
            var minX = layout.OriginX + minCx * cs;
            var minZ = layout.OriginY + minCy * cs;
            var maxX = layout.OriginX + (maxCx + 1) * cs;
            var maxZ = layout.OriginY + (maxCy + 1) * cs;
            if (!WildernessLocalWorldProjection.IsInSafeInterior(minX, minZ, bounds) ||
                !WildernessLocalWorldProjection.IsInSafeInterior(maxX, maxZ, bounds) ||
                WildernessLocalWorldProjection.IsInExitTriggerBand(minX, minZ, bounds, layout.ExitTriggerDepth) ||
                WildernessLocalWorldProjection.IsInExitTriggerBand(maxX, maxZ, bounds, layout.ExitTriggerDepth))
                return false;

            // 至少保留一条可站立的接近边。
            for (var x = minCx; x <= maxCx; x++)
                if (grid.IsWalkable(x, minCy - 1) || grid.IsWalkable(x, maxCy + 1))
                    return true;
            for (var y = minCy; y <= maxCy; y++)
                if (grid.IsWalkable(minCx - 1, y) || grid.IsWalkable(maxCx + 1, y))
                    return true;
            return false;
        }

        public static void ApplyWalkGridBlock(
            FactionFlagState flag, MapLayoutDefinition layout, WalkGrid grid)
        {
            if (!TryResolvePosition(flag, layout, grid, out var cx, out var cz))
                return;
            if (!TryGetCellRect(layout, grid, cx, cz, out var minX, out var minY, out var maxX, out var maxY))
                return;
            grid.SetBlockedRect(minX, minY, maxX, maxY, true);
        }

        public static bool TryPickAtMouse(Camera camera, FactionFlagState flag, MapLayoutDefinition layout,
            out string flagId)
        {
            flagId = string.Empty;
            if (camera == null || flag == null ||
                !HostPresentationSpace.TryRaycastPlane(camera, Input.mousePosition, out var wp))
                return false;
            return TryPickAtWorld(flag, layout, wp, out flagId);
        }

        public static bool TryPickAtWorld(FactionFlagState flag, MapLayoutDefinition layout,
            Vector3 worldPoint, out string flagId)
        {
            flagId = string.Empty;
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            if (!TryGetFootprint(flag, layout, out var minX, out var maxX, out var minZ, out var maxZ))
                return false;
            if (p.x < minX || p.x > maxX || p.y < minZ || p.y > maxZ)
                return false;
            flagId = flag.FlagId;
            return true;
        }

        public static bool IsAnyPointNear(FactionFlagState flag, MapLayoutDefinition layout,
            IReadOnlyList<(float X, float Z)> points)
        {
            if (points == null || !TryGetFootprint(flag, layout, out var minX, out var maxX, out var minZ, out var maxZ))
                return false;
            minX -= MeleeMargin; maxX += MeleeMargin; minZ -= MeleeMargin; maxZ += MeleeMargin;
            for (var i = 0; i < points.Count; i++)
                if (points[i].X >= minX && points[i].X <= maxX && points[i].Z >= minZ && points[i].Z <= maxZ)
                    return true;
            return false;
        }

        public static bool TryGetCenter(FactionFlagState flag, MapLayoutDefinition layout, out Vector3 center)
        {
            center = default;
            if (!TryGetFootprint(flag, layout, out var minX, out var maxX, out var minZ, out var maxZ))
                return false;
            center = HostPresentationSpace.FromPresentation((minX + maxX) * .5f, (minZ + maxZ) * .5f);
            return true;
        }

        public static bool TryGetApproachPoint(FactionFlagState flag, MapLayoutDefinition layout, WalkGrid grid,
            out Vector3 approach)
        {
            approach = default;
            if (!TryGetFootprint(flag, layout, out var minX, out var maxX, out var minZ, out var maxZ))
                return false;
            var candidates = new[]
            {
                new Vector2((minX + maxX) * .5f, minZ - .75f),
                new Vector2(maxX + .75f, (minZ + maxZ) * .5f),
                new Vector2((minX + maxX) * .5f, maxZ + .75f),
                new Vector2(minX - .75f, (minZ + maxZ) * .5f)
            };
            for (var i = 0; i < candidates.Length; i++)
            {
                if (grid != null && (!grid.TryWorldToCell(candidates[i].x, candidates[i].y, out var x, out var y) ||
                                     !grid.IsWalkable(x, y)))
                    continue;
                approach = HostPresentationSpace.FromPresentation(candidates[i].x, candidates[i].y);
                return true;
            }
            return false;
        }

        public static bool TryGetFootprint(FactionFlagState flag, MapLayoutDefinition layout,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = maxX = minZ = maxZ = 0f;
            if (flag == null || layout == null)
                return false;
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            var cx = flag.LocalX;
            var cz = flag.LocalZ;
            if (!flag.HasLocalPosition)
            {
                var baseGrid = MapLayoutWalkGridBuilder.Create(layout);
                if (!TryResolvePosition(flag, layout, baseGrid, out cx, out cz))
                    return false;
            }
            var half = FootprintCells * cs * .5f;
            minX = cx - half; maxX = cx + half; minZ = cz - half; maxZ = cz + half;
            return true;
        }

        static bool TryGetCellRect(MapLayoutDefinition layout, WalkGrid grid, float cx, float cz,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
            minX = (int)Math.Floor((cx - layout.OriginX) / cs - FootprintCells * .5f + .001f);
            minY = (int)Math.Floor((cz - layout.OriginY) / cs - FootprintCells * .5f + .001f);
            maxX = minX + FootprintCells - 1;
            maxY = minY + FootprintCells - 1;
            return grid.InBounds(minX, minY) && grid.InBounds(maxX, maxY);
        }
    }
}
