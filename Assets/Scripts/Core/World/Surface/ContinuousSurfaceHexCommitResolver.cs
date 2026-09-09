using System;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Surface
{
    /// <summary>W1C WorldPosition→CurrentHex commit rule; deliberately independent of legacy LocalMap projection.</summary>
    public static class ContinuousSurfaceHexCommitResolver
    {
        public const float HysteresisFraction = 0.04f;
        public static HexCoord Resolve(HexCoord committed, WorldVec2 position, float hexSize)
        {
            var size = hexSize > 0f ? hexSize : 1f;
            var derived = HexMath.WorldToHex(position.X, position.Y, size);
            if (derived.Equals(committed) || HexMath.Distance(committed, derived) > 1) return derived;
            HexMath.ToWorldPosition(committed, size, out var ax, out var ay);
            HexMath.ToWorldPosition(derived, size, out var bx, out var by);
            var dx = bx - ax; var dy = by - ay;
            var length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0.0001f) return committed;
            var mx = (ax + bx) * 0.5f; var my = (ay + by) * 0.5f;
            var depth = ((position.X - mx) * dx + (position.Y - my) * dy) / length;
            return depth > size * HysteresisFraction ? derived : committed;
        }
    }
}
