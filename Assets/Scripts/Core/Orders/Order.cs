using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Schedule;

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
            SourceRef? modifierSource = null,
            string targetRef = null,
            ScheduleActivity? activity = null)
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
            TargetRef = targetRef ?? string.Empty;
            Activity = activity;
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
        /// <summary>WorkArea id (or Location id) for Move／Work orders.</summary>
        public string TargetRef { get; }
        /// <summary>Schedule activity for Work／Move context.</summary>
        public ScheduleActivity? Activity { get; }
    }
}
