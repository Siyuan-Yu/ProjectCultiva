using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    /// <summary>
    /// VS0.5 Phase D: relationship-gated thin recruitment into recruiter's faction.
    /// </summary>
    public sealed class RecruitService
    {
        readonly RelationshipService _relationships;

        public RecruitService(RelationshipService relationships = null)
        {
            _relationships = relationships ?? new RelationshipService();
        }

        /// <summary>
        /// Target joins recruiter's faction as Member when target→recruiter score ≥ threshold.
        /// </summary>
        public Result TryRecruit(SimulationWorld world, EntityId recruiter, EntityId target)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(recruiter, out var recruiterEntity))
                return Result.Failure(ErrorCode.EntityNotFound, "Recruiter missing.", recruiter.ToString());
            if (!world.Entities.TryGet(target, out var targetEntity))
                return Result.Failure(ErrorCode.EntityNotFound, "Target missing.", target.ToString());

            EnsureMembership(recruiterEntity);
            EnsureMembership(targetEntity);

            var recruiterMem = recruiterEntity.Get<FactionMembershipComponent>();
            var targetMem = targetEntity.Get<FactionMembershipComponent>();

            if (!recruiterMem.IsAffiliated)
                return Result.Failure(ErrorCode.InvalidOperation, "Recruiter has no faction.");

            if (targetMem.IsAffiliated &&
                string.Equals(targetMem.FactionId, recruiterMem.FactionId, System.StringComparison.Ordinal))
            {
                return Result.Failure(ErrorCode.InvalidOperation, "Target already in recruiter faction.");
            }

            var willingness = world.Relationships.Score(target, recruiter);
            if (willingness < SocialAlphaConstants.RecruitMinScore)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Relationship score below recruit threshold.",
                    "score=" + willingness + ";need=" + SocialAlphaConstants.RecruitMinScore);
            }

            targetMem.Assign(recruiterMem.FactionId, FactionRoleKind.Member);

            var bond = _relationships.Record(
                world,
                target,
                recruiter,
                5,
                SocialAlphaConstants.ReasonRecruited);
            if (bond.IsFailure)
                return bond;

            world.Events.Publish(
                EventType.FactionMembershipChanged,
                world.Tick,
                actor: recruiter,
                target: target,
                payload: "joined=" + targetMem.FactionId + ";role=Member");

            return Result.Success();
        }

        public Result TryLeave(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());

            EnsureMembership(entity);
            var mem = entity.Get<FactionMembershipComponent>();
            if (!mem.IsAffiliated)
                return Result.Failure(ErrorCode.InvalidOperation, "Subject has no faction to leave.");

            var leftFaction = mem.FactionId;
            mem.ClearMembership();

            world.Events.Publish(
                EventType.FactionMembershipChanged,
                world.Tick,
                actor: subject,
                target: subject,
                payload: "left=" + leftFaction);

            return Result.Success();
        }

        static void EnsureMembership(XianXia.Core.Entities.Entity entity)
        {
            if (!entity.TryGet<FactionMembershipComponent>(out _))
                entity.AddComponent(new FactionMembershipComponent());
        }
    }
}
