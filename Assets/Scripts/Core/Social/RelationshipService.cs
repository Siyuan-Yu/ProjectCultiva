using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    /// <summary>
    /// Only legal write path for relationships: Ledger append → cache refresh → DomainEvent.
    /// </summary>
    public sealed class RelationshipService
    {
        public Result Record(
            SimulationWorld world,
            EntityId from,
            EntityId to,
            int delta,
            string reasonTag,
            EventId? causeEventId = null)
            => RecordAttitudeDelta(
                world, from, to, SocialAttitudeAxis.Affection, delta, reasonTag,
                out _, causeEventId, null);

        public Result RecordAttitudeDelta(
            SimulationWorld world,
            EntityId from,
            EntityId to,
            SocialAttitudeAxis axis,
            int requestedDelta,
            string reasonTag,
            out int actualDelta,
            EventId? causeEventId = null,
            EntityId? contextEntityId = null)
        {
            actualDelta = 0;
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (from.IsNone || to.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "From／To EntityId must be non-None.");
            if (from == to)
                return Result.Failure(ErrorCode.InvalidArgument, "Cannot record relationship to self.");
            if (string.IsNullOrWhiteSpace(reasonTag))
                return Result.Failure(ErrorCode.InvalidArgument, "ReasonTag required.");
            if (!System.Enum.IsDefined(typeof(SocialAttitudeAxis), axis))
                return Result.Failure(ErrorCode.InvalidArgument, "Unknown SocialAttitudeAxis.");
            if (!world.Entities.TryGet(from, out var fromEntity))
                return Result.Failure(ErrorCode.EntityNotFound, "From entity missing.", from.ToString());
            if (!world.Entities.TryGet(to, out var toEntity))
                return Result.Failure(ErrorCode.EntityNotFound, "To entity missing.", to.ToString());

            EnsureRelationshipComponent(fromEntity);
            EnsureRelationshipComponent(toEntity);

            var current = world.Relationships.GetValue(from, to, axis);
            var next = SocialAttitudeRules.Clamp(axis, current + requestedDelta);
            actualDelta = next - current;
            if (actualDelta == 0)
                return Result.Success();

            var evt = new RelationshipEvent(
                world.Tick, from, to, axis, actualDelta, reasonTag.Trim(), causeEventId, contextEntityId);
            world.Relationships.Append(evt);

            RefreshPairCaches(world, from, to);

            world.Events.Publish(
                EventType.RelationshipChanged,
                world.Tick,
                actor: from,
                target: to,
                payload: "axis=" + axis + ";delta=" + actualDelta + ";reason=" + evt.ReasonTag +
                         ";value=" + world.Relationships.GetValue(from, to, axis));

            return Result.Success();
        }

        public int Score(SimulationWorld world, EntityId from, EntityId to)
        {
            if (world == null)
                return 0;
            return world.Relationships.Score(from, to);
        }

        public static void RefreshPairCaches(SimulationWorld world, EntityId a, EntityId b)
        {
            if (world == null || a.IsNone || b.IsNone)
                return;

            if (world.Entities.TryGet(a, out var ea) &&
                ea.TryGet<RelationshipComponent>(out var ca))
            {
                ca.ReplaceCachedAttitude(b, world.Relationships.GetAttitude(a, b));
            }

            if (world.Entities.TryGet(b, out var eb) &&
                eb.TryGet<RelationshipComponent>(out var cb))
            {
                cb.ReplaceCachedAttitude(a, world.Relationships.GetAttitude(b, a));
            }
        }

        public static void RebuildAllCaches(SimulationWorld world)
        {
            if (world == null)
                return;
            foreach (var entity in world.Entities.All)
                if (entity.TryGet<RelationshipComponent>(out var cache))
                    cache.ClearCache();
            var events = world.Relationships.Events;
            for (var i = 0; i < events.Count; i++)
                RefreshPairCaches(world, events[i].From, events[i].To);
        }

        static void EnsureRelationshipComponent(XianXia.Core.Entities.Entity entity)
        {
            if (!entity.TryGet<RelationshipComponent>(out _))
                entity.AddComponent(new RelationshipComponent());
        }
    }
}
