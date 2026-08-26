using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 固定 Canonical Exit Trigger Geometry（只依赖 PlayableBounds + ExitTriggerDepth + 方向）。
    /// 禁止依赖角色位置 / EntryDirection / WorldPosition / CurrentHex。
    /// </summary>
    public readonly struct SurfaceExitZoneGeometry
    {
        public SurfaceExitZoneGeometry(
            int directionIndex,
            WildernessLocalWorldProjection.WildernessLocalMapBounds playableBounds,
            float exitTriggerDepth)
        {
            DirectionIndex = directionIndex;
            PlayableBounds = playableBounds;
            ExitTriggerDepth = exitTriggerDepth;
        }

        public int DirectionIndex { get; }
        public WildernessLocalWorldProjection.WildernessLocalMapBounds PlayableBounds { get; }
        public float ExitTriggerDepth { get; }

        public bool Contains(float localX, float localY) =>
            SurfaceExitZoneCalculator.PointBelongsToDirection(
                localX, localY, PlayableBounds, ExitTriggerDepth, DirectionIndex);
    }

    /// <summary>Runtime 可用性（可变）；不改变 Geometry。</summary>
    public readonly struct SurfaceExitAvailability
    {
        public SurfaceExitAvailability(int directionIndex, bool isPassable, HexCoord destinationHex)
        {
            DirectionIndex = directionIndex;
            IsPassable = isPassable;
            DestinationHex = destinationHex;
        }

        public int DirectionIndex { get; }
        public bool IsPassable { get; }
        public HexCoord DestinationHex { get; }
    }

    /// <summary>沿边界的精确覆盖矩形（Presentation = Detection 同一几何的离散覆盖）。</summary>
    public readonly struct SurfaceExitCoverageRect
    {
        public SurfaceExitCoverageRect(float minX, float maxX, float minY, float maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
    }

    /// <summary>可见 Active Zone = Canonical Geometry ∩ Availability。</summary>
    public readonly struct SurfaceExitVisibleZone
    {
        public SurfaceExitVisibleZone(SurfaceExitZoneGeometry geometry, HexCoord destinationHex)
        {
            Geometry = geometry;
            DestinationHex = destinationHex;
        }

        public SurfaceExitZoneGeometry Geometry { get; }
        public HexCoord DestinationHex { get; }
        public int DirectionIndex => Geometry.DirectionIndex;

        public bool Contains(float localX, float localY) => Geometry.Contains(localX, localY);
    }

    /// <summary>
    /// Surface Exit Zone 真源：Geometry 固定；Availability 运行时；Detection 与 Presentation 共用。
    /// </summary>
    public static class SurfaceExitZoneCalculator
    {
        /// <summary>默认 ExitTriggerDepth（world units）。窄边缘带，不随地图半宽比例膨胀。</summary>
        public const float DefaultExitTriggerDepth = 1.25f;

        const float CoverageSampleStep = 0.25f;

        public static float NormalizeDepth(
            float authoredDepth,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            var depth = authoredDepth > 0.0001f ? authoredDepth : DefaultExitTriggerDepth;
            var maxAllowed = Math.Min(bounds.HalfWidth, bounds.HalfHeight) * 0.35f;
            if (maxAllowed < DefaultExitTriggerDepth)
                maxAllowed = DefaultExitTriggerDepth;
            if (depth > maxAllowed)
                depth = maxAllowed;
            if (depth < 0.05f)
                depth = 0.05f;
            return depth;
        }

        public static float ResolveDepthFromSession(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            var authored = world?.LocalMap != null ? world.LocalMap.ExitTriggerDepth : 0f;
            return NormalizeDepth(authored, bounds);
        }

        /// <summary>非 Interior 的 Surface LocalMap 才启用 Exit Zone 管线。</summary>
        public static bool ShouldPresent(SimulationWorld world)
        {
            if (world?.LocalMap == null)
                return false;
            if (world.LocalMap.IsInInterior)
                return false;
            if (string.IsNullOrWhiteSpace(world.LocalMap.ActiveMapLayoutId))
                return false;
            return true;
        }

        /// <summary>
        /// 只由 bounds+depth 生成 6 向 Canonical Geometry。与角色/Hex/Entry 无关。
        /// </summary>
        public static int BuildCanonicalGeometries(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            IList<SurfaceExitZoneGeometry> geometriesOut)
        {
            if (geometriesOut == null)
                return 0;
            geometriesOut.Clear();
            var depth = NormalizeDepth(exitTriggerDepth, bounds);
            for (var dir = 0; dir < 6; dir++)
                geometriesOut.Add(new SurfaceExitZoneGeometry(dir, bounds, depth));
            return geometriesOut.Count;
        }

        public static bool PointBelongsToDirection(
            float localX,
            float localY,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            int directionIndex)
        {
            var depth = NormalizeDepth(exitTriggerDepth, bounds);
            if (!WildernessLocalWorldProjection.IsInExitTriggerBand(localX, localY, bounds, depth))
                return false;
            if (WildernessLocalWorldProjection.IsOutsideBounds(localX, localY, bounds))
                return false;
            if (!WildernessLocalWorldProjection.TryClassifyExitTriggerDirection(
                    localX, localY, bounds, depth, out var dir))
                return false;
            return dir == NormalizeDirection(directionIndex);
        }

        /// <summary>精确覆盖矩形：仅 ExitTriggerDepth 边缘带 ∩ 方向扇区（无 AABB 膨胀）。</summary>
        public static int AppendCoverageRects(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            int directionIndex,
            IList<SurfaceExitCoverageRect> rectsOut)
        {
            if (rectsOut == null)
                return 0;
            var depth = NormalizeDepth(exitTriggerDepth, bounds);
            var dir = NormalizeDirection(directionIndex);
            var before = rectsOut.Count;

            AppendHorizontalEdgeRuns(
                bounds, depth, dir,
                y0: bounds.MaxY - depth, y1: bounds.MaxY,
                sampleY: bounds.MaxY - depth * 0.5f,
                rectsOut);
            AppendHorizontalEdgeRuns(
                bounds, depth, dir,
                y0: bounds.MinY, y1: bounds.MinY + depth,
                sampleY: bounds.MinY + depth * 0.5f,
                rectsOut);
            AppendVerticalEdgeRuns(
                bounds, depth, dir,
                x0: bounds.MaxX - depth, x1: bounds.MaxX,
                sampleX: bounds.MaxX - depth * 0.5f,
                rectsOut);
            AppendVerticalEdgeRuns(
                bounds, depth, dir,
                x0: bounds.MinX, x1: bounds.MinX + depth,
                sampleX: bounds.MinX + depth * 0.5f,
                rectsOut);

            return rectsOut.Count - before;
        }

        public static int CollectAvailability(
            SimulationWorld world,
            IList<SurfaceExitAvailability> availabilityOut)
        {
            if (availabilityOut == null)
                return 0;
            availabilityOut.Clear();
            for (var dir = 0; dir < 6; dir++)
            {
                if (!PlayerPartyWildernessTransitionService.TryEvaluateSurfaceExitLegality(
                        world, dir, out var neighbor, out var passable))
                {
                    availabilityOut.Add(new SurfaceExitAvailability(dir, false, default));
                    continue;
                }

                availabilityOut.Add(new SurfaceExitAvailability(dir, passable, neighbor));
            }

            return availabilityOut.Count;
        }

        /// <summary>Canonical Geometry + Runtime Availability → 可见 Zones（几何不变）。</summary>
        public static int CollectVisibleZones(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            IList<SurfaceExitVisibleZone> zonesOut)
        {
            if (zonesOut == null)
                return 0;
            zonesOut.Clear();
            if (!ShouldPresent(world))
                return 0;

            var depth = NormalizeDepth(exitTriggerDepth, bounds);
            var geometries = new List<SurfaceExitZoneGeometry>(6);
            BuildCanonicalGeometries(bounds, depth, geometries);

            var availability = new List<SurfaceExitAvailability>(6);
            CollectAvailability(world, availability);

            for (var i = 0; i < geometries.Count; i++)
            {
                var g = geometries[i];
                SurfaceExitAvailability a = default;
                var found = false;
                for (var j = 0; j < availability.Count; j++)
                {
                    if (availability[j].DirectionIndex != g.DirectionIndex)
                        continue;
                    a = availability[j];
                    found = true;
                    break;
                }

                if (!found || !a.IsPassable)
                    continue;
                zonesOut.Add(new SurfaceExitVisibleZone(g, a.DestinationHex));
            }

            return zonesOut.Count;
        }

        static void AppendHorizontalEdgeRuns(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth,
            int directionIndex,
            float y0,
            float y1,
            float sampleY,
            IList<SurfaceExitCoverageRect> rectsOut)
        {
            if (y1 <= y0)
                return;
            float runStart = 0f;
            var inRun = false;
            for (var x = bounds.MinX; x <= bounds.MaxX + 0.0001f; x += CoverageSampleStep)
            {
                var px = x > bounds.MaxX ? bounds.MaxX : x;
                var belongs = PointBelongsToDirection(px, sampleY, bounds, depth, directionIndex);
                if (belongs && !inRun)
                {
                    inRun = true;
                    runStart = px;
                }
                else if (!belongs && inRun)
                {
                    inRun = false;
                    var runEnd = px;
                    if (runEnd > runStart + 0.001f)
                        rectsOut.Add(new SurfaceExitCoverageRect(runStart, runEnd, y0, y1));
                }
            }

            if (inRun)
                rectsOut.Add(new SurfaceExitCoverageRect(runStart, bounds.MaxX, y0, y1));
        }

        static void AppendVerticalEdgeRuns(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth,
            int directionIndex,
            float x0,
            float x1,
            float sampleX,
            IList<SurfaceExitCoverageRect> rectsOut)
        {
            if (x1 <= x0)
                return;
            // 角落已由水平条覆盖：竖直条内缩避免重复加厚视觉（几何 Contains 仍用 PointBelongs）。
            var yMin = bounds.MinY + depth;
            var yMax = bounds.MaxY - depth;
            if (yMax <= yMin)
                return;

            float runStart = 0f;
            var inRun = false;
            for (var y = yMin; y <= yMax + 0.0001f; y += CoverageSampleStep)
            {
                var py = y > yMax ? yMax : y;
                var belongs = PointBelongsToDirection(sampleX, py, bounds, depth, directionIndex);
                if (belongs && !inRun)
                {
                    inRun = true;
                    runStart = py;
                }
                else if (!belongs && inRun)
                {
                    inRun = false;
                    var runEnd = py;
                    if (runEnd > runStart + 0.001f)
                        rectsOut.Add(new SurfaceExitCoverageRect(x0, x1, runStart, runEnd));
                }
            }

            if (inRun)
                rectsOut.Add(new SurfaceExitCoverageRect(x0, x1, runStart, yMax));
        }

        static int NormalizeDirection(int directionIndex)
        {
            var d = directionIndex % 6;
            if (d < 0)
                d += 6;
            return d;
        }
    }
}
