using System;

namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// Core 连续世界平面坐标（不依赖 UnityEngine）。Odd-R HexLayout 世界平面。
    /// </summary>
    public struct WorldVec2 : IEquatable<WorldVec2>
    {
        public float X;
        public float Y;

        public WorldVec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static WorldVec2 Lerp(WorldVec2 a, WorldVec2 b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return new WorldVec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        public static float Distance(WorldVec2 a, WorldVec2 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(WorldVec2 other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object obj) => obj is WorldVec2 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString() => "(" + X.ToString("0.###") + "," + Y.ToString("0.###") + ")";
    }
}
