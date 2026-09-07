using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;

namespace XianXia.Core.Social
{
    /// <summary>
    /// 一次定向态度变化。RelationshipLedger 事件流是 Runtime／SaveLoad authority。
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
            : this(tick, from, to, SocialAttitudeAxis.Affection, delta, reasonTag, causeEventId, null)
        {
        }

        public RelationshipEvent(
            WorldTick tick,
            EntityId from,
            EntityId to,
            SocialAttitudeAxis axis,
            int delta,
            string reasonTag,
            EventId? causeEventId = null,
            EntityId? contextEntityId = null)
        {
            Tick = tick;
            From = from;
            To = to;
            Axis = axis;
            Delta = delta;
            ReasonTag = reasonTag ?? string.Empty;
            CauseEventId = causeEventId;
            ContextEntityId = contextEntityId;
        }

        public WorldTick Tick { get; }

        public EntityId From { get; }

        public EntityId To { get; }

        public SocialAttitudeAxis Axis { get; }

        public int Delta { get; }

        public string ReasonTag { get; }

        public EventId? CauseEventId { get; }

        public EntityId? ContextEntityId { get; }
    }
}
