using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;

namespace XianXia.Core.Events
{
    public sealed class DomainEvent
    {
        public DomainEvent(
            EventId id,
            EventType type,
            WorldTick tick,
            EntityId? actor = null,
            EntityId? target = null,
            string payload = null,
            EventId? causeEventId = null)
        {
            Id = id;
            Type = type;
            Tick = tick;
            Actor = actor;
            Target = target;
            Payload = payload ?? string.Empty;
            CauseEventId = causeEventId;
        }

        public EventId Id { get; }

        public EventType Type { get; }

        public WorldTick Tick { get; }

        public EntityId? Actor { get; }

        public EntityId? Target { get; }

        public string Payload { get; }

        public EventId? CauseEventId { get; }
    }
}
