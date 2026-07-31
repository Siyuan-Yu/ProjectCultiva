using System;

namespace XianXia.Core.Orders
{
    public readonly struct OrderId : IEquatable<OrderId>
    {
        public static OrderId None => new OrderId(0);

        public OrderId(ulong value) { Value = value; }

        public ulong Value { get; }

        public bool IsNone => Value == 0;

        public bool Equals(OrderId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is OrderId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(OrderId left, OrderId right) => left.Equals(right);

        public static bool operator !=(OrderId left, OrderId right) => !left.Equals(right);
    }
}
