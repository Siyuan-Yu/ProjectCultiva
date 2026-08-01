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
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (from.IsNone || to.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "From／To EntityId must be non-None.");
            if (from == to)
                return Result.Failure(ErrorCode.InvalidArgument, "Cannot record relationship to self.");
            if (string.IsNullOrWhiteSpace(reasonTag))
                return Result.Failure(ErrorCode.InvalidArgument, "ReasonTag required.");
            if (!world.Entities.TryGet(from, out var fromEntity))
                return Result.Failure(ErrorCode.EntityNotFound, "From entity missing.", from.ToString());
            if (!world.Entities.TryGet(to, out var toEntity))
                return Result.Failure(ErrorCode.EntityNotFound, "To entity missing.", to.ToString());

            EnsureRelationshipComponent(fromEntity);
            EnsureRelationshipComponent(toEntity);

            var evt = new RelationshipEvent(world.Tick, from, to, delta, reasonTag.Trim(), causeEventId);
            world.Relationships.Append(evt);

            RefreshPairCaches(world, from, to);

            world.Events.Publish(
                EventType.RelationshipChanged,
                world.Tick,
                actor: from,
                target: to,
                payload: "delta=" + delta + ";reason=" + evt.ReasonTag + ";score=" + world.Relationships.Score(from, to));

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
                ca.ReplaceCachedToward(b, world.Relationships.Score(a, b));
            }

            if (world.Entities.TryGet(b, out var eb) &&
                eb.TryGet<RelationshipComponent>(out var cb))
            {
                cb.ReplaceCachedToward(a, world.Relationships.Score(b, a));
            }
        }

        static void EnsureRelationshipComponent(XianXia.Core.Entities.Entity entity)
        {
            if (!entity.TryGet<RelationshipComponent>(out _))
                entity.AddComponent(new RelationshipComponent());
        }
    }
}
