using System;

namespace XianXia.Core.Domain.Ids
{
    /// <summary>
    /// Opaque AttributeModifier instance id. Minimal stable handle only — not the Modifier model.
    /// </summary>
    public readonly struct ModifierId : IEquatable<ModifierId>
    {
        public static ModifierId None => new ModifierId(0);

        public ModifierId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool IsNone => Value == 0;

        public bool Equals(ModifierId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ModifierId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(ModifierId left, ModifierId right) => left.Equals(right);

        public static bool operator !=(ModifierId left, ModifierId right) => !left.Equals(right);
    }
}
