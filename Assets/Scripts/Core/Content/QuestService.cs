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

            if (runtime.Status == QuestStatus.Active || runtime.Status == QuestStatus.Completed)
                return Result.Success();

            if (!ContentConditionEvaluator.AllPass(world, subject, spec.OfferConditions))
                return Result.Failure(ErrorCode.InvalidOperation, "Quest offer conditions not met.", questId);

            runtime.Status = QuestStatus.Active;
            runtime.ProgressCount = 0;
            world.Events.Publish(EventType.QuestStarted, world.Tick, target: subject, payload: questId);
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

                if (spec.FailConditions.Count > 0 &&
                    ContentConditionEvaluator.AllPass(world, subject, spec.FailConditions))
                {
                    runtime.Status = QuestStatus.Failed;
                    ContentOutcomeApplier.ApplyAll(world, subject, spec.FailResults);
                    world.Events.Publish(EventType.QuestFailed, world.Tick, target: subject, payload: spec.Id);
                    continue;
                }

                if (ContentConditionEvaluator.AllPass(world, subject, spec.CompleteConditions))
                {
                    runtime.Status = QuestStatus.Completed;
                    runtime.ProgressCount++;
                    var rewarded = ContentOutcomeApplier.ApplyAll(world, subject, spec.Rewards);
                    if (rewarded.IsFailure)
                        return rewarded;
                    world.Events.Publish(EventType.QuestCompleted, world.Tick, target: subject, payload: spec.Id);
                    new ContentEventService().TryTrigger(world, subject, "onQuestCompleted", spec.Id);
                }
            }

            return Result.Success();
        }
    }
}
