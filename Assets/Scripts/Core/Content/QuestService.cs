using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Inventory;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>Fixed quests and character-issued quest instances share one authoritative service.</summary>
    public sealed class QuestService
    {
        public Result TryStart(SimulationWorld world, string questId, EntityId subject)
        {
            if (!TryResolve(world, questId, out var spec, out var runtime, out var failure)) return failure;
            if (spec.IsCharacterCommission || string.Equals(spec.AcceptanceMode, "interaction", StringComparison.OrdinalIgnoreCase))
                return Result.Failure(ErrorCode.InvalidOperation, "Character commission requires interaction target.", questId);
            if (!ContentConditionEvaluator.AllPass(world, subject, spec.OfferConditions))
                return Result.Failure(ErrorCode.InvalidOperation, "Quest offer conditions not met.", questId);
            return StartRuntime(world, spec, runtime, subject, false);
        }

        public bool CanOfferFromTarget(SimulationWorld world, string questDefinitionId, ContentInteractionContext context)
        {
            if (!TryValidateCommissionContext(world, questDefinitionId, context, out var spec, out _, out _)) return false;
            if (!ContentConditionEvaluator.AllPass(world, context.ActorId, spec.OfferConditions, context)) return false;
            if (!world.Quests.TryGetForIssuer(questDefinitionId, context.TargetEntityId, out var existing)) return true;
            return existing.Status == QuestStatus.Inactive && !QuestDeadline.IsExpired(world, existing);
        }

        public Result TryAcceptFromTarget(SimulationWorld world, string questDefinitionId, ContentInteractionContext context)
        {
            if (!TryValidateCommissionContext(world, questDefinitionId, context, out var spec, out var opportunity, out var failure)) return failure;
            if (!ContentConditionEvaluator.AllPass(world, context.ActorId, spec.OfferConditions, context))
                return Result.Failure(ErrorCode.InvalidOperation, "Quest offer conditions not met.", questDefinitionId);
            if (!world.Quests.TryGetForIssuer(questDefinitionId, context.TargetEntityId, out var runtime))
            {
                var name = context.TargetDisplayName;
                if (string.IsNullOrWhiteSpace(name) && world.Entities.TryGet(context.TargetEntityId, out var issuer))
                    name = string.IsNullOrWhiteSpace(issuer.DisplayName) ? issuer.DefinitionId.ToString() : issuer.DisplayName;
                runtime = world.Quests.CreateCommission(questDefinitionId, context.TargetEntityId,
                    opportunity?.InstanceId, name);
            }
            return StartRuntime(world, spec, runtime, context.ActorId, true);
        }

        public Result TryDeliverToTarget(SimulationWorld world, string questDefinitionId, ContentInteractionContext context)
        {
            if (!TryValidateCommissionContext(world, questDefinitionId, context, out var spec, out _, out var failure)) return failure;
            if (!world.Quests.TryGetForIssuer(questDefinitionId, context.TargetEntityId, out var runtime))
                return Result.Failure(ErrorCode.NotFound, "Quest instance for issuer is missing.", questDefinitionId);
            if (runtime.Status != QuestStatus.Active || runtime.DeliveryCompleted)
                return Result.Failure(ErrorCode.InvalidOperation, "Quest instance is not awaiting delivery.", runtime.QuestInstanceId);
            if (QuestDeadline.IsExpired(world, runtime))
                return Result.Failure(ErrorCode.InvalidOperation, "Quest instance has expired.", runtime.QuestInstanceId);
            if (spec.DeliveryRequirements.Count == 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Quest has no delivery requirements.", questDefinitionId);
            for (var i = 0; i < spec.DeliveryRequirements.Count; i++)
            {
                var requirement = spec.DeliveryRequirements[i];
                var amount = requirement.Amount > 0 ? requirement.Amount : 1;
                if (world.InventoryCatalog.HasTag(requirement.ItemId, "resource"))
                {
                    var consumed = PlayerStrategicResourceService.TryConsume(world, requirement.ItemId, amount, out _);
                    if (consumed.IsFailure) return consumed;
                }
                else if (world.Inventory.GetCount(requirement.ItemId) < amount || !world.Inventory.TryRemoveAll(requirement.ItemId, amount))
                    return Result.Failure(ErrorCode.InvalidOperation, "Party bag stock insufficient.", requirement.ItemId);
            }
            runtime.DeliveryCompleted = true;
            runtime.ProgressMax = spec.DeliveryRequirements.Count;
            runtime.ProgressCount = runtime.ProgressMax;
            runtime.Status = QuestStatus.ReadyToClaim;
            world.Events.Publish(EventType.QuestCompleted, world.Tick, actor: context.ActorId,
                target: runtime.IssuerEntityId, payload: runtime.QuestInstanceId);
            return Result.Success();
        }

        public bool CanDeliverToTarget(SimulationWorld world, string questDefinitionId, ContentInteractionContext context)
        {
            if (!TryValidateCommissionContext(world, questDefinitionId, context, out var spec, out _, out _)) return false;
            if (!world.Quests.TryGetForIssuer(questDefinitionId, context.TargetEntityId, out var runtime) ||
                runtime.Status != QuestStatus.Active || runtime.DeliveryCompleted || QuestDeadline.IsExpired(world, runtime)) return false;
            if (spec.DeliveryRequirements.Count == 0) return false;
            for (var i = 0; i < spec.DeliveryRequirements.Count; i++)
            {
                var requirement = spec.DeliveryRequirements[i];
                var amount = requirement.Amount > 0 ? requirement.Amount : 1;
                var have = world.InventoryCatalog.HasTag(requirement.ItemId, "resource")
                    ? PlayerStrategicResourceService.GetPlayerAccessibleCount(world, requirement.ItemId)
                    : world.Inventory.GetCount(requirement.ItemId);
                if (have < amount) return false;
            }
            return true;
        }

        public Result TryClaimRewards(SimulationWorld world, string questKey, EntityId subject)
        {
            if (!TryResolve(world, questKey, out var spec, out var runtime, out var failure)) return failure;
            if (runtime.Status != QuestStatus.ReadyToClaim)
                return Result.Failure(ErrorCode.InvalidOperation, "Quest not ready to claim.", questKey);
            var rewardContext = new ContentInteractionContext
            { ActorId = subject, IssuerEntityId = runtime.IssuerEntityId };
            var rewarded = ContentOutcomeApplier.ApplyAll(world, subject, spec.Rewards, rewardContext, () =>
            { runtime.Status = QuestStatus.Completed; return Result.Success(); });
            if (rewarded.IsFailure) return rewarded;
            world.Events.Publish(EventType.QuestRewardsClaimed, world.Tick, target: subject, payload: runtime.QuestInstanceId);
            return Result.Success();
        }

        public Result TryAbandon(SimulationWorld world, string questKey, EntityId subject)
        {
            if (!TryResolve(world, questKey, out var spec, out var runtime, out var failure)) return failure;
            if (!spec.Abandonable) return Result.Failure(ErrorCode.InvalidOperation, "Quest cannot be abandoned.", questKey);
            if (runtime.Status != QuestStatus.Active || runtime.DeliveryCompleted)
                return Result.Failure(ErrorCode.InvalidOperation, "Only an undelivered active quest can be abandoned.", questKey);
            runtime.Status = QuestStatus.Inactive;
            runtime.ProgressCount = 0; runtime.ProgressMax = 0;
            if (!spec.IsCharacterCommission) { runtime.AcceptedAtDayIndex = 0; runtime.DeadlineDayIndexExclusive = 0; }
            world.Events.Publish(EventType.QuestAbandoned, world.Tick, target: subject, payload: runtime.QuestInstanceId);
            return Result.Success();
        }

        public Result Evaluate(SimulationWorld world, EntityId subject)
        {
            if (world == null) return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            foreach (var runtime in world.Quests.Runtime.Values)
            {
                if (!world.Quests.TryGetSpec(runtime.QuestId, out var spec)) continue;
                if (runtime.Status == QuestStatus.Inactive && spec.IsCharacterCommission &&
                    runtime.DeadlineDayIndexExclusive > 0 && QuestDeadline.IsExpired(world, runtime))
                { FailQuest(world, spec, runtime, subject, "expired_after_abandon"); continue; }
                if (runtime.Status == QuestStatus.Inactive && !spec.IsCharacterCommission && spec.AutoOffer &&
                    ContentConditionEvaluator.AllPass(world, subject, spec.OfferConditions))
                    StartRuntime(world, spec, runtime, subject, false);
                if (runtime.Status != QuestStatus.Active) continue;
                RefreshProgress(world, spec, runtime);
                if (QuestDeadline.IsExpired(world, runtime)) { FailQuest(world, spec, runtime, subject, "expired"); continue; }
                if (spec.FailConditions.Count > 0 && ContentConditionEvaluator.AllPass(world, subject, spec.FailConditions))
                { FailQuest(world, spec, runtime, subject, spec.Id); continue; }
                if (spec.DeliveryRequirements.Count > 0 || spec.CompleteConditions.Count == 0) continue;
                if (ContentConditionEvaluator.AllPass(world, subject, spec.CompleteConditions))
                {
                    runtime.Status = QuestStatus.ReadyToClaim;
                    if (runtime.ProgressMax > 0) runtime.ProgressCount = runtime.ProgressMax;
                    world.Events.Publish(EventType.QuestCompleted, world.Tick, target: subject, payload: runtime.QuestInstanceId);
                    new ContentEventService().TryTrigger(world, subject, "onQuestCompleted", spec.Id);
                }
            }
            return Result.Success();
        }

        public void FailIssuerCommissions(SimulationWorld world, EntityId issuer, string opportunityInstanceId, string reason)
        {
            if (world == null || issuer.IsNone) return;
            foreach (var runtime in world.Quests.Runtime.Values)
            {
                if (runtime.IssuerEntityId != issuer || runtime.Status != QuestStatus.Active || runtime.DeliveryCompleted) continue;
                if (!string.IsNullOrEmpty(opportunityInstanceId) && !string.Equals(runtime.SourceOpportunityInstanceId, opportunityInstanceId, StringComparison.Ordinal)) continue;
                if (world.Quests.TryGetSpec(runtime.QuestId, out var spec))
                    FailQuest(world, spec, runtime, runtime.AcceptedByEntityId, reason);
            }
        }

        static bool TryResolve(SimulationWorld world, string questKey, out QuestSpec spec, out QuestRuntime runtime, out Result failure)
        {
            spec = null; runtime = null;
            if (world == null) { failure = Result.Failure(ErrorCode.InvalidArgument, "World null."); return false; }
            if (!world.Quests.TryGet(questKey, out runtime) || !world.Quests.TryGetSpec(runtime.QuestId, out spec))
            { failure = Result.Failure(ErrorCode.NotFound, "Quest runtime or template missing.", questKey); return false; }
            failure = Result.Success(); return true;
        }

        static bool TryValidateCommissionContext(SimulationWorld world, string questDefinitionId,
            ContentInteractionContext context, out QuestSpec spec, out WorldOpportunityInstance opportunity, out Result failure)
        {
            spec = null; opportunity = null;
            if (world == null || context == null || context.ActorId.IsNone || context.TargetEntityId.IsNone)
            { failure = Result.Failure(ErrorCode.InvalidArgument, "Commission requires actor and target context."); return false; }
            if (!world.Entities.TryGet(context.TargetEntityId, out _))
            { failure = Result.Failure(ErrorCode.EntityNotFound, "Commission issuer is missing."); return false; }
            if (!world.Quests.TryGetSpec(questDefinitionId, out spec) || !spec.IsCharacterCommission ||
                !string.Equals(spec.AcceptanceMode, "interaction", StringComparison.OrdinalIgnoreCase))
            { failure = Result.Failure(ErrorCode.InvalidOperation, "Quest is not an interaction commission.", questDefinitionId); return false; }
            world.WorldOpportunities.TryGetByEntity(context.TargetEntityId, out opportunity);
            failure = Result.Success(); return true;
        }

        static Result StartRuntime(SimulationWorld world, QuestSpec spec, QuestRuntime runtime, EntityId subject, bool commission)
        {
            if (runtime.Status == QuestStatus.Active || runtime.Status == QuestStatus.ReadyToClaim || runtime.Status == QuestStatus.Completed) return Result.Success();
            if (runtime.Status == QuestStatus.Failed) return Result.Failure(ErrorCode.InvalidOperation, "Quest already failed.", runtime.QuestInstanceId);
            if (commission && runtime.AcceptedAtDayIndex > 0 && QuestDeadline.IsExpired(world, runtime))
                return Result.Failure(ErrorCode.InvalidOperation, "Original commission deadline has expired.", runtime.QuestInstanceId);
            runtime.Status = QuestStatus.Active; runtime.AcceptedByEntityId = subject; runtime.FailureReason = string.Empty;
            runtime.ProgressCount = 0; runtime.ProgressMax = ResolveProgressMax(spec);
            if (!commission || runtime.DeadlineDayIndexExclusive == 0) QuestDeadline.BindOnStart(spec, runtime, world);
            RefreshProgress(world, spec, runtime);
            world.Events.Publish(EventType.QuestStarted, world.Tick, actor: subject, target: runtime.IssuerEntityId, payload: runtime.QuestInstanceId);
            return Result.Success();
        }

        static int ResolveProgressMax(QuestSpec spec)
        {
            if (spec.DeliveryRequirements.Count > 0) return spec.DeliveryRequirements.Count;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c != null && (string.Equals(c.Kind, "uniqueLaborAtLocation", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Kind, "uniqueHarvestAtLocation", StringComparison.OrdinalIgnoreCase)))
                    return c.Amount > 0 ? c.Amount : 1;
            }
            var stock = SumConditionAmounts(spec, "stockAtLeast");
            if (stock > 0) return stock;
            var counters = SumConditionAmounts(spec, "counterAtLeast");
            if (counters > 0) return counters;
            return spec.CompleteConditions.Count;
        }

        static void RefreshProgress(SimulationWorld world, QuestSpec spec, QuestRuntime runtime)
        {
            if (spec.DeliveryRequirements.Count > 0)
            { runtime.ProgressMax = spec.DeliveryRequirements.Count; runtime.ProgressCount = runtime.DeliveryCompleted ? runtime.ProgressMax : 0; return; }
            var stockMax = SumConditionAmounts(spec, "stockAtLeast");
            if (stockMax > 0)
            {
                runtime.ProgressMax = stockMax; runtime.ProgressCount = 0;
                for (var i = 0; i < spec.CompleteConditions.Count; i++)
                {
                    var c = spec.CompleteConditions[i];
                    if (c == null || !string.Equals(c.Kind, "stockAtLeast", StringComparison.OrdinalIgnoreCase)) continue;
                    var need = c.Amount > 0 ? c.Amount : 1;
                    runtime.ProgressCount += Math.Min(need, PlayerStrategicResourceService.GetPlayerAccessibleCount(world, c.Id));
                }
                return;
            }
            var counterMax = SumConditionAmounts(spec, "counterAtLeast");
            if (counterMax > 0)
            {
                runtime.ProgressMax = counterMax; runtime.ProgressCount = 0;
                for (var i = 0; i < spec.CompleteConditions.Count; i++)
                {
                    var c = spec.CompleteConditions[i];
                    if (c == null || !string.Equals(c.Kind, "counterAtLeast", StringComparison.OrdinalIgnoreCase)) continue;
                    var need = c.Amount > 0 ? c.Amount : 1;
                    runtime.ProgressCount += Math.Min(need, world.ContentCounters.Get(c.Id));
                }
                return;
            }
            var done = 0;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
                if (ContentConditionEvaluator.Pass(world, runtime.AcceptedByEntityId, spec.CompleteConditions[i])) done++;
            runtime.ProgressMax = spec.CompleteConditions.Count; runtime.ProgressCount = done;
        }

        static int SumConditionAmounts(QuestSpec spec, string kind)
        {
            var sum = 0;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c != null && string.Equals(c.Kind, kind, StringComparison.OrdinalIgnoreCase))
                    sum += c.Amount > 0 ? c.Amount : 1;
            }
            return sum;
        }

        static void FailQuest(SimulationWorld world, QuestSpec spec, QuestRuntime runtime, EntityId subject, string reason)
        {
            if (runtime.Status == QuestStatus.Failed || runtime.Status == QuestStatus.Completed || runtime.Status == QuestStatus.ReadyToClaim) return;
            var failureReason = reason ?? string.Empty;
            var failureContext = new ContentInteractionContext
            { ActorId = subject, IssuerEntityId = runtime.IssuerEntityId };
            var applied = ContentOutcomeApplier.ApplyAll(world, subject, spec.FailResults, failureContext, () =>
            { runtime.Status = QuestStatus.Failed; runtime.FailureReason = failureReason; return Result.Success(); });
            if (applied.IsFailure) return;
            world.Events.Publish(EventType.QuestFailed, world.Tick, target: subject, payload: runtime.QuestInstanceId);
        }
    }
}
