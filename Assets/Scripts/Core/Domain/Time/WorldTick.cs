using System;

namespace XianXia.Core.Domain.Time
{
    /// <summary>
    /// Unique world timeline (ADR-0018). 1 tick = 5 game minutes; 1 day = 288 ticks (24h).
    /// Overflow uses checked arithmetic and throws <see cref="OverflowException"/> (invariant violation).
    /// </summary>
    public readonly struct WorldTick : IEquatable<WorldTick>, IComparable<WorldTick>
    {
        public const int GameMinutesPerTick = 5;
        public const int TicksPerDay = 288;

        public static WorldTick Zero => new WorldTick(0);

        public WorldTick(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public WorldTick Add(ulong delta)
        {
            checked
            {
                return new WorldTick(Value + delta);
            }
        }

        public WorldTick Subtract(ulong delta)
        {
            if (delta > Value)
                throw new OverflowException("WorldTick subtraction underflow.");
            return new WorldTick(Value - delta);
        }

        public bool TrySubtract(ulong delta, out WorldTick result)
        {
            if (delta > Value)
            {
                result = default;
                return false;
            }

            result = new WorldTick(Value - delta);
            return true;
        }

        public int CompareTo(WorldTick other) => Value.CompareTo(other.Value);

        public bool Equals(WorldTick other) => Value == other.Value;

        public override bool Equals(object obj) => obj is WorldTick other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(WorldTick left, WorldTick right) => left.Equals(right);

        public static bool operator !=(WorldTick left, WorldTick right) => !left.Equals(right);

        public static bool operator <(WorldTick left, WorldTick right) => left.Value < right.Value;

        public static bool operator >(WorldTick left, WorldTick right) => left.Value > right.Value;

        public static bool operator <=(WorldTick left, WorldTick right) => left.Value <= right.Value;

        public static bool operator >=(WorldTick left, WorldTick right) => left.Value >= right.Value;

        public static WorldTick operator +(WorldTick tick, ulong delta) => tick.Add(delta);

        public static WorldTick operator -(WorldTick tick, ulong delta) => tick.Subtract(delta);
    }
}
