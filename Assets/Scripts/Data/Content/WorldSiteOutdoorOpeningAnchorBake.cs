using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Opening entity anchor 的 <b>source LocalMap 侧</b> authored placement 解析 + bake。
    ///
    /// <para>
    /// 迁移契约（§2/§5/§6）：opening NPC 的落点必须由「旧 Site source LocalMap 中的 authored
    /// placement」经<b>与 SitePlacements／SitePlaces 完全相同</b>的 bake transform
    /// （<see cref="WorldSiteOutdoorBakeTransform"/>）得到，而不是 Location center／arrival
    /// point／随便一个 walkable point。
    /// </para>
    ///
    /// <para>
    /// 多人共享同一个 location 时（例如 6 名凡人都在 <c>loc_ref_houses</c>），每个人拿到一个
    /// <b>source local point</b> 级别的确定性 slot：slot 0 = 该 location 的 authored
    /// presentation 点本身，其后按固定 8 向环展开并 clamp 进该 location 绑定的 placement 矩形
    /// （保证仍落在对应建筑／住房内部）。因此 §5 的校验对每个人都是
    /// 「source local point → shared bake transform ≈ checked-in anchor」。
    /// </para>
    ///
    /// 纯函数：无随机、无时间、无 Unity 依赖。
    /// </summary>
    public static class WorldSiteOutdoorOpeningAnchorBake
    {
        /// <summary>同一 location 内 slot 间距（source LocalMap cell 数）。</summary>
        public const int SlotSpacingSourceCells = 3;

        /// <summary>环展开的最大圈数（1 + 8×MaxRings 个候选）。</summary>
        public const int MaxSlotRings = 6;

        /// <summary>slot 去重量化（source local 世界单位）。</summary>
        const float SlotDistinctEpsilon = 1e-4f;

        /// <summary>
        /// 该 location 绑定的 source placements 在 LocalMap 世界单位下的矩形包围盒
        /// （cell 矩形而非 cell 中心，因此单个 zoneHousing 也能给出真实房间大小）。
        /// </summary>
        public static bool TryGetBoundPlacementLocalExtent(
            MapLayoutDefinition sourceLayout,
            string locationId,
            out float minX,
            out float minY,
            out float maxX,
            out float maxY)
        {
            minX = float.MaxValue;
            minY = float.MaxValue;
            maxX = float.MinValue;
            maxY = float.MinValue;
            if (sourceLayout?.Placements == null || string.IsNullOrWhiteSpace(locationId))
                return false;

            var cell = sourceLayout.CellSize > 0f ? sourceLayout.CellSize : 1f;
            var any = false;
            for (var i = 0; i < sourceLayout.Placements.Count; i++)
            {
                var placement = sourceLayout.Placements[i];
                if (placement == null ||
                    !string.Equals(placement.BoundLocationId, locationId, StringComparison.Ordinal))
                    continue;
                var w = placement.W > 0 ? placement.W : 1;
                var h = placement.H > 0 ? placement.H : 1;
                var x0 = sourceLayout.OriginX + placement.X * cell;
                var y0 = sourceLayout.OriginY + placement.Y * cell;
                var x1 = sourceLayout.OriginX + (placement.X + w) * cell;
                var y1 = sourceLayout.OriginY + (placement.Y + h) * cell;
                minX = Math.Min(minX, Math.Min(x0, x1));
                maxX = Math.Max(maxX, Math.Max(x0, x1));
                minY = Math.Min(minY, Math.Min(y0, y1));
                maxY = Math.Max(maxY, Math.Max(y0, y1));
                any = true;
            }

            return any;
        }

        /// <summary>
        /// 生成该 location 的 <paramref name="slotCount"/> 个确定性 source local slot 点。
        ///
        /// <para>
        /// slot 0 恒为 authored presentation 点本身（§3：不允许用 Location center 覆盖 authored
        /// LocalPosition）；只有当该 authored 点落在自己的 bound placement 矩形<b>之外</b>
        /// （Content 自相矛盾，例如 <c>base:loc_ref_spring</c> 的 presentation 不在 zone_spring 内）
        /// 才改用该矩形的中心，保证 §6「anchor 位于对应 bound location 的合理内部区域」。
        /// 其余 slot 按固定 8 向环展开，clamp 进 authored placement 矩形（有）与 source MapLayout
        /// bounds（始终）并保证互不重合。
        /// </para>
        /// </summary>
        public static bool TryBuildPlaceSlotLocalPoints(
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds sourceBounds,
            WorldVec2 placeLocalPosition,
            bool hasAuthoredExtent,
            float extentMinX,
            float extentMinY,
            float extentMaxX,
            float extentMaxY,
            int slotCount,
            List<WorldVec2> into)
        {
            if (into == null || slotCount <= 0 || !sourceBounds.IsValid)
                return false;

            into.Clear();
            var spacing = SlotSpacingSourceCells * sourceBounds.CellSize;
            if (spacing <= 0f)
                spacing = SlotSpacingSourceCells;

            var marginX = hasAuthoredExtent ? 0.5f * sourceBounds.CellSize : 0f;
            var marginY = marginX;

            var originX = placeLocalPosition.X;
            var originY = placeLocalPosition.Y;
            if (hasAuthoredExtent && !IsInsideExtent(placeLocalPosition.X, placeLocalPosition.Y,
                    extentMinX, extentMinY, extentMaxX, extentMaxY))
            {
                originX = (extentMinX + extentMaxX) * 0.5f;
                originY = (extentMinY + extentMaxY) * 0.5f;
            }

            for (var ring = 0; ring <= MaxSlotRings && into.Count < slotCount; ring++)
            {
                var steps = ring == 0 ? 1 : 8;
                for (var step = 0; step < steps && into.Count < slotCount; step++)
                {
                    var angle = step * (Math.PI / 4.0);
                    var radius = spacing * ring;
                    var candidate = new WorldVec2(
                        originX + (float)Math.Cos(angle) * radius,
                        originY + (float)Math.Sin(angle) * radius);

                    if (hasAuthoredExtent)
                    {
                        candidate = new WorldVec2(
                            Clamp(candidate.X, extentMinX + marginX, extentMaxX - marginX),
                            Clamp(candidate.Y, extentMinY + marginY, extentMaxY - marginY));
                    }

                    candidate = new WorldVec2(
                        Clamp(candidate.X, sourceBounds.MinX + marginX, sourceBounds.MaxX - marginX),
                        Clamp(candidate.Y, sourceBounds.MinY + marginY, sourceBounds.MaxY - marginY));

                    if (IsDuplicate(into, candidate))
                        continue;
                    into.Add(candidate);
                }
            }

            return into.Count > 0;
        }

        static bool IsInsideExtent(
            float x, float y, float minX, float minY, float maxX, float maxY) =>
            x >= minX && x <= maxX && y >= minY && y <= maxY;

        /// <summary>一个 slot 的完整 bake：source local slot 点 → canonical anchor。</summary>
        public static bool TryBakeSlotAnchor(
            IReadOnlyList<HexCoord> footprint,
            float hexSize,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds sourceBounds,
            WorldVec2 placeLocalPosition,
            bool hasAuthoredExtent,
            float extentMinX,
            float extentMinY,
            float extentMaxX,
            float extentMaxY,
            int slotIndex,
            out WorldVec2 sourceLocalPosition,
            out WorldVec2 anchor)
        {
            sourceLocalPosition = default;
            anchor = default;
            if (slotIndex < 0)
                return false;

            var slots = new List<WorldVec2>(slotIndex + 1);
            if (!TryBuildPlaceSlotLocalPoints(
                    sourceBounds, placeLocalPosition, hasAuthoredExtent,
                    extentMinX, extentMinY, extentMaxX, extentMaxY, slotIndex + 1, slots) ||
                slots.Count <= slotIndex)
                return false;

            sourceLocalPosition = slots[slotIndex];
            return WorldSiteOutdoorBakeTransform.TryBake(
                footprint, hexSize, sourceBounds, sourceLocalPosition, out anchor);
        }

        static bool IsDuplicate(List<WorldVec2> accepted, WorldVec2 candidate)
        {
            for (var i = 0; i < accepted.Count; i++)
            {
                var dx = accepted[i].X - candidate.X;
                var dy = accepted[i].Y - candidate.Y;
                if (dx * dx + dy * dy <= SlotDistinctEpsilon * SlotDistinctEpsilon)
                    return true;
            }

            return false;
        }

        static float Clamp(float value, float min, float max)
        {
            if (max < min)
                return (min + max) * 0.5f;
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
