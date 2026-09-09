using System;

namespace XianXia.Core.World.Surface
{
    /// <summary>
    /// Rectangular continuous-surface grid coordinate.  This is deliberately not HexCoord:
    /// Surface streaming and authored storage must not inherit strategic topology.
    /// </summary>
    public readonly struct SurfaceChunkCoord : IEquatable<SurfaceChunkCoord>, IComparable<SurfaceChunkCoord>
    {
        public SurfaceChunkCoord(int x, int y) { X = x; Y = y; }
        public int X { get; }
        public int Y { get; }
        public bool Equals(SurfaceChunkCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is SurfaceChunkCoord other && Equals(other);
        public override int GetHashCode() { unchecked { return (X * 397) ^ Y; } }
        public int CompareTo(SurfaceChunkCoord other) { var x = X.CompareTo(other.X); return x != 0 ? x : Y.CompareTo(other.Y); }
        public override string ToString() => "(" + X + "," + Y + ")";
        public static bool operator ==(SurfaceChunkCoord a, SurfaceChunkCoord b) => a.Equals(b);
        public static bool operator !=(SurfaceChunkCoord a, SurfaceChunkCoord b) => !a.Equals(b);
    }
}
