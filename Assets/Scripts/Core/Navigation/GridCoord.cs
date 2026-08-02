using System;

namespace XianXia.Core.Navigation
{
    public readonly struct GridCoord : IEquatable<GridCoord>
    {
        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);

        public override int GetHashCode() => unchecked((X * 397) ^ Y);

        public override string ToString() => "(" + X + "," + Y + ")";
    }
}
