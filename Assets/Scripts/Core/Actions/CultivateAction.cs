using XianXia.Core.Concealment;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.Npc;

namespace XianXia.Core.Actions
{
    /// <summary>
    /// 打坐修炼：每 Tick +<see cref="CultivationProgressRules.BaseProgressPerTick"/>（可加天赋），到瓶颈封顶。
    /// </summary>
    public sealed class CultivateAction : IAction
    {
        public CultivateAction(ActionId id, EntityId subject, OrderId sourceOrderId, ulong durationTicks)
        {
            Id = id;
            Subject = subject;
            SourceOrderId = sourceOrderId;
            Clock = ActionClock.Start(durationTicks);
            Status = ActionStatus.Pending;
        }

        public ActionId Id { get; }
        public EntityId Subject { get; }
        public OrderId SourceOrderId { get; }
        public ActionStatus Status { get; private set; }
        public ActionClock Clock { get; private set; }

        public Result CanStart(SimulationWorld world)
        {
            if (AutonomousActionContinuationService.BlocksOrdinaryWork(world, Subject))
                return Result.Failure(ErrorCode.ActionCannotStart, AutonomousActionContinuationService.EncounterWorkBlockedReason);
            if (!world.Entities.TryGet(Subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (XianXia.Core.Combat.CombatInjuryService.HasBrokenMeridians(entity))
                return Result.Failure(ErrorCode.ActionCannotStart, "经脉断裂，无法修炼；需后续专门修复。");
            var burden = MortalTransportConstraintService.ValidateUnburdenedAction(world, Subject);
            if (burden.IsFailure) return burden;
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return Result.Failure(ErrorCode.ComponentMissing, "Lifecycle missing.");
            if (life.IsDead || life.IsRemoved || life.IsIncapacitated)
                return Result.Failure(ErrorCode.ActionCannotStart, "Subject cannot cultivate.", life.State.ToString());
            if (!entity.TryGet<CultivationComponent>(out _))
                return Result.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");
            return HuangcunSurvivalService.ValidateCultivation(world, entity);
        }

        public Result Start(SimulationWorld world)
        {
            var can = CanStart(world);
            if (can.IsFailure) return can;
            if (world.Entities.TryGet(Subject, out var entity) &&
                entity.TryGet<CultivationComponent>(out var cultivation) &&
                cultivation.CultivationSpeed <= 0)
            {
                cultivation.CultivationSpeed = CultivationProgressRules.BaseProgressPerTick;
            }

            Status = ActionStatus.Running;
            return Result.Success();
        }

        public Result Advance(SimulationWorld world)
        {
            if (AutonomousActionContinuationService.BlocksOrdinaryWork(world, Subject))
            { Cancel(); return Result.Failure(ErrorCode.ActionCannotStart, AutonomousActionContinuationService.EncounterWorkBlockedReason); }
            if (Status != ActionStatus.Running)
                return Result.Failure(ErrorCode.InvalidOperation, "Action not running.");

            if (!world.Entities.TryGet(Subject, out var entity) ||
                !entity.TryGet<CultivationComponent>(out var cultivation))
            {
                Status = ActionStatus.Failed;
                return Result.Failure(ErrorCode.ActionFailed, "CultivationComponent missing during cultivate.");
            }

            if (XianXia.Core.Combat.CombatInjuryService.HasBrokenMeridians(entity))
            { Status = ActionStatus.Failed; return Result.Failure(ErrorCode.ActionFailed, "经脉断裂，修炼中断。"); }
            var survivalCheck = HuangcunSurvivalService.ValidateCultivation(world, entity);
            if (survivalCheck.IsFailure) { Status = ActionStatus.Failed; world.HuangcunSurvival.Message = survivalCheck.Error.ToString(); return survivalCheck; }
            Clock = Clock.Consume(1);
            var talentBonus = 0;
            if (entity.TryGet<PersonalityProfileComponent>(out var profile))
                talentBonus = TalentGrowthRules.ExtraCultivateProgress(profile);

            // 有功法：用功法 cultivationSpeed；无功法则用感应境基础节奏。
            var gain = cultivation.HasLearnedManual && cultivation.CultivationSpeed > 0
                ? cultivation.CultivationSpeed
                : CultivationProgressRules.BaseProgressPerTick;
            gain += talentBonus;
            var survival = HuangcunSurvivalService.Applies(world, entity) && XianXia.Core.World.Strategic.HuangcunOpeningAccess.IsLocked(world);
            if (survival && !HuangcunSurvivalService.AtCamp(world, Subject)) gain = System.Math.Max(1, gain / 2);
            if (entity.TryGet<PersonalLifeComponent>(out var personalLife))
            {
                if (cultivation.Realm < RealmStage.GoldenCore) personalLife.Energy--;
                personalLife.CurrentMood--;
                if (survival && !cultivation.IsAtBottleneck) personalLife.Satiety--;
                personalLife.Clamp();
            }
            var cap = cultivation.BreakthroughProgressRequired;
            var previousProgress = cultivation.Progress;
            if (cap > 0 && cultivation.Progress + gain > cap)
                cultivation.Progress = cap;
            else
                cultivation.Progress += gain;

            // 可选荒村札记：只记录主控真正获得修为，不由听取建议或 NPC 自修代办。
            if (cultivation.Progress > previousProgress && world.Flags.Has("story:huangcun_production") &&
                world.Strategic.PlayerPartyContext?.ActiveCharacterId == Subject)
                StoryFlagService.Set(world, "story:huangcun_first_cultivation_tick", Subject);

            if (cultivation.HasLearnedManual)
                new SkillMasteryService().AddManualMasteryProgress(
                    world, Subject, SkillMasteryRules.CultivateManualProgressGain);

            if (entity.TryGet<PersonalConcealmentRiskComponent>(out var risk))
                risk.Add(ConcealmentExposureRules.CultivateRiskDelta(world, Subject));

            if (Clock.IsComplete)
                Status = ActionStatus.Completed;

            return Result.Success();
        }

        public void Cancel()
        {
            if (Status == ActionStatus.Pending || Status == ActionStatus.Running)
                Status = ActionStatus.Cancelled;
        }

        public void Restore(ActionStatus status, ActionClock clock)
        {
            Status = status;
            Clock = clock;
        }
    }
}
