using System;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// HexWorld 平面方向 ↔ LocalMap 表现平面方向的唯一转换入口。
    /// Hex world (X,Y) 与 LocalMap (localX, localY) 共用同一视觉轴向：+X 右、+Y 上。
    /// </summary>
    public static class LocalMapHexDirectionProjection
    {
        /// <summary>
        /// 将 Hex 中心差分向量转为 LocalMap 平面方向（当前为恒等映射，集中在此便于审计轴翻转）。
        /// </summary>
        public static void HexWorldDeltaToLocalPlane(
            float hexWorldDeltaX,
            float hexWorldDeltaY,
            out float localPlaneX,
            out float localPlaneY)
        {
            localPlaneX = hexWorldDeltaX;
            localPlaneY = hexWorldDeltaY;
        }

        /// <summary>
        /// 从 LocalMap 中心沿方向射线投射到 PlayableBounds 周界，得 Exit Zone 中心。
        /// </summary>
        public static bool TryProjectToPerimeterCenter(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float localPlaneDirX,
            float localPlaneDirY,
            out float centerLocalX,
            out float centerLocalY)
        {
            centerLocalX = bounds.CenterX;
            centerLocalY = bounds.CenterY;
            if (!TryNormalize(localPlaneDirX, localPlaneDirY, out var nx, out var ny))
                return false;

            var t = RayDistanceToBounds(bounds, nx, ny);
            centerLocalX = bounds.CenterX + nx * t;
            centerLocalY = bounds.CenterY + ny * t;
            return true;
        }

        /// <summary>
        /// 在 perimeter 交点生成 partial-edge Canonical Slot（Detection = Presentation）。
        /// </summary>
        public static bool TryBuildSlotRect(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            float spanFraction,
            float centerLocalX,
            float centerLocalY,
            float localPlaneDirX,
            float localPlaneDirY,
            out SurfaceExitCoverageRect rect)
        {
            var edgeW = bounds.MaxX - bounds.MinX;
            var edgeH = bounds.MaxY - bounds.MinY;
            var span = Math.Abs(localPlaneDirX) >= Math.Abs(localPlaneDirY)
                ? SurfaceExitZoneCalculator.EffectiveSlotSpan(edgeH, spanFraction)
                : SurfaceExitZoneCalculator.EffectiveSlotSpan(edgeW, spanFraction);
            return TryBuildSlotRectAtSpan(
                bounds, exitTriggerDepth, span, centerLocalX, centerLocalY,
                localPlaneDirX, localPlaneDirY, out rect);
        }

        /// <summary>指定沿边绝对跨度（overlap 消解后）。</summary>
        public static bool TryBuildSlotRectAtSpan(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            float spanLength,
            float centerLocalX,
            float centerLocalY,
            float localPlaneDirX,
            float localPlaneDirY,
            out SurfaceExitCoverageRect rect)
        {
            rect = default;
            var depth = SurfaceExitZoneCalculator.NormalizeDepth(exitTriggerDepth, bounds);
            if (!TryNormalize(localPlaneDirX, localPlaneDirY, out var nx, out var ny))
                return false;

            var span = Math.Max(0.01f, spanLength);

            if (Math.Abs(nx) >= Math.Abs(ny))
            {
                if (nx > 0f)
                {
                    rect = new SurfaceExitCoverageRect(
                        bounds.MaxX - depth,
                        bounds.MaxX,
                        centerLocalY - span * 0.5f,
                        centerLocalY + span * 0.5f);
                }
                else
                {
                    rect = new SurfaceExitCoverageRect(
                        bounds.MinX,
                        bounds.MinX + depth,
                        centerLocalY - span * 0.5f,
                        centerLocalY + span * 0.5f);
                }
            }
            else if (ny > 0f)
            {
                rect = new SurfaceExitCoverageRect(
                    centerLocalX - span * 0.5f,
                    centerLocalX + span * 0.5f,
                    bounds.MaxY - depth,
                    bounds.MaxY);
            }
            else
            {
                rect = new SurfaceExitCoverageRect(
                    centerLocalX - span * 0.5f,
                    centerLocalX + span * 0.5f,
                    bounds.MinY,
                    bounds.MinY + depth);
            }

            return rect.Width > 0.0001f && rect.Height > 0.0001f;
        }

        /// <summary>
        /// 从 enteringHex 沿 cameFromHex 方向在 perimeter 内侧落点（Entry Inset）。
        /// </summary>
        public static bool TryGetEntryPositionNearEdge(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            HexCoord enteringHex,
            HexCoord cameFromHex,
            float hexSize,
            float exitTriggerDepth,
            float spanFraction,
            out float localX,
            out float localY)
        {
            localX = bounds.CenterX;
            localY = bounds.CenterY;
            if (hexSize <= 0.0001f)
                return false;

            HexMath.ToWorldPosition(cameFromHex, hexSize, out var ax, out var ay);
            HexMath.ToWorldPosition(enteringHex, hexSize, out var bx, out var by);
            HexWorldDeltaToLocalPlane(ax - bx, ay - by, out var ldx, out var ldy);
            if (!TryProjectToPerimeterCenter(bounds, ldx, ldy, out var cx, out var cy))
                return false;

            localX = cx;
            localY = cy;
            var inset = Math.Max(
                Math.Max(
                    WildernessLocalWorldProjection.NearEdgeMarginX(bounds),
                    WildernessLocalWorldProjection.NearEdgeMarginY(bounds)) * 1.5f,
                Math.Min(bounds.HalfWidth, bounds.HalfHeight) * 0.22f);

            if (!TryNormalize(bounds.CenterX - cx, bounds.CenterY - cy, out var inX, out var inY))
                return false;

            localX += inX * inset;
            localY += inY * inset;
            localX = Clamp(localX, bounds.MinX + 0.01f, bounds.MaxX - 0.01f);
            localY = Clamp(localY, bounds.MinY + 0.01f, bounds.MaxY - 0.01f);
            return true;
        }

        public static float RayDistanceToBounds(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float dirX,
            float dirY)
        {
            var tx = float.PositiveInfinity;
            var ty = float.PositiveInfinity;
            if (Math.Abs(dirX) > 0.0001f)
            {
                var toMax = (bounds.MaxX - bounds.CenterX) / dirX;
                var toMin = (bounds.MinX - bounds.CenterX) / dirX;
                if (toMax > 0f)
                    tx = Math.Min(tx, toMax);
                if (toMin > 0f)
                    tx = Math.Min(tx, toMin);
            }

            if (Math.Abs(dirY) > 0.0001f)
            {
                var toMax = (bounds.MaxY - bounds.CenterY) / dirY;
                var toMin = (bounds.MinY - bounds.CenterY) / dirY;
                if (toMax > 0f)
                    ty = Math.Min(ty, toMax);
                if (toMin > 0f)
                    ty = Math.Min(ty, toMin);
            }

            var t = Math.Min(tx, ty);
            if (!float.IsFinite(t) || t <= 0f)
                return Math.Min(bounds.HalfWidth, bounds.HalfHeight);
            return t;
        }

        static bool TryNormalize(float x, float y, out float nx, out float ny)
        {
            nx = 0f;
            ny = 0f;
            var len = (float)Math.Sqrt(x * x + y * y);
            if (len < 1e-6f)
                return false;
            nx = x / len;
            ny = y / len;
            return true;
        }

        static float Clamp(float v, float min, float max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }
    }
}
