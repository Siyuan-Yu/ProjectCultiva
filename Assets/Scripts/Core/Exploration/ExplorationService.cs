using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;

namespace XianXia.Core.Exploration
{
    /// <summary>Travel between abstract locations and explore for resources／sites／content.</summary>
    public sealed class ExplorationService
    {
        readonly QuestService _quests = new QuestService();
        readonly ContentEventService _contentEvents = new ContentEventService();

        public Result Travel(SimulationWorld world, EntityId subject, string targetLocationId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (string.IsNullOrWhiteSpace(targetLocationId))
                return Result.Failure(ErrorCode.InvalidArgument, "Target location required.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return Result.Failure(ErrorCode.InvalidOperation, "Subject has no current location.");
            if (!world.WorldRegion.TryGet(targetLocationId, out var target))
                return Result.Failure(ErrorCode.NotFound, "Target location missing.", targetLocationId);
            if (!world.WorldRegion.AreAdjacent(loc.LocationId, targetLocationId))
                return Result.Failure(ErrorCode.InvalidOperation, "Target location not adjacent.", targetLocationId);

            if (!ContentConditionEvaluator.AllPass(world, subject, target.EnterConditions))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Location enter conditions not met.",
                    targetLocationId);
            }

            loc.LocationId = targetLocationId;
            world.Events.Publish(
                EventType.LocationChanged,
                world.Tick,
                target: subject,
                payload: targetLocationId);

            OfferLocationQuests(world, subject, target);
            var evaluated = _quests.Evaluate(world, subject);
            if (evaluated.IsFailure)
                return evaluated;
            _contentEvents.TryTrigger(world, subject, "onArrive", targetLocationId);
            return _quests.Evaluate(world, subject);
        }

        public Result ExploreHere(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return Result.Failure(ErrorCode.InvalidOperation, "Subject has no current location.");
            if (!world.WorldRegion.TryGet(loc.LocationId, out var location))
                return Result.Failure(ErrorCode.NotFound, "Current location missing.", loc.LocationId);

            var foundAnything = false;

            if (!string.IsNullOrEmpty(location.ResourceOnExploreId) &&
                location.ResourceOnExploreAmount > 0 &&
                world.Settlements.TryGetPrimary(out var settlement))
            {
                settlement.AddStock(location.ResourceOnExploreId, location.ResourceOnExploreAmount);
                world.Events.Publish(
                    EventType.SettlementStockChanged,
                    world.Tick,
                    target: subject,
                    payload: settlement.Id + ":" + location.ResourceOnExploreId +
                             ":+" + location.ResourceOnExploreAmount);
                foundAnything = true;
            }

            if (!string.IsNullOrEmpty(location.OpportunitySiteId) &&
                entity.TryGet<KnownSitesComponent>(out var known))
            {
                var siteParsed = DefinitionId.Parse(location.OpportunitySiteId);
                if (siteParsed.IsSuccess &&
                    world.TryGetOpportunitySite(siteParsed.Value, out _) &&
                    !known.Knows(siteParsed.Value))
                {
                    known.Discover(siteParsed.Value);
                    world.Events.Publish(
                        EventType.OpportunitySiteDiscovered,
                        world.Tick,
                        target: subject,
                        payload: location.OpportunitySiteId);
                    foundAnything = true;
                }
            }

            world.Flags.Set(ContentConditionEvaluator.ExploredFlag(location.Id));

            world.Events.Publish(
                EventType.LocationExplored,
                world.Tick,
                target: subject,
                payload: location.Id + ";found=" + (foundAnything ? "1" : "0"));

            OfferLocationQuests(world, subject, location);
            var evaluated = _quests.Evaluate(world, subject);
            if (evaluated.IsFailure)
                return evaluated;
            _contentEvents.TryTrigger(world, subject, "onExplore", location.Id);
            return _quests.Evaluate(world, subject);
        }

        static void OfferLocationQuests(
            SimulationWorld world,
            EntityId subject,
            WorldLocationState location)
        {
            if (location.QuestOfferIds == null || location.QuestOfferIds.Count == 0)
                return;
            var quests = new QuestService();
            for (var i = 0; i < location.QuestOfferIds.Count; i++)
                quests.TryStart(world, location.QuestOfferIds[i], subject);
        }
    }
}
