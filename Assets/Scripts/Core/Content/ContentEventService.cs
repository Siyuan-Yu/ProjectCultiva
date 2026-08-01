using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    public sealed class ContentEventService
    {
        public Result TryTrigger(
            SimulationWorld world,
            EntityId subject,
            string trigger,
            string contextId = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (world.ContentEvents.HasActive)
                return Result.Success();

            foreach (var kv in world.ContentEvents.Specs)
            {
                var spec = kv.Value;
                if (!string.Equals(spec.Trigger, trigger, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (spec.Once && world.ContentEvents.HasFired(spec.Id))
                    continue;

                if (!string.IsNullOrEmpty(spec.LocationId) &&
                    (string.Equals(trigger, "onExplore", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(trigger, "onArrive", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!string.Equals(spec.LocationId, contextId, StringComparison.Ordinal))
                        continue;
                }

                if (!string.IsNullOrEmpty(spec.QuestId) &&
                    string.Equals(trigger, "onQuestCompleted", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(spec.QuestId, contextId, StringComparison.Ordinal))
                    continue;

                if (!ContentConditionEvaluator.AllPass(world, subject, spec.Conditions))
                    continue;

                world.ContentEvents.SetActive(spec.Id);
                world.Events.Publish(
                    EventType.ContentEventPresented,
                    world.Tick,
                    target: subject,
                    payload: spec.Id);
                return Result.Success();
            }

            return Result.Success();
        }

        /// <summary>
        /// Present a specific content event by id. When <paramref name="force"/>,
        /// skip once／condition gates (debug／day-beat authoring aids).
        /// </summary>
        public Result TryPresentById(
            SimulationWorld world,
            EntityId subject,
            string eventId,
            bool force = false)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (world.ContentEvents.HasActive)
                return Result.Success();
            if (!world.ContentEvents.TryGet(eventId, out var spec))
                return Result.Failure(ErrorCode.NotFound, "Content event missing.", eventId);
            if (!force)
            {
                if (spec.Once && world.ContentEvents.HasFired(spec.Id))
                    return Result.Success();
                if (!ContentConditionEvaluator.AllPass(world, subject, spec.Conditions))
                    return Result.Success();
            }

            world.ContentEvents.SetActive(spec.Id);
            world.Events.Publish(
                EventType.ContentEventPresented,
                world.Tick,
                target: subject,
                payload: spec.Id + (force ? ";force=1" : ""));
            return Result.Success();
        }

        public Result ResolveChoice(SimulationWorld world, EntityId subject, string choiceId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (!world.ContentEvents.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "No active content event.");
            if (!world.ContentEvents.TryGet(world.ContentEvents.ActiveEventId, out var spec))
                return Result.Failure(ErrorCode.NotFound, "Active content event missing.");

            ContentEventChoiceSpec choice = null;
            for (var i = 0; i < spec.Choices.Count; i++)
            {
                if (string.Equals(spec.Choices[i].Id, choiceId, StringComparison.Ordinal))
                {
                    choice = spec.Choices[i];
                    break;
                }
            }

            if (choice == null)
                return Result.Failure(ErrorCode.NotFound, "Choice missing.", choiceId);
            if (!ContentConditionEvaluator.AllPass(world, subject, choice.Conditions))
                return Result.Failure(ErrorCode.InvalidOperation, "Choice conditions not met.", choiceId);

            var applied = ContentOutcomeApplier.ApplyAll(world, subject, choice.Outcomes);
            if (applied.IsFailure)
                return applied;

            if (spec.Once)
                world.ContentEvents.MarkFired(spec.Id);
            var eventId = spec.Id;
            world.ContentEvents.ClearActive();
            world.Events.Publish(
                EventType.ContentEventResolved,
                world.Tick,
                target: subject,
                payload: eventId + ":" + choiceId);

            return new QuestService().Evaluate(world, subject);
        }
    }
}
