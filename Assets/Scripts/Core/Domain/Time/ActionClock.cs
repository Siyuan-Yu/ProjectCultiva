using System;

namespace XianXia.Core.Domain.Time
{
    /// <summary>
    /// Per-action duration clock. Not a second world timeline; never mutates <see cref="WorldTick"/>.
    /// Durations are measured in world ticks.
    /// </summary>
    public readonly struct ActionClock : IEquatable<ActionClock>
    {
        public ActionClock(ulong totalDurationTicks, ulong remainingTicks)
        {
            if (remainingTicks > totalDurationTicks)
                throw new ArgumentOutOfRangeException(nameof(remainingTicks), "Remaining cannot exceed total duration.");

            TotalDurationTicks = totalDurationTicks;
            RemainingTicks = remainingTicks;
        }

        public static ActionClock Start(ulong totalDurationTicks) =>
            new ActionClock(totalDurationTicks, totalDurationTicks);

        public ulong TotalDurationTicks { get; }

        public ulong RemainingTicks { get; }

        public ulong ElapsedTicks => TotalDurationTicks - RemainingTicks;

        public bool IsComplete => RemainingTicks == 0;

        /// <summary>
        /// Consume up to <paramref name="ticks"/> of remaining duration. Remaining never goes below zero.
        /// </summary>
        public ActionClock Consume(ulong ticks)
        {
            if (ticks >= RemainingTicks)
                return new ActionClock(TotalDurationTicks, 0);

            return new ActionClock(TotalDurationTicks, RemainingTicks - ticks);
        }

        public bool Equals(ActionClock other) =>
            TotalDurationTicks == other.TotalDurationTicks &&
            RemainingTicks == other.RemainingTicks;

        public override bool Equals(object obj) => obj is ActionClock other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (TotalDurationTicks.GetHashCode() * 397) ^ RemainingTicks.GetHashCode();
            }
        }

        public override string ToString() => RemainingTicks + "/" + TotalDurationTicks;

        public static bool operator ==(ActionClock left, ActionClock right) => left.Equals(right);

        public static bool operator !=(ActionClock left, ActionClock right) => !left.Equals(right);
    }
}
