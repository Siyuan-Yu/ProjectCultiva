using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Orders
{
    public sealed class Order
    {
        public Order(
            OrderId id,
            EntityId subject,
            OrderType type,
            OrderSource source,
            ulong waitTicks = 0,
            AttributeId? modifierAttribute = null,
            ModifierOperation? modifierOperation = null,
            double modifierValue = 0,
            SourceRef? modifierSource = null)
        {
            Id = id;
            Subject = subject;
            Type = type;
            Source = source;
            WaitTicks = waitTicks;
            ModifierAttribute = modifierAttribute;
            ModifierOperation = modifierOperation;
            ModifierValue = modifierValue;
            ModifierSource = modifierSource;
        }

        public OrderId Id { get; }
        public EntityId Subject { get; }
        public OrderType Type { get; }
        public OrderSource Source { get; }
        public ulong WaitTicks { get; }
        public AttributeId? ModifierAttribute { get; }
        public ModifierOperation? ModifierOperation { get; }
        public double ModifierValue { get; }
        public SourceRef? ModifierSource { get; }
    }
}
