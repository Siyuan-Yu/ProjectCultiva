using System;
using XianXia.Core.Attributes;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// Learn manual + breakthrough for Cultivation Slice 0.1 (Mortal → QiRefining only).
    /// </summary>
    public sealed class CultivationService
    {
        public Result LearnManual(SimulationWorld world, EntityId subject, CultivationManualSpec manual)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World is null.");
            if (manual == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Manual spec is null.");
            if (manual.CultivationSpeed <= 0)
                return Result.Failure(ErrorCode.InvalidArgument, "CultivationSpeed must be > 0.");
            if (manual.BreakthroughProgress <= 0)
                return Result.Failure(ErrorCode.InvalidArgument, "BreakthroughProgress must be > 0.");

            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<CultivationComponent>(out var cultivation))
                return Result.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");
            if (!entity.TryGet<AttributesComponent>(out var attrs))
                return Result.Failure(ErrorCode.ComponentMissing, "AttributesComponent missing.");
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return Result.Failure(ErrorCode.ComponentMissing, "Lifecycle missing.");
            if (life.IsDead || life.IsRemoved)
                return Result.Failure(ErrorCode.InvalidOperation, "Subject cannot learn manual.");

            if (!TryParseRealm(manual.RequiredRealm, out var required))
                return Result.Failure(ErrorCode.InvalidArgument, "Unsupported RequiredRealm for slice 0.1.", manual.RequiredRealm);

            if (cultivation.Realm != required)
                return Result.Failure(ErrorCode.InvalidOperation, "Realm does not satisfy RequiredRealm.", cultivation.Realm + " vs " + required);

            if (cultivation.HasLearnedManual &&
                cultivation.LearnedManualId.HasValue &&
                cultivation.LearnedManualId.Value.Equals(manual.Id))
            {
                return Result.Success();
            }

            var source = new SourceRef(SourceKind.Manual, manual.Id, subject);
            if (manual.GrantedModifiers != null)
            {
                foreach (var grant in manual.GrantedModifiers)
                {
                    var added = attrs.AddModifier(grant.TargetAttribute, grant.Operation, grant.Value, source);
                    if (added.IsFailure)
                        return Result.Failure(added.Error);
                    world.Events.Publish(
                        EventType.ModifierAdded,
                        world.Tick,
                        actor: subject,
                        target: subject,
                        payload: grant.TargetAttribute + ":" + grant.Value);
                }
            }

            cultivation.LearnedManualId = manual.Id;
            cultivation.CultivationSpeed = manual.CultivationSpeed;
            cultivation.BreakthroughProgressRequired = manual.BreakthroughProgress;
            cultivation.RequiredRealmName = required.ToString();
            return Result.Success();
        }

        public Result TryBreakthrough(SimulationWorld world, EntityId subject)
        {
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<CultivationComponent>(out var cultivation))
                return Result.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");

            if (cultivation.Realm != RealmStage.Mortal)
                return Result.Success();

            if (cultivation.Progress < cultivation.BreakthroughProgressRequired)
                return Result.Success();

            cultivation.Realm = RealmStage.QiRefining;
            if (entity.TryGet<PersonalityProfileComponent>(out var profile) &&
                entity.TryGet<AttributesComponent>(out var attributes))
            {
                TalentGrowthRules.ApplyBreakthroughBonuses(profile, attributes);
            }

            world.Events.Publish(
                EventType.Breakthrough,
                world.Tick,
                actor: subject,
                target: subject,
                payload: nameof(RealmStage.Mortal) + "->" + nameof(RealmStage.QiRefining));
            return Result.Success();
        }

        public static bool TryParseRealm(string text, out RealmStage realm)
        {
            realm = RealmStage.Mortal;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (string.Equals(text, "凡人", StringComparison.Ordinal) ||
                string.Equals(text, "Mortal", StringComparison.OrdinalIgnoreCase))
            {
                realm = RealmStage.Mortal;
                return true;
            }

            if (string.Equals(text, "炼气", StringComparison.Ordinal) ||
                string.Equals(text, "QiRefining", StringComparison.OrdinalIgnoreCase))
            {
                realm = RealmStage.QiRefining;
                return true;
            }

            return Enum.TryParse(text, true, out realm);
        }
    }
}
