using System;

namespace XianXia.Core.Random
{
    /// <summary>Opaque id for a named random stream (world / combat / loot, etc.).</summary>
    public readonly struct RandomStreamId : IEquatable<RandomStreamId>
    {
        public static RandomStreamId World => new RandomStreamId(1);
        public static RandomStreamId None => new RandomStreamId(0);

        public RandomStreamId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool Equals(RandomStreamId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is RandomStreamId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(RandomStreamId left, RandomStreamId right) => left.Equals(right);

        public static bool operator !=(RandomStreamId left, RandomStreamId right) => !left.Equals(right);
    }
}
