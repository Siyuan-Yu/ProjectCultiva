using System;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>Logical content asset reference. Never an absolute path or Unity GUID.</summary>
    public readonly struct AssetId : IEquatable<AssetId>
    {
        public AssetId(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("AssetId must be non-empty.", nameof(value));
            Value = value;
        }

        public string Value { get; }

        public bool Equals(AssetId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AssetId other && Equals(other);

        public override int GetHashCode() =>
            Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;

        public override string ToString() => Value;

        public static bool operator ==(AssetId left, AssetId right) => left.Equals(right);

        public static bool operator !=(AssetId left, AssetId right) => !left.Equals(right);
    }
}
