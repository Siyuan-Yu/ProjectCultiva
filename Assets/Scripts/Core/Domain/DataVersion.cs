using System;

namespace XianXia.Core.Domain
{
    /// <summary>Simple content/data version token used by packages and snapshots.</summary>
    public readonly struct DataVersion : IEquatable<DataVersion>
    {
        public DataVersion(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("DataVersion must be non-empty.", nameof(value));
            Value = value;
        }

        public string Value { get; }

        public bool Equals(DataVersion other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is DataVersion other && Equals(other);

        public override int GetHashCode() =>
            Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;

        public override string ToString() => Value;

        public static bool operator ==(DataVersion left, DataVersion right) => left.Equals(right);

        public static bool operator !=(DataVersion left, DataVersion right) => !left.Equals(right);

        public static bool TryParse(string text, out DataVersion version)
        {
            if (string.IsNullOrEmpty(text))
            {
                version = default;
                return false;
            }

            version = new DataVersion(text);
            return true;
        }
    }
}
