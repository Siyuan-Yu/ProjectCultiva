using System;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>
    /// Runtime entity instance id. Never interchangeable with <see cref="DefinitionId"/>.
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static EntityId None => new EntityId(0);

        public EntityId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool IsNone => Value == 0;

        public bool Equals(EntityId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }
}
