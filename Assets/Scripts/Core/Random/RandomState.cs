using System;

namespace XianXia.Core.Random
{
    /// <summary>
    /// Full PRNG internal state for snapshot capture/restore (not seed-only).
    /// </summary>
    public readonly struct RandomState : IEquatable<RandomState>
    {
        public RandomState(ulong s0, ulong s1, RandomStreamId streamId)
        {
            S0 = s0;
            S1 = s1;
            StreamId = streamId;
        }

        public ulong S0 { get; }

        public ulong S1 { get; }

        public RandomStreamId StreamId { get; }

        public bool Equals(RandomState other) =>
            S0 == other.S0 && S1 == other.S1 && StreamId.Equals(other.StreamId);

        public override bool Equals(object obj) => obj is RandomState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = S0.GetHashCode();
                hash = hash * 31 + S1.GetHashCode();
                hash = hash * 31 + StreamId.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(RandomState left, RandomState right) => left.Equals(right);

        public static bool operator !=(RandomState left, RandomState right) => !left.Equals(right);
    }
}
