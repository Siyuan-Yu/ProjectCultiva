using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    public sealed class QuestService
    {
        public Result TryStart(SimulationWorld world, string questId, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Quests.TryGetSpec(questId, out var spec) || !world.Quests.TryGet(questId, out var runtime))
                return Result.Failure(ErrorCode.NotFound, "Quest missing.", questId);

            if (runtime.Status == QuestStatus.Active ||
                runtime.Status == QuestStatus.ReadyToClaim ||
                runtime.Status == QuestStatus.Completed)
                return Result.Success();

            if (runtime.Status == QuestStatus.Failed)
                return Result.Failure(ErrorCode.InvalidOperation, "Quest already failed.", questId);

            if (!ContentConditionEvaluator.AllPass(world, subject, spec.OfferConditions))
                return Result.Failure(ErrorCode.InvalidOperation, "Quest offer conditions not met.", questId);

            runtime.Status = QuestStatus.Active;
            runtime.ProgressCount = 0;
            runtime.ProgressMax = ResolveProgressMax(spec);
            QuestDeadline.BindOnStart(spec, runtime, world);
            RefreshProgress(world, spec, runtime);
            world.Events.Publish(EventType.QuestStarted, world.Tick, target: subject, payload: questId);
            return Result.Success();
        }

        /// <summary>目标达成后领取奖励 → Completed。</summary>
        public Result TryClaimRewards(SimulationWorld world, string questId, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Quests.TryGetSpec(questId, out var spec) || !world.Quests.TryGet(questId, out var runtime))
                return Result.Failure(ErrorCode.NotFound, "Quest missing.", questId);

            if (runtime.Status != QuestStatus.ReadyToClaim)
                return Result.Failure(ErrorCode.InvalidOperation, "Quest not ready to claim.", questId);

            var rewarded = ContentOutcomeApplier.ApplyAll(world, subject, spec.Rewards);
            if (rewarded.IsFailure)
                return rewarded;

            runtime.Status = QuestStatus.Completed;
            world.Events.Publish(EventType.QuestRewardsClaimed, world.Tick, target: subject, payload: questId);
            return Result.Success();
        }

        /// <summary>放弃进行中的任务（需 Spec.Abandonable）。</summary>
        public Result TryAbandon(SimulationWorld world, string questId, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.Quests.TryGetSpec(questId, out var spec) || !world.Quests.TryGet(questId, out var runtime))
                return Result.Failure(ErrorCode.NotFound, "Quest missing.", questId);

            if (!spec.Abandonable)
                return Result.Failure(ErrorCode.InvalidOperation, "Quest cannot be abandoned.", questId);

            if (runtime.Status != QuestStatus.Active && runtime.Status != QuestStatus.ReadyToClaim)
                return Result.Failure(ErrorCode.InvalidOperation, "Quest not abandonable in current status.", questId);

            runtime.Status = QuestStatus.Inactive;
            runtime.ProgressCount = 0;
            runtime.ProgressMax = 0;
            runtime.AcceptedAtDayIndex = 0;
            runtime.DeadlineDayIndexExclusive = 0;
            world.Events.Publish(EventType.QuestAbandoned, world.Tick, target: subject, payload: questId);
            return Result.Success();
        }

        public Result Evaluate(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");

            foreach (var kv in world.Quests.Specs)
            {
                var spec = kv.Value;
                if (!world.Quests.TryGet(spec.Id, out var runtime))
                    continue;

                if (runtime.Status == QuestStatus.Inactive && spec.AutoOffer)
                {
                    if (ContentConditionEvaluator.AllPass(world, subject, spec.OfferConditions))
                        TryStart(world, spec.Id, subject);
                }

                if (runtime.Status != QuestStatus.Active)
                    continue;

                RefreshProgress(world, spec, runtime);

                if (QuestDeadline.IsExpired(world, runtime))
                {
                    FailQuest(world, spec, runtime, subject, "expired");
                    continue;
                }

                if (spec.FailConditions.Count > 0 &&
                    ContentConditionEvaluator.AllPass(world, subject, spec.FailConditions))
                {
                    FailQuest(world, spec, runtime, subject, spec.Id);
                    continue;
                }

                if (ContentConditionEvaluator.AllPass(world, subject, spec.CompleteConditions))
                {
                    // 目标达成 → 待领奖；奖励在 TryClaimRewards 发放。
                    runtime.Status = QuestStatus.ReadyToClaim;
                    if (runtime.ProgressMax > 0)
                        runtime.ProgressCount = runtime.ProgressMax;
                    world.Events.Publish(EventType.QuestCompleted, world.Tick, target: subject, payload: spec.Id);
                    new ContentEventService().TryTrigger(world, subject, "onQuestCompleted", spec.Id);
                }
            }

            return Result.Success();
        }

        static int ResolveProgressMax(QuestSpec spec)
        {
            if (spec?.CompleteConditions == null)
                return 0;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null || string.IsNullOrEmpty(c.Kind))
                    continue;
                if (string.Equals(c.Kind, "uniqueLaborAtLocation", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Kind, "uniqueHarvestAtLocation", System.StringComparison.OrdinalIgnoreCase))
                    return c.Amount > 0 ? c.Amount : 1;
            }

            var stockMax = SumStockAtLeastAmounts(spec);
            if (stockMax > 0)
                return stockMax;

            return spec.CompleteConditions.Count;
        }

        static void RefreshProgress(SimulationWorld world, QuestSpec spec, QuestRuntime runtime)
        {
            if (spec?.CompleteConditions == null)
                return;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null || string.IsNullOrEmpty(c.Kind))
                    continue;
                if (string.Equals(c.Kind, "uniqueHarvestAtLocation", System.StringComparison.OrdinalIgnoreCase))
                {
                    runtime.ProgressMax = c.Amount > 0 ? c.Amount : 1;
                    runtime.ProgressCount = ContentConditionEvaluator.CountUniqueHarvestersAtLocation(world, c.Id);
                    if (runtime.ProgressCount > runtime.ProgressMax)
                        runtime.ProgressCount = runtime.ProgressMax;
                    return;
                }

                if (string.Equals(c.Kind, "uniqueLaborAtLocation", System.StringComparison.OrdinalIgnoreCase))
                {
                    runtime.ProgressMax = c.Amount > 0 ? c.Amount : 1;
                    runtime.ProgressCount = ContentConditionEvaluator.CountUniqueLaborersAtLocation(
                        world,
                        c.Id,
                        ContentConditionEvaluator.UniqueLaborSeconds(c));
                    if (runtime.ProgressCount > runtime.ProgressMax)
                        runtime.ProgressCount = runtime.ProgressMax;
                    return;
                }
            }

            var stockMax = SumStockAtLeastAmounts(spec);
            if (stockMax > 0)
            {
                runtime.ProgressMax = stockMax;
                runtime.ProgressCount = SumStockAtLeastProgress(world, spec);
                if (runtime.ProgressCount > runtime.ProgressMax)
                    runtime.ProgressCount = runtime.ProgressMax;
                return;
            }

            var counterMax = SumCounterAtLeastAmounts(spec);
            if (counterMax > 0)
            {
                runtime.ProgressMax = counterMax;
                runtime.ProgressCount = SumCounterAtLeastProgress(world, spec);
                if (runtime.ProgressCount > runtime.ProgressMax)
                    runtime.ProgressCount = runtime.ProgressMax;
                return;
            }

            // Fallback: how many completeConditions already pass.
            runtime.ProgressMax = spec.CompleteConditions.Count;
            var done = 0;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                if (ContentConditionEvaluator.Pass(world, default, spec.CompleteConditions[i]))
                    done++;
            }

            runtime.ProgressCount = done;
        }

        static int SumStockAtLeastAmounts(QuestSpec spec)
        {
            if (spec?.CompleteConditions == null)
                return 0;
            var sum = 0;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null ||
                    !string.Equals(c.Kind, "stockAtLeast", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                sum += c.Amount > 0 ? c.Amount : 1;
            }

            return sum;
        }

        static int SumStockAtLeastProgress(SimulationWorld world, QuestSpec spec)
        {
            if (world == null || spec?.CompleteConditions == null)
                return 0;
            var sum = 0;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null ||
                    !string.Equals(c.Kind, "stockAtLeast", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var need = c.Amount > 0 ? c.Amount : 1;
                var have = world.Inventory.GetCount(c.Id);
                if (have > need)
                    have = need;
                sum += have;
            }

            return sum;
        }

        static int SumCounterAtLeastAmounts(QuestSpec spec)
        {
            if (spec?.CompleteConditions == null)
                return 0;
            var sum = 0;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null ||
                    !string.Equals(c.Kind, "counterAtLeast", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                sum += c.Amount > 0 ? c.Amount : 1;
            }

            return sum;
        }

        static int SumCounterAtLeastProgress(SimulationWorld world, QuestSpec spec)
        {
            if (world == null || spec?.CompleteConditions == null)
                return 0;
            var sum = 0;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null ||
                    !string.Equals(c.Kind, "counterAtLeast", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var need = c.Amount > 0 ? c.Amount : 1;
                var have = world.ContentCounters.Get(c.Id);
                if (have > need)
                    have = need;
                sum += have;
            }

            return sum;
        }

        static void FailQuest(
            SimulationWorld world,
            QuestSpec spec,
            QuestRuntime runtime,
            EntityId subject,
            string payload)
        {
            runtime.Status = QuestStatus.Failed;
            ContentOutcomeApplier.ApplyAll(world, subject, spec.FailResults);
            world.Events.Publish(EventType.QuestFailed, world.Tick, target: subject, payload: payload);
        }
    }
}
