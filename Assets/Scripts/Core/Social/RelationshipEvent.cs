using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;

namespace XianXia.Core.Social
{
    /// <summary>
    /// One directed relationship change. Final scores are sums over these events (ADR-0017).
    /// </summary>
    public sealed class RelationshipEvent
    {
        public RelationshipEvent(
            WorldTick tick,
            EntityId from,
            EntityId to,
            int delta,
            string reasonTag,
            EventId? causeEventId = null)
        {
            Tick = tick;
            From = from;
            To = to;
            Delta = delta;
            ReasonTag = reasonTag ?? string.Empty;
            CauseEventId = causeEventId;
        }

        public WorldTick Tick { get; }

        public EntityId From { get; }

        public EntityId To { get; }

        public int Delta { get; }

        public string ReasonTag { get; }

        public EventId? CauseEventId { get; }
    }
}
