using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Navigation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>阵营旗建筑占地、落点、拾取与接近点的 Host 几何真源。</summary>
    public static class HostFactionFlagQuery
    {
        public const int FootprintCells = 4;
        public const float MeleeMargin = 2f;

        public static bool TryGetContinuousFootprint(
            FactionFlagState flag, ContinuousOutdoorSurfaceRuntime continuous,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = maxX = minZ = maxZ = 0f;
            if (flag == null || !flag.HasWorldPosition || continuous == null ||
                !continuous.IsActive || continuous.Mapper == null)
                return false;
            if (!string.IsNullOrEmpty(flag.SurfaceId) &&
                !string.Equals(flag.SurfaceId, continuous.ActiveSurfaceId, StringComparison.Ordinal))
                return false;
            continuous.Mapper.WorldToPresentation(flag.WorldX, flag.WorldY, out var cx, out var cz);
            var half = FootprintCells * .5f;
            minX = cx - half; maxX = cx + half; minZ = cz - half; maxZ = cz + half;
            return true;
        }

        public static bool TryPickAtWorld(
            FactionFlagState flag, ContinuousOutdoorSurfaceRuntime continuous,
            Vector3 worldPoint, out string flagId)
        {
            flagId = string.Empty;
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            if (!TryGetContinuousFootprint(flag, continuous, out var minX, out var maxX, out var minZ, out var maxZ) ||
                p.x < minX || p.x > maxX || p.y < minZ || p.y > maxZ) return false;
            flagId = flag.FlagId;
            return true;
        }

        public static bool IsAnyPointNear(
            FactionFlagState flag, ContinuousOutdoorSurfaceRuntime continuous,
            IReadOnlyList<(float X, float Z)> points)
        {
            if (points == null || !TryGetContinuousFootprint(flag, continuous,
                    out var minX, out var maxX, out var minZ, out var maxZ)) return false;
            minX -= MeleeMargin; maxX += MeleeMargin; minZ -= MeleeMargin; maxZ += MeleeMargin;
            for (var i = 0; i < points.Count; i++)
                if (points[i].X >= minX && points[i].X <= maxX && points[i].Z >= minZ && points[i].Z <= maxZ)
                    return true;
            return false;
        }

        public static bool TryGetCenter(
            FactionFlagState flag, ContinuousOutdoorSurfaceRuntime continuous, out Vector3 center)
        {
            center = default;
            if (!TryGetContinuousFootprint(flag, continuous, out var minX, out var maxX, out var minZ, out var maxZ))
                return false;
            center = HostPresentationSpace.FromPresentation((minX + maxX) * .5f, (minZ + maxZ) * .5f);
            return true;
        }

        public static bool TryGetApproachPoint(
            FactionFlagState flag, ContinuousOutdoorSurfaceRuntime continuous, WalkGrid grid,
            out Vector3 approach)
        {
            approach = default;
            if (!TryGetContinuousFootprint(flag, continuous, out var minX, out var maxX, out var minZ, out var maxZ))
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
                                     !grid.IsWalkable(x, y))) continue;
                approach = HostPresentationSpace.FromPresentation(candidates[i].x, candidates[i].y);
                return true;
            }
            return false;
        }

        public static bool TryResolveLegalCenterAtContinuous(
            WalkGrid grid, float targetX, float targetZ, out float centerX, out float centerZ)
        {
            centerX = centerZ = 0f;
            if (grid == null || !grid.TryWorldToCell(targetX, targetZ, out var cx, out var cy)) return false;
            var minX = cx - FootprintCells / 2; var minY = cy - FootprintCells / 2;
            var maxX = minX + FootprintCells - 1; var maxY = minY + FootprintCells - 1;
            if (!grid.InBounds(minX, minY) || !grid.InBounds(maxX, maxY)) return false;
            for (var y = minY; y <= maxY; y++) for (var x = minX; x <= maxX; x++)
                if (!grid.IsWalkable(x, y)) return false;
            centerX = grid.OriginX + (minX + FootprintCells * .5f) * grid.CellSize;
            centerZ = grid.OriginY + (minY + FootprintCells * .5f) * grid.CellSize;
            return true;
        }

        public static void ApplyWalkGridBlock(
            FactionFlagState flag, ContinuousOutdoorSurfaceRuntime continuous, WalkGrid grid)
        {
            if (grid == null || !TryGetContinuousFootprint(flag, continuous,
                    out var minX, out var maxX, out var minZ, out var maxZ) ||
                !grid.TryWorldToCell(minX, minZ, out var x0, out var y0) ||
                !grid.TryWorldToCell(maxX - .001f, maxZ - .001f, out var x1, out var y1)) return;
            grid.SetBlockedRect(x0, y0, x1, y1, true);
        }
    }
}
