using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Cultivation
{
    /// <summary>Learn manual + player-initiated breakthrough along the realm ladder.</summary>
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
                return Result.Failure(ErrorCode.InvalidArgument, "Unsupported RequiredRealm.", manual.RequiredRealm);

            // 感应境功法：Mortal 任意小阶可学；炼气功法需已入炼气。
            if (required == RealmStage.Mortal)
            {
                if (cultivation.Realm != RealmStage.Mortal)
                    return Result.Failure(ErrorCode.InvalidOperation, "Already past Mortal for this manual.");
            }
            else if (cultivation.Realm < required)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Realm does not satisfy RequiredRealm.",
                    cultivation.Realm + " vs " + required);
            }

            if (cultivation.HasLearnedManual &&
                cultivation.LearnedManualId.HasValue &&
                cultivation.LearnedManualId.Value.Equals(manual.Id))
            {
                return Result.Success();
            }

            // 一人一本：换功法时先卸掉旧本修饰与熟练。
            if (cultivation.HasLearnedManual && cultivation.LearnedManualId.HasValue)
            {
                var oldSource = new SourceRef(SourceKind.Manual, cultivation.LearnedManualId.Value, subject);
                attrs.RemoveBySource(oldSource);
                cultivation.ManualMastery = null;
                world.Events.Publish(
                    EventType.ModifierRemoved,
                    world.Tick,
                    actor: subject,
                    target: subject,
                    payload: "manual:" + cultivation.LearnedManualId.Value);
            }

            var source = new SourceRef(SourceKind.Manual, manual.Id, subject);
            if (cultivation.ManualMastery == null)
                cultivation.ManualMastery = SkillMasteryState.CreateEntry(
                    SkillMasteryLookup.EnsureOrDefaultManual(manual));

            // 属性修饰用定义绝对值，不随熟练连乘；修为速度按当前档绝对值。
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
            cultivation.CultivationSpeed = SkillMasteryLookup.ResolveCultivationSpeed(
                manual, cultivation.ManualMastery.Tier);
            cultivation.RequiredRealmName = required.ToString();
            SyncProgressRequired(world, cultivation, manual.BreakthroughProgress);
            return Result.Success();
        }

        /// <summary>熟练突破后按当前档绝对值重挂修为速度（修饰仍用定义绝对值）。</summary>
        public Result ReapplyManualModifiers(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<CultivationComponent>(out var cultivation) ||
                !cultivation.HasLearnedManual ||
                !cultivation.LearnedManualId.HasValue)
                return Result.Failure(ErrorCode.InvalidOperation, "No manual.");
            if (!entity.TryGet<AttributesComponent>(out var attrs))
                return Result.Failure(ErrorCode.ComponentMissing, "Attributes missing.");
            if (!world.TryGetManual(cultivation.LearnedManualId.Value, out var manual) || manual == null)
                return Result.Failure(ErrorCode.NotFound, "Manual missing.");

            var source = new SourceRef(SourceKind.Manual, manual.Id, subject);
            attrs.RemoveBySource(source);
            if (cultivation.ManualMastery == null)
                cultivation.ManualMastery = SkillMasteryState.CreateEntry(
                    SkillMasteryLookup.EnsureOrDefaultManual(manual));
            if (manual.GrantedModifiers != null)
            {
                foreach (var grant in manual.GrantedModifiers)
                {
                    var added = attrs.AddModifier(
                        grant.TargetAttribute, grant.Operation, grant.Value, source);
                    if (added.IsFailure)
                        return Result.Failure(added.Error);
                }
            }

            cultivation.CultivationSpeed = SkillMasteryLookup.ResolveCultivationSpeed(
                manual, cultivation.ManualMastery.Tier);
            return Result.Success();
        }

        public void SyncProgressRequired(
            SimulationWorld world,
            CultivationComponent cultivation,
            int fallbackRequired = 0)
        {
            if (cultivation == null)
                return;
            var ladder = world != null ? world.RealmLadder : null;
            if (ladder != null &&
                ladder.TryGetStep(cultivation.Realm, cultivation.MinorStage, out var step) &&
                step.ProgressRequired > 0)
            {
                cultivation.BreakthroughProgressRequired = step.ProgressRequired;
                return;
            }

            if (fallbackRequired > 0)
                cultivation.BreakthroughProgressRequired = fallbackRequired;
            else if (cultivation.BreakthroughProgressRequired <= 0)
                cultivation.BreakthroughProgressRequired = 100;
        }

        public void SyncAllEntities(SimulationWorld world)
        {
            if (world == null)
                return;
            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<CultivationComponent>(out var cult))
                    continue;
                SyncProgressRequired(world, cult);
                CombatDamageRules.EnsureVitals(entity);
            }
        }

        public bool CanAttemptBreakthrough(SimulationWorld world, EntityId subject, out string reason)
        {
            reason = string.Empty;
            if (!world.Entities.TryGet(subject, out var entity) ||
                !entity.TryGet<CultivationComponent>(out var cultivation))
            {
                reason = "无修炼数据";
                return false;
            }

            // 感应境突破不依赖功法；炼气及以后才需已得功法。
            if (cultivation.Realm >= RealmStage.QiRefining && !cultivation.HasLearnedManual)
            {
                reason = "炼气后需得功法，方可冲击瓶颈";
                return false;
            }

            if (!cultivation.IsAtBottleneck)
            {
                reason = "修为未满，未到瓶颈";
                return false;
            }

            var ladder = world.RealmLadder;
            if (ladder == null ||
                !ladder.TryGetStep(cultivation.Realm, cultivation.MinorStage, out _))
            {
                reason = "已无下一阶（或阶梯未配置）";
                return false;
            }

            return true;
        }

        /// <summary>Player-initiated breakthrough. Not called automatically on cultivate complete.</summary>
        public Result<BreakthroughReport> TryBreakthrough(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result<BreakthroughReport>.Failure(ErrorCode.InvalidArgument, "World is null.");
            if (!CanAttemptBreakthrough(world, subject, out var reason))
                return Result<BreakthroughReport>.Failure(ErrorCode.InvalidOperation, reason);

            if (!world.Entities.TryGet(subject, out var entity) ||
                !entity.TryGet<CultivationComponent>(out var cultivation))
                return Result<BreakthroughReport>.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");

            world.RealmLadder.TryGetStep(cultivation.Realm, cultivation.MinorStage, out var step);

            var actorName = string.IsNullOrEmpty(entity.DisplayName)
                ? subject.ToString()
                : entity.DisplayName;
            var fromLabel = RealmDisplay.Format(cultivation.Realm, cultivation.MinorStage);
            var before = SnapshotFinals(entity);

            var successChance = step.SuccessPercent;
            if (entity.TryGet<AttributesComponent>(out var attrs))
            {
                var comprehension = attrs.GetFinal(AttributeId.Comprehension);
                // 悟性轻推：每点约 +0.5%，上限 +15。
                var bonus = comprehension / 2;
                if (bonus > 15)
                    bonus = 15;
                successChance += bonus;
            }

            if (successChance > 99)
                successChance = 99;
            if (successChance < 1)
                successChance = 1;

            var report = new BreakthroughReport
            {
                Subject = subject,
                ActorName = actorName,
                FromRealmLabel = fromLabel,
                ToRealmLabel = fromLabel
            };

            var roll = world.Random.NextInt(0, 100);
            if (roll >= successChance)
            {
                var loss = cultivation.BreakthroughProgressRequired / 10;
                if (loss < 1)
                    loss = 1;
                cultivation.Progress -= loss;
                if (cultivation.Progress < 0)
                    cultivation.Progress = 0;

                report.Succeeded = false;
                report.ProgressLost = loss;
                report.Detail = "突破失败，修为小损 " + loss + "。";
                world.Events.Publish(
                    EventType.Breakthrough,
                    world.Tick,
                    actor: subject,
                    target: subject,
                    payload: "fail:" + fromLabel + ";roll=" + roll + ";need=" + successChance);
                return Result<BreakthroughReport>.Success(report);
            }

            cultivation.Realm = step.ToRealm;
            cultivation.MinorStage = step.ToMinor;
            cultivation.Progress = 0;
            SyncProgressRequired(world, cultivation);

            if (entity.TryGet<AttributesComponent>(out var attributes))
            {
                ApplyStepBonuses(attributes, step);
                if (entity.TryGet<PersonalityProfileComponent>(out var profile) && step.MajorRealmJump)
                    TalentGrowthRules.ApplyBreakthroughBonuses(profile, attributes);

                if (step.GrantSpiritPower > 0)
                {
                    var cur = attributes.GetBase(AttributeId.SpiritPower);
                    if (step.GrantSpiritPower > cur)
                        attributes.SetBase(AttributeId.SpiritPower, step.GrantSpiritPower);
                }

                CombatDamageRules.EnsureVitals(entity);
                if (entity.TryGet<CombatVitalsComponent>(out var vitals) &&
                    cultivation.Realm >= RealmStage.QiRefining)
                {
                    vitals.SyncMaxFromAttributes(attributes, fillSpirit: true);
                }
            }

            var toLabel = RealmDisplay.Format(cultivation.Realm, cultivation.MinorStage);
            report.Succeeded = true;
            report.ToRealmLabel = toLabel;
            report.Detail = actorName + " 由 " + fromLabel + " 突破至 " + toLabel + "。";
            FillAttributeDeltas(report, before, SnapshotFinals(entity));

            world.Events.Publish(
                EventType.Breakthrough,
                world.Tick,
                actor: subject,
                target: subject,
                payload: fromLabel + "->" + toLabel);
            return Result<BreakthroughReport>.Success(report);
        }

        /// <summary>蓄势被打断／强制失败：修为小损，不掷骰、不升境。</summary>
        public Result<BreakthroughReport> FailBreakthroughChannel(
            SimulationWorld world,
            EntityId subject,
            string detail)
        {
            if (world == null)
                return Result<BreakthroughReport>.Failure(ErrorCode.InvalidArgument, "World is null.");
            if (!world.Entities.TryGet(subject, out var entity) ||
                !entity.TryGet<CultivationComponent>(out var cultivation))
                return Result<BreakthroughReport>.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");

            var actorName = string.IsNullOrEmpty(entity.DisplayName)
                ? subject.ToString()
                : entity.DisplayName;
            var fromLabel = RealmDisplay.Format(cultivation.Realm, cultivation.MinorStage);
            var loss = cultivation.BreakthroughProgressRequired / 10;
            if (loss < 1)
                loss = 1;
            if (cultivation.Progress > 0)
            {
                cultivation.Progress -= loss;
                if (cultivation.Progress < 0)
                    cultivation.Progress = 0;
            }
            else
                loss = 0;

            var report = new BreakthroughReport
            {
                Subject = subject,
                ActorName = actorName,
                Succeeded = false,
                FromRealmLabel = fromLabel,
                ToRealmLabel = fromLabel,
                ProgressLost = loss,
                Detail = string.IsNullOrEmpty(detail)
                    ? "冲击被打断，突破失败。"
                    : detail
            };

            world.Events.Publish(
                EventType.Breakthrough,
                world.Tick,
                actor: subject,
                target: subject,
                payload: "fail:interrupt:" + fromLabel + ";loss=" + loss);
            return Result<BreakthroughReport>.Success(report);
        }

        static readonly AttributeId[] SnapshotOrder =
        {
            AttributeId.Physique, AttributeId.MaxHp, AttributeId.Attack, AttributeId.Defense, AttributeId.Speed,
            AttributeId.Stamina, AttributeId.SpiritSense, AttributeId.Comprehension,
            AttributeId.SpiritPower, AttributeId.Cultivation, AttributeId.MindState
        };

        static Dictionary<AttributeId, int> SnapshotFinals(Entity entity)
        {
            var map = new Dictionary<AttributeId, int>(SnapshotOrder.Length);
            if (entity == null || !entity.TryGet<AttributesComponent>(out var attrs))
                return map;
            for (var i = 0; i < SnapshotOrder.Length; i++)
            {
                var id = SnapshotOrder[i];
                map[id] = attrs.GetFinal(id);
            }

            return map;
        }

        static void FillAttributeDeltas(
            BreakthroughReport report,
            Dictionary<AttributeId, int> before,
            Dictionary<AttributeId, int> after)
        {
            if (report == null || after == null)
                return;
            for (var i = 0; i < SnapshotOrder.Length; i++)
            {
                var id = SnapshotOrder[i];
                after.TryGetValue(id, out var a);
                var b = 0;
                if (before != null)
                    before.TryGetValue(id, out b);
                if (a == b)
                    continue;
                report.AttributeChanges.Add(new BreakthroughReport.AttributeDelta(id, b, a));
            }
        }

        static void ApplyStepBonuses(AttributesComponent attributes, RealmLadderStep step)
        {
            if (attributes == null || step?.AttributeBonuses == null)
                return;
            foreach (var kv in step.AttributeBonuses)
            {
                if (kv.Value == 0)
                    continue;
                attributes.SetBase(kv.Key, attributes.GetBase(kv.Key) + kv.Value);
            }
        }

        public static bool TryParseRealm(string text, out RealmStage realm)
        {
            realm = RealmStage.Mortal;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (string.Equals(text, "凡人", StringComparison.Ordinal) ||
                string.Equals(text, "感应境", StringComparison.Ordinal) ||
                string.Equals(text, "Mortal", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Perception", StringComparison.OrdinalIgnoreCase))
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

            if (string.Equals(text, "筑基", StringComparison.Ordinal) ||
                string.Equals(text, "Foundation", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "FoundationBuilding", StringComparison.OrdinalIgnoreCase))
            {
                realm = RealmStage.Foundation;
                return true;
            }

            return Enum.TryParse(text, true, out realm);
        }
    }
}
