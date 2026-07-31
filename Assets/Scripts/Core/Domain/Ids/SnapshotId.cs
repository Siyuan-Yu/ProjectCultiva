using System;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>Opaque Snapshot instance id. Serialization shape is <see cref="Value"/>.</summary>
    public readonly struct SnapshotId : IEquatable<SnapshotId>
    {
        public static SnapshotId None => new SnapshotId(0);

        public SnapshotId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool IsNone => Value == 0;

        public bool Equals(SnapshotId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SnapshotId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(SnapshotId left, SnapshotId right) => left.Equals(right);

        public static bool operator !=(SnapshotId left, SnapshotId right) => !left.Equals(right);
    }
}
