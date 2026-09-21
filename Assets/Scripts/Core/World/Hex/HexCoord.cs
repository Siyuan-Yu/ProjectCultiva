using System;

namespace XianXia.Core.World.Hex
{
    /// <summary>
    /// Legacy grid／tool Hex 坐标（Odd-R offset：Q=列, R=行；pointy-top 布局）。
    /// 在该兼容几何内，邻居／距离必须走 <see cref="HexMath"/>；正常 Surface 仍以 exact WorldPosition 为真源。
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public int Q { get; }
        public int R { get; }

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        /// <summary>
        /// Cube S 仅在 axial 空间有意义。存储为 Odd-R 时请先
        /// <see cref="HexMath.OffsetOddRToAxial"/>，不要直接用本属性算距离。
        /// </summary>
        public int S => -Q - R;

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;

        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Q * 397) ^ R;
            }
        }

        public static bool operator ==(HexCoord left, HexCoord right) => left.Equals(right);

        public static bool operator !=(HexCoord left, HexCoord right) => !left.Equals(right);

        public override string ToString() => "(" + Q + "," + R + ")";

        public static bool TryParse(string text, out HexCoord coord)
        {
            coord = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();
            if (text.StartsWith("(", StringComparison.Ordinal))
                text = text.Substring(1);
            if (text.EndsWith(")", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 1);

            var parts = text.Split(',');
            if (parts.Length != 2)
                return false;
            if (!int.TryParse(parts[0].Trim(), out var q))
                return false;
            if (!int.TryParse(parts[1].Trim(), out var r))
                return false;

            coord = new HexCoord(q, r);
            return true;
        }
    }
}
