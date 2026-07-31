using System;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>Opaque DomainEvent instance id.</summary>
    public readonly struct EventId : IEquatable<EventId>
    {
        public static EventId None => new EventId(0);

        public EventId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool IsNone => Value == 0;

        public bool Equals(EventId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is EventId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(EventId left, EventId right) => left.Equals(right);

        public static bool operator !=(EventId left, EventId right) => !left.Equals(right);
    }
}
