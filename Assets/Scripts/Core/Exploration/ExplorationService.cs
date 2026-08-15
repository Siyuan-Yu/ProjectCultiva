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
            if (!world.WorldRegion.TryGet(locationId, out var location))
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
            if (!world.WorldRegion.TryGet(loc.LocationId, out var location))
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
                        EventType.SettlementStockChanged,
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
        /// 从洞口等入口进入另一张 LocalMap；需已发现机缘点（若入口绑了 opportunitySiteId）。
        /// </summary>
        public Result EnterLocalMap(SimulationWorld world, EntityId subject, string entranceLocationId = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return Result.Failure(ErrorCode.InvalidOperation, "Subject has no current location.");

            var entranceId = string.IsNullOrWhiteSpace(entranceLocationId) ? loc.LocationId : entranceLocationId.Trim();
            if (!world.WorldRegion.TryGet(entranceId, out var entrance))
                return Result.Failure(ErrorCode.NotFound, "Entrance location missing.", entranceId);
            if (string.IsNullOrEmpty(entrance.EnterLocalMapId) || string.IsNullOrEmpty(entrance.EnterSpawnLocationId))
                return Result.Failure(ErrorCode.InvalidOperation, "Location is not a LocalMap entrance.", entranceId);
            if (!string.Equals(loc.LocationId, entranceId, System.StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "Must stand at entrance to enter.", entranceId);

            if (!ContentConditionEvaluator.AllPass(world, subject, entrance.EnterConditions))
                return Result.Failure(ErrorCode.InvalidOperation, "Entrance conditions not met.", entranceId);

            if (!string.IsNullOrEmpty(entrance.OpportunitySiteId))
            {
                if (!entity.TryGet<KnownSitesComponent>(out var known) ||
                    !DefinitionId.TryParse(entrance.OpportunitySiteId, out var siteId) ||
                    !known.Knows(siteId))
                {
                    return Result.Failure(
                        ErrorCode.InvalidOperation,
                        "Discover the cave first (explore).",
                        entrance.OpportunitySiteId);
                }
            }

            if (!world.WorldRegion.TryGet(entrance.EnterSpawnLocationId, out _))
                return Result.Failure(ErrorCode.NotFound, "Spawn location missing.", entrance.EnterSpawnLocationId);

            var session = world.LocalMap;
            if (string.IsNullOrEmpty(session.OverworldMapLayoutId))
                session.OverworldMapLayoutId = session.ActiveMapLayoutId;
            session.ReturnLocationId = entranceId;
            session.ActiveMapLayoutId = entrance.EnterLocalMapId;

            // 仅移动已站在洞口的己方（Host 先把选中随行者 Location 设到洞口）。
            MovePartyAtLocationTo(world, entranceId, entrance.EnterSpawnLocationId);

            world.Events.Publish(
                EventType.LocalMapChanged,
                world.Tick,
                target: subject,
                payload: session.ActiveMapLayoutId + ";" + entrance.EnterSpawnLocationId);

            return NotifyArrived(world, subject, entrance.EnterSpawnLocationId, setLocation: false);
        }

        public Result LeaveLocalMap(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (!world.Entities.TryGet(subject, out _))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());

            var session = world.LocalMap;
            if (!session.IsInInterior)
                return Result.Failure(ErrorCode.InvalidOperation, "Not inside a LocalMap interior.");
            if (string.IsNullOrEmpty(session.OverworldMapLayoutId))
                return Result.Failure(ErrorCode.InvalidOperation, "Overworld map missing.");
            if (string.IsNullOrEmpty(session.ReturnLocationId) ||
                !world.WorldRegion.TryGet(session.ReturnLocationId, out _))
                return Result.Failure(ErrorCode.NotFound, "Return location missing.", session.ReturnLocationId);

            var returnId = session.ReturnLocationId;
            var interiorMap = session.ActiveMapLayoutId;
            session.ActiveMapLayoutId = session.OverworldMapLayoutId;
            MoveInteriorPartyTo(world, interiorMap, returnId);

            world.Events.Publish(
                EventType.LocalMapChanged,
                world.Tick,
                target: subject,
                payload: session.ActiveMapLayoutId + ";" + returnId);

            return NotifyArrived(world, subject, returnId, setLocation: false);
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
            var maxR = 0f;
            for (var i = 0; i < probes.Count; i++)
            {
                if (probes[i].Radius > maxR)
                    maxR = probes[i].Radius;
            }

            foreach (var kv in world.WorldRegion.Locations)
            {
                var entrance = kv.Value;
                if (!OpportunityEntranceRules.IsHiddenEntrance(entrance))
                    continue;
                if (OpportunityEntranceRules.IsRevealed(world, entrance))
                    continue;

                var hit = false;
                for (var i = 0; i < probes.Count; i++)
                {
                    var p = probes[i];
                    if (OpportunityEntranceRules.IsWithinSurveyRange(p.X, p.Z, p.Radius, entrance))
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                    continue;

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
                         ";found=" + found + ";at=" + at);
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
            if (!world.WorldRegion.TryGet(loc.LocationId, out var place))
                return false;
            probes.Add(new SurveyProbe
            {
                X = place.PresentationX,
                Z = place.PresentationZ,
                Radius = defaultRadius
            });
            return true;
        }

        static void MovePartyTo(SimulationWorld world, EntityId subject, string locationId)
        {
            foreach (var e in world.Entities.All)
            {
                if (e == null)
                    continue;
                var isParty = (e.Tags & EntityTag.Character) != 0 && (e.Tags & EntityTag.Npc) == 0;
                if (!isParty && e.Id != subject)
                    continue;
                if (!e.TryGet<EntityLocationComponent>(out var lc))
                    continue;
                lc.LocationId = locationId;
                world.Events.Publish(
                    EventType.LocationChanged,
                    world.Tick,
                    target: e.Id,
                    payload: locationId);
            }
        }

        /// <summary>把当前站在 fromLocationId 的己方角色移到 toLocationId。</summary>
        static void MovePartyAtLocationTo(SimulationWorld world, string fromLocationId, string toLocationId)
        {
            if (world == null || string.IsNullOrEmpty(fromLocationId) || string.IsNullOrEmpty(toLocationId))
                return;
            foreach (var e in world.Entities.All)
            {
                if (e == null)
                    continue;
                if ((e.Tags & EntityTag.Character) == 0 || (e.Tags & EntityTag.Npc) != 0)
                    continue;
                if (!e.TryGet<EntityLocationComponent>(out var lc) || !lc.HasLocation)
                    continue;
                if (!string.Equals(lc.LocationId, fromLocationId, System.StringComparison.Ordinal))
                    continue;
                lc.LocationId = toLocationId;
                world.Events.Publish(
                    EventType.LocationChanged,
                    world.Tick,
                    target: e.Id,
                    payload: toLocationId);
            }
        }

        /// <summary>把位于某 LocalMap 内室地点的己方移到地表落点。</summary>
        static void MoveInteriorPartyTo(SimulationWorld world, string interiorMapLayoutId, string returnLocationId)
        {
            if (world == null || string.IsNullOrEmpty(interiorMapLayoutId) || string.IsNullOrEmpty(returnLocationId))
                return;
            foreach (var e in world.Entities.All)
            {
                if (e == null)
                    continue;
                if ((e.Tags & EntityTag.Character) == 0 || (e.Tags & EntityTag.Npc) != 0)
                    continue;
                if (!e.TryGet<EntityLocationComponent>(out var lc) || !lc.HasLocation)
                    continue;
                if (!world.WorldRegion.TryGet(lc.LocationId, out var place))
                    continue;
                if (!string.Equals(place.LocalMapId, interiorMapLayoutId, System.StringComparison.Ordinal))
                    continue;
                lc.LocationId = returnLocationId;
                world.Events.Publish(
                    EventType.LocationChanged,
                    world.Tick,
                    target: e.Id,
                    payload: returnLocationId);
            }
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
