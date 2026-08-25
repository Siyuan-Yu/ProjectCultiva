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
            var outside = localX < bounds.MinX ||
                          localX > bounds.MaxX ||
                          localY < bounds.MinY ||
                          localY > bounds.MaxY;
            if (!outside)
            {
                var marginX = bounds.HalfWidth * EdgeMarginFraction;
                var marginY = bounds.HalfHeight * EdgeMarginFraction;
                var nearEdge = localX <= bounds.MinX + marginX ||
                               localX >= bounds.MaxX - marginX ||
                               localY <= bounds.MinY + marginY ||
                               localY >= bounds.MaxY - marginY;
                if (!nearEdge)
                    return false;
            }

            if (Math.Abs(dx) < 0.0001f && Math.Abs(dy) < 0.0001f)
            {
                directionIndex = 0;
                return true;
            }

            var angle = (float)Math.Atan2(dy, dx);
            directionIndex = AngleToDirection(angle);
            return true;
        }

        /// <summary>进入邻格后，在 LocalMap 对侧边缘附近落点（Materialize 用）。</summary>
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
            var t = RayDistanceToBounds(bounds, cos, sin) * 0.88f;
            localX = bounds.CenterX + cos * t;
            localY = bounds.CenterY + sin * t;
        }

        /// <summary>跨 Hex 边界后，在邻格内靠近来向边缘的连续世界坐标。</summary>
        public static WorldVec2 ComputeCrossEdgeWorldPosition(
            HexCoord fromHex,
            HexCoord toHex,
            WorldVec2 currentWorldPosition,
            float hexSize)
        {
            if (hexSize <= 0.0001f)
                return currentWorldPosition;

            HexMath.ToWorldPosition(fromHex, hexSize, out var fcx, out var fcy);
            HexMath.ToWorldPosition(toHex, hexSize, out var tcx, out var tcy);
            var dx = tcx - fcx;
            var dy = tcy - fcy;
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len <= 0.0001f)
                return currentWorldPosition;

            var radius = hexSize * InteriorRadiusFactor;
            return new WorldVec2(
                tcx - (dx / len) * radius,
                tcy - (dy / len) * radius);
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
