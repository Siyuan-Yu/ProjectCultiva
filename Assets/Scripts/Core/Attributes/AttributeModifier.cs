using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Attributes
{
    public sealed class AttributeModifier
    {
        public AttributeModifier(
            ModifierId id,
            AttributeId target,
            ModifierOperation operation,
            double value,
            SourceRef source)
        {
            Id = id;
            Target = target;
            Operation = operation;
            Value = value;
            Source = source;
        }

        public ModifierId Id { get; }

        public AttributeId Target { get; }

        public ModifierOperation Operation { get; }

        /// <summary>
        /// Fixed: flat addend. Percentage: fraction where 0.20 means +20%.
        /// </summary>
        public double Value { get; }

        public SourceRef Source { get; }
    }
}
