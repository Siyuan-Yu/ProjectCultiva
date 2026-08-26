using System;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 普通 Wilderness Hex 的逻辑 Local ↔ Continuous WorldPosition 映射（非 GIS）。
    /// Local X/Y 对应表现层 X 与 Z（Y 轴）。
    /// </summary>
    public static class WildernessLocalWorldProjection
    {
        /// <summary>格内偏移半径系数，保证在出边前仍落在当前 Hex 内。</summary>
        public const float InteriorRadiusFactor = 0.45f;

        const float EdgeMarginFraction = 0.08f;

        static readonly float[] DirectionAngles = BuildDirectionAngles();

        public readonly struct WildernessLocalMapBounds
        {
            public WildernessLocalMapBounds(float minX, float maxX, float minY, float maxY)
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

            public float CenterX => (MinX + MaxX) * 0.5f;
            public float CenterY => (MinY + MaxY) * 0.5f;
            public float HalfWidth => Math.Max(0.01f, (MaxX - MinX) * 0.5f);
            public float HalfHeight => Math.Max(0.01f, (MaxY - MinY) * 0.5f);

            public static WildernessLocalMapBounds FromOriginSize(
                float originX,
                float originY,
                float cellSize,
                int width,
                int height)
            {
                var cs = cellSize > 0.0001f ? cellSize : 1f;
                var w = Math.Max(1, width);
                var h = Math.Max(1, height);
                return new WildernessLocalMapBounds(
                    originX,
                    originX + w * cs,
                    originY,
                    originY + h * cs);
            }
        }

        public static int OppositeDirection(int directionIndex) => (directionIndex + 3) % 6;

        public static bool TryProjectLocalToWorld(
            HexCoord currentHex,
            float localX,
            float localY,
            WildernessLocalMapBounds bounds,
            float hexSize,
            out WorldVec2 worldPosition)
        {
            worldPosition = default;
            if (hexSize <= 0.0001f)
                return false;

            var normX = (localX - bounds.CenterX) / bounds.HalfWidth;
            var normY = (localY - bounds.CenterY) / bounds.HalfHeight;
            HexMath.ToWorldPosition(currentHex, hexSize, out var cx, out var cy);
            var radius = hexSize * InteriorRadiusFactor;
            worldPosition = new WorldVec2(
                cx + normX * radius,
                cy + normY * radius);
            return true;
        }

        public static bool TryProjectWorldToLocal(
            WorldVec2 worldPosition,
            WildernessLocalMapBounds bounds,
            float hexSize,
            out float localX,
            out float localY)
        {
            localX = bounds.CenterX;
            localY = bounds.CenterY;
            if (hexSize <= 0.0001f)
                return false;

            var hex = HexMath.WorldToHex(worldPosition.X, worldPosition.Y, hexSize);
            HexMath.ToWorldPosition(hex, hexSize, out var cx, out var cy);
            var radius = hexSize * InteriorRadiusFactor;
            if (radius <= 0.0001f)
                return false;

            var normX = (worldPosition.X - cx) / radius;
            var normY = (worldPosition.Y - cy) / radius;
            // 钳制到可玩矩形，避免投影落在 playable bounds 外导致 Active「看不见」。
            localX = Clamp(bounds.CenterX + normX * bounds.HalfWidth, bounds.MinX, bounds.MaxX);
            localY = Clamp(bounds.CenterY + normY * bounds.HalfHeight, bounds.MinY, bounds.MaxY);
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

        /// <summary>近缘带宽（与 Classify 一致）：至少半格，或半宽的 EdgeMarginFraction。</summary>
        public static float NearEdgeMarginX(WildernessLocalMapBounds bounds) =>
            Math.Max(bounds.HalfWidth * EdgeMarginFraction, 0.55f);

        public static float NearEdgeMarginY(WildernessLocalMapBounds bounds) =>
            Math.Max(bounds.HalfHeight * EdgeMarginFraction, 0.55f);

        public static bool IsInNearEdgeBand(
            float localX,
            float localY,
            WildernessLocalMapBounds bounds)
        {
            if (IsOutsideBounds(localX, localY, bounds))
                return true;
            var marginX = NearEdgeMarginX(bounds);
            var marginY = NearEdgeMarginY(bounds);
            return localX <= bounds.MinX + marginX ||
                   localX >= bounds.MaxX - marginX ||
                   localY <= bounds.MinY + marginY ||
                   localY >= bounds.MaxY - marginY;
        }

        /// <summary>
        /// Canonical Exit Trigger Band：从 playable 边界向内 ExitTriggerDepth。
        /// 与 Presentation / Detection 共用；不随地图半宽比例膨胀。
        /// </summary>
        public static bool IsInExitTriggerBand(
            float localX,
            float localY,
            WildernessLocalMapBounds bounds,
            float exitTriggerDepth)
        {
            if (IsOutsideBounds(localX, localY, bounds))
                return false;
            var depth = SurfaceExitZoneCalculator.NormalizeDepth(exitTriggerDepth, bounds);
            return localX <= bounds.MinX + depth ||
                   localX >= bounds.MaxX - depth ||
                   localY <= bounds.MinY + depth ||
                   localY >= bounds.MaxY - depth;
        }

        /// <summary>Safe Interior：在 playable 内且不在近缘带（Rearm 条件）。</summary>
        public static bool IsInSafeInterior(
            float localX,
            float localY,
            WildernessLocalMapBounds bounds) =>
            !IsOutsideBounds(localX, localY, bounds) &&
            !IsInNearEdgeBand(localX, localY, bounds);

        /// <summary>
        /// 跨边意图：必须从界内穿越到界外／跨出近缘（Inside→Outside），禁止“靠边站着”误触发。
        /// 保留给 Ping-Pong / 既有 EDGE 测试；正式 Surface Exit Detection 请用
        /// <see cref="TryResolveExitTriggerIntent"/>。
        /// </summary>
        public static bool TryResolveCrossingIntent(
            float fromX,
            float fromY,
            float toX,
            float toY,
            WildernessLocalMapBounds bounds,
            out int directionIndex)
        {
            directionIndex = 0;
            var moveX = toX - fromX;
            var moveY = toY - fromY;
            if (moveX * moveX + moveY * moveY < 1e-12f)
                return false;

            // 正式：上一帧在内，本帧意图出界。
            var fromOutside = IsOutsideBounds(fromX, fromY, bounds);
            var toOutside = IsOutsideBounds(toX, toY, bounds);
            if (!fromOutside && toOutside)
                return TryClassifyEdgeDirection(toX, toY, bounds, out directionIndex);

            // WalkGrid Clamp：无法真正 OutOfBounds 时，要求从 Safe Interior 走进近缘并朝外。
            if (fromOutside)
                return false;
            if (IsInSafeInterior(fromX, fromY, bounds) &&
                IsInNearEdgeBand(toX, toY, bounds))
            {
                var fromCx = fromX - bounds.CenterX;
                var fromCy = fromY - bounds.CenterY;
                if (fromCx * moveX + fromCy * moveY <= 0f)
                    return false;
                return TryClassifyEdgeDirection(toX, toY, bounds, out directionIndex);
            }

            return false;
        }

        /// <summary>
        /// Canonical Exit Trigger Detection：已在 Enabled Trigger Zone 内 + 继续向外，
        /// 或 playable bounds Inside→Outside。进入 Zone 本身不触发。
        /// </summary>
        public static bool TryResolveExitTriggerIntent(
            float fromX,
            float fromY,
            float toX,
            float toY,
            WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            out int directionIndex)
        {
            directionIndex = 0;
            var moveX = toX - fromX;
            var moveY = toY - fromY;
            if (moveX * moveX + moveY * moveY < 1e-12f)
                return false;

            var fromOutside = IsOutsideBounds(fromX, fromY, bounds);
            var toOutside = IsOutsideBounds(toX, toY, bounds);
            if (fromOutside)
                return false;

            if (!fromOutside && toOutside)
                return TryClassifyExitTriggerDirection(
                    toX, toY, bounds, exitTriggerDepth, out directionIndex);

            var depth = SurfaceExitZoneCalculator.NormalizeDepth(exitTriggerDepth, bounds);
            if (!IsInExitTriggerBand(fromX, fromY, bounds, depth))
                return false;
            if (!toOutside && !IsInExitTriggerBand(toX, toY, bounds, depth))
                return false;

            var fromCx = fromX - bounds.CenterX;
            var fromCy = fromY - bounds.CenterY;
            if (fromCx * moveX + fromCy * moveY <= 0f)
                return false;

            return TryClassifyExitTriggerDirection(
                toX, toY, bounds, exitTriggerDepth, out directionIndex);
        }

        /// <summary>Exit Trigger Band（或界外）上的方向分类。</summary>
        public static bool TryClassifyExitTriggerDirection(
            float localX,
            float localY,
            WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            out int directionIndex)
        {
            directionIndex = 0;
            var depth = SurfaceExitZoneCalculator.NormalizeDepth(exitTriggerDepth, bounds);
            var outside = IsOutsideBounds(localX, localY, bounds);
            if (!outside && !IsInExitTriggerBand(localX, localY, bounds, depth))
                return false;

            var dx = localX - bounds.CenterX;
            var dy = localY - bounds.CenterY;
            if (Math.Abs(dx) < 0.0001f && Math.Abs(dy) < 0.0001f)
            {
                directionIndex = 0;
                return true;
            }

            directionIndex = AngleToDirection((float)Math.Atan2(dy, dx));
            return true;
        }

        public static bool IsOutsideBounds(
            float localX,
            float localY,
            WildernessLocalMapBounds bounds) =>
            localX < bounds.MinX ||
            localX > bounds.MaxX ||
            localY < bounds.MinY ||
            localY > bounds.MaxY;

        /// <summary>
        /// 在 Local 越界或靠近外缘时，按中心 atan2 映射到 AxialDirections 扇区（E=0…SE=5）。
        /// </summary>
        public static bool TryClassifyEdgeDirection(
            float localX,
            float localY,
            WildernessLocalMapBounds bounds,
            out int directionIndex)
        {
            directionIndex = 0;
            var dx = localX - bounds.CenterX;
            var dy = localY - bounds.CenterY;
            var outside = IsOutsideBounds(localX, localY, bounds);
            if (!outside && !IsInNearEdgeBand(localX, localY, bounds))
                return false;

            if (Math.Abs(dx) < 0.0001f && Math.Abs(dy) < 0.0001f)
            {
                directionIndex = 0;
                return true;
            }

            var angle = (float)Math.Atan2(dy, dx);
            directionIndex = AngleToDirection(angle);
            return true;
        }

        /// <summary>进入邻格后，在 LocalMap 对侧边缘内侧落点（明确 Interior Inset，避开近缘带）。</summary>
        public static void GetLocalPositionNearEdge(
            WildernessLocalMapBounds bounds,
            int entryDirectionIndex,
            out float localX,
            out float localY)
        {
            var dir = NormalizeDirection(entryDirectionIndex);
            var angle = DirectionAngles[dir];
            var cos = (float)Math.Cos(angle);
            var sin = (float)Math.Sin(angle);
            var edgeDist = RayDistanceToBounds(bounds, cos, sin);
            // 比近缘带宽更深：避免 Spawn 后立刻被 Classify 为仍在跨边。
            var inset = Math.Max(
                Math.Max(NearEdgeMarginX(bounds), NearEdgeMarginY(bounds)) * 1.5f,
                Math.Min(bounds.HalfWidth, bounds.HalfHeight) * 0.22f);
            var t = Math.Max(0f, edgeDist - inset);
            localX = bounds.CenterX + cos * t;
            localY = bounds.CenterY + sin * t;
            // 钳制进 playable 内（绝不生成在界外）。
            localX = Clamp(localX, bounds.MinX + 0.01f, bounds.MaxX - 0.01f);
            localY = Clamp(localY, bounds.MinY + 0.01f, bounds.MaxY - 0.01f);
        }

        /// <summary>
        /// 跨 Hex 后连续 WorldPosition：沿 current→toHex 中心推进极小步，直到 DerivedHex==toHex，
        /// 再加一丁点 interior epsilon。禁止 Snap 到 toHex.Center。
        /// </summary>
        public static WorldVec2 ComputeCrossEdgeWorldPosition(
            HexCoord fromHex,
            HexCoord toHex,
            WorldVec2 currentWorldPosition,
            float hexSize)
        {
            if (hexSize <= 0.0001f)
                return currentWorldPosition;

            HexMath.ToWorldPosition(toHex, hexSize, out var tcx, out var tcy);
            HexMath.ToWorldPosition(fromHex, hexSize, out var fcx, out var fcy);

            var eps = Math.Max(hexSize * 0.03f, 0.02f);
            var toCenter = new WorldVec2(tcx, tcy);
            var fromCenter = new WorldVec2(fcx, fcy);

            // 连续推进：从当前点朝 toHex 中心走，直到明确落入 toHex。
            var pos = currentWorldPosition;
            var dx = tcx - pos.X;
            var dy = tcy - pos.Y;
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6f)
            {
                dx = tcx - fcx;
                dy = tcy - fcy;
                len = (float)Math.Sqrt(dx * dx + dy * dy);
                pos = fromCenter;
            }

            if (len > 1e-6f)
            {
                var ux = dx / len;
                var uy = dy / len;
                for (var i = 0; i < 16; i++)
                {
                    pos = new WorldVec2(pos.X + ux * eps, pos.Y + uy * eps);
                    if (HexMath.WorldToHex(pos.X, pos.Y, hexSize) == toHex)
                    {
                        // 再一丁点 interior，消除边界歧义。
                        pos = new WorldVec2(pos.X + ux * eps, pos.Y + uy * eps);
                        break;
                    }
                }
            }

            if (HexMath.WorldToHex(pos.X, pos.Y, hexSize) != toHex)
            {
                // 回退：落在 toHex 靠 from 一侧的近缘内侧（仍非 Center Snap）。
                var fdx = tcx - fcx;
                var fdy = tcy - fcy;
                var flen = (float)Math.Sqrt(fdx * fdx + fdy * fdy);
                if (flen > 1e-6f)
                {
                    var radius = hexSize * InteriorRadiusFactor;
                    pos = new WorldVec2(
                        tcx - (fdx / flen) * radius,
                        tcy - (fdy / flen) * radius);
                    // epsilon toward center
                    pos = new WorldVec2(
                        pos.X + (tcx - pos.X) * 0.08f,
                        pos.Y + (tcy - pos.Y) * 0.08f);
                }
                else
                {
                    pos = toCenter;
                }
            }

            return pos;
        }

        static float[] BuildDirectionAngles()
        {
            var angles = new float[6];
            HexMath.ToWorldPosition(new HexCoord(0, 0), 1f, out var cx, out var cy);
            for (var i = 0; i < 6; i++)
            {
                var neighbor = HexMath.Neighbor(new HexCoord(0, 0), i);
                HexMath.ToWorldPosition(neighbor, 1f, out var nx, out var ny);
                angles[i] = (float)Math.Atan2(ny - cy, nx - cx);
            }

            return angles;
        }

        static int NormalizeDirection(int directionIndex)
        {
            var d = directionIndex % 6;
            if (d < 0)
                d += 6;
            return d;
        }

        static int AngleToDirection(float angle)
        {
            var best = 0;
            var bestDiff = float.MaxValue;
            for (var i = 0; i < 6; i++)
            {
                var diff = AbsAngleDiff(angle, DirectionAngles[i]);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }

            return best;
        }

        static float AbsAngleDiff(float a, float b)
        {
            var diff = a - b;
            while (diff > Math.PI)
                diff -= (float)(2.0 * Math.PI);
            while (diff < -Math.PI)
                diff += (float)(2.0 * Math.PI);
            return Math.Abs(diff);
        }

        static float RayDistanceToBounds(WildernessLocalMapBounds bounds, float dirX, float dirY)
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
    }
}
