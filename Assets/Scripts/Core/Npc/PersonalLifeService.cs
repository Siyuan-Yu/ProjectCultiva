using System;
using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.World;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;

namespace XianXia.Core.Npc
{
    /// <summary>One WorldTick authority for non-civilian individual needs and free-time choices.</summary>
    public static class PersonalLifeService
    {
        const int HungerThreshold = 35;
        const int ExhaustionThreshold = 30;
        const int MinCultivationMood = 25;
        const ulong SessionTicks = 6;

        public static bool CanEditPreference(SimulationWorld world, Entity entity)
        {
            if (world == null || entity == null ||
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                MortalCivilianQuery.IsManagedCivilian(world, entity) ||
                world.QuestCompanions.TryGet(entity.Id, out _) ||
                !entity.TryGet<FactionMembershipComponent>(out var membership)) return false;
            return membership.IsAffiliated &&
                   string.Equals(membership.FactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal);
        }

        public static PersonalLifeComponent Ensure(SimulationWorld world, Entity entity)
        {
            if (world == null || entity == null ||
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                MortalCivilianQuery.IsManagedCivilian(world, entity)) return null;
            if (entity.TryGet<PersonalLifeComponent>(out var life)) return life;
            life = new PersonalLifeComponent { LastProcessedTick = world.Tick.Value };
            entity.AddComponent(life);
            return life;
        }

        public static void Drive(SimulationWorld world, SimulationLoop loop)
        {
            if (world == null || loop == null) return;
            foreach (var entity in world.Entities.All)
            {
                if (!HuangcunOpeningAccess.IsSimulated(world, entity)) continue;
                var personal = Ensure(world, entity);
                if (personal == null) continue;
                AdvanceNeeds(world, entity, personal);
                var partyMember = world.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true;
                if (!string.IsNullOrEmpty(personal.MeditationSpotId) &&
                    (!world.MeditationSpots.Spots.ContainsKey(personal.MeditationSpotId) ||
                     !CombatLifeStateService.CanFight(entity) || partyMember ||
                     CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) ||
                     SeparateSpaceCombatPolicy.IsEntityInActiveSpace(world, entity.Id) ||
                     world.BackgroundCharacterTravel.IsTraveling(entity.Id) ||
                     entity.TryGet<NpcAutonomyState>(out var meditationAutonomy) && meditationAutonomy.Retreating ||
                     personal.Energy <= ExhaustionThreshold || personal.Satiety <= HungerThreshold ||
                     world.GetOrCreateOrderQueue(entity.Id).HasSource(OrderSource.Player) ||
                     world.GetOrCreateOrderQueue(entity.Id).HasSource(OrderSource.Event) ||
                     !CanSpendFreeTime(world, entity) ||
                     (!personal.MeditationTraveling && personal.Activity != PersonalLifeActivity.Cultivating)))
                    ReleaseMeditation(personal);
                var needsState = NpcAutonomyState.Ensure(entity);
                if (partyMember && CombatLifeStateService.CanFight(entity) &&
                    !CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id))
                {
                    if (personal.Energy == 0) PartyRestService.EnterExhaustion(world, entity);
                    if (needsState.Exhausted && personal.Energy >= PartyRestService.ExhaustionRecoveryThreshold)
                        needsState.Exhausted = false;
                    if (needsState.Exhausted)
                    {
                        personal.Activity = PersonalLifeActivity.Resting;
                        personal.Status = "精力耗尽：原地休息";
                        if (!PartyRestService.IsResting(world, entity))
                        {
                            loop.StopSubject(entity.Id);
                            if (entity.TryGet<MovementIntentComponent>(out var tiredMove)) tiredMove.Clear();
                            loop.EnqueueOrder(new Order(loop.AllocateOrderId(), entity.Id, OrderType.Rest,
                                OrderSource.Ai, waitTicks: SessionTicks));
                        }
                        continue;
                    }
                }
                if (entity.TryGet<NpcAutonomyState>(out var autonomy) && autonomy.Retreating)
                {
                    // A queued personal retreat is stale once a higher movement authority takes over.
                    if (!CombatLifeStateService.CanFight(entity) || PartyRestService.BlocksMovement(entity) ||
                        partyMember || SquadCommandService.OwnsIndividualSchedule(world, entity.Id) ||
                        SquadWorldMotionService.OwnsCharacter(world, entity.Id) ||
                        world.BackgroundCharacterTravel.IsTraveling(entity.Id) ||
                        CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) ||
                        SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, entity.Id))
                    {
                        autonomy.Retreating = false;
                        if (entity.TryGet<MovementIntentComponent>(out var staleRetreat) &&
                            staleRetreat.Active && staleRetreat.DestinationKey == "personal-retreat")
                            staleRetreat.Clear();
                    }
                }
                if (autonomy != null && autonomy.Retreating)
                {
                    var goal = new WorldVec2(autonomy.RetreatX, autonomy.RetreatY);
                    if (!world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
                    {
                        if (!NpcColdMovement.Advance(world, entity, autonomy.RetreatSurface, goal,
                                SpotRadius(world, autonomy.RetreatSurface), out var arrived) || arrived)
                            autonomy.Retreating = false;
                    }
                    else
                    {
                        if (!entity.TryGet<MovementIntentComponent>(out var retreat))
                        { retreat = new MovementIntentComponent(); entity.AddComponent(retreat); }
                        if (retreat.Active && retreat.DestinationKey == "personal-retreat" &&
                            (retreat.HostArrived || retreat.HostPathState == MovementHostPathState.PermanentFailure ||
                             retreat.HostPathState == MovementHostPathState.RetryablePathUnavailable))
                        { retreat.Clear(); autonomy.Retreating = false; }
                        else if (!retreat.Active || retreat.DestinationKey != "personal-retreat")
                            retreat.BeginWorldPoint(autonomy.RetreatSurface, goal.X, goal.Y,
                                destinationKey: "personal-retreat", arrivalRadiusWorld: SpotRadius(world, autonomy.RetreatSurface));
                    }
                    personal.Status = "负伤撤离";
                    if (autonomy.Retreating) continue;
                }
                if (world.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true &&
                    personal.Satiety <= HungerThreshold &&
                    !CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) &&
                    TryConsumePartyFood(world))
                {
                    personal.Satiety += 45;
                    personal.Clamp();
                    personal.Status = "随队取食";
                }
                if (partyMember && !needsState.Exhausted && needsState.DetachedByExhaustion &&
                    world.Strategic.PlayerPartyContext.ActiveCharacterId == entity.Id)
                {
                    needsState.DetachedByExhaustion = false;
                    PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, world.Strategic.PlayerPartyContext);
                }
                if (partyMember && PartyRestService.IsResting(world, entity)) continue;
                if (!CombatLifeStateService.CanFight(entity) ||
                    CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) ||
                    SeparateSpaceCombatPolicy.IsEntityInActiveSpace(world, entity.Id) ||
                    world.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true ||
                    SquadCommandService.OwnsIndividualSchedule(world, entity.Id) ||
                    world.BackgroundCharacterTravel.IsTraveling(entity.Id))
                {
                    if (entity.TryGet<MovementIntentComponent>(out var personalIntent) &&
                        personalIntent.Active && personalIntent.DestinationKey != null &&
                        personalIntent.DestinationKey.StartsWith("personal-", StringComparison.Ordinal))
                        personalIntent.Clear();
                    if (entity.TryGet<ActionStateComponent>(out var owned) && owned.HasActiveAction &&
                        owned.ActiveOrderSource == OrderSource.Ai)
                        loop.CancelOrdersFromSource(entity.Id, OrderSource.Ai);
                    personal.Activity = PersonalLifeActivity.None;
                    continue;
                }
                if (ChessRecreationService.Owns(world, entity.Id))
                {
                    if (ChessRecreationService.CanParticipate(world, entity)) continue;
                    ChessRecreationService.Interrupt(world, loop, entity.Id);
                }
                if (!entity.TryGet<ActionStateComponent>(out var action)) continue;
                var queue = world.GetOrCreateOrderQueue(entity.Id);
                var urgentNeeds = personal.Satiety <= HungerThreshold ||
                                  personal.Energy <= ExhaustionThreshold;
                if (queue.HasSource(OrderSource.Player) || queue.HasSource(OrderSource.Event) ||
                    (queue.HasSource(OrderSource.Schedule) && !urgentNeeds) ||
                    (action.HasActiveAction && action.ActiveOrderSource != OrderSource.Ai &&
                     (action.ActiveOrderSource != OrderSource.Schedule || !urgentNeeds)))
                    continue;

                if (action.HasActiveAction && action.ActiveOrderSource == OrderSource.Ai)
                {
                    // Only an urgent need interrupts a chosen free-time session.
                    if (personal.Activity == PersonalLifeActivity.Recreational && urgentNeeds ||
                        personal.Activity == PersonalLifeActivity.Cultivating &&
                        (urgentNeeds || personal.CurrentMood < MinCultivationMood ||
                         entity.TryGet<XianXia.Core.Cultivation.CultivationComponent>(out var c) && c.IsAtBottleneck ||
                         !IsAtMeditationSpot(world, entity.Id)))
                        loop.CancelOrdersFromSource(entity.Id, OrderSource.Ai);
                    else continue;
                }

                var hasNpcSchedule = (entity.Tags & EntityTag.Npc) != 0 &&
                    entity.TryGet<ScheduleComponent>(out var schedule) &&
                    !string.IsNullOrEmpty(schedule.DefinitionId) &&
                    world.TryGetSchedule(schedule.DefinitionId, out _);
                if (hasNpcSchedule && personal.Satiety > HungerThreshold &&
                    personal.Energy > ExhaustionThreshold) continue;
                if (hasNpcSchedule && action.HasActiveAction &&
                    action.ActiveOrderSource == OrderSource.Schedule)
                    loop.CancelOrdersFromSource(entity.Id, OrderSource.Schedule);

                if (personal.Energy <= ExhaustionThreshold && TryRest(world, entity, personal, loop)) continue;
                if (TryFeed(world, entity, personal, loop)) continue;
                if (hasNpcSchedule) continue;
                if (personal.Satiety <= HungerThreshold)
                {
                    personal.Activity = PersonalLifeActivity.None;
                    continue; // Missing food never turns into free leisure or cultivation.
                }
                personal.FoodRoomId = string.Empty;
                if (!CanSpendFreeTime(world, entity))
                {
                    personal.Activity = PersonalLifeActivity.None;
                    personal.Status = "暂无安全的自由活动地点";
                    continue;
                }
                var cultivate = entity.TryGet<XianXia.Core.Cultivation.CultivationComponent>(out var cultivation) &&
                                !cultivation.IsAtBottleneck && personal.Energy > ExhaustionThreshold &&
                                personal.Satiety > HungerThreshold && personal.CurrentMood >= MinCultivationMood;
                var wantCultivate = cultivate && PreferCultivation(world, personal);
                var meditationBlocked = false;
                if (wantCultivate)
                {
                    if (!TryPrepareMeditation(world, entity, personal, out var arrived))
                    {
                        wantCultivate = false;
                        meditationBlocked = true;
                    }
                    else if (!arrived) continue;
                }
                if (!wantCultivate) ReleaseMeditation(personal);
                if (!wantCultivate && ChessRecreationService.TryJoin(world, loop, entity)) continue;
                if (!wantCultivate && TryReachLeisureArea(world, entity, personal)) continue;
                personal.Activity = wantCultivate ? PersonalLifeActivity.Cultivating : PersonalLifeActivity.Recreational;
                personal.Status = wantCultivate ? "自主修炼" : meditationBlocked
                    ? "修炼受阻：没有可达蒲团；闲时娱乐" : cultivation != null && cultivation.IsAtBottleneck
                        ? "已到瓶颈，等待玩家决定突破；闲时娱乐" : "闲时娱乐";
                if (wantCultivate) personal.LastCultivationTick = world.Tick.Value;
                else personal.LastRecreationTick = world.Tick.Value;
                loop.EnqueueOrder(new Order(loop.AllocateOrderId(), entity.Id,
                    wantCultivate ? OrderType.Cultivate : OrderType.Wait, OrderSource.Ai,
                    waitTicks: SessionTicks));
            }
        }

        static void AdvanceNeeds(SimulationWorld world, Entity entity, PersonalLifeComponent personal)
        {
            var now = world.Tick.Value;
            if (now <= personal.LastProcessedTick) return;
            var elapsed = now - personal.LastProcessedTick;
            // Recovered saves do not replay unbounded time in one frame.
            if (elapsed > 288UL) elapsed = 288UL;
            for (ulong i = 0; i < elapsed; i++)
            {
                var tick = personal.LastProcessedTick + i + 1UL;
                if (tick % 12UL == 0UL) personal.Satiety--;
                if (entity.TryGet<ActionStateComponent>(out var action) && action.HasActiveAction &&
                    world.ActiveActions.TryGetValue(action.ActiveActionId, out var running))
                {
                    if (running is RestAction || running is BedRestAction) personal.Energy += 3;
                    else if (action.ActiveOrderSource == OrderSource.Ai &&
                             personal.Activity == PersonalLifeActivity.Recreational && running is WaitAction)
                    { personal.CurrentMood += 2; if (tick % 8UL == 0UL) personal.Energy--; }
                    else if (tick % 8UL == 0UL && !(running is CultivateAction)) personal.Energy--;
                }
                else if (tick % 8UL == 0UL) personal.Energy--;
            }
            personal.LastProcessedTick = now;
            personal.Clamp();
        }

        static bool PreferCultivation(SimulationWorld world, PersonalLifeComponent personal)
        {
            if (personal.Preference == PersonalLifePreference.Cultivation)
                return personal.CurrentMood > 35 || personal.LastCultivationTick <= personal.LastRecreationTick;
            if (personal.Preference == PersonalLifePreference.Recreation)
                return world.Tick.Value - personal.LastCultivationTick >= 48UL && personal.CurrentMood >= 45;
            return personal.LastCultivationTick <= personal.LastRecreationTick || personal.CurrentMood >= 85;
        }

        static bool CanSpendFreeTime(SimulationWorld world, Entity entity)
        {
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            if (string.IsNullOrEmpty(siteId) || !world.Strategic.Sites.TryGet(siteId, out var site) ||
                !site.IsCoreActive) return false;
            if (!entity.TryGet<FactionMembershipComponent>(out var membership) || !membership.IsAffiliated)
                return true;
            // A resident can cultivate at a non-belligerent Site without owning its stores.
            // Storage and bed usage keep their separate access checks.
            return !WarGateService.IsAtWar(world, membership.FactionId, site.OwnerFactionId);
        }

        static float SpotRadius(SimulationWorld world, string surfaceId) =>
            world.SurfaceSpatial.TryGet(surfaceId, out var metric) && metric != null
                ? metric.CellSize * .75f : .02f;

        static bool IsAtMeditationSpot(SimulationWorld world, EntityId id)
        {
            if (!CharacterWorldPresenceQuery.TryResolve(world, id, out var presence) ||
                !presence.HasWorldPosition) return false;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, id);
            foreach (var spot in world.MeditationSpots.Spots.Values)
            {
                if (spot.SiteId != siteId || spot.SurfaceId != presence.SurfaceId) continue;
                if (WorldVec2.Distance(presence.WorldPosition, spot.Position) <=
                    SpotRadius(world, spot.SurfaceId)) return true;
            }
            return false;
        }

        static void ReleaseMeditation(PersonalLifeComponent personal)
        {
            personal.MeditationSpotId = string.Empty;
            personal.MeditationTraveling = false;
        }

        /// <summary>Do not grant AI cultivation progress until physical cushion arrival.</summary>
        static bool TryPrepareMeditation(SimulationWorld world, Entity entity,
            PersonalLifeComponent personal, out bool arrived)
        {
            arrived = false;
            if (!CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) ||
                !presence.HasWorldPosition) return false;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            if (string.IsNullOrEmpty(siteId) ||
                !world.SurfaceGround.TryGet(presence.SurfaceId, out var navigation) || navigation == null)
                return false;
            var candidates = new List<MeditationSpot>();
            foreach (var spot in world.MeditationSpots.Spots.Values)
                if (spot.SiteId == siteId && spot.SurfaceId == presence.SurfaceId)
                    candidates.Add(spot);
            candidates.Sort((a, b) =>
            {
                if (a.Id == b.Id) return 0;
                if (a.Id == personal.MeditationSpotId) return -1;
                if (b.Id == personal.MeditationSpotId) return 1;
                var d = WorldVec2.Distance(presence.WorldPosition, a.Position).CompareTo(
                    WorldVec2.Distance(presence.WorldPosition, b.Position));
                return d != 0 ? d : string.CompareOrdinal(a.Id, b.Id);
            });
            var route = new List<WorldVec2>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var spot = candidates[i];
                if (IsMeditationSpotClaimed(world, entity.Id, spot)) continue;
                if (navigation.TryFindRoute(presence.WorldPosition, spot.Position, route) !=
                    SurfaceGroundRouteStatus.Found) continue;
                var radius = SpotRadius(world, spot.SurfaceId);
                if (WorldVec2.Distance(presence.WorldPosition, spot.Position) <= radius)
                {
                    personal.MeditationSpotId = spot.Id;
                    personal.MeditationTraveling = false;
                    if (entity.TryGet<MovementIntentComponent>(out var atSpot) &&
                        atSpot.DestinationKey == "personal-meditation:" + spot.Id) atSpot.Clear();
                    arrived = true;
                    return true;
                }
                personal.Activity = PersonalLifeActivity.None;
                personal.Status = "前往蒲团打坐";
                personal.MeditationSpotId = spot.Id;
                personal.MeditationTraveling = true;
                if (!world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
                {
                    if (!NpcColdMovement.Advance(world, entity, spot.SurfaceId, spot.Position, radius, out _))
                    { ReleaseMeditation(personal); continue; }
                    return true;
                }
                if (!entity.TryGet<MovementIntentComponent>(out var intent))
                { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
                var key = "personal-meditation:" + spot.Id;
                if (!intent.Active || intent.DestinationKey != key)
                    intent.BeginWorldPoint(spot.SurfaceId, spot.Position.X, spot.Position.Y,
                        destinationKey: key, arrivalRadiusWorld: radius);
                if (intent.HostPathState == MovementHostPathState.PermanentFailure ||
                    intent.HostPathState == MovementHostPathState.RetryablePathUnavailable)
                { intent.Clear(); ReleaseMeditation(personal); continue; }
                return true;
            }
            personal.Status = "修炼受阻：没有可达蒲团";
            ReleaseMeditation(personal);
            return false;
        }

        static bool IsMeditationSpotClaimed(SimulationWorld world, EntityId seeker, MeditationSpot spot)
        {
            var key = "personal-meditation:" + spot.Id;
            foreach (var other in world.Entities.All)
            {
                if (other.Id == seeker || !other.TryGet<PersonalLifeComponent>(out var life)) continue;
                if (life.MeditationSpotId == spot.Id &&
                    (life.MeditationTraveling || life.Activity == PersonalLifeActivity.Cultivating)) return true;
                if (other.TryGet<MovementIntentComponent>(out var intent) && intent.Active &&
                    intent.DestinationKey == key) return true;
                if (life.Activity != PersonalLifeActivity.Cultivating ||
                    !CharacterWorldPresenceQuery.TryResolve(world, other.Id, out var presence) ||
                    !presence.HasWorldPosition || presence.SurfaceId != spot.SurfaceId) continue;
                if (WorldVec2.Distance(presence.WorldPosition, spot.Position) <=
                    SpotRadius(world, spot.SurfaceId)) return true;
            }
            return false;
        }

        static bool TryReachLeisureArea(SimulationWorld world, Entity entity,
            PersonalLifeComponent personal)
        {
            if (!world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id) ||
                !CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) ||
                !presence.HasWorldPosition) return false;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            if (string.IsNullOrEmpty(siteId)) return false;
            var ids = new List<string>(world.WorkAreas.Keys); ids.Sort(StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
            {
                if (!world.TryGetWorkArea(ids[i], out var area) ||
                    !(area.Tags.Contains("leisure") || area.Tags.Contains("recreation")) ||
                    !MortalCivilianQuery.TryResolveWorkAreaWorldCenter(world, siteId, area,
                        out var surfaceId, out var center, out var cellSize) ||
                    surfaceId != presence.SurfaceId) continue;
                if (MortalCivilianQuery.IsAtWorkArea(world, entity.Id, siteId, area))
                {
                    if (entity.TryGet<MovementIntentComponent>(out var arrived) &&
                        arrived.DestinationKey == "personal-leisure:" + area.Id) arrived.Clear();
                    return false;
                }
                if (!world.SurfaceGround.TryGet(surfaceId, out var navigation) || navigation == null)
                    continue;
                var route = new List<WorldVec2>();
                if (navigation.TryFindRoute(presence.WorldPosition, center, route) !=
                    XianXia.Core.World.Surface.SurfaceGroundRouteStatus.Found) continue;
                if (!entity.TryGet<MovementIntentComponent>(out var intent))
                { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
                var key = "personal-leisure:" + area.Id;
                if (!intent.Active || intent.DestinationKey != key)
                    intent.BeginWorldPoint(surfaceId, center.X, center.Y,
                        destinationKey: key, arrivalRadiusWorld: cellSize * .75f);
                if (intent.HostPathState == MovementHostPathState.PermanentFailure ||
                    intent.HostPathState == MovementHostPathState.RetryablePathUnavailable)
                { intent.Clear(); continue; }
                personal.Activity = PersonalLifeActivity.Recreational;
                personal.Status = "前往休闲地点：" + area.Name;
                return true;
            }
            return false; // No valid leisure facility: safe current Site is the fallback.
        }

        static bool TryRest(SimulationWorld world, Entity entity, PersonalLifeComponent personal, SimulationLoop loop)
        {
            if (world.Tick.Value >= personal.BedRetryAfterTick &&
                RoomBedFacilityService.TryGetAssignedHome(world, entity.Id, out var bed, out _, out var access) &&
                RoomBedFacilityService.CanUseBed(world, bed, entity.Id, out _))
            {
                if (!RoomBedFacilityService.IsAtAccessPoint(world, bed, entity.Id))
                {
                    if (!world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
                    {
                        personal.Activity = PersonalLifeActivity.Resting;
                        personal.Status = "前往床铺休息";
                        if (!NpcColdMovement.Advance(world, entity, bed.SurfaceId, access,
                                SpotRadius(world, bed.SurfaceId), out _))
                        {
                            personal.BedRetryAfterTick = world.Tick.Value + 8UL;
                            return StartGroundRest(world, entity, personal, loop);
                        }
                        return true;
                    }
                    if (!entity.TryGet<MovementIntentComponent>(out var intent))
                    { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
                    var key = "personal-bed:" + bed.StableAssetId;
                    if (intent.Active && intent.DestinationKey == key && intent.HostArrived)
                    {
                        // A Host arrival that Core cannot verify must not strand the resident
                        // forever beside the bed with zero energy.
                        intent.Clear();
                        personal.BedRetryAfterTick = world.Tick.Value + 8UL;
                        personal.Status = "床边到位失败：暂时就地休息";
                        return StartGroundRest(world, entity, personal, loop);
                    }
                    if (!intent.Active || intent.DestinationKey != key)
                        intent.BeginWorldPoint(bed.SurfaceId, access.X, access.Y,
                            destinationKey: key, arrivalRadiusWorld: SpotRadius(world, bed.SurfaceId));
                    if (intent.HostPathState == MovementHostPathState.PermanentFailure ||
                        intent.HostPathState == MovementHostPathState.RetryablePathUnavailable)
                    {
                        intent.Clear();
                        personal.BedRetryAfterTick = world.Tick.Value + 8UL;
                        personal.Status = "返床受阻：等待重新寻路";
                        return StartGroundRest(world, entity, personal, loop);
                    }
                    personal.Activity = PersonalLifeActivity.Resting;
                    personal.Status = "前往床铺休息";
                    return true;
                }
                if (entity.TryGet<MovementIntentComponent>(out var arrived)) arrived.Clear();
                personal.Activity = PersonalLifeActivity.Resting;
                personal.Status = "床上休息";
                loop.EnqueueOrder(new Order(loop.AllocateOrderId(), entity.Id, OrderType.RestAtBed,
                    OrderSource.Ai, waitTicks: SessionTicks, targetRef: bed.StableAssetId));
                return true;
            }
            return StartGroundRest(world, entity, personal, loop);
        }

        static bool StartGroundRest(SimulationWorld world, Entity entity,
            PersonalLifeComponent personal, SimulationLoop loop)
        {
            if (!CanSpendFreeTime(world, entity) && personal.Energy > 15)
            { personal.Status = "疲劳：没有安全休息处"; return true; }
            personal.Activity = PersonalLifeActivity.Resting;
            personal.Status = "就地休息";
            loop.EnqueueOrder(new Order(loop.AllocateOrderId(), entity.Id, OrderType.Rest,
                OrderSource.Ai, waitTicks: SessionTicks));
            return true;
        }

        static bool TryFeed(SimulationWorld world, Entity entity, PersonalLifeComponent personal, SimulationLoop loop)
        {
            if (personal.Satiety > HungerThreshold) return false;
            if (world.Tick.Value < personal.FoodRetryAfterTick)
            { personal.Status = "取食受阻：等待重新寻路"; return false; }
            var id = entity.Id;
            if (world.Strategic.PlayerPartyContext?.IsMember(id) == true)
            {
                if (TryConsumePartyFood(world))
                { BeginMeal(world, entity, personal, loop); return true; }
                personal.Status = "饥饿：队伍背包无食物";
                return true;
            }
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, id);
            if (string.IsNullOrEmpty(siteId) || !world.Strategic.Sites.TryGet(siteId, out var site) ||
                !entity.TryGet<FactionMembershipComponent>(out var faction) || !faction.IsAffiliated ||
                !WorldSiteStorageAccessPolicy.CanFactionUse(world, faction.FactionId, site) ||
                !world.SiteStorageRooms.TryGetBySite(siteId, out var room))
            { personal.Status = "饥饿：没有合法食物来源"; return false; }
            if (!world.Strategic.SitePublicStocks.TryGet(siteId, out var stock))
            { personal.Status = "饥饿：公库无粮"; return false; }
            var foods = new List<string>(stock.Resources.Keys); foods.Sort(StringComparer.Ordinal);
            var foodId = string.Empty;
            for (var i = 0; i < foods.Count; i++)
                if (stock.GetCount(foods[i]) > 0 && world.InventoryCatalog.HasTag(foods[i], "food"))
                { foodId = foods[i]; break; }
            if (string.IsNullOrEmpty(foodId)) { personal.Status = "饥饿：公库无粮"; return false; }
            if (world.ContinuousOutdoorMaterialization.IsMaterialized(id))
            {
                if (!CharacterWorldPresenceQuery.TryResolve(world, id, out var presence) ||
                    !presence.HasWorldPosition || presence.SurfaceId != room.SurfaceId ||
                    !room.TryGetAccessCandidate((int)((id.Value + (ulong)personal.FoodAccessAttempt) %
                        (ulong)WorldSiteStorageRoomState.AccessCandidateCount), out var access))
                { personal.Status = "取食受阻：无法到达储藏室"; return false; }
                if (!entity.TryGet<MovementIntentComponent>(out var intent))
                { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
                var key = "personal-food:" + room.StorageRoomId;
                if (!intent.Active || intent.DestinationKey != key)
                    intent.BeginWorldPoint(room.SurfaceId, access.X, access.Y,
                        destinationKey: key, arrivalRadiusWorld: SpotRadius(world, room.SurfaceId));
                if (intent.HostPathState == MovementHostPathState.PermanentFailure ||
                    intent.HostPathState == MovementHostPathState.RetryablePathUnavailable)
                {
                    intent.Clear();
                    personal.FoodAccessAttempt++;
                    personal.FoodRetryAfterTick = world.Tick.Value + 8UL;
                    personal.Status = "取食受阻：等待另一访问点";
                    return false;
                }
                personal.FoodRoomId = room.StorageRoomId;
                personal.Activity = PersonalLifeActivity.FetchingFood;
                if (!intent.HostArrived) { personal.Status = "前往储藏室取食"; return true; }
                intent.Clear();
            }
            // Stock is removed at physical arrival, or when the same real Character is cold at its own Site.
            if (WorldSitePublicStockService.TryRemove(world, siteId, foodId, 1, id).IsFailure)
            { personal.Status = "取食受阻：食物已被取走"; return false; }
            BeginMeal(world, entity, personal, loop);
            return true;
        }

        static void BeginMeal(SimulationWorld world, Entity entity, PersonalLifeComponent personal, SimulationLoop loop)
        {
            personal.FoodRoomId = string.Empty;
            personal.FoodAccessAttempt = 0;
            personal.FoodRetryAfterTick = 0;
            personal.Satiety += 45; personal.Clamp();
            personal.Activity = PersonalLifeActivity.Eating;
            personal.Status = "进食";
            loop.EnqueueOrder(new Order(loop.AllocateOrderId(), entity.Id, OrderType.Wait,
                OrderSource.Ai, waitTicks: 2UL));
        }

        static bool TryConsumePartyFood(SimulationWorld world)
        {
            var ids = new List<string>();
            foreach (var slot in world.Inventory.Slots)
                if (!slot.IsEmpty && world.InventoryCatalog.HasTag(slot.ItemId, "food") &&
                    !ids.Contains(slot.ItemId)) ids.Add(slot.ItemId);
            ids.Sort(StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
                if (world.Inventory.TryRemoveAll(ids[i], 1)) return true;
            return false;
        }
    }
}
