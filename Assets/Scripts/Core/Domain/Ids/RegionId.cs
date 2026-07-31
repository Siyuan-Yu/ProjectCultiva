using System;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>
    /// Region identifier placeholder for M1 (single-region). Opaque ulong wrapper only.
    /// </summary>
    public readonly struct RegionId : IEquatable<RegionId>
    {
        public static RegionId None => new RegionId(0);

        public RegionId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool IsNone => Value == 0;

        public bool Equals(RegionId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is RegionId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(RegionId left, RegionId right) => left.Equals(right);

        public static bool operator !=(RegionId left, RegionId right) => !left.Equals(right);
    }
}
