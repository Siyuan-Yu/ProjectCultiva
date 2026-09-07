using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    public sealed class SocialConsequenceService
    {
        readonly RelationshipService _relationships;

        public SocialConsequenceService(RelationshipService relationships = null)
        {
            _relationships = relationships ?? new RelationshipService();
        }

        public Result ApplyCharacterAttacked(
            SimulationWorld world, EntityId attacker, EntityId victim, EventId cause)
        {
            var affection = Record(world, victim, attacker, SocialAttitudeAxis.Affection, -10,
                "character_attacked_reaction", cause, victim);
            if (affection.result.IsFailure)
                return affection.result;
            var grudge = Record(world, victim, attacker, SocialAttitudeAxis.Grudge, 10,
                "character_attacked_reaction", cause, victim);
            if (grudge.result.IsFailure)
                return grudge.result;
            PublishReaction(world, victim, attacker, "character_attacked", affection.actual, victim, cause);
            return Result.Success();
        }

        public Result ApplyCharacterHelped(
            SimulationWorld world, EntityId helper, EntityId target, EventId cause)
        {
            var affection = Record(world, target, helper, SocialAttitudeAxis.Affection, 10,
                "character_helped_reaction", cause, target);
            if (affection.result.IsFailure)
                return affection.result;
            var trust = Record(world, target, helper, SocialAttitudeAxis.Trust, 5,
                "character_helped_reaction", cause, target);
            if (trust.result.IsFailure)
                return trust.result;
            PublishReaction(world, target, helper, "character_helped", affection.actual, target, cause);
            return Result.Success();
        }

        public Result ApplyCharacterRescued(
            SimulationWorld world, EntityId rescuer, EntityId target, EventId cause)
        {
            var affection = Record(world, target, rescuer, SocialAttitudeAxis.Affection, 25,
                "character_rescued_reaction", cause, target);
            if (affection.result.IsFailure)
                return affection.result;
            var trust = Record(world, target, rescuer, SocialAttitudeAxis.Trust, 15,
                "character_rescued_reaction", cause, target);
            if (trust.result.IsFailure)
                return trust.result;
            PublishReaction(world, target, rescuer, "character_rescued", affection.actual, target, cause);
            return Result.Success();
        }

        public Result ApplyCharacterKilled(
            SimulationWorld world, EntityId killer, EntityId victim, EventId cause)
        {
            foreach (var reactor in world.Entities.All)
            {
                if (reactor == null || reactor.Id == killer || reactor.Id == victim ||
                    !SocialBondService.IsCharacter(reactor) ||
                    !reactor.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved)
                    continue;

                ResolveKillDelta(SocialAttachmentQuery.Get(world, reactor.Id, victim),
                    out var affectionDelta, out var grudgeDelta);
                if (affectionDelta == 0 && grudgeDelta == 0)
                    continue;
                var affection = Record(world, reactor.Id, killer, SocialAttitudeAxis.Affection,
                    affectionDelta, "character_killed_reaction", cause, victim);
                if (affection.result.IsFailure)
                    return affection.result;
                if (grudgeDelta > 0)
                {
                    var grudge = Record(world, reactor.Id, killer, SocialAttitudeAxis.Grudge,
                        grudgeDelta, "character_killed_reaction", cause, victim);
                    if (grudge.result.IsFailure)
                        return grudge.result;
                }
                PublishReaction(world, reactor.Id, killer, "character_killed",
                    affection.actual, victim, cause);
            }
            return Result.Success();
        }

        static void ResolveKillDelta(int attachment, out int affection, out int grudge)
        {
            grudge = 0;
            if (attachment >= 70) { affection = -45; grudge = 70; }
            else if (attachment >= 40) { affection = -25; grudge = 40; }
            else if (attachment >= 15) { affection = -10; grudge = 15; }
            else if (attachment <= -70) affection = 45;
            else if (attachment <= -40) affection = 25;
            else if (attachment <= -15) affection = 10;
            else affection = 0;
        }

        (Result result, int actual) Record(
            SimulationWorld world,
            EntityId from,
            EntityId to,
            SocialAttitudeAxis axis,
            int delta,
            string reason,
            EventId cause,
            EntityId context)
        {
            var result = _relationships.RecordAttitudeDelta(
                world, from, to, axis, delta, reason, out var actual, cause, context);
            return (result, actual);
        }

        static void PublishReaction(
            SimulationWorld world,
            EntityId reactor,
            EntityId target,
            string reason,
            int actualAffectionDelta,
            EntityId context,
            EventId cause)
        {
            if (actualAffectionDelta == 0)
                return;
            world.Events.Publish(
                EventType.SocialReaction,
                world.Tick,
                reactor,
                target,
                SocialReactionPayload.Encode(reason, actualAffectionDelta, context),
                cause);
        }
    }
}
