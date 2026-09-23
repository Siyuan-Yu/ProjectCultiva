using System;
using System.Collections.Generic;
using XianXia.Core.Entities;
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

            if (string.Equals(trigger, "onTalk", StringComparison.OrdinalIgnoreCase))
                return TryTalkToNpc(world, subject, contextId);

            foreach (var kv in world.ContentEvents.Specs)
            {
                var spec = kv.Value;
                if (!string.Equals(spec.Trigger, trigger, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!RepeatAllowed(world, spec, subject, EntityId.None))
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

                world.ContentEvents.SetActive(spec.Id, subject, EntityId.None, false);
                world.Events.Publish(
                    EventType.ContentEventPresented,
                    world.Tick,
                    target: subject,
                    payload: spec.Id);
                return Result.Success();
            }

            return Result.Success();
        }

        // Compatibility caller: no instance target means target-scoped content is ineligible.
        public Result TryTalkToNpc(SimulationWorld world, EntityId subject, string npcDefinitionId)
        {
            if (world == null || string.IsNullOrEmpty(npcDefinitionId))
                return Result.Failure(ErrorCode.InvalidArgument, "World / npcDefinitionId required.");
            var context = ContentInteractionContext.ForNpc(subject, EntityId.None, npcDefinitionId);
            var candidates = ResolveInteractionCandidates(world, context, "onTalk");
            return candidates.Count == 1
                ? BeginInteraction(world, context, candidates[0].Id, "onTalk")
                : Result.Success();
        }

        public IReadOnlyList<ContentEventSpec> ResolveInteractionCandidates(
            SimulationWorld world, EntityId actor, EntityId target, string trigger, string definitionId)
            => ResolveInteractionCandidates(world,
                ContentInteractionContext.ForNpc(actor, target, definitionId), trigger);

        public IReadOnlyList<ContentEventSpec> ResolveInteractionCandidates(
            SimulationWorld world, ContentInteractionContext context, string trigger)
        {
            var result = new List<ContentEventSpec>();
            if (world == null || context == null || world.ContentEvents.HasActive) return result;
            var priority = int.MinValue;
            foreach (var spec in world.ContentEvents.Specs.Values)
            {
                if (!string.Equals(spec.Trigger, trigger, StringComparison.OrdinalIgnoreCase) ||
                    !TargetBindingMatches(spec, context, trigger) ||
                    !RepeatAllowed(world, spec, context.ActorId, context.TargetKey) ||
                    !InteractionConditionsPass(world, context.ActorId, spec.Conditions)) continue;
                if (spec.Priority < priority) continue;
                if (spec.Priority > priority) { result.Clear(); priority = spec.Priority; }
                result.Add(spec);
            }
            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            return result;
        }

        public Result BeginInteraction(SimulationWorld world, EntityId actor, EntityId target,
            string eventId, string trigger, string definitionId)
            => BeginInteraction(world, ContentInteractionContext.ForNpc(actor, target, definitionId), eventId, trigger);

        public Result BeginInteraction(SimulationWorld world, ContentInteractionContext context,
            string eventId, string trigger)
        {
            if (world == null || context == null || world.ContentEvents.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "An event is already active or world is missing.");
            if (string.Equals(context.TargetKind, "npc", StringComparison.OrdinalIgnoreCase) &&
                !context.TargetEntityId.IsNone &&
                (!world.Entities.TryGet(context.TargetEntityId, out var entity) ||
                 entity.DefinitionId.ToString() != context.TargetDefinitionId))
                return Result.Failure(ErrorCode.NotFound, "Interaction target changed or missing.");
            foreach (var candidate in ResolveInteractionCandidates(world, context, trigger))
            {
                if (candidate.Id != eventId) continue;
                world.ContentEvents.SetActive(eventId, context, true);
                world.Events.Publish(EventType.ContentEventPresented, world.Tick, target: context.ActorId, payload: eventId);
                return Result.Success();
            }
            return Result.Failure(ErrorCode.InvalidOperation, "Interaction is no longer eligible.", eventId);
        }

        static bool TargetBindingMatches(ContentEventSpec spec, ContentInteractionContext context, string trigger)
        {
            if (string.Equals(trigger, "onTalk", StringComparison.OrdinalIgnoreCase))
                return string.Equals(context.TargetKind, "npc", StringComparison.OrdinalIgnoreCase) &&
                       (string.IsNullOrEmpty(spec.NpcDefinitionId) ||
                        string.Equals(spec.NpcDefinitionId, context.TargetDefinitionId, StringComparison.Ordinal));
            if (string.Equals(trigger, "onInspect", StringComparison.OrdinalIgnoreCase))
                return !string.IsNullOrEmpty(context.TargetKey) &&
                       string.Equals(spec.WorldObjectKind, context.TargetKind, StringComparison.OrdinalIgnoreCase) &&
                       (string.IsNullOrEmpty(spec.WorldObjectId) ||
                        string.Equals(spec.WorldObjectId, context.TargetDefinitionId, StringComparison.Ordinal));
            return true;
        }

        static bool RepeatAllowed(SimulationWorld world, ContentEventSpec spec, EntityId actor, EntityId target)
            => RepeatAllowed(world, spec, actor, target.IsNone ? string.Empty : target.Value.ToString());

        static bool RepeatAllowed(SimulationWorld world, ContentEventSpec spec, EntityId actor, string targetKey)
        {
            if (!spec.Once) return true;
            if (spec.OnceScope != "global" && string.IsNullOrEmpty(targetKey)) return false;
            if (spec.OnceScope == "perActorTarget" && actor.IsNone) return false;
            return !world.ContentEvents.HasFired(world.ContentEvents.FiredKey(spec, actor, targetKey));
        }

        /// <summary>One valid party member must satisfy the entire set; actor/outcome identity never changes.</summary>
        public static bool InteractionConditionsPass(SimulationWorld world, EntityId actor, IReadOnlyList<ContentCondition> conditions)
        {
            var party = world?.Strategic?.PlayerPartyContext;
            if (party == null || !party.IsMember(actor))
                return ContentConditionEvaluator.AllPass(world, actor, conditions);
            foreach (var id in party.Members)
            {
                if (!world.Entities.TryGet(id, out var member) || (member.Tags & EntityTag.Character) == 0) continue;
                if (member.TryGet<LifecycleComponent>(out var life) && life.State != LifecycleState.Alive) continue;
                if (ContentConditionEvaluator.AllPass(world, id, conditions)) return true;
            }
            return false;
        }

        public static bool ActiveConditionsPass(SimulationWorld world, IReadOnlyList<ContentCondition> conditions)
        {
            var board = world.ContentEvents;
            return board.ActiveInteraction
                ? InteractionConditionsPass(world, board.ActiveActorId, conditions)
                : ContentConditionEvaluator.AllPass(world, board.ActiveActorId, conditions);
        }

        public static ContentEventStepSpec ActiveStep(SimulationWorld world)
        {
            return world != null && world.ContentEvents.TryGet(world.ContentEvents.ActiveEventId, out var spec)
                ? spec.GetStep(world.ContentEvents.ActiveStepId) : null;
        }

        public static Result ResolveSpeaker(SimulationWorld world, string speakerRef, out string name)
        {
            name = "";
            if (string.IsNullOrEmpty(speakerRef)) return Result.Success();
            Entity speaker = null;
            if (speakerRef == "@actor" || speakerRef == "@target")
                world.Entities.TryGet(speakerRef == "@actor" ? world.ContentEvents.ActiveActorId : world.ContentEvents.ActiveTargetEntityId, out speaker);
            else
                foreach (var entity in world.Entities.All)
                    if (entity.DefinitionId.ToString() == speakerRef && (entity.Tags & (EntityTag.Character | EntityTag.Npc)) != 0)
                    {
                        if (speaker != null) return Result.Failure(ErrorCode.InvalidOperation, "Speaker DefinitionId resolves to multiple instances.", speakerRef);
                        speaker = entity;
                    }
            if (speaker == null && speakerRef == "@target" &&
                !string.IsNullOrEmpty(world.ContentEvents.ActiveTargetDisplayName))
            {
                name = world.ContentEvents.ActiveTargetDisplayName;
                return Result.Success();
            }
            if (speaker == null) return Result.Failure(ErrorCode.NotFound, "Speaker cannot be resolved.", speakerRef);
            name = string.IsNullOrEmpty(speaker.DisplayName) ? speaker.DefinitionId.ToString() : speaker.DisplayName;
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
                if (!RepeatAllowed(world, spec, subject, EntityId.None))
                    return Result.Success();
                if (!ContentConditionEvaluator.AllPass(world, subject, spec.Conditions))
                    return Result.Success();
            }

            world.ContentEvents.SetActive(spec.Id, subject, EntityId.None, false);
            world.Events.Publish(
                EventType.ContentEventPresented,
                world.Tick,
                target: subject,
                payload: spec.Id + (force ? ";force=1" : ""));
            return Result.Success();
        }

        public Result ResolveChoice(SimulationWorld world, EntityId subject, string choiceId)
        {
            if (world == null || !world.ContentEvents.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "No active content event.");
            var board = world.ContentEvents;
            if (!board.TryGet(board.ActiveEventId, out var spec))
                return Result.Failure(ErrorCode.NotFound, "Active event missing.");
            var step = ActiveStep(world);
            if (step == null) return Result.Failure(ErrorCode.NotFound, "Active step missing.");
            // The bound actor is authoritative throughout the event. Legacy SetActive callers may lack it.
            subject = board.ActiveActorId.IsNone ? subject : board.ActiveActorId;
            ContentEventChoiceSpec choice = null;
            if (step.Choices.Count > 0)
            {
                choice = step.Choices.Find(c => c.Id == choiceId);
                if (choice == null) return Result.Failure(ErrorCode.NotFound, "Choice missing.", choiceId);
                var pass = board.ActiveInteraction ? InteractionConditionsPass(world, subject, choice.Conditions)
                    : ContentConditionEvaluator.AllPass(world, subject, choice.Conditions);
                if (!pass) return Result.Failure(ErrorCode.InvalidOperation, "Choice conditions not met.", choiceId);
            }
            else if (!string.IsNullOrEmpty(choiceId))
                return Result.Failure(ErrorCode.NotFound, "This step has no choices.", choiceId);
            var next = choice == null ? step.NextStepId : choice.NextStepId;
            if (!string.IsNullOrEmpty(next) && spec.GetStep(next) == null)
                return Result.Failure(ErrorCode.NotFound, "Next step missing.", next);
            var outcomes = new List<ContentOutcome>(step.Outcomes);
            if (choice != null) outcomes.AddRange(choice.Outcomes);
            var finished = string.IsNullOrEmpty(next);
            var applied = ContentOutcomeApplier.ApplyAll(world, subject, outcomes, () =>
            {
                if (!finished) { board.AdvanceStep(next); return Result.Success(); }
                if (spec.Once) board.MarkFired(board.FiredKey(spec, subject, board.ActiveTargetKey));
                // Evaluate while this event still owns active: no automatic chained event after Finish.
                new QuestService().Evaluate(world, subject);
                board.ClearActive();
                return Result.Success();
            });
            if (applied.IsFailure) return applied;
            if (finished)
                world.Events.Publish(EventType.ContentEventResolved, world.Tick, target: subject, payload: spec.Id + ":" + choiceId);
            return Result.Success();
        }
    }
}
