using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    public sealed class SocialEventService
    {
        readonly SocialConsequenceService _consequences;

        public SocialEventService(SocialConsequenceService consequences = null)
        {
            _consequences = consequences ?? new SocialConsequenceService();
        }

        public Result RecordCharacterAttacked(SimulationWorld world, EntityId attacker, EntityId victim) =>
            Record(world, attacker, victim, EventType.SocialCharacterAttacked,
                e => _consequences.ApplyCharacterAttacked(world, attacker, victim, e.Id));

        public Result RecordCharacterKilled(SimulationWorld world, EntityId killer, EntityId victim) =>
            Record(world, killer, victim, EventType.SocialCharacterKilled,
                e => _consequences.ApplyCharacterKilled(world, killer, victim, e.Id));

        public Result RecordCharacterHelped(SimulationWorld world, EntityId helper, EntityId target) =>
            Record(world, helper, target, EventType.SocialCharacterHelped,
                e => _consequences.ApplyCharacterHelped(world, helper, target, e.Id));

        public Result RecordCharacterRescued(SimulationWorld world, EntityId rescuer, EntityId target) =>
            Record(world, rescuer, target, EventType.SocialCharacterRescued,
                e => _consequences.ApplyCharacterRescued(world, rescuer, target, e.Id));

        static Result Record(
            SimulationWorld world,
            EntityId actor,
            EntityId target,
            EventType type,
            System.Func<DomainEvent, Result> consequence)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld 不能为空。");
            if (actor.IsNone || target.IsNone || actor == target ||
                !world.Entities.TryGet(actor, out var actorEntity) ||
                !world.Entities.TryGet(target, out var targetEntity) ||
                !SocialBondService.IsCharacter(actorEntity) ||
                !SocialBondService.IsCharacter(targetEntity))
                return Result.Failure(ErrorCode.InvalidArgument, "社会事件双方必须是不同的 Character。");

            var objective = world.Events.Publish(type, world.Tick, actor, target);
            return consequence(objective);
        }
    }
}
