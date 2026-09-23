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
            => ApplyAll(world, subject, outcomes, null);

        internal static Result ApplyAll(
            SimulationWorld world,
            EntityId subject,
            System.Collections.Generic.IReadOnlyList<ContentOutcome> outcomes,
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
                        var r = ApplyOne(world, subject, outcomes[i]);
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

        static Result ApplyOne(SimulationWorld world, EntityId subject, ContentOutcome o)
        {
            if (world == null || o == null || string.IsNullOrEmpty(o.Kind))
                return Result.Failure(ErrorCode.InvalidArgument, "Outcome invalid.");

            switch (o.Kind.Trim().ToLowerInvariant())
            {
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
                case "relationdelta":
                    return ApplyRelation(world, o);
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

        static Result ApplyRelation(SimulationWorld world, ContentOutcome o)
        {
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
            readonly Dictionary<string, QuestRuntime> _quests;
            readonly ContentEventBoard.RuntimeState _contentEventState;
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
                _contentEventState = world.ContentEvents.CaptureState();
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
                _world.ContentEvents.RestoreState(_contentEventState);
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
