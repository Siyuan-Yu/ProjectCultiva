using System;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>Opaque Action instance id.</summary>
    public readonly struct ActionId : IEquatable<ActionId>
    {
        public static ActionId None => new ActionId(0);

        public ActionId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool IsNone => Value == 0;

        public bool Equals(ActionId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ActionId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(ActionId left, ActionId right) => left.Equals(right);

        public static bool operator !=(ActionId left, ActionId right) => !left.Equals(right);
    }
}
