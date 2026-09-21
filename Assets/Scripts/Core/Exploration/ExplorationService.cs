using XianXia.Core.World;
using XianXia.Core.Attributes;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
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

        /// <summary>
        /// 村内地点瞬间换点（非正式宏观旅行）。宏观请用 WorldTravelService／地图面板。
        /// </summary>
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
            if (!world.LocalPlaces.TryGet(targetLocationId, out var target))
                return Result.Failure(ErrorCode.NotFound, "Target location missing.", targetLocationId);
            if (!world.LocalPlaces.AreAdjacent(loc.LocationId, targetLocationId))
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

            return NotifyArrived(world, subject, targetLocationId, setLocation: false);
        }

        /// <summary>
        /// Host 表现抵达或 Travel 共用：挂地点任务＋onArrive 内容事件。
        /// </summary>
        public Result NotifyArrived(
            SimulationWorld world,
            EntityId subject,
            string locationId,
            bool setLocation = true)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (string.IsNullOrWhiteSpace(locationId))
                return Result.Failure(ErrorCode.InvalidArgument, "Location required.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());
            if (!WorldLocationQuery.TryGet(world, locationId, out var location))
                return Result.Failure(ErrorCode.NotFound, "Location missing.", locationId);

            if (setLocation)
            {
                if (!entity.TryGet<EntityLocationComponent>(out var loc))
                    return Result.Failure(ErrorCode.ComponentMissing, "EntityLocationComponent missing.");
                if (!string.Equals(loc.LocationId, locationId, System.StringComparison.Ordinal))
                {
                    loc.LocationId = locationId;
                    world.Events.Publish(
                        EventType.LocationChanged,
                        world.Tick,
                        target: subject,
                        payload: locationId);
                }
            }

            OfferLocationQuests(world, subject, location);
            var evaluated = _quests.Evaluate(world, subject);
            if (evaluated.IsFailure)
                return evaluated;
            _contentEvents.TryTrigger(world, subject, "onArrive", locationId);
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
            if (!world.LocalPlaces.TryGet(loc.LocationId, out var location))
                return Result.Failure(ErrorCode.NotFound, "Current location missing.", loc.LocationId);

            var foundAnything = false;

            if (!string.IsNullOrEmpty(location.ResourceOnExploreId) &&
                location.ResourceOnExploreAmount > 0)
            {
                var added = world.Inventory.TryAdd(
                    location.ResourceOnExploreId,
                    location.ResourceOnExploreAmount);
                if (added > 0)
                {
                    world.Events.Publish(
                        EventType.PartyInventoryChanged,
                        world.Tick,
                        target: subject,
                        payload: "bag:" + location.ResourceOnExploreId + ":+" + added);
                    foundAnything = true;
                }
            }

            if (!string.IsNullOrEmpty(location.OpportunitySiteId) &&
                entity.TryGet<KnownSitesComponent>(out var known) &&
                !OpportunityEntranceRules.IsHiddenEntrance(location))
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

            StoryFlagService.Set(world, ContentConditionEvaluator.ExploredFlag(location.Id), subject);

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

        /// <summary>
        /// Continuous Outdoor → Separate Space。Domain 真源见 SeparateSpaceTransitionService。
        /// Proximity 由 Host 门禁；不再要求 EntityLocation.LocationId 已等于洞口。
        /// </summary>
        public Result EnterLocalMap(SimulationWorld world, EntityId subject, string entranceLocationId = null)
        {
            var entered = SeparateSpaceTransitionService.Enter(world, subject, entranceLocationId);
            if (entered.IsFailure)
                return entered;
            if (world != null &&
                !string.IsNullOrEmpty(entranceLocationId) &&
                WorldLocationQuery.TryGet(world, entranceLocationId.Trim(), out var entrance) &&
                !string.IsNullOrEmpty(entrance.EnterSpawnLocationId))
                return NotifyArrived(world, subject, entrance.EnterSpawnLocationId, setLocation: false);
            return entered;
        }

        /// <summary>Separate Space → Continuous Outdoor exact return（或 legacy Overworld）。</summary>
        public Result LeaveLocalMap(SimulationWorld world, EntityId subject)
        {
            if (world?.LocalMap == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            var returnId = world.LocalMap.ReturnLocationId;
            var continuousReturn = world.LocalMap.HasOutdoorReturn;
            var left = SeparateSpaceTransitionService.Leave(world, subject);
            if (left.IsFailure)
                return left;
            // Continuous：Host 之后再激活 Surface；legacy 仍走地点抵达钩子。
            return continuousReturn
                ? Result.Success()
                : NotifyArrived(world, subject, returnId, setLocation: false);
        }

        /// <summary>
        /// 瞬时勘查：圆心＋半径探针扫描未显形洞口。
        /// <paramref name="presentationHint"/>：
        /// <c>x,z</c>／<c>x,z,r</c>；多人用 <c>;</c> 分隔（多选框选时每人一圈）。
        /// 未给 r 时半径＝神识×2。
        /// </summary>
        public Result SurveyEntrance(SimulationWorld world, EntityId subject, string presentationHint = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());
            if (!entity.TryGet<AttributesComponent>(out var attrs))
                return Result.Failure(ErrorCode.ComponentMissing, "Attributes missing.");
            if (!entity.TryGet<KnownSitesComponent>(out var known))
                return Result.Failure(ErrorCode.ComponentMissing, "KnownSites missing.");

            var defaultRadius = OpportunityEntranceRules.SurveyRadius(attrs.GetFinal(AttributeId.SpiritSense));
            if (!TryBuildSurveyProbes(world, entity, presentationHint, defaultRadius, out var probes) ||
                probes.Count == 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot resolve survey center.");

            var found = 0;
            var blockedBySense = 0;
            var nearButOutOfRange = 0;
            var sense = attrs.GetFinal(AttributeId.SpiritSense);
            var maxR = 0f;
            for (var i = 0; i < probes.Count; i++)
            {
                if (probes[i].Radius > maxR)
                    maxR = probes[i].Radius;
            }

            foreach (var entrance in OpportunityEntranceQuery.EnumerateAvailableEntrances(world))
            {
                if (!OpportunityEntranceRules.IsHiddenEntrance(entrance))
                    continue;
                if (OpportunityEntranceRules.IsKnownToCharacter(world, subject, entrance))
                    continue;

                var inSurvey = false;
                var inHint = false;
                for (var i = 0; i < probes.Count; i++)
                {
                    var p = probes[i];
                    var hintR = System.Math.Max(OpportunityEntranceRules.DefaultHintRadius, p.Radius);
                    if (OpportunityEntranceRules.IsWithinSurveyRange(p.X, p.Z, hintR, entrance))
                        inHint = true;
                    if (OpportunityEntranceRules.IsWithinSurveyRange(p.X, p.Z, p.Radius, entrance))
                        inSurvey = true;
                }

                if (!inSurvey)
                {
                    if (inHint)
                        nearButOutOfRange++;
                    continue;
                }

                if (!OpportunityEntranceRules.MeetsSenseRequirement(entrance, sense))
                {
                    blockedBySense++;
                    continue;
                }

                if (!DefinitionId.TryParse(entrance.OpportunitySiteId, out var siteId))
                    continue;
                if (!world.TryGetOpportunitySite(siteId, out _))
                    continue;

                known.Discover(siteId);
                StoryFlagService.Set(world, ContentConditionEvaluator.ExploredFlag(entrance.Id), subject);
                world.Events.Publish(
                    EventType.OpportunitySiteDiscovered,
                    world.Tick,
                    target: subject,
                    payload: entrance.OpportunitySiteId);
                found++;
            }

            var at = probes[0].X.ToString("0.##") + "," + probes[0].Z.ToString("0.##");
            world.Events.Publish(
                EventType.LocationExplored,
                world.Tick,
                target: subject,
                payload: "survey;r=" + maxR.ToString("0.##") + ";probes=" + probes.Count +
                         ";found=" + found +
                         ";blockedSense=" + blockedBySense +
                         ";nearOut=" + nearButOutOfRange +
                         ";at=" + at);
            return Result.Success();
        }

        struct SurveyProbe
        {
            public float X;
            public float Z;
            public float Radius;
        }

        static bool TryBuildSurveyProbes(
            SimulationWorld world,
            Entity entity,
            string presentationHint,
            float defaultRadius,
            out System.Collections.Generic.List<SurveyProbe> probes)
        {
            probes = new System.Collections.Generic.List<SurveyProbe>(4);
            if (!string.IsNullOrWhiteSpace(presentationHint))
            {
                var chunks = presentationHint.Split(';');
                for (var i = 0; i < chunks.Length; i++)
                {
                    var chunk = chunks[i].Trim();
                    if (chunk.Length == 0)
                        continue;
                    var parts = chunk.Split(',');
                    if (parts.Length < 2)
                        continue;
                    if (!float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var x) ||
                        !float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var z))
                        continue;
                    var r = defaultRadius;
                    if (parts.Length >= 3 &&
                        float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var explicitR))
                        r = System.Math.Max(0f, explicitR);
                    probes.Add(new SurveyProbe { X = x, Z = z, Radius = r });
                }

                if (probes.Count > 0)
                    return true;
            }

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return false;
            if (!WorldLocationQuery.TryGet(world, loc.LocationId, out var place))
                return false;
            probes.Add(new SurveyProbe
            {
                X = place.PresentationX,
                Z = place.PresentationZ,
                Radius = defaultRadius
            });
            return true;
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
