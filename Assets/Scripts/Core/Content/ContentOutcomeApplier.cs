using System;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Content
{
    public static class ContentOutcomeApplier
    {
        public static Result ApplyAll(
            SimulationWorld world,
            EntityId subject,
            System.Collections.Generic.IReadOnlyList<ContentOutcome> outcomes)
        {
            if (outcomes == null)
                return Result.Success();
            for (var i = 0; i < outcomes.Count; i++)
            {
                var r = Apply(world, subject, outcomes[i]);
                if (r.IsFailure)
                    return r;
            }

            return Result.Success();
        }

        public static Result Apply(SimulationWorld world, EntityId subject, ContentOutcome o)
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
                    var added = world.Inventory.TryAdd(o.Id, amt);
                    if (added <= 0)
                        return Result.Failure(ErrorCode.InvalidOperation, "Party bag full.", o.Id);
                    world.Events.Publish(
                        EventType.SettlementStockChanged,
                        world.Tick,
                        target: subject,
                        payload: "bag:" + o.Id + ":+" + added);
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
                default:
                    return Result.Failure(ErrorCode.InvalidArgument, "Unknown outcome kind.", o.Kind);
            }
        }

        static Result ApplyRelation(SimulationWorld world, ContentOutcome o)
        {
            if (!DefinitionId.TryParse(o.FromDefinitionId, out var fromDef) ||
                !DefinitionId.TryParse(o.ToDefinitionId, out var toDef))
                return Result.Failure(ErrorCode.InvalidDefinitionId, "relationDelta definition ids invalid.");

            EntityId from = EntityId.None;
            EntityId to = EntityId.None;
            foreach (var e in world.Entities.All)
            {
                if (e.DefinitionId.Equals(fromDef))
                    from = e.Id;
                if (e.DefinitionId.Equals(toDef))
                    to = e.Id;
            }

            if (from.IsNone || to.IsNone)
                return Result.Failure(ErrorCode.EntityNotFound, "relationDelta endpoints missing.");

            return new RelationshipService().Record(
                world, from, to, o.Amount, "content_event");
        }
    }
}
