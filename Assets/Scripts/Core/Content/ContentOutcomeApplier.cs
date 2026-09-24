using System;
using System.Collections.Generic;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.Inventory;
using XianXia.Core.Combat;
using XianXia.Core.Attributes;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Content
{
    public static class ContentOutcomeApplier
    {
        public static Result ApplyAll(
            SimulationWorld world,
            EntityId subject,
            System.Collections.Generic.IReadOnlyList<ContentOutcome> outcomes)
            => ApplyAll(world, subject, outcomes, null, null);

        public static Result ApplyAll(
            SimulationWorld world,
            EntityId subject,
            System.Collections.Generic.IReadOnlyList<ContentOutcome> outcomes,
            ContentInteractionContext context)
            => ApplyAll(world, subject, outcomes, context, null);

        internal static Result ApplyAll(
            SimulationWorld world,
            EntityId subject,
            System.Collections.Generic.IReadOnlyList<ContentOutcome> outcomes,
            Func<Result> finalize)
            => ApplyAll(world, subject, outcomes, null, finalize);

        internal static Result ApplyAll(
            SimulationWorld world,
            EntityId subject,
            System.Collections.Generic.IReadOnlyList<ContentOutcome> outcomes,
            ContentInteractionContext context,
            Func<Result> finalize)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            var transaction = new OutcomeTransaction(world, subject);
            try
            {
                if (outcomes != null)
                    for (var i = 0; i < outcomes.Count; i++)
                    {
                        var r = ApplyOne(world, subject, outcomes[i], context);
                        if (r.IsFailure)
                        {
                            transaction.Rollback();
                            return r;
                        }
                    }
                if (finalize != null)
                {
                    var finalized = finalize();
                    if (finalized.IsFailure)
                    {
                        transaction.Rollback();
                        return finalized;
                    }
                }
                return Result.Success();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Result.Failure(ErrorCode.InvalidOperation, "Outcome transaction failed.", ex.Message);
            }
        }

        public static Result Apply(SimulationWorld world, EntityId subject, ContentOutcome o)
            => ApplyAll(world, subject, new[] { o });

        static Result ApplyOne(SimulationWorld world, EntityId subject, ContentOutcome o,
            ContentInteractionContext context)
        {
            if (world == null || o == null || string.IsNullOrEmpty(o.Kind))
                return Result.Failure(ErrorCode.InvalidArgument, "Outcome invalid.");

            switch (o.Kind.Trim().ToLowerInvariant())
            {
                case "scheduleevent":
                    return world.ScheduledContentEvents.Schedule(world, subject, o.Id, o.Amount, context);
                case "setflag":
                case "setstoryflag":
                    StoryFlagService.Set(world, o.Id, subject);
                    return Result.Success();
                case "clearflag":
                case "clearstoryflag":
                    StoryFlagService.Clear(world, o.Id, subject);
                    return Result.Success();
                case "addstock":
                {
                    var amt = o.Amount <= 0 ? 1 : o.Amount;
                    if (!world.Inventory.TryAddAll(o.Id, amt))
                        return Result.Failure(ErrorCode.InvalidOperation, "Party bag full.", o.Id);
                    world.Events.Publish(
                        EventType.PartyInventoryChanged,
                        world.Tick,
                        target: subject,
                        payload: "bag:" + o.Id + ":+" + amt);
                    QuestProgressRefresh.AfterWorldChange(world, subject);
                    return Result.Success();
                }
                case "removestock":
                {
                    var amt = o.Amount <= 0 ? 1 : o.Amount;
                    if (world.InventoryCatalog.HasTag(o.Id, "resource"))
                    {
                        var consumed = PlayerStrategicResourceService.TryConsume(
                            world, o.Id, amt, out _);
                        if (consumed.IsFailure)
                            return consumed;
                    }
                    else if (world.Inventory.GetCount(o.Id) < amt ||
                             !world.Inventory.TryRemoveAll(o.Id, amt))
                        return Result.Failure(ErrorCode.InvalidOperation, "Party bag stock insufficient.", o.Id);
                    world.Events.Publish(
                        EventType.PartyInventoryChanged,
                        world.Tick,
                        target: subject,
                        payload: "bag:" + o.Id + ":-" + amt);
                    QuestProgressRefresh.AfterWorldChange(world, subject);
                    return Result.Success();
                }
                case "startquest":
                    return new QuestService().TryStart(world, o.Id, subject);
                case "acceptquestfromtarget":
                    return new QuestService().TryAcceptFromTarget(world, o.Id, context);
                case "deliverquesttotarget":
                    return new QuestService().TryDeliverToTarget(world, o.Id, context);
                case "relationdelta":
                    return ApplyRelation(world, o, context);
                case "grantprogress":
                    if (!world.Entities.TryGet(subject, out var e) ||
                        !e.TryGet<CultivationComponent>(out var cult))
                        return Result.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");
                    cult.Progress += o.Amount <= 0 ? 1 : o.Amount;
                    return Result.Success();
                case "discoversite":
                    if (!world.Entities.TryGet(subject, out var es) ||
                        !es.TryGet<KnownSitesComponent>(out var known))
                        return Result.Failure(ErrorCode.ComponentMissing, "KnownSitesComponent missing.");
                    if (!DefinitionId.TryParse(o.Id, out var siteId))
                        return Result.Failure(ErrorCode.InvalidDefinitionId, "discoverSite id invalid.", o.Id);
                    if (known.Discover(siteId))
                    {
                        world.Events.Publish(
                            EventType.OpportunitySiteDiscovered,
                            world.Tick,
                            target: subject,
                            payload: o.Id);
                    }

                    return Result.Success();
                case "addcounter":
                {
                    var delta = o.Amount == 0 ? 1 : o.Amount;
                    world.ContentCounters.Add(o.Id, delta);
                    QuestProgressRefresh.AfterWorldChange(world, subject);
                    return Result.Success();
                }
                case "setcounter":
                    world.ContentCounters.Set(o.Id, o.Amount < 0 ? 0 : o.Amount);
                    QuestProgressRefresh.AfterWorldChange(world, subject);
                    return Result.Success();
                case "setdailyflag":
                    world.ContentDaily.MarkToday(o.Id, world.Tick);
                    return Result.Success();
                case "cleardailyflag":
                    world.ContentDaily.Clear(o.Id);
                    return Result.Success();
                case "setencountercleared":
                    StoryFlagService.Set(world, ContentConditionEvaluator.EncounterFlag(o.Id), subject);
                    QuestProgressRefresh.AfterWorldChange(world, subject);
                    return Result.Success();
                case "startminigame":
                    // Host 拦截并打开小游戏；Core 侧视为已接受该 outcome。
                    return Result.Success();
                case "resolvecurrentopportunity":
                {
                    if (context == null || !string.Equals(context.TargetKind, "opportunityObject", StringComparison.OrdinalIgnoreCase))
                        return Result.Failure(ErrorCode.InvalidOperation,
                            "resolveCurrentOpportunity requires an opportunityObject interaction context.");
                    const string prefix = "opportunityObject:";
                    var objectId = context.TargetKey != null && context.TargetKey.StartsWith(prefix, StringComparison.Ordinal)
                        ? context.TargetKey.Substring(prefix.Length) : string.Empty;
                    if (!world.WorldOpportunities.TryGetByWorldObject(objectId, out var opportunity))
                        return Result.Failure(ErrorCode.NotFound, "Current WorldOpportunity is no longer active.", objectId);
                    return WorldOpportunityDriver.ResolveCurrent(world, opportunity.InstanceId);
                }
                case "learnmanual":
                {
                    if (!DefinitionId.TryParse(o.Id, out var manualId))
                        return Result.Failure(ErrorCode.InvalidDefinitionId, "learnManual id invalid.", o.Id);
                    if (!world.TryGetManual(manualId, out var manual))
                        return Result.Failure(ErrorCode.InvalidDefinitionId, "Manual missing.", o.Id);
                    var learned = new CultivationService().LearnManual(world, subject, manual);
                    if (learned.IsFailure)
                        return learned;
                    QuestProgressRefresh.AfterWorldChange(world, subject);
                    return Result.Success();
                }
                default:
                    return Result.Failure(ErrorCode.InvalidArgument, "Unknown outcome kind.", o.Kind);
            }
        }

        static Result ApplyRelation(SimulationWorld world, ContentOutcome o, ContentInteractionContext context)
        {
            if (o.FromDefinitionId != null && o.FromDefinitionId.StartsWith("@", StringComparison.Ordinal))
                return ApplyContextRelation(world, o, context);
            if (!DefinitionId.TryParse(o.FromDefinitionId, out var fromDef))
                return Result.Failure(ErrorCode.InvalidDefinitionId, "relationDelta definition ids invalid.");

            EntityId from = EntityId.None;
            foreach (var e in world.Entities.All)
            {
                if (e.DefinitionId.Equals(fromDef))
                {
                    from = e.Id;
                    break;
                }
            }

            if (from.IsNone)
                return Result.Failure(ErrorCode.EntityNotFound, "relationDelta endpoints missing.");

            var targetDefs = ResolveRelationTargetDefinitions(world, o);
            if (targetDefs.Count == 0)
                return Result.Failure(ErrorCode.InvalidDefinitionId, "relationDelta targets missing.");

            var targetIds = new List<EntityId>(targetDefs.Count);
            foreach (var toDef in targetDefs)
            {
                EntityId to = EntityId.None;
                foreach (var e in world.Entities.All)
                {
                    if (e.DefinitionId.Equals(toDef))
                    {
                        to = e.Id;
                        break;
                    }
                }

                if (to.IsNone)
                    return Result.Failure(ErrorCode.EntityNotFound, "relationDelta target missing.", toDef.ToString());

                if (to == from)
                    return Result.Failure(ErrorCode.InvalidArgument, "relationDelta cannot target self.");
                targetIds.Add(to);
            }

            var svc = new RelationshipService();
            for (var i = 0; i < targetIds.Count; i++)
            {
                var r = svc.Record(world, from, targetIds[i], o.Amount, "content_event");
                if (r.IsFailure)
                    return r;
            }

            return Result.Success();
        }

        static Result ApplyContextRelation(SimulationWorld world, ContentOutcome o, ContentInteractionContext context)
        {
            var fromResult = ContentEntityReferenceResolver.Resolve(world, context, o.FromDefinitionId, out var from);
            if (fromResult.IsFailure) return fromResult;
            var refs = new List<string>();
            if (o.ToDefinitionIds != null) refs.AddRange(o.ToDefinitionIds);
            if (refs.Count == 0 && !string.IsNullOrWhiteSpace(o.ToDefinitionId)) refs.Add(o.ToDefinitionId);
            if (refs.Count == 0) return Result.Failure(ErrorCode.InvalidArgument, "relationDelta target required.");
            var service = new RelationshipService();
            foreach (var reference in refs)
            {
                var targetResult = ContentEntityReferenceResolver.Resolve(world, context, reference, out var to);
                if (targetResult.IsFailure) return targetResult;
                var recorded = service.Record(world, from, to, o.Amount, "content_event");
                if (recorded.IsFailure) return recorded;
            }
            return Result.Success();
        }

        static List<DefinitionId> ResolveRelationTargetDefinitions(SimulationWorld world, ContentOutcome o)
        {
            var resolved = new List<DefinitionId>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddRaw(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return;
                var token = raw.Trim();
                if (!seen.Add(token))
                    return;

                if (string.Equals(token, "@party", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var e in world.Entities.All)
                    {
                        if ((e.Tags & EntityTag.Character) == 0)
                            continue;
                        if (seen.Add(e.DefinitionId.ToString()))
                            resolved.Add(e.DefinitionId);
                    }

                    return;
                }

                if (DefinitionId.TryParse(token, out var id))
                    resolved.Add(id);
            }

            if (o.ToDefinitionIds != null)
            {
                for (var i = 0; i < o.ToDefinitionIds.Count; i++)
                    AddRaw(o.ToDefinitionIds[i]);
            }

            if (resolved.Count == 0)
                AddRaw(o.ToDefinitionId);

            return resolved;
        }

        sealed class OutcomeTransaction
        {
            readonly SimulationWorld _world;
            readonly EntityId _subject;
            readonly List<InventorySlot> _inventory;
            readonly WorldSitePublicStockBoard.RuntimeState _sitePublicStocks;
            readonly List<string> _flags, _flagHistory, _known;
            readonly Dictionary<string, int> _counters, _daily;
            readonly QuestBoard.RuntimeState _quests;
            readonly List<QuestCompanionBinding> _companions;
            readonly ContentEventBoard.RuntimeState _contentEventState;
            readonly ScheduledContentEventBoard.RuntimeState _scheduledEvents;
            readonly WorldOpportunityBoard.RuntimeState _worldOpportunities;
            readonly XianXia.Core.World.WorldActivityBoard.RuntimeState _worldActivities;
            readonly int _relationshipCount, _eventCursor;
            readonly ulong _eventNext;
            readonly List<DomainEvent> _events;
            readonly CultivationState _cultivation;
            readonly List<AttributeModifier> _modifiers;
            readonly ulong _modifierNext;

            public OutcomeTransaction(SimulationWorld world, EntityId subject)
            {
                _world = world;
                _subject = subject;
                _inventory = world.Inventory.CaptureState();
                _sitePublicStocks = world.Strategic.SitePublicStocks.CaptureState();
                world.Flags.CaptureState(out _flags, out _flagHistory);
                _counters = world.ContentCounters.CaptureState();
                _daily = world.ContentDaily.CaptureState();
                _quests = world.Quests.CaptureRuntime();
                _companions = world.QuestCompanions.Capture();
                _contentEventState = world.ContentEvents.CaptureState();
                _scheduledEvents = world.ScheduledContentEvents.CaptureState();
                _worldOpportunities = world.WorldOpportunities.CaptureRuntimeState();
                _worldActivities = world.WorldActivities.CaptureRuntimeState();
                _relationshipCount = world.Relationships.EventCount;
                world.Events.CaptureState(out _events, out _eventCursor, out _eventNext);
                if (world.Entities.TryGet(subject, out var entity))
                {
                    if (entity.TryGet<KnownSitesComponent>(out var known)) _known = new List<string>(known.KnownIds);
                    if (entity.TryGet<CultivationComponent>(out var cult)) _cultivation = new CultivationState(cult);
                    if (entity.TryGet<AttributesComponent>(out var attrs))
                    {
                        _modifiers = attrs.CaptureModifiers();
                        _modifierNext = attrs.PeekNextModifierId;
                    }
                }
            }

            public void Rollback()
            {
                _world.Inventory.RestoreState(_inventory);
                _world.Strategic.SitePublicStocks.RestoreState(_sitePublicStocks);
                _world.Flags.RestoreState(_flags, _flagHistory);
                _world.ContentCounters.RestoreState(_counters);
                _world.ContentDaily.RestoreState(_daily);
                _world.Quests.RestoreRuntime(_quests);
                _world.QuestCompanions.Restore(_companions);
                _world.ContentEvents.RestoreState(_contentEventState);
                _world.ScheduledContentEvents.RestoreState(_scheduledEvents);
                _world.WorldOpportunities.RestoreRuntimeState(_worldOpportunities);
                _world.WorldActivities.RestoreRuntimeState(_worldActivities);
                _world.Relationships.Truncate(_relationshipCount);
                RelationshipService.RebuildAllCaches(_world);
                _world.Events.RestoreState(_events, _eventCursor, _eventNext);
                if (_world.Entities.TryGet(_subject, out var entity))
                {
                    if (_known != null && entity.TryGet<KnownSitesComponent>(out var known)) known.Restore(_known);
                    if (_cultivation != null && entity.TryGet<CultivationComponent>(out var cult)) _cultivation.Restore(cult);
                    if (_modifiers != null && entity.TryGet<AttributesComponent>(out var attrs))
                        attrs.RestoreModifiers(_modifiers, _modifierNext);
                }
            }

            sealed class CultivationState
            {
                readonly RealmStage _realm; readonly int _minor, _progress, _required, _speed;
                readonly DefinitionId? _manual; readonly string _requiredRealm; readonly SkillMasteryState _mastery;
                public CultivationState(CultivationComponent c)
                {
                    _realm = c.Realm; _minor = c.MinorStage; _progress = c.Progress;
                    _required = c.BreakthroughProgressRequired; _speed = c.CultivationSpeed;
                    _manual = c.LearnedManualId; _requiredRealm = c.RequiredRealmName;
                    _mastery = c.ManualMastery?.Clone();
                }
                public void Restore(CultivationComponent c)
                {
                    c.Realm = _realm; c.MinorStage = _minor; c.Progress = _progress;
                    c.BreakthroughProgressRequired = _required; c.CultivationSpeed = _speed;
                    c.LearnedManualId = _manual; c.RequiredRealmName = _requiredRealm;
                    c.ManualMastery = _mastery?.Clone();
                }
            }
        }
    }
}
