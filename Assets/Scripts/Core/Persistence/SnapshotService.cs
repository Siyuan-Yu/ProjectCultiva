using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Concealment;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Inventory;
using XianXia.Core.Labor;
using XianXia.Core.Npc;
using XianXia.Core.Opportunity;
using XianXia.Core.Orders;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    public sealed class SnapshotService
    {
        readonly ISnapshotSerializer _serializer;
        ulong _nextSnapshotId = 1;

        public SnapshotService(ISnapshotSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public Result<string> CaptureJson(
            SimulationWorld world,
            SimulationLoop loop,
            PlayerPartyRuntime playerParty = null)
        {
            if (world?.Strategic?.CharacterEncounter?.Phase == CharacterEncounterPhase.Committed)
                return Result.Fail<string>(ErrorCode.InvalidOperation, "Close encounter report before saving.");
            if (world?.Strategic?.CharacterEncounter?.Phase == CharacterEncounterPhase.Preparing)
                return Result.Fail<string>(ErrorCode.InvalidOperation, "Encounter is preparing; save after entry completes.");
            if (world?.ContentEvents?.HasActive == true)
                return Result.Fail<string>(ErrorCode.InvalidOperation, "请先完成当前对话/事件后再保存。");
            var snap = Capture(world, loop, playerParty);
            var spatialCapture = StrategicSnapshotHelper.ValidateCaptureForSerialization(
                world, snap.Strategic);
            if (spatialCapture.IsFailure)
                return Result.Fail<string>(spatialCapture.Error);
            return _serializer.Serialize(snap);
        }

        public WorldSnapshot Capture(
            SimulationWorld world,
            SimulationLoop loop,
            PlayerPartyRuntime playerParty = null)
        {
            // Reconcile every due auction before its authoritative state is captured.
            AuctionHouseService.ProcessDue(world);
            CharacterFactionLoyaltyService.EnsureAll(world);
            XianXia.Core.Npc.MortalCivilianService.ValidateRecoverySpotPlacements(world);
            var random = world.Random.CaptureState();
            var snap = new WorldSnapshot
            {
                SchemaVersion = WorldSnapshot.CurrentSchemaVersion,
                SnapshotId = _nextSnapshotId++,
                WorldTick = world.Tick.Value,
                RegionId = world.RegionId.Value,
                EnabledPackageId = world.EnabledPackageId,
                EnabledPackageVersion = world.EnabledPackageVersion,
                RandomS0 = random.S0,
                RandomS1 = random.S1,
                RandomStreamId = random.StreamId.Value,
                EventCursor = world.Events.Cursor,
                NextEventId = world.Events.PeekNextId,
                NextEntityId = world.Entities.Ids.PeekNext,
                NextOrderId = loop.PeekNextOrderId,
                NextActionId = world.Translator.PeekNextActionId,
                NextModifierId = 1,
                ObservationDiscoverChancePercent = world.ObservationDiscoverChancePercent
            };

            snap.ChessGames = world.ChessGames.Values.Select(g => g.Copy()).ToList();
            snap.HuangcunSurvival = world.HuangcunSurvival.Copy();
            snap.PeaceNegotiations = world.Strategic.PeaceNegotiations.Select(n => n.Copy()).ToList();
            foreach (var entity in world.Entities.All)
            {
                var dto = new EntitySnapshotDto
                {
                    Id = entity.Id.Value,
                    DefinitionId = entity.DefinitionId.ToString(),
                    DisplayName = entity.DisplayName,
                    Tags = (int)entity.Tags
                };

                if (entity.TryGet<LifecycleComponent>(out var life))
                {
                    dto.Lifecycle = (int)life.State;
                    dto.BleedOutAfterTick = life.BleedOutAfterTick;
                    dto.IncapacitatedAtTick = life.IncapacitatedAtTick;
                    dto.HasMedicalProgress = life.HasMedicalProgress;
                    dto.DyingReserveMilli = life.DyingReserveMilli;
                    dto.HerbDoses = life.HerbDoses;
                    dto.Convalescing = life.Convalescing;
                    dto.RecoveryHpMilli = life.RecoveryHpMilli;

                }

                if (entity.TryGet<AttributesComponent>(out var attrs))
                {
                    foreach (var kv in attrs.BaseValues)
                        dto.Bases.Add(new AttrBaseDto { AttributeId = (int)kv.Key, Value = kv.Value });

                    ulong maxMod = 0;
                    foreach (var m in attrs.Modifiers)
                    {
                        if (m.Id.Value > maxMod) maxMod = m.Id.Value;
                        dto.Modifiers.Add(ToModifierDto(m));
                    }

                    if (maxMod + 1 > snap.NextModifierId)
                        snap.NextModifierId = maxMod + 1;
                }

                if (entity.TryGet<ActionStateComponent>(out var actionState))
                {
                    dto.ActiveActionId = actionState.ActiveActionId.Value;
                    dto.ActiveOrderSource = (int)actionState.ActiveOrderSource;
                    if (actionState.ActiveClock.HasValue)
                    {
                        dto.HasActiveClock = true;
                        dto.ActiveTotalTicks = actionState.ActiveClock.Value.TotalDurationTicks;
                        dto.ActiveRemainingTicks = actionState.ActiveClock.Value.RemainingTicks;
                    }
                }

                if (entity.TryGet<CultivationComponent>(out var cultivation))
                {
                    dto.HasCultivation = true;
                    dto.Realm = (int)cultivation.Realm;
                    dto.CultivationMinorStage = cultivation.MinorStage;
                    dto.CultivationProgress = cultivation.Progress;
                    dto.BreakthroughProgressRequired = cultivation.BreakthroughProgressRequired;
                    dto.CultivationSpeed = cultivation.CultivationSpeed;
                    dto.LearnedManualId = cultivation.LearnedManualId.HasValue
                        ? cultivation.LearnedManualId.Value.ToString()
                        : string.Empty;
                    dto.RequiredRealmName = cultivation.RequiredRealmName ?? string.Empty;
                    if (cultivation.ManualMastery != null)
                    {
                        dto.HasManualMastery = true;
                        dto.ManualMasteryTier = (int)cultivation.ManualMastery.Tier;
                        dto.ManualMasteryProgress = cultivation.ManualMastery.Progress;
                        dto.ManualMasteryProgressRequired = cultivation.ManualMastery.ProgressRequired;
                    }
                }

                if (entity.TryGet<CombatArtsComponent>(out var arts))
                {
                    for (var i = 0; i < arts.Learned.Count; i++)
                        dto.CombatArtsLearned.Add(arts.Learned[i].ToString());
                    for (var s = 0; s < CombatArtsComponent.MaxEquippedSlots; s++)
                    {
                        var eq = arts.GetEquipped(s);
                        dto.CombatArtsEquipped.Add(eq.HasValue ? eq.Value.ToString() : string.Empty);
                    }

                    foreach (var kv in arts.AllMastery)
                    {
                        if (kv.Value == null)
                            continue;
                        dto.CombatArtMastery.Add(new ArtMasterySnapshotDto
                        {
                            ArtId = kv.Key,
                            Tier = (int)kv.Value.Tier,
                            Progress = kv.Value.Progress,
                            ProgressRequired = kv.Value.ProgressRequired
                        });
                    }
                }

                if (entity.TryGet<DailyTaskComponent>(out var daily))
                {
                    dto.HasDailyTask = true;
                    dto.RequiredAmount = daily.RequiredAmount;
                    dto.CompletedAmount = daily.CompletedAmount;
                    dto.Deviation = daily.Deviation;
                    dto.PendingReprimand = daily.PendingReprimand;
                    dto.LastSettledDeviation = daily.LastSettledDeviation;
                    dto.LaborProgress = daily.CompletedAmount;
                    dto.LaborQuota = daily.RequiredAmount;
                }

                if (entity.TryGet<ScheduleComponent>(out var schedule))
                {
                    dto.HasSchedule = true;
                    dto.ScheduleDefinitionId = schedule.DefinitionId ?? string.Empty;
                }

                if (entity.TryGet<KnownSitesComponent>(out var known))
                {
                    foreach (var siteId in known.KnownIds)
                        dto.KnownSiteIds.Add(siteId);
                }

                if (entity.TryGet<PersonalConcealmentRiskComponent>(out var risk))
                    dto.PersonalConcealmentRisk = risk.Value;

                if (entity.TryGet<EntityLocationComponent>(out var location) && location != null)
                {
                    dto.HasEntityLocation = true;
                    dto.LocationId = location.LocationId ?? string.Empty;
                    dto.HasPresentationOverride = location.HasPresentationOverride;
                    dto.PresentationOverrideX = location.PresentationOverrideX;
                    dto.PresentationOverrideZ = location.PresentationOverrideZ;
                }
                dto.EntityLocationSnapshotFieldPresent = true;

                if (entity.TryGet<FactionMembershipComponent>(out var factionMem) && factionMem != null)
                {
                    dto.FactionId = factionMem.FactionId ?? string.Empty;
                    dto.FactionRole = (int)factionMem.Role;
                    dto.InitialLoyalty = factionMem.InitialLoyalty;
                }

                if (entity.TryGet<CombatVitalsComponent>(out var vitals) && vitals != null)
                {
                    dto.HasCombatVitals = true;
                    dto.CurrentHp = vitals.CurrentHp;
                    dto.CurrentSpiritPower = vitals.CurrentSpiritPower;
                    dto.VitalsPoolsInitialized = vitals.PoolsInitialized;
                }

                if (entity.TryGet<FlightComponent>(out var flight))
                    dto.Flight = new FlightComponent { Airborne = flight.Airborne, SurfaceId = flight.SurfaceId,
                        ScopeId = flight.ScopeId, HasGround = flight.HasGround, GroundX = flight.GroundX,
                        GroundY = flight.GroundY, DetachedFollower = flight.DetachedFollower };

                if (entity.TryGet<CorpseComponent>(out var corpse) && corpse != null)
                {
                    dto.HasCorpse = true;
                    dto.CorpseRemoveAfterTick = corpse.RemoveAfterTick;
                }

                if (entity.TryGet<CombatDeathAttributionComponent>(out var attribution) &&
                    attribution != null && !attribution.ResponsibleAttackerId.IsNone)
                {
                    dto.HasResponsibleAttacker = true;
                    dto.ResponsibleAttackerEntityId = attribution.ResponsibleAttackerId.Value;
                }

                if (entity.TryGet<PersonalityProfileComponent>(out var personality) &&
                    personality != null &&
                    personality.Count > 0)
                {
                    foreach (var tag in personality.Tags)
                        dto.PersonalityTags.Add(tag);
                }

                if (entity.TryGet<NpcAutonomyState>(out var autonomy))
                {
                    dto.HasNpcAutonomy = true;
                    dto.NpcStrikeCooldown = autonomy.StrikeCooldown;
                    dto.NpcArt0 = autonomy.Art0;
                    dto.NpcArt1 = autonomy.Art1;
                    dto.NpcArt2 = autonomy.Art2;
                    dto.NpcArt3 = autonomy.Art3;
                    dto.NpcArt4 = autonomy.Art4;
                    dto.NpcArt5 = autonomy.Art5;
                    dto.NpcRetreating = autonomy.Retreating;
                    dto.NpcRetreatSurface = autonomy.RetreatSurface;
                    dto.NpcRetreatX = autonomy.RetreatX;
                    dto.NpcRetreatY = autonomy.RetreatY;
                    dto.NpcLastMoveTick = autonomy.LastMoveTick;
                    dto.NpcExhausted = autonomy.Exhausted;
                    dto.NpcDetachedByExhaustion = autonomy.DetachedByExhaustion;
                }

                if (entity.TryGet<PersonalLifeComponent>(out var personalLife))
                {
                    dto.HasPersonalLife = true;
                    dto.PersonalLifePreference = (int)personalLife.Preference;
                    dto.PersonalLifeActivity = (int)personalLife.Activity;
                    dto.PersonalSatiety = personalLife.Satiety;
                    dto.PersonalEnergy = personalLife.Energy;
                    dto.PersonalCurrentMood = personalLife.CurrentMood;
                    dto.PersonalLastProcessedTick = personalLife.LastProcessedTick;
                    dto.PersonalLastCultivationTick = personalLife.LastCultivationTick;
                    dto.PersonalLastRecreationTick = personalLife.LastRecreationTick;
                    dto.PersonalFoodRoomId = personalLife.FoodRoomId ?? string.Empty;
                    dto.PersonalFoodAccessAttempt = personalLife.FoodAccessAttempt;
                    dto.PersonalFoodRetryAfterTick = personalLife.FoodRetryAfterTick;
                    dto.PersonalBedRetryAfterTick = personalLife.BedRetryAfterTick;
                    dto.PersonalMeditationSpotId = personalLife.MeditationSpotId ?? string.Empty;
                    dto.PersonalMeditationTraveling = personalLife.MeditationTraveling;
                }

                if (world.Civilians.TryGet(entity.Id, out var civilian))
                {
                    dto.HasMortalCivilian = true;
                    dto.MortalSatiety = civilian.Satiety; dto.MortalEnergy = civilian.Energy;
                    dto.MortalProfession = (int)civilian.Profession; dto.MortalActivity = (int)civilian.Activity;
                    dto.CivilianDisposition = (int)civilian.Disposition;
                    dto.MortalResidenceWorkAreaId = civilian.ResidenceWorkAreaId; dto.MortalCurrentSiteId = civilian.CurrentSiteId;
                    dto.MortalLastUpdateTick = civilian.LastUpdateTick; dto.MortalSleepStartedTick = civilian.SleepStartedTick;
                    dto.MortalSleepPhase = (int)civilian.SleepPhase;
                    dto.MortalFleeStartedTick = civilian.FleeStartedTick; dto.MortalHasFleeTarget = civilian.HasFleeTarget;
                    dto.MortalFleeTargetX = civilian.FleeTargetX; dto.MortalFleeTargetY = civilian.FleeTargetY;
                    dto.MortalFleeSourceSiteId = civilian.FleeSourceSiteId;
                    dto.MortalRefugeTargetSiteId = civilian.RefugeTargetSiteId;
                    dto.MortalBackgroundNeedsLastProcessedTick = civilian.BackgroundNeedsLastProcessedTick;
                    dto.MortalFleeCandidateIndex = civilian.FleeCandidateIndex;
                    dto.MortalFleeAttemptCount = civilian.FleeAttemptCount;
                    dto.MortalCarrierId = civilian.CarrierId.Value;
                    dto.MortalDetainedResidenceWorkAreaId = civilian.DetainedResidenceWorkAreaId;
                    dto.MortalRescueTargetId = civilian.RescueTargetId.Value;
                    dto.MedicTargetId = civilian.MedicTargetId.Value;
                    dto.MedicCarryingHerb = civilian.MedicCarryingHerb;
                    dto.MedicSourceSiteId = civilian.MedicSourceSiteId;
                    dto.MedicSourceRoomId = civilian.MedicSourceRoomId;
                    dto.MedicSourcePartyMemberId = civilian.MedicSourcePartyMemberId.Value;
                    dto.MedicTreatmentSeconds = civilian.MedicTreatmentSeconds;
                    dto.MedicLastTreatmentTick = civilian.MedicLastTreatmentTick;
                    dto.MedicRetryAfterTick = civilian.MedicRetryAfterTick;
                    dto.MedicStatus = civilian.MedicStatus;

                    dto.MortalRescueCarrying = civilian.RescueCarrying;
                    dto.MortalRescueDestinationId = civilian.RescueDestinationId;
                    dto.MortalRescueRejectedDestinationIds = civilian.RescueRejectedDestinationId;
                    dto.MortalCareDayIndex = civilian.CareDayIndex; dto.MortalAteToday = civilian.AteToday; dto.MortalSleptToday = civilian.SleptToday;
                    dto.MortalLastWorkOutputTick = civilian.LastWorkOutputTick;
                    dto.MortalPendingCaptureCarrierId = civilian.PendingCaptureCarrierId.Value;
                }

                snap.Entities.Add(dto);
            }

            foreach (var entry in world.FactionLoyalties.Entries)
                snap.CharacterFactionLoyalties.Add(new CharacterFactionLoyaltySnapshotDto {
                    CharacterEntityId = entry.CharacterId.Value,
                    FactionId = entry.FactionId,
                    Loyalty = entry.Loyalty
                });

            foreach (var usage in world.Civilians.ResidenceUsages)
                snap.CivilianResidenceUsages.Add(new CivilianResidenceUsageSnapshotDto { WorkAreaId = usage.Key, Usage = (int)usage.Value });

            foreach (var kv in world.Schedules)
            {
                var defDto = new ScheduleDefinitionSnapshotDto { Id = kv.Key };
                foreach (var block in kv.Value.Blocks)
                {
                    defDto.Blocks.Add(new ScheduleBlockSnapshotDto
                    {
                        StartTickInDay = block.StartTickInDay,
                        EndTickInDay = block.EndTickInDay,
                        Activity = (int)block.Activity,
                        OrderDurationTicks = block.OrderDurationTicks
                    });
                }

                snap.Schedules.Add(defDto);
            }

            foreach (var kv in world.OpportunitySites)
            {
                var site = kv.Value;
                snap.OpportunitySites.Add(new OpportunitySiteSnapshotDto
                {
                    Id = site.Id.ToString(),
                    AllowsCultivation = site.AllowsCultivation,
                    OfferedManualId = site.OfferedManualId.HasValue ? site.OfferedManualId.Value.ToString() : string.Empty,
                    NameKey = site.NameKey ?? string.Empty,
                    Description = site.Description ?? string.Empty
                });
            }

            foreach (var kv in world.Manuals)
            {
                var manual = kv.Value;
                snap.Manuals.Add(new ManualSnapshotDto
                {
                    Id = manual.Id.ToString(),
                    RequiredRealm = manual.RequiredRealm ?? string.Empty,
                    CultivationSpeed = manual.CultivationSpeed,
                    BreakthroughProgress = manual.BreakthroughProgress
                });
            }

            foreach (var kv in world.ActiveActions)
            {
                var action = kv.Value;
                string kind;
                string targetRef = null;
                var activity = 0;
                if (action is WaitAction) kind = "Wait";
                else if (action is CultivateAction) kind = "Cultivate";
                else if (action is LaborAction) kind = "Labor";
                else if (action is RestAction) kind = "Rest";
                else if (action is RecoveryAction recovery)
                {
                    kind = "Recover";
                    targetRef = recovery.RecoverySpotId;
                }
                else if (action is BedRestAction bedRest)
                {
                    kind = "BedRest";
                    targetRef = bedRest.BedId;
                }
                else if (action is ObserveAction) kind = "Observe";
                else if (action is MoveAction move)
                {
                    kind = "Move";
                    targetRef = move.TargetWorkAreaId;
                }
                else if (action is WorkAction work)
                {
                    kind = "Work";
                    targetRef = work.TargetWorkAreaId;
                    activity = (int)work.Activity;
                }
                else if (action is HuangcunSurvivalAction survival) { kind = "HuangcunSurvival"; targetRef = survival.Task; }
                else kind = "ApplyModifier";

                snap.ActiveActions.Add(new ActiveActionSnapshotDto
                {
                    Id = action.Id.Value,
                    SubjectId = action.Subject.Value,
                    SourceOrderId = action.SourceOrderId.Value,
                    Kind = kind,
                    Status = (int)action.Status,
                    TotalTicks = action.Clock.TotalDurationTicks,
                    RemainingTicks = action.Clock.RemainingTicks,
                    TargetRef = targetRef ?? string.Empty,
                    Activity = activity
                });
            }

            foreach (var queueKv in world.OrderQueues)
            {
                foreach (var order in queueKv.Value.Snapshot())
                {
                    snap.Orders.Add(new OrderSnapshotDto
                    {
                        Id = order.Id.Value,
                        SubjectId = order.Subject.Value,
                        Type = (int)order.Type,
                        Source = (int)order.Source,
                        WaitTicks = order.WaitTicks,
                        TargetRef = order.TargetRef ?? string.Empty,
                        Activity = order.Activity.HasValue ? (int)order.Activity.Value : 0
                    });
                }
            }

            snap.Strategic = StrategicSnapshotHelper.Capture(world, playerParty);
            snap.SuppressedCharacterContacts.AddRange(world.Strategic.SuppressedCharacterContacts);
            snap.SuppressedCharacterContacts.Sort(System.StringComparer.Ordinal);
            snap.CharacterEncounter = world.Strategic.CharacterEncounter;
            CapturePartyInventory(world, snap);
            snap.HasTakenWorldLootSnapshotAuthority = true;
            WorldLootPickupService.CaptureTakenSpotIds(world, snap.TakenWorldLootSpotIds);
            var dropIds = new List<string>(world.GroundLoot.Keys); dropIds.Sort(StringComparer.Ordinal);
            foreach (var id in dropIds) snap.GroundLoot.Add(world.GroundLoot[id].Copy());
            CaptureSocialBonds(world, snap);
            CaptureRelationshipLedger(world, snap);
            CaptureOutdoorStatefulObjects(world, snap);
            CaptureCivilianConstructionJobs(world, snap);
            CaptureWorldOpportunities(world, snap);
            CaptureWorldActivities(world, snap);
            snap.Commerce = world.Commerce.Capture();
            snap.ContentProgress = ContentProgressSnapshotHelper.Capture(world);
            return snap;
        }

        static void CaptureWorldActivities(SimulationWorld world, WorldSnapshot snap)
        {
            var runtime = new WorldActivityRuntimeSnapshotDto
            {
                HasAuthority = true,
                NextActivitySequence = world.WorldActivities.NextActivitySequence
            };
            foreach (var entry in world.WorldActivities.Entries.Values)
                runtime.Entries.Add(new WorldActivityEntrySnapshotDto
                {
                    ActivityId = entry.ActivityId,
                    SourceKind = entry.SourceKind,
                    SourceId = entry.SourceId,
                    Title = entry.Title,
                    Body = entry.Body,
                    CreatedDayIndex = entry.CreatedDayIndex,
                    State = entry.State,
                    IsRead = entry.IsRead,
                    HasResolvedDayIndex = entry.ResolvedDayIndex.HasValue,
                    ResolvedDayIndex = entry.ResolvedDayIndex ?? 0
                });
            snap.WorldActivityRuntime = runtime;
        }

        static void CaptureWorldOpportunities(SimulationWorld world, WorldSnapshot snap)
        {
            var runtime = new WorldOpportunityRuntimeSnapshotDto
            {
                HasAuthority = true,
                NextInstanceSequence = world.WorldOpportunities.NextInstanceSequence
            };
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
                runtime.Instances.Add(new WorldOpportunityInstanceSnapshotDto
                {
                    InstanceId = instance.InstanceId,
                    OpportunityDefinitionId = instance.OpportunityDefinitionId,
                    SurfaceId = instance.SurfaceId,
                    SpawnKind = instance.SpawnKind,
                    SpawnedEntityId = instance.SpawnedEntityId.Value,
                    WorldObjectInstanceId = instance.WorldObjectInstanceId,
                    WorldX = instance.WorldX,
                    WorldY = instance.WorldY,
                    CreatedDayIndex = instance.CreatedDayIndex,
                    ExpireDayIndexExclusive = instance.ExpireDayIndexExclusive,
                    DiscoveryMode = instance.DiscoveryMode,
                    IsDiscovered = instance.IsDiscovered
                });
            foreach (var state in world.WorldOpportunities.SurfaceRefreshStates)
                runtime.SurfaceRefreshStates.Add(new WorldOpportunitySurfaceRefreshSnapshotDto
                {
                    SurfaceId = state.Key,
                    LastRefreshDayIndex = state.Value
                });
            snap.WorldOpportunityRuntime = runtime;
        }

        static void CapturePartyInventory(SimulationWorld world, WorldSnapshot snap)
        {
            if (world?.Inventory == null || snap == null)
                return;

            var slots = world.Inventory.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null || s.IsEmpty)
                    continue;
                snap.PartyInventorySlots.Add(new PartyInventorySlotSnapshotDto
                {
                    ItemId = s.ItemId ?? string.Empty,
                    Count = s.Count
                });
            }
        }

        static void CaptureRelationshipLedger(SimulationWorld world, WorldSnapshot snap)
        {
            if (world?.Relationships == null || snap == null)
                return;

            var events = world.Relationships.Events;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e == null)
                    continue;
                var dto = new RelationshipEventSnapshotDto
                {
                    Tick = e.Tick.Value,
                    FromEntityId = e.From.Value,
                    ToEntityId = e.To.Value,
                    Axis = (int)e.Axis,
                    HasAxis = true,
                    Delta = e.Delta,
                    ReasonTag = e.ReasonTag ?? string.Empty
                };
                if (e.CauseEventId.HasValue && !e.CauseEventId.Value.IsNone)
                {
                    dto.HasCauseEventId = true;
                    dto.CauseEventId = e.CauseEventId.Value.Value;
                }
                if (e.ContextEntityId.HasValue && !e.ContextEntityId.Value.IsNone)
                {
                    dto.HasContextEntityId = true;
                    dto.ContextEntityId = e.ContextEntityId.Value.Value;
                }

                snap.RelationshipEvents.Add(dto);
            }

        }

        static void CaptureOutdoorStatefulObjects(SimulationWorld world, WorldSnapshot snap)
        {
            snap.NextOutdoorConstructedAssetSequence = world.OutdoorConstructedAssets.NextSequence;
            foreach (var asset in world.OutdoorConstructedAssets.Assets.Values)
                snap.OutdoorConstructedAssets.Add(new OutdoorConstructedAssetSnapshotDto {
                    StableAssetId = asset.StableAssetId,
                    BuildingId = asset.BuildingId,
                    Kind = asset.Kind,
                    SurfaceId = asset.SurfaceId,
                    WorldX = asset.WorldX,
                    WorldY = asset.WorldY,
                    WorldWidth = asset.WorldWidth,
                    WorldHeight = asset.WorldHeight,
                    CellsW = asset.CellsW,
                    CellsH = asset.CellsH,
                    BoundLocationId = asset.BoundLocationId,
                    BoundWorldSiteId = asset.BoundWorldSiteId,
                    QuarterTurns = asset.QuarterTurns,
                    ParentRoomId = asset.ParentRoomId,
                    RoomUsage = (int)asset.RoomUsage,
                    AssignedCharacterId = asset.AssignedCharacterId.Value,
                    ReservedCharacterId = asset.ReservedCharacterId.Value,
                    OccupantCharacterId = asset.OccupantCharacterId.Value,
                    InitialAssigneeSpawnKey = asset.InitialAssigneeSpawnKey,
                    StructureDurability = asset.StructureDurability });
            foreach (var placement in world.RecoverySpotDeliveries.Placements)
                snap.RecoverySpotPlacements.Add(new RecoverySpotPlacementSnapshotDto {
                    RecoverySpotId = placement.Key, InjuredCharacterId = placement.Value.Value });
            foreach (var kv in world.OutdoorStatefulObjects.Destructibles)
                snap.OutdoorDestructibles.Add(new OutdoorDestructibleSnapshotDto
                { StableId = kv.Key, CurrentHp = kv.Value.Hp, Destroyed = kv.Value.Destroyed });
            foreach (var kv in world.OutdoorStatefulObjects.FarmPlots)
                snap.OutdoorFarmPlots.Add(new OutdoorFarmPlotSnapshotDto
                { StableCellId = kv.Key, CropId = kv.Value.CropId, CropStage = kv.Value.CropStage, Growth = kv.Value.Growth });
        }

        static void CaptureCivilianConstructionJobs(SimulationWorld world, WorldSnapshot snap)
        {
            snap.NextCivilianConstructionJobSequence = world.CivilianConstructionJobs.NextSequence;
            foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
            {
                var dto = new CivilianConstructionJobSnapshotDto {
                    JobId = job.JobId, BuildingId = job.BuildingId,
                    DemolitionFlagId = job.DemolitionFlagId, SiteId = job.SiteId,
                    FactionId = job.FactionId, SurfaceId = job.SurfaceId,
                    WorldX = job.WorldX, WorldY = job.WorldY,
                    QuarterTurns = job.QuarterTurns, ParentRoomId = job.ParentRoomId,
                    RequiredLabor = job.RequiredLabor,
                    LaborProgress = job.LaborProgress, LastLaborTick = job.LastLaborTick,
                    CarrierId = job.CarrierId.Value, PayloadItemId = job.PayloadItemId,
                    PayloadCount = job.PayloadCount
                };
                for (var i = 0; i < job.Required.Count; i++)
                    dto.Required.Add(new ConstructionMaterialSnapshotDto {
                        ItemId = job.Required[i].ItemId, Count = job.Required[i].Count });
                foreach (var row in job.Delivered)
                    dto.Delivered.Add(new ConstructionMaterialSnapshotDto { ItemId = row.Key, Count = row.Value });
                snap.CivilianConstructionJobs.Add(dto);
            }
        }

        static void CaptureSocialBonds(SimulationWorld world, WorldSnapshot snap)
        {
            if (world?.SocialBonds == null || snap == null)
                return;
            var bonds = world.SocialBonds.All;
            for (var i = 0; i < bonds.Count; i++)
            {
                var bond = bonds[i];
                snap.SocialBonds.Add(new SocialBondSnapshotDto
                {
                    Kind = (int)bond.Kind,
                    FromEntityId = bond.From.Value,
                    ToEntityId = bond.To.Value
                });
            }
        }

        public Result<(SimulationWorld world, SimulationLoop loop)> RestoreJson(string json, string expectedPackageVersion = null,
            Func<SimulationWorld, WorldSnapshot, Result> prepareSiteIdentities = null)
        {
            var parsed = _serializer.Deserialize(json);
            if (parsed.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(parsed.Error);

            return Restore(parsed.Value, expectedPackageVersion, prepareSiteIdentities);
        }

        public Result<(SimulationWorld world, SimulationLoop loop)> Restore(WorldSnapshot snap, string expectedPackageVersion = null,
            Func<SimulationWorld, WorldSnapshot, Result> prepareSiteIdentities = null)
        {
            if (snap == null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Snapshot null.");
            if (snap.CharacterEncounter != null && snap.Strategic?.PendingEngagement != null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Conflicting encounter identities.");
            if (snap.SchemaVersion >= WorldSnapshot.LegacySchemaVersion &&
                snap.SchemaVersion <= WorldSnapshot.LegacySchemaVersionV14)
            {
                return Result.Fail<(SimulationWorld, SimulationLoop)>(
                    ErrorCode.SnapshotVersionMismatch,
                    "Schema v1-v14 saves lack ROOM-01 room/bed authority. Start a new game (schema v18 required; v15/v16/v17 JSON can be upgraded on load).",
                    snap.SchemaVersion.ToString());
            }

            if (snap.SchemaVersion != WorldSnapshot.CurrentSchemaVersion)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotVersionMismatch, "Unsupported snapshot schema.", snap.SchemaVersion.ToString());
            if (snap.Entities == null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(
                    ErrorCode.SnapshotInvalid,
                    "Snapshot entity authority is missing.");
            var snapshotEntityIds = new HashSet<ulong>(snap.Entities.Where(x => x != null).Select(x => x.Id));
            if (snapshotEntityIds.Count != snap.Entities.Count)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Snapshot contains duplicate or null entity identity.");
            for (var i = 0; i < snap.Entities.Count; i++)
            {
                var entity = snap.Entities[i];
                if (entity == null || !entity.EntityLocationSnapshotFieldPresent)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(
                        ErrorCode.SnapshotInvalid,
                        "Snapshot lacks current EntityLocation authority and requires offline conversion.",
                        "EntityIndex=" + i + " EntityId=" + (entity?.Id ?? 0UL));
                if (entity.DyingReserveMilli < 0 || entity.DyingReserveMilli > 200000 ||
                    entity.HerbDoses < 0 || entity.HerbDoses > 1000000 ||
                    entity.RecoveryHpMilli < 0 || entity.RecoveryHpMilli >= 1000 ||
                    entity.Convalescing && (!entity.HasMedicalProgress || entity.Lifecycle != (int)LifecycleState.Incapacitated && entity.Lifecycle != (int)LifecycleState.Captured) ||
                    entity.MedicTreatmentSeconds < 0 || entity.MedicTreatmentSeconds >= MedicalRecoveryService.TreatmentSeconds ||
                    entity.MedicTargetId != 0 && !snapshotEntityIds.Contains(entity.MedicTargetId) ||
                    entity.MedicSourcePartyMemberId != 0 && !snapshotEntityIds.Contains(entity.MedicSourcePartyMemberId))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid,
                        "Invalid medical recovery or carried-medicine state.");
                if (entity.HasMortalCivilian &&
                    (entity.MortalSatiety < 0 || entity.MortalSatiety > 100 ||
                     entity.MortalEnergy < 0 || entity.MortalEnergy > 100 ||
                     !Enum.IsDefined(typeof(MortalProfession), entity.MortalProfession) ||
                     !Enum.IsDefined(typeof(MortalActivity), entity.MortalActivity) ||
                     !Enum.IsDefined(typeof(CivilianSleepPhase), entity.MortalSleepPhase) ||
                     !Enum.IsDefined(typeof(CivilianDisposition), entity.CivilianDisposition) ||
                     entity.MortalCarrierId != 0 && !snapshotEntityIds.Contains(entity.MortalCarrierId) ||
                     entity.MortalRescueTargetId != 0 && !snapshotEntityIds.Contains(entity.MortalRescueTargetId) ||
                     entity.MortalRescueCarrying && (entity.MortalRescueTargetId == 0 || string.IsNullOrWhiteSpace(entity.MortalRescueDestinationId)) ||
                     entity.CivilianDisposition == (int)CivilianDisposition.CapturedEscorted && entity.MortalCarrierId == 0 ||
                     entity.MortalPendingCaptureCarrierId != 0 && !snapshotEntityIds.Contains(entity.MortalPendingCaptureCarrierId) ||
                     entity.CivilianDisposition == (int)CivilianDisposition.Detained && string.IsNullOrWhiteSpace(entity.MortalDetainedResidenceWorkAreaId) ||
                     entity.CivilianDisposition == (int)CivilianDisposition.Fleeing &&
                         (!entity.MortalHasFleeTarget || string.IsNullOrWhiteSpace(entity.MortalFleeSourceSiteId) ||
                          entity.MortalFleeCandidateIndex < 0 || entity.MortalFleeCandidateIndex >= 8 || entity.MortalFleeAttemptCount < 0 ||
                          float.IsNaN(entity.MortalFleeTargetX) || float.IsInfinity(entity.MortalFleeTargetX) ||
                          float.IsNaN(entity.MortalFleeTargetY) || float.IsInfinity(entity.MortalFleeTargetY)) ||
                     entity.CivilianDisposition == (int)CivilianDisposition.Displaced &&
                         (!string.IsNullOrEmpty(entity.MortalRefugeTargetSiteId) &&
                          string.Equals(entity.MortalRefugeTargetSiteId, entity.MortalFleeSourceSiteId,
                              StringComparison.Ordinal) ||
                          entity.MortalBackgroundNeedsLastProcessedTick > snap.WorldTick)))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid,
                        "Invalid CIVILIAN-LIFE-01 entity state.", "EntityId=" + entity.Id);
            }
            if (snap.CharacterFactionLoyalties == null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid,
                    "Character faction loyalty authority is missing.");
            var loyaltyByCharacter = new Dictionary<ulong, CharacterFactionLoyaltySnapshotDto>();
            foreach (var loyalty in snap.CharacterFactionLoyalties)
            {
                if (loyalty == null || loyalty.CharacterEntityId == 0 ||
                    !snapshotEntityIds.Contains(loyalty.CharacterEntityId) ||
                    string.IsNullOrWhiteSpace(loyalty.FactionId) || loyalty.Loyalty < 0 || loyalty.Loyalty > 100 ||
                    loyaltyByCharacter.ContainsKey(loyalty.CharacterEntityId))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid,
                        "Invalid or duplicate character faction loyalty entry.");
                loyaltyByCharacter.Add(loyalty.CharacterEntityId, loyalty);
            }
            foreach (var entity in snap.Entities)
            {
                if (entity.InitialLoyalty < 0 || entity.InitialLoyalty > 100 ||
                    !Enum.IsDefined(typeof(FactionRoleKind), entity.FactionRole))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid,
                        "Invalid character faction seed or role.", "EntityId=" + entity.Id);
                var affiliated = !string.IsNullOrWhiteSpace(entity.FactionId) &&
                    entity.FactionRole != (int)FactionRoleKind.None;
                if (affiliated != loyaltyByCharacter.TryGetValue(entity.Id, out var loyalty) ||
                    affiliated && !string.Equals(loyalty.FactionId, entity.FactionId, StringComparison.Ordinal))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid,
                        "Character faction loyalty does not match current membership.", "EntityId=" + entity.Id);
            }
            if (snap.CivilianResidenceUsages == null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Civilian residence usage authority is missing.");
            var residenceUsageIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < snap.CivilianResidenceUsages.Count; i++)
                if (snap.CivilianResidenceUsages[i] == null || string.IsNullOrWhiteSpace(snap.CivilianResidenceUsages[i].WorkAreaId) ||
                    !Enum.IsDefined(typeof(ResidenceUsage), snap.CivilianResidenceUsages[i].Usage) ||
                    !residenceUsageIds.Add(snap.CivilianResidenceUsages[i].WorkAreaId))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid civilian residence usage.");
            var prisonerResidences = new HashSet<string>(snap.OutdoorConstructedAssets
                .Where(x => x != null && string.Equals(x.Kind,
                                OutdoorConstructedAssetSemantics.RoomKind, StringComparison.Ordinal) &&
                            x.RoomUsage == (int)RoomUsage.Prison)
                .Select(x => x.StableAssetId), StringComparer.Ordinal);
            foreach (var entity in snap.Entities)
                if (entity.HasMortalCivilian && entity.CivilianDisposition == (int)CivilianDisposition.Detained &&
                    !prisonerResidences.Contains(entity.MortalDetainedResidenceWorkAreaId))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid,
                        "Detained civilian lacks a formal Prison room authority.", "EntityId=" + entity.Id);
            if (!string.IsNullOrEmpty(expectedPackageVersion) &&
                !string.Equals(snap.EnabledPackageVersion, expectedPackageVersion, StringComparison.Ordinal))
            {
                return Result.Fail<(SimulationWorld, SimulationLoop)>(
                    ErrorCode.IncompatibleContentVersion,
                    "存档内容版本不兼容。当前世界尺寸或内容布局已更新，请新开局；旧存档保留，未自动迁移或重置。",
                    snap.EnabledPackageVersion + " != " + expectedPackageVersion);
            }

            var ids = new EntityIdFactory();
            ids.Reset(snap.NextEntityId);
            var world = new SimulationWorld(new EntityStore(ids), regionId: new RegionId(snap.RegionId));
            world.Tick = new WorldTick(snap.WorldTick);
            world.EnabledPackageId = snap.EnabledPackageId;
            world.EnabledPackageVersion = snap.EnabledPackageVersion;
            world.ObservationDiscoverChancePercent = snap.ObservationDiscoverChancePercent;
            world.Events.RestoreCursor(snap.EventCursor, snap.NextEventId);
            world.Translator.RestoreNextActionId(snap.NextActionId);

            if (snap.Schedules != null)
            {
                foreach (var s in snap.Schedules)
                {
                    var def = new ScheduleDefinition(s.Id ?? string.Empty);
                    if (s.Blocks != null)
                    {
                        foreach (var b in s.Blocks)
                            def.AddBlock(b.StartTickInDay, b.EndTickInDay, (ScheduleActivity)b.Activity, b.OrderDurationTicks);
                    }

                    world.RegisterSchedule(def);
                }
            }

            if (snap.OpportunitySites != null)
            {
                foreach (var s in snap.OpportunitySites)
                {
                    if (!DefinitionId.TryParse(s.Id, out var siteId))
                        continue;
                    DefinitionId? manualId = null;
                    if (!string.IsNullOrEmpty(s.OfferedManualId) &&
                        DefinitionId.TryParse(s.OfferedManualId, out var mid))
                        manualId = mid;
                    world.RegisterOpportunitySite(new OpportunitySite(
                        siteId,
                        s.AllowsCultivation,
                        manualId,
                        s.NameKey,
                        s.Description));
                }
            }

            if (snap.Manuals != null)
            {
                foreach (var m in snap.Manuals)
                {
                    if (!DefinitionId.TryParse(m.Id, out var manualId))
                        continue;
                    world.RegisterManual(new CultivationManualSpec
                    {
                        Id = manualId,
                        RequiredRealm = m.RequiredRealm ?? string.Empty,
                        CultivationSpeed = m.CultivationSpeed,
                        BreakthroughProgress = m.BreakthroughProgress
                    });
                }
            }

            var random = new DeterministicRandom(1, new RandomStreamId(snap.RandomStreamId));
            random.RestoreState(new RandomState(snap.RandomS0, snap.RandomS1, new RandomStreamId(snap.RandomStreamId)));
            world.Random = random;

            var modifierFactory = new ModifierIdFactory();
            modifierFactory.Reset(snap.NextModifierId);

            foreach (var e in snap.Entities)
            {
                var def = DefinitionId.Parse(e.DefinitionId);
                if (def.IsFailure)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(def.Error);

                var entity = new Entity(new EntityId(e.Id), def.Value, (EntityTag)e.Tags, e.DisplayName);
                entity.AddComponent(new IdentityComponent(def.Value, e.DisplayName));
                var attrs = new AttributesComponent(modifierFactory);
                foreach (var b in e.Bases)
                    attrs.SetBase((AttributeId)b.AttributeId, b.Value);
                foreach (var m in e.Modifiers)
                    RestoreModifier(attrs, m);
                entity.AddComponent(attrs);
                var life = new LifecycleComponent((LifecycleState)e.Lifecycle)
                {
                    BleedOutAfterTick = e.BleedOutAfterTick,
                    IncapacitatedAtTick = e.IncapacitatedAtTick,
                    HasMedicalProgress = e.HasMedicalProgress,
                    DyingReserveMilli = e.DyingReserveMilli,
                    HerbDoses = e.HerbDoses,
                    Convalescing = e.Convalescing,
                    RecoveryHpMilli = e.RecoveryHpMilli
                };
                entity.AddComponent(life);
                var actionState = new ActionStateComponent
                {
                    ActiveActionId = new ActionId(e.ActiveActionId),
                    ActiveOrderSource = (OrderSource)e.ActiveOrderSource
                };
                if (e.HasActiveClock)
                    actionState.ActiveClock = new ActionClock(e.ActiveTotalTicks, e.ActiveRemainingTicks);
                entity.AddComponent(actionState);

                if (e.HasCultivation)
                {
                    var cultivation = new CultivationComponent
                    {
                        Realm = (RealmStage)e.Realm,
                        MinorStage = e.CultivationMinorStage,
                        Progress = e.CultivationProgress,
                        BreakthroughProgressRequired = e.BreakthroughProgressRequired,
                        CultivationSpeed = e.CultivationSpeed,
                        RequiredRealmName = e.RequiredRealmName ?? string.Empty
                    };
                    if (!string.IsNullOrEmpty(e.LearnedManualId) &&
                        DefinitionId.TryParse(e.LearnedManualId, out var manualId))
                    {
                        cultivation.LearnedManualId = manualId;
                    }

                    if (e.HasManualMastery)
                    {
                        cultivation.ManualMastery = new SkillMasteryState
                        {
                            Tier = (SkillMasteryTier)e.ManualMasteryTier,
                            Progress = e.ManualMasteryProgress,
                            ProgressRequired = e.ManualMasteryProgressRequired > 0
                                ? e.ManualMasteryProgressRequired
                                : SkillMasteryRules.ProgressRequiredToNext((SkillMasteryTier)e.ManualMasteryTier)
                        };
                    }
                    else if (cultivation.HasLearnedManual)
                    {
                        cultivation.ManualMastery = SkillMasteryState.CreateEntry();
                    }

                    entity.AddComponent(cultivation);
                }
                else
                {
                    entity.AddComponent(new CultivationComponent());
                }

                var artsComp = new CombatArtsComponent();
                if (e.CombatArtsLearned != null)
                {
                    for (var i = 0; i < e.CombatArtsLearned.Count; i++)
                    {
                        if (DefinitionId.TryParse(e.CombatArtsLearned[i], out var artId))
                            artsComp.TryLearn(artId);
                    }
                }

                if (e.CombatArtMastery != null)
                {
                    for (var i = 0; i < e.CombatArtMastery.Count; i++)
                    {
                        var m = e.CombatArtMastery[i];
                        if (m == null || string.IsNullOrEmpty(m.ArtId) ||
                            !DefinitionId.TryParse(m.ArtId, out var artId))
                            continue;
                        artsComp.SetMastery(artId, new SkillMasteryState
                        {
                            Tier = (SkillMasteryTier)m.Tier,
                            Progress = m.Progress,
                            ProgressRequired = m.ProgressRequired
                        });
                    }
                }

                for (var s = 0; s < CombatArtsComponent.MaxEquippedSlots; s++)
                    artsComp.ClearSlot(s);
                if (e.CombatArtsEquipped != null)
                {
                    for (var s = 0; s < CombatArtsComponent.MaxEquippedSlots && s < e.CombatArtsEquipped.Count; s++)
                    {
                        var text = e.CombatArtsEquipped[s];
                        if (string.IsNullOrEmpty(text))
                            continue;
                        if (DefinitionId.TryParse(text, out var eqId))
                            artsComp.TryEquipToSlot(s, eqId);
                    }
                }

                entity.AddComponent(artsComp);

                if (e.HasDailyTask)
                {
                    var required = e.RequiredAmount > 0 ? e.RequiredAmount : (e.LaborQuota > 0 ? e.LaborQuota : 10);
                    var completed = e.CompletedAmount > 0 ? e.CompletedAmount : e.LaborProgress;
                    entity.AddComponent(new DailyTaskComponent
                    {
                        RequiredAmount = required,
                        CompletedAmount = completed,
                        Deviation = e.Deviation,
                        PendingReprimand = e.PendingReprimand,
                        LastSettledDeviation = e.LastSettledDeviation
                    });
                }
                else
                {
                    entity.AddComponent(new DailyTaskComponent());
                }

                if (e.HasSchedule && !string.IsNullOrEmpty(e.ScheduleDefinitionId))
                    entity.AddComponent(new ScheduleComponent(e.ScheduleDefinitionId));

                var known = new KnownSitesComponent();
                known.Restore(e.KnownSiteIds);
                entity.AddComponent(known);
                entity.AddComponent(new PersonalConcealmentRiskComponent { Value = e.PersonalConcealmentRisk });

                var faction = new FactionMembershipComponent { InitialLoyalty = e.InitialLoyalty };
                if (!string.IsNullOrEmpty(e.FactionId))
                    faction.Assign(e.FactionId, (FactionRoleKind)e.FactionRole);
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
                else if (!string.IsNullOrEmpty(snap.Strategic?.PlayerFactionId) &&
                         IsLikelyPlayerRosterCharacter(snap, e.Id))
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[SnapshotFactionRestore] CharacterId=" + e.Id +
                        " FactionId=(empty) FAILED: membership missing from snapshot entity JSON");
                }
#endif
                entity.AddComponent(faction);

                var vitals = new CombatVitalsComponent();
                if (e.HasCombatVitals)
                {
                    vitals.CurrentHp = e.CurrentHp;
                    vitals.CurrentSpiritPower = e.CurrentSpiritPower;
                    vitals.PoolsInitialized = e.VitalsPoolsInitialized;
                }
                entity.AddComponent(vitals);

                if (e.Flight != null)
                    entity.AddComponent(new FlightComponent { Airborne = e.Flight.Airborne,
                        SurfaceId = e.Flight.SurfaceId, ScopeId = e.Flight.ScopeId, HasGround = e.Flight.HasGround,
                        GroundX = e.Flight.GroundX, GroundY = e.Flight.GroundY, DetachedFollower = e.Flight.DetachedFollower });

                if (e.HasCorpse)
                {
                    entity.AddComponent(new CorpseComponent
                    {
                        RemoveAfterTick = e.CorpseRemoveAfterTick
                    });
                }

                if (e.HasResponsibleAttacker && e.ResponsibleAttackerEntityId != 0)
                {
                    var attribution = new CombatDeathAttributionComponent();
                    attribution.Set(new EntityId(e.ResponsibleAttackerEntityId));
                    entity.AddComponent(attribution);
                }

                var personality = new PersonalityProfileComponent();
                if (e.PersonalityTags != null && e.PersonalityTags.Count > 0)
                    personality.SetTags(e.PersonalityTags);
                entity.AddComponent(personality);

                if (e.HasNpcAutonomy)
                    entity.AddComponent(new NpcAutonomyState
                    {
                        StrikeCooldown = Math.Max(0, e.NpcStrikeCooldown) ,
                        Art0 = Math.Max(0, e.NpcArt0) ,
                        Art1 = Math.Max(0, e.NpcArt1) ,
                        Art2 = Math.Max(0, e.NpcArt2) ,
                        Art3 = Math.Max(0, e.NpcArt3) ,
                        Art4 = Math.Max(0, e.NpcArt4) ,
                        Art5 = Math.Max(0, e.NpcArt5) ,
                        Retreating = e.NpcRetreating ,
                        RetreatSurface = e.NpcRetreatSurface ,
                        RetreatX = e.NpcRetreatX ,
                        RetreatY = e.NpcRetreatY ,
                        LastMoveTick = e.NpcLastMoveTick ,
                        Exhausted = e.NpcExhausted ,
                        DetachedByExhaustion = e.NpcDetachedByExhaustion ,
                    });

                if (e.HasPersonalLife)
                {
                    var personalLife = new PersonalLifeComponent
                    {
                        Preference = System.Enum.IsDefined(typeof(PersonalLifePreference), e.PersonalLifePreference)
                            ? (PersonalLifePreference)e.PersonalLifePreference : PersonalLifePreference.Balanced,
                        Activity = System.Enum.IsDefined(typeof(PersonalLifeActivity), e.PersonalLifeActivity)
                            ? (PersonalLifeActivity)e.PersonalLifeActivity : PersonalLifeActivity.None,
                        Satiety = e.PersonalSatiety,
                        Energy = e.PersonalEnergy,
                        CurrentMood = e.PersonalCurrentMood,
                        LastProcessedTick = e.PersonalLastProcessedTick,
                        LastCultivationTick = e.PersonalLastCultivationTick,
                        LastRecreationTick = e.PersonalLastRecreationTick,
                        FoodRoomId = e.PersonalFoodRoomId ?? string.Empty,
                        FoodAccessAttempt = Math.Max(0, e.PersonalFoodAccessAttempt),
                        FoodRetryAfterTick = e.PersonalFoodRetryAfterTick,
                        BedRetryAfterTick = e.PersonalBedRetryAfterTick,
                        MeditationSpotId = e.PersonalMeditationSpotId ?? string.Empty,
                        MeditationTraveling = e.PersonalMeditationTraveling
                    };
                    personalLife.Clamp();
                    entity.AddComponent(personalLife);
                }

                if (e.HasEntityLocation)
                {
                    entity.AddComponent(new EntityLocationComponent
                    {
                        LocationId = e.LocationId ?? string.Empty,
                        HasPresentationOverride = e.HasPresentationOverride,
                        PresentationOverrideX = e.PresentationOverrideX,
                        PresentationOverrideZ = e.PresentationOverrideZ
                    });
                }
                entity.AddComponent(new EntityLocationSnapshotAuthorityComponent
                {
                    SnapshotFieldPresent = e.EntityLocationSnapshotFieldPresent
                });

                entity.AddComponent(new RelationshipComponent());

                // Inject into store via reflection-free path: recreate through internal add
                InjectEntity(world.Entities, entity);
                if (!e.HasPersonalLife)
                    PersonalLifeService.Ensure(world, entity);
                if (e.HasMortalCivilian)
                {
                    var civilian = world.Civilians.GetOrCreate(entity.Id);
                    civilian.Satiety = e.MortalSatiety; civilian.Energy = e.MortalEnergy;
                    civilian.Profession = (MortalProfession)e.MortalProfession; civilian.Activity = (MortalActivity)e.MortalActivity;
                    civilian.Disposition = (CivilianDisposition)e.CivilianDisposition;
                    civilian.ResidenceWorkAreaId = e.MortalResidenceWorkAreaId ?? string.Empty;
                    civilian.CurrentSiteId = e.MortalCurrentSiteId ?? string.Empty;
                    civilian.LastUpdateTick = e.MortalLastUpdateTick; civilian.SleepStartedTick = e.MortalSleepStartedTick;
                    civilian.SleepPhase = (CivilianSleepPhase)e.MortalSleepPhase;
                    civilian.FleeStartedTick = e.MortalFleeStartedTick; civilian.HasFleeTarget = e.MortalHasFleeTarget;
                    civilian.FleeTargetX = e.MortalFleeTargetX; civilian.FleeTargetY = e.MortalFleeTargetY;
                    civilian.FleeSourceSiteId = e.MortalFleeSourceSiteId ?? string.Empty;
                    civilian.RefugeTargetSiteId = e.MortalRefugeTargetSiteId ?? string.Empty;
                    civilian.BackgroundNeedsLastProcessedTick = e.MortalBackgroundNeedsLastProcessedTick;
                    civilian.FleeCandidateIndex = e.MortalFleeCandidateIndex;
                    civilian.FleeAttemptCount = e.MortalFleeAttemptCount;
                    civilian.CarrierId = new EntityId(e.MortalCarrierId);
                    civilian.DetainedResidenceWorkAreaId = e.MortalDetainedResidenceWorkAreaId ?? string.Empty;
                    civilian.RescueTargetId = new EntityId(e.MortalRescueTargetId);
                    civilian.MedicTargetId = new EntityId(e.MedicTargetId);
                    civilian.MedicCarryingHerb = e.MedicCarryingHerb;
                    civilian.MedicSourceSiteId = e.MedicSourceSiteId ?? string.Empty;
                    civilian.MedicSourceRoomId = e.MedicSourceRoomId ?? string.Empty;
                    civilian.MedicSourcePartyMemberId = new EntityId(e.MedicSourcePartyMemberId);
                    civilian.MedicTreatmentSeconds = e.MedicTreatmentSeconds;
                    civilian.MedicLastTreatmentTick = e.MedicLastTreatmentTick;
                    civilian.MedicRetryAfterTick = e.MedicRetryAfterTick;
                    civilian.MedicStatus = e.MedicStatus ?? string.Empty;

                    civilian.RescueCarrying = e.MortalRescueCarrying;
                    civilian.RescueDestinationId = e.MortalRescueDestinationId ?? string.Empty;
                    civilian.RescueRejectedDestinationId = e.MortalRescueRejectedDestinationIds ?? string.Empty;
                    civilian.CareDayIndex = e.MortalCareDayIndex; civilian.AteToday = e.MortalAteToday; civilian.SleptToday = e.MortalSleptToday;
                    civilian.LastWorkOutputTick = e.MortalLastWorkOutputTick;
                    civilian.PendingCaptureCarrierId = new EntityId(e.MortalPendingCaptureCarrierId);
                    civilian.Clamp();
                }
            }

            foreach (var loyalty in snap.CharacterFactionLoyalties)
                world.FactionLoyalties.Restore(new CharacterFactionLoyaltyEntry {
                    CharacterId = new EntityId(loyalty.CharacterEntityId),
                    FactionId = loyalty.FactionId,
                    Loyalty = loyalty.Loyalty
                });

            if (snap.CivilianResidenceUsages != null)
                for (var i = 0; i < snap.CivilianResidenceUsages.Count; i++)
                {
                    var usage = snap.CivilianResidenceUsages[i];
                    if (usage != null && !string.IsNullOrWhiteSpace(usage.WorkAreaId))
                        world.Civilians.SetResidenceUsage(usage.WorkAreaId, (ResidenceUsage)usage.Usage);
                }

            foreach (var a in snap.ActiveActions)
            {
                if (a.Kind == "Wait")
                {
                    var wait = new WaitAction(
                        new ActionId(a.Id),
                        new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId),
                        a.TotalTicks);
                    wait.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[wait.Id] = wait;
                }
                else if (a.Kind == "Cultivate")
                {
                    var cultivate = new CultivateAction(
                        new ActionId(a.Id),
                        new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId),
                        a.TotalTicks);
                    cultivate.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[cultivate.Id] = cultivate;
                }
                else if (a.Kind == "Labor")
                {
                    var labor = new LaborAction(
                        new ActionId(a.Id),
                        new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId),
                        a.TotalTicks);
                    labor.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[labor.Id] = labor;
                }
                else if (a.Kind == "Rest")
                {
                    var rest = new RestAction(
                        new ActionId(a.Id),
                        new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId),
                        a.TotalTicks);
                    rest.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[rest.Id] = rest;
                }
                else if (a.Kind == "Recover")
                {
                    var recovery = new RecoveryAction(
                        new ActionId(a.Id),
                        new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId),
                        a.TotalTicks,
                        a.TargetRef ?? string.Empty);
                    recovery.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[recovery.Id] = recovery;
                }
                else if (a.Kind == "HuangcunSurvival")
                {
                    if (!XianXia.Core.Npc.HuangcunSurvivalService.IsKnownTask(a.TargetRef) ||
                        a.TotalTicks != XianXia.Core.Npc.HuangcunSurvivalService.Duration(a.TargetRef ?? string.Empty))
                        return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid survival action clock.");
                    var survival = new HuangcunSurvivalAction(new ActionId(a.Id), new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId), a.TotalTicks, a.TargetRef ?? string.Empty);
                    survival.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[survival.Id] = survival;
                }
                else if (a.Kind == "BedRest")
                {
                    var bedRest = new BedRestAction(new ActionId(a.Id), new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId), a.TotalTicks, a.TargetRef ?? string.Empty);
                    bedRest.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[bedRest.Id] = bedRest;
                    if ((ActionStatus)a.Status == ActionStatus.Running &&
                        !RoomBedFacilityService.TryOccupy(world, a.TargetRef ?? string.Empty, new EntityId(a.SubjectId)))
                        return Result.Fail<(SimulationWorld world, SimulationLoop loop)>(
                            ErrorCode.SnapshotInvalid, "Bed rest occupancy could not be restored.");
                }
                else if (a.Kind == "Observe")
                {
                    var observe = new ObserveAction(
                        new ActionId(a.Id),
                        new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId),
                        a.TotalTicks);
                    observe.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[observe.Id] = observe;
                }
                else if (a.Kind == "Move")
                {
                    var move = new MoveAction(
                        new ActionId(a.Id),
                        new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId),
                        a.TotalTicks,
                        a.TargetRef ?? string.Empty);
                    move.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[move.Id] = move;
                }
                else if (a.Kind == "Work")
                {
                    var activity = a.Activity > 0
                        ? (XianXia.Core.Schedule.ScheduleActivity)a.Activity
                        : XianXia.Core.Schedule.ScheduleActivity.Labor;
                    var work = new WorkAction(
                        new ActionId(a.Id),
                        new EntityId(a.SubjectId),
                        new OrderId(a.SourceOrderId),
                        a.TotalTicks,
                        activity,
                        a.TargetRef ?? string.Empty);
                    work.Restore((ActionStatus)a.Status, new ActionClock(a.TotalTicks, a.RemainingTicks));
                    world.ActiveActions[work.Id] = work;
                }
            }

            foreach (var group in snap.Orders.GroupBy(o => o.SubjectId))
            {
                var queue = world.GetOrCreateOrderQueue(new EntityId(group.Key));
                foreach (var o in group)
                {
                    XianXia.Core.Schedule.ScheduleActivity? activity = null;
                    if (o.Activity > 0)
                        activity = (XianXia.Core.Schedule.ScheduleActivity)o.Activity;
                    queue.Enqueue(new Order(
                        new OrderId(o.Id),
                        new EntityId(o.SubjectId),
                        (OrderType)o.Type,
                        (OrderSource)o.Source,
                        waitTicks: o.WaitTicks,
                        targetRef: o.TargetRef,
                        activity: activity));
                }
            }

            if (snap.SchemaVersion >= WorldSnapshot.CurrentSchemaVersion && snap.Strategic != null)
            {
                // Site references on squads/facilities are validated below, before the full
                // content rehydration phase. Populate identities only; never replay NewGame.
                if (prepareSiteIdentities != null)
                {
                    var identities = prepareSiteIdentities(world, snap);
                    if (identities.IsFailure)
                        return Result.Fail<(SimulationWorld, SimulationLoop)>(identities.Error);
                }
                world.ChessGames.Clear();
                foreach (var game in snap.ChessGames) world.ChessGames.Add(game.BoardId, game.Copy());
                world.Strategic.PeaceNegotiations.Clear();
                world.Strategic.PeaceNegotiations.AddRange(snap.PeaceNegotiations.Select(n => n.Copy()));
                var strategicRestore = StrategicSnapshotHelper.Restore(world, snap.Strategic);
                if (strategicRestore.IsFailure)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(strategicRestore.Error);
                var personalRestore = StrategicSnapshotHelper.ValidateRestoredCharacterWorldPresences(
                    world, snap.Strategic);
                if (personalRestore.IsFailure)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(personalRestore.Error);
            }
            else if (snap.SchemaVersion >= WorldSnapshot.CurrentSchemaVersion)
            {
                return Result.Fail<(SimulationWorld, SimulationLoop)>(
                    ErrorCode.SnapshotInvalid,
                    "Schema v2 snapshot missing strategic state.",
                    snap.SchemaVersion.ToString());
            }

            RestorePartyInventory(world, snap);
            if (snap.HasTakenWorldLootSnapshotAuthority)
                WorldLootPickupService.RestoreTakenSpotIds(world, snap.TakenWorldLootSpotIds);
            var farmRestore = RestoreConstructedAssets(world, snap);
            if (farmRestore.IsFailure) return Result.Fail<(SimulationWorld, SimulationLoop)>(farmRestore.Error);
            world.HuangcunSurvival = (snap.HuangcunSurvival ?? new XianXia.Core.Npc.HuangcunSurvivalState()).Copy();
            var survivalState = world.HuangcunSurvival;
            if (survivalState.Enabled &&
                (!world.Entities.TryGet(new EntityId(survivalState.PrisonerId), out _) ||
                 !world.OutdoorConstructedAssets.Assets.ContainsKey(survivalState.PrisonRoomId) ||
                 !string.IsNullOrEmpty(survivalState.CampRoomId) && !world.OutdoorConstructedAssets.Assets.ContainsKey(survivalState.CampRoomId)))
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Survival character or room reference missing.");
            if (snap.GroundLoot == null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Ground loot authority missing.");
            foreach (var drop in snap.GroundLoot)
            {
                if (drop == null || !drop.IsValid || world.GroundLoot.ContainsKey(drop.Id) || WorldLootPickupService.IsTaken(world, drop.Id))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid or duplicate ground loot.");
                world.GroundLoot.Add(drop.Id, drop.Copy());
            }
            var recoveryRestore = RestoreRecoverySpotPlacements(world, snap);
            if (recoveryRestore.IsFailure) return Result.Fail<(SimulationWorld, SimulationLoop)>(recoveryRestore.Error);
            var recoveryPhysical = XianXia.Core.Npc.MortalCivilianService
                .ValidateRecoverySpotPlacements(world, true);
            if (recoveryPhysical.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(recoveryPhysical.Error);
            var transportPhysical = MortalCivilianService.ValidateTransportPlacements(world);
            if (transportPhysical.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(transportPhysical.Error);
            var jobRestore = RestoreCivilianConstructionJobs(world, snap);
            if (jobRestore.IsFailure) return Result.Fail<(SimulationWorld, SimulationLoop)>(jobRestore.Error);
            RestoreOutdoorStatefulObjects(world, snap);
            var bondRestore = RestoreSocialBonds(world, snap);
            if (bondRestore.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(bondRestore.Error);
            var relationshipRestore = RestoreRelationshipLedger(world, snap);
            if (relationshipRestore.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(relationshipRestore.Error);
            var encounterRestore = CharacterEncounterService.ValidateRestored(world, snap.CharacterEncounter);
            if (encounterRestore.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(encounterRestore.Error);
            foreach (var key in snap.SuppressedCharacterContacts) world.Strategic.SuppressedCharacterContacts.Add(key);
            world.Strategic.CharacterEncounter = snap.CharacterEncounter;
            CharacterEncounterService.BindRuntime(world);
            if (snap.Commerce == null || snap.Commerce.PlayerWallet == null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Commerce authority missing.");
            foreach (var p in snap.Commerce.CharacterWallets)
                if (p.Key.IsNone || p.Value == null || !world.Entities.TryGet(p.Key, out var owner) || (owner.Tags & (EntityTag.Character | EntityTag.Npc)) == 0)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid character wallet owner.");
            foreach (var owner in world.Entities.All)
                if ((owner.Tags & (EntityTag.Character | EntityTag.Npc)) != 0 && !snap.Commerce.CharacterWallets.ContainsKey(owner.Id))
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Character wallet authority missing.", owner.Id.ToString());
            foreach (var p in snap.Commerce.Shops)
            {
                if (string.IsNullOrWhiteSpace(p.Key) || p.Value == null || p.Value.Wallet == null)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid shop wallet.");
                foreach (var entry in p.Value.Stock) if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid shop stock.");
            }
            foreach (var p in snap.Commerce.AuctionHouses)
            {
                if (string.IsNullOrWhiteSpace(p.Key) || p.Value == null || p.Value.HouseWallet == null || p.Value.NextListingSequence == 0 || p.Value.NextClaimSequence == 0)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid auction house authority.");
                var listingIds = new HashSet<string>(StringComparer.Ordinal);
                ulong maxListingSequence = 0;
                foreach (var listingPair in p.Value.Listings)
                {
                    var listing = listingPair.Value;
                    if (listing == null || listingPair.Key != listing.ListingId || listing.AuctionHouseId != p.Key || string.IsNullOrWhiteSpace(listing.ListingId) || !listingIds.Add(listing.ListingId) ||
                        listing.Sequence == 0 || listing.Quantity <= 0 || !SpiritStoneWallet.ValidGrade(listing.Grade) || listing.StartingPriceAmount <= 0 ||
                        listing.CurrentBidAmount < 0 || listing.PlayerEscrowAmount < 0 || listing.CreatedTick > listing.EndTick ||
                        (listing.Status == AuctionListingStatus.Open && listing.CurrentBidderKind == AuctionBidderKind.Player && listing.PlayerEscrowAmount != listing.CurrentBidAmount) ||
                        ((listing.Status != AuctionListingStatus.Open || listing.CurrentBidderKind != AuctionBidderKind.Player) && listing.PlayerEscrowAmount != 0))
                        return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid auction listing authority.", listing?.ListingId);
                    if (listing.Sequence > maxListingSequence) maxListingSequence = listing.Sequence;
                }
                var claimIds = new HashSet<string>(StringComparer.Ordinal);
                ulong maxClaimSequence = 0;
                foreach (var claimPair in p.Value.Claims)
                {
                    var claim = claimPair.Value;
                    if (claim == null || claimPair.Key != claim.ClaimId || claim.AuctionHouseId != p.Key || string.IsNullOrWhiteSpace(claim.ClaimId) || !claimIds.Add(claim.ClaimId) ||
                        claim.Sequence == 0 || claim.Quantity <= 0 || string.IsNullOrWhiteSpace(claim.ItemId) || string.IsNullOrWhiteSpace(claim.SourceListingId))
                        return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Invalid auction claim authority.", claim?.ClaimId);
                    if (claim.Sequence > maxClaimSequence) maxClaimSequence = claim.Sequence;
                }
                if (p.Value.NextListingSequence <= maxListingSequence || p.Value.NextClaimSequence <= maxClaimSequence)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Auction sequence authority is not ahead of stored identities.", p.Key);
            }
            world.Commerce = snap.Commerce.Capture();
            var contentProgressRestore = ContentProgressSnapshotHelper.Restore(world, snap.ContentProgress);
            if (contentProgressRestore.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(contentProgressRestore.Error);
            var companionRestore = QuestCompanionService.ValidateRestored(world,snap.Strategic.ControlledSquadId,false);
            if (companionRestore.IsFailure) return Result.Fail<(SimulationWorld, SimulationLoop)>(companionRestore.Error);
            var opportunityRestore = RestoreWorldOpportunities(world, snap.WorldOpportunityRuntime);
            if (opportunityRestore.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(opportunityRestore.Error);
            var activityRestore = RestoreWorldActivities(world, snap.WorldActivityRuntime);
            if (activityRestore.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(activityRestore.Error);
            var loop = PlayableSimulationLoopFactory.Create(world, enableSocialTick: false);
            loop.RestoreNextOrderId(snap.NextOrderId);
            return Result.Ok((world, loop));
        }

        static Result RestoreWorldActivities(SimulationWorld world, WorldActivityRuntimeSnapshotDto runtime)
        {
            if (runtime == null || !runtime.HasAuthority) return Result.Success();
            if (runtime.NextActivitySequence == 0)
                return Result.Failure(ErrorCode.SnapshotInvalid, "WorldActivity next sequence is invalid.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var allSources = new HashSet<string>(StringComparer.Ordinal);
            ulong highestSequence = 0;
            var historyCount = 0;
            var entries = runtime.Entries ?? new List<WorldActivityEntrySnapshotDto>();
            for (var i = 0; i < entries.Count; i++)
            {
                var saved = entries[i];
                ulong sequence = 0;
                var validSequence = saved != null && saved.ActivityId != null &&
                    saved.ActivityId.StartsWith("activity:", StringComparison.Ordinal) &&
                    ulong.TryParse(saved.ActivityId.Substring("activity:".Length), out sequence) && sequence > 0;
                var active = saved != null && saved.State == WorldActivityState.Active;
                var history = saved != null && saved.State == WorldActivityState.History;
                ulong sourceSequence = 0;
                var validOpportunitySource = saved != null && saved.SourceId != null &&
                    saved.SourceId.StartsWith("opportunity:", StringComparison.Ordinal) &&
                    ulong.TryParse(saved.SourceId.Substring("opportunity:".Length), out sourceSequence) &&
                    sourceSequence > 0;
                if (saved == null || !validSequence || !ids.Add(saved.ActivityId) ||
                    string.IsNullOrWhiteSpace(saved.SourceKind) || string.IsNullOrWhiteSpace(saved.SourceId) ||
                    !validOpportunitySource || string.IsNullOrWhiteSpace(saved.Title) ||
                    string.IsNullOrWhiteSpace(saved.Body) || (!active && !history) ||
                    (active && saved.HasResolvedDayIndex) || (history && !saved.HasResolvedDayIndex) ||
                    (saved.HasResolvedDayIndex && saved.ResolvedDayIndex < saved.CreatedDayIndex) ||
                    !string.Equals(saved.SourceKind, WorldActivitySourceKind.WorldOpportunity, StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid WorldActivity entry.", i.ToString());
                var sourceKey = saved.SourceKind + "\n" + saved.SourceId;
                if (!allSources.Add(sourceKey) ||
                    (active && (!world.WorldOpportunities.ActiveInstances.TryGetValue(saved.SourceId, out var source) ||
                                (source.DiscoveryMode != WorldOpportunityDiscoveryMode.PublicNotice &&
                                 !(source.DiscoveryMode == WorldOpportunityDiscoveryMode.HiddenUntilDiscovered &&
                                   source.IsDiscovered)))))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "WorldActivity source is duplicated or its active Opportunity is missing.", saved.SourceId);
                if (history && ++historyCount > WorldActivityBoard.HistoryCapacity)
                    return Result.Failure(ErrorCode.SnapshotInvalid, "WorldActivity history exceeds capacity.");
                if (!world.WorldActivities.RestoreEntry(new WorldActivityEntry
                {
                    ActivityId = saved.ActivityId,
                    SourceKind = saved.SourceKind,
                    SourceId = saved.SourceId,
                    Title = saved.Title,
                    Body = saved.Body ?? string.Empty,
                    CreatedDayIndex = saved.CreatedDayIndex,
                    State = saved.State,
                    IsRead = saved.IsRead,
                    ResolvedDayIndex = saved.HasResolvedDayIndex ? saved.ResolvedDayIndex : (ulong?)null
                }))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "WorldActivity entry cannot be restored.", saved.ActivityId);
                if (sequence > highestSequence) highestSequence = sequence;
            }
            if (runtime.NextActivitySequence <= highestSequence)
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "WorldActivity next sequence would collide with an existing entry.");
            world.WorldActivities.RestoreSequence(runtime.NextActivitySequence);
            return Result.Success();
        }

        static Result RestoreWorldOpportunities(SimulationWorld world, WorldOpportunityRuntimeSnapshotDto runtime)
        {
            if (runtime == null || !runtime.HasAuthority) return Result.Success();
            if (runtime.NextInstanceSequence == 0)
                return Result.Failure(ErrorCode.SnapshotInvalid, "WorldOpportunity next sequence is invalid.");
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            var entityIds = new HashSet<ulong>();
            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            ulong highestInstanceSequence = 0;
            var instances = runtime.Instances ?? new List<WorldOpportunityInstanceSnapshotDto>();
            for (var i = 0; i < instances.Count; i++)
            {
                var saved = instances[i];
                ulong instanceSequence = 0;
                var hasValidInstanceSequence = saved != null &&
                    saved.InstanceId != null &&
                    saved.InstanceId.StartsWith("opportunity:", StringComparison.Ordinal) &&
                    ulong.TryParse(saved.InstanceId.Substring("opportunity:".Length), out instanceSequence) &&
                    instanceSequence > 0;
                if (saved == null || string.IsNullOrWhiteSpace(saved.InstanceId) ||
                    !hasValidInstanceSequence ||
                    !instanceIds.Add(saved.InstanceId) ||
                    !DefinitionId.TryParse(saved.OpportunityDefinitionId, out _) ||
                    string.IsNullOrWhiteSpace(saved.SurfaceId) ||
                    saved.ExpireDayIndexExclusive <= saved.CreatedDayIndex ||
                    (saved.DiscoveryMode != WorldOpportunityDiscoveryMode.WorldVisible &&
                     saved.DiscoveryMode != WorldOpportunityDiscoveryMode.PublicNotice &&
                     saved.DiscoveryMode != WorldOpportunityDiscoveryMode.HiddenUntilDiscovered))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid or duplicate WorldOpportunity instance.", i.ToString());
                if (instanceSequence > highestInstanceSequence) highestInstanceSequence = instanceSequence;
                var npc = saved.SpawnKind == WorldOpportunitySpawnKind.Npc;
                var obj = saved.SpawnKind == WorldOpportunitySpawnKind.WorldObject;
                var finite = !float.IsNaN(saved.WorldX) && !float.IsInfinity(saved.WorldX) &&
                             !float.IsNaN(saved.WorldY) && !float.IsInfinity(saved.WorldY);
                if ((!npc && !obj) ||
                    (npc && (saved.SpawnedEntityId == 0 || !entityIds.Add(saved.SpawnedEntityId) ||
                             !string.IsNullOrEmpty(saved.WorldObjectInstanceId))) ||
                    (obj && (saved.SpawnedEntityId != 0 || string.IsNullOrWhiteSpace(saved.WorldObjectInstanceId) ||
                             !objectIds.Add(saved.WorldObjectInstanceId) || !finite ||
                             saved.WorldObjectInstanceId != WorldOpportunityBoard.WorldObjectIdFor(saved.InstanceId))) ||
                    (saved.DiscoveryMode == WorldOpportunityDiscoveryMode.HiddenUntilDiscovered && !obj))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid WorldOpportunity spawn identity.", saved.InstanceId);
                var entityId = new EntityId(saved.SpawnedEntityId);
                if (npc && !world.Entities.TryGet(entityId, out _))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "WorldOpportunity entity is missing.", saved.SpawnedEntityId.ToString());
                if (!world.WorldOpportunities.AddInstance(new WorldOpportunityInstance
                {
                    InstanceId = saved.InstanceId,
                    OpportunityDefinitionId = saved.OpportunityDefinitionId,
                    SurfaceId = saved.SurfaceId,
                    SpawnKind = saved.SpawnKind,
                    SpawnedEntityId = entityId,
                    WorldObjectInstanceId = saved.WorldObjectInstanceId,
                    WorldX = saved.WorldX,
                    WorldY = saved.WorldY,
                    CreatedDayIndex = saved.CreatedDayIndex,
                    ExpireDayIndexExclusive = saved.ExpireDayIndexExclusive,
                    DiscoveryMode = saved.DiscoveryMode,
                    IsDiscovered = saved.IsDiscovered
                }))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "WorldOpportunity binding cannot be restored.", saved.InstanceId);
            }
            var surfaces = runtime.SurfaceRefreshStates ?? new List<WorldOpportunitySurfaceRefreshSnapshotDto>();
            for (var i = 0; i < surfaces.Count; i++)
            {
                var saved = surfaces[i];
                if (saved == null || !world.WorldOpportunities.RestoreRefreshState(saved.SurfaceId, saved.LastRefreshDayIndex))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid or duplicate WorldOpportunity Surface refresh state.", i.ToString());
            }
            if (runtime.NextInstanceSequence <= highestInstanceSequence)
                return Result.Failure(ErrorCode.SnapshotInvalid, "WorldOpportunity next sequence would collide with an existing instance.");
            world.WorldOpportunities.RestoreSequence(runtime.NextInstanceSequence);
            return Result.Success();
        }

        static void RestorePartyInventory(SimulationWorld world, WorldSnapshot snap)
        {
            if (world?.Inventory == null || snap?.PartyInventorySlots == null)
                return;

            var slots = world.Inventory.Slots;
            for (var i = 0; i < slots.Count; i++)
                slots[i].Clear();

            for (var i = 0; i < snap.PartyInventorySlots.Count; i++)
            {
                var s = snap.PartyInventorySlots[i];
                if (s == null || string.IsNullOrEmpty(s.ItemId) || s.Count <= 0)
                    continue;
                world.Inventory.TryAdd(s.ItemId, s.Count);
            }
        }

        static Result RestoreRelationshipLedger(SimulationWorld world, WorldSnapshot snap)
        {
            if (world?.Relationships == null || snap?.RelationshipEvents == null)
                return Result.Success();

            world.Relationships.Clear();
            for (var i = 0; i < snap.RelationshipEvents.Count; i++)
            {
                var d = snap.RelationshipEvents[i];
                if (d == null)
                    continue;
                EventId? cause = null;
                if (d.HasCauseEventId && d.CauseEventId != 0)
                    cause = new EventId(d.CauseEventId);
                EntityId? context = null;
                if (d.HasContextEntityId && d.ContextEntityId != 0)
                    context = new EntityId(d.ContextEntityId);
                if (d.HasAxis && !Enum.IsDefined(typeof(SocialAttitudeAxis), d.Axis))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Relationship axis 无效。", i.ToString());
                var axis = d.HasAxis ? (SocialAttitudeAxis)d.Axis : SocialAttitudeAxis.Affection;
                world.Relationships.Append(new RelationshipEvent(
                    new WorldTick(d.Tick),
                    new EntityId(d.FromEntityId),
                    new EntityId(d.ToEntityId),
                    axis,
                    d.Delta,
                    d.ReasonTag ?? string.Empty,
                    cause,
                    context));
            }

            RelationshipService.RebuildAllCaches(world);
            return Result.Success();
        }

        static Result RestoreConstructedAssets(SimulationWorld world, WorldSnapshot snap)
        {
            world.OutdoorConstructedAssets.Clear();
            if (snap.OutdoorConstructedAssets == null)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Runtime constructed asset list is null.");
            foreach (var dto in snap.OutdoorConstructedAssets)
            {
                if (dto == null) return Result.Failure(ErrorCode.SnapshotInvalid, "Runtime constructed asset is null.");
                var asset = new XianXia.Core.Construction.OutdoorConstructedAssetState {
                    StableAssetId = dto.StableAssetId,
                    BuildingId = dto.BuildingId,
                    Kind = dto.Kind,
                    SurfaceId = dto.SurfaceId,
                    WorldX = dto.WorldX,
                    WorldY = dto.WorldY,
                    WorldWidth = dto.WorldWidth,
                    WorldHeight = dto.WorldHeight,
                    CellsW = dto.CellsW,
                    CellsH = dto.CellsH,
                    BoundLocationId = dto.BoundLocationId,
                    BoundWorldSiteId = dto.BoundWorldSiteId,
                    QuarterTurns = dto.QuarterTurns,
                    ParentRoomId = dto.ParentRoomId ?? string.Empty,
                    RoomUsage = (RoomUsage)dto.RoomUsage,
                    AssignedCharacterId = new EntityId(dto.AssignedCharacterId),
                    ReservedCharacterId = new EntityId(dto.ReservedCharacterId),
                    OccupantCharacterId = new EntityId(dto.OccupantCharacterId),
                    InitialAssigneeSpawnKey = dto.InitialAssigneeSpawnKey ?? string.Empty,
                    StructureDurability = dto.StructureDurability };
                if (!Enum.IsDefined(typeof(RoomUsage), dto.RoomUsage))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Runtime room usage is invalid.", dto.StableAssetId);
                if (string.Equals(asset.Kind,
                        XianXia.Core.Construction.OutdoorConstructedAssetSemantics.StorageRoomKind,
                        StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(asset.BoundWorldSiteId) ||
                     !world.Strategic.Sites.TryGet(asset.BoundWorldSiteId, out _)))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Runtime storage room references a missing WorldSite.", dto.StableAssetId);
                if (!world.OutdoorConstructedAssets.TryRegister(asset))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid or duplicate runtime constructed asset.", dto.StableAssetId);
            }
            return world.OutdoorConstructedAssets.RestoreSequence(snap.NextOutdoorConstructedAssetSequence)
                ? Result.Success() : Result.Failure(ErrorCode.SnapshotInvalid, "Invalid runtime constructed asset sequence.");
        }

        static Result RestoreRecoverySpotPlacements(SimulationWorld world, WorldSnapshot snap)
        {
            world.RecoverySpotDeliveries.Clear();
            if (snap.RecoverySpotPlacements == null)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Recovery spot placement list is null.");
            foreach (var dto in snap.RecoverySpotPlacements)
            {
                var injured = new EntityId(dto?.InjuredCharacterId ?? 0UL);
                if (dto == null || injured.IsNone || !world.Entities.TryGet(injured, out var entity) ||
                    !entity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life) || !life.IsIncapacitated ||
                    !world.OutdoorConstructedAssets.Assets.TryGetValue(dto.RecoverySpotId, out var spot) ||
                    !(RoomBedGeometry.IsBed(spot) &&
                      (RoomBedFacilityService.BedHasUsage(world, spot, RoomUsage.Medical) ||
                       RoomBedFacilityService.BedHasUsage(world, spot, RoomUsage.Residential))) ||
                    spot.OccupantCharacterId != injured || spot.ReservedCharacterId != injured ||
                    !world.RecoverySpotDeliveries.TryRestore(dto.RecoverySpotId, injured))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Invalid or duplicate recovery spot placement.", dto?.RecoverySpotId ?? string.Empty);
            }
            return Result.Success();
        }

        static Result RestoreCivilianConstructionJobs(SimulationWorld world, WorldSnapshot snap)
        {
            world.CivilianConstructionJobs.Clear();
            if (snap.CivilianConstructionJobs == null || snap.NextCivilianConstructionJobSequence < 1)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Civilian construction job authority is invalid.");
            foreach (var dto in snap.CivilianConstructionJobs)
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.JobId) ||
                    !dto.JobId.StartsWith("civilian:construction:", StringComparison.Ordinal) ||
                    !long.TryParse(dto.JobId.Substring("civilian:construction:".Length),
                        NumberStyles.None, CultureInfo.InvariantCulture, out var jobNumber) ||
                    jobNumber < 1 || jobNumber >= snap.NextCivilianConstructionJobSequence ||
                    string.IsNullOrWhiteSpace(dto.BuildingId) || string.IsNullOrWhiteSpace(dto.SiteId) ||
                    string.IsNullOrWhiteSpace(dto.FactionId) || string.IsNullOrWhiteSpace(dto.SurfaceId) ||
                    !world.Strategic.Sites.TryGet(dto.SiteId, out _) ||
                    !string.IsNullOrEmpty(dto.DemolitionFlagId) &&
                        !world.Strategic.FactionFlags.Flags.ContainsKey(dto.DemolitionFlagId) ||
                    dto.RequiredLabor <= 0 || dto.LaborProgress < 0 || dto.LaborProgress >= dto.RequiredLabor ||
                    dto.Required == null || dto.Delivered == null || dto.PayloadCount < 0 ||
                    float.IsNaN(dto.WorldX) || float.IsInfinity(dto.WorldX) ||
                    float.IsNaN(dto.WorldY) || float.IsInfinity(dto.WorldY) ||
                    dto.CarrierId != 0 && !world.Entities.TryGet(new EntityId(dto.CarrierId), out _) ||
                    (dto.CarrierId == 0) != (dto.PayloadCount == 0) ||
                    (dto.PayloadCount > 0 && string.IsNullOrWhiteSpace(dto.PayloadItemId)))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Civilian construction job invalid.", dto?.JobId);
                var job = new CivilianConstructionJob {
                    JobId = dto.JobId, BuildingId = dto.BuildingId,
                    DemolitionFlagId = dto.DemolitionFlagId ?? string.Empty, SiteId = dto.SiteId,
                    FactionId = dto.FactionId, SurfaceId = dto.SurfaceId,
                    WorldX = dto.WorldX, WorldY = dto.WorldY, LaborProgress = dto.LaborProgress,
                    QuarterTurns = dto.QuarterTurns, ParentRoomId = dto.ParentRoomId ?? string.Empty,
                    RequiredLabor = dto.RequiredLabor,
                    LastLaborTick = dto.LastLaborTick, CarrierId = new EntityId(dto.CarrierId),
                    PayloadItemId = dto.PayloadItemId ?? string.Empty, PayloadCount = dto.PayloadCount
                };
                foreach (var row in dto.Required)
                {
                    if (row == null || string.IsNullOrWhiteSpace(row.ItemId) || row.Count <= 0)
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Construction cost invalid.");
                    job.Required.Add(new ConstructionMaterialCost { ItemId = row.ItemId, Count = row.Count });
                }
                foreach (var row in dto.Delivered)
                {
                    if (row == null || string.IsNullOrWhiteSpace(row.ItemId) || row.Count < 0 ||
                        job.Delivered.ContainsKey(row.ItemId))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Construction delivery invalid.");
                    job.Delivered.Add(row.ItemId, row.Count);
                }
                foreach (var delivered in job.Delivered)
                {
                    var required = 0;
                    foreach (var cost in job.Required)
                        if (cost.ItemId == delivered.Key) required += cost.Count;
                    if (required == 0 || delivered.Value > required)
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Construction delivery exceeds required materials.");
                }
                if (job.PayloadCount > 0)
                {
                    var required = 0;
                    foreach (var cost in job.Required)
                        if (cost.ItemId == job.PayloadItemId) required += cost.Count;
                    job.Delivered.TryGetValue(job.PayloadItemId, out var delivered);
                    if (required == 0 || delivered + job.PayloadCount > required)
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Construction payload exceeds required materials.");
                }
                if (!world.CivilianConstructionJobs.Restore(job))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Duplicate construction job.", dto.JobId);
            }
            world.CivilianConstructionJobs.RestoreSequence(snap.NextCivilianConstructionJobSequence);
            return Result.Success();
        }

        static void RestoreOutdoorStatefulObjects(SimulationWorld world, WorldSnapshot snap)
        {
            world.OutdoorStatefulObjects.Clear();
            if (snap.OutdoorDestructibles != null)
                for (var i = 0; i < snap.OutdoorDestructibles.Count; i++)
                {
                    var state = snap.OutdoorDestructibles[i];
                    if (state != null) world.OutdoorStatefulObjects.SetDestructible(
                        state.StableId, state.CurrentHp, state.Destroyed);
                }
            if (snap.OutdoorFarmPlots != null)
                for (var i = 0; i < snap.OutdoorFarmPlots.Count; i++)
                {
                    var state = snap.OutdoorFarmPlots[i];
                    if (state != null) world.OutdoorStatefulObjects.SetFarmPlot(
                        state.StableCellId, state.CropId, state.CropStage, state.Growth);
                }
        }

        static Result RestoreSocialBonds(SimulationWorld world, WorldSnapshot snap)
        {
            if (world?.SocialBonds == null || snap?.SocialBonds == null)
                return Result.Success();
            world.SocialBonds.Clear();
            var service = new SocialBondService();
            for (var i = 0; i < snap.SocialBonds.Count; i++)
            {
                var dto = snap.SocialBonds[i];
                if (dto == null || !Enum.IsDefined(typeof(SocialBondKind), dto.Kind))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Social Bond kind 无效。", i.ToString());
                var restored = service.RestoreBond(
                    world,
                    (SocialBondKind)dto.Kind,
                    new EntityId(dto.FromEntityId),
                    new EntityId(dto.ToEntityId));
                if (restored.IsFailure)
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Social Bond 恢复失败。", restored.Error.Message);
            }
            return Result.Success();
        }

        static bool IsLikelyPlayerRosterCharacter(WorldSnapshot snap, ulong entityId)
        {
            var party = snap?.Strategic?.PlayerParty;
            if (party != null)
            {
                if (party.ActiveCharacterId == entityId)
                    return true;
                if (party.MemberCharacterIds != null)
                {
                    for (var i = 0; i < party.MemberCharacterIds.Count; i++)
                    {
                        if (party.MemberCharacterIds[i] == entityId)
                            return true;
                    }
                }
            }

            var playerFaction = snap?.Strategic?.PlayerFactionId;
            if (string.IsNullOrEmpty(playerFaction) || snap.Strategic.FormalArmies == null)
                return false;

            for (var i = 0; i < snap.Strategic.FormalArmies.Count; i++)
            {
                var army = snap.Strategic.FormalArmies[i];
                if (army == null ||
                    !string.Equals(army.FactionId, playerFaction, StringComparison.Ordinal))
                    continue;
                if (army.LeaderCharacterId == entityId)
                    return true;
                if (army.MemberCharacterIds == null)
                    continue;
                for (var m = 0; m < army.MemberCharacterIds.Count; m++)
                {
                    if (army.MemberCharacterIds[m] == entityId)
                        return true;
                }
            }

            return false;
        }

        static ModifierSnapshotDto ToModifierDto(AttributeModifier m)
        {
            var dto = new ModifierSnapshotDto
            {
                Id = m.Id.Value,
                AttributeId = (int)m.Target,
                Operation = (int)m.Operation,
                Value = m.Value,
                SourceKind = (int)m.Source.Kind,
                SourceDefinitionId = m.Source.DefinitionId.HasValue ? m.Source.DefinitionId.Value.ToString() : string.Empty,
                HasSourceEntity = m.Source.EntityId.HasValue,
                SourceEntityId = m.Source.EntityId.HasValue ? m.Source.EntityId.Value.Value : 0UL,
                HasSourceModifier = m.Source.ModifierId.HasValue,
                SourceModifierId = m.Source.ModifierId.HasValue ? m.Source.ModifierId.Value.Value : 0UL
            };
            return dto;
        }

        static void RestoreModifier(AttributesComponent attrs, ModifierSnapshotDto m)
        {
            DefinitionId? def = null;
            if (!string.IsNullOrEmpty(m.SourceDefinitionId) && DefinitionId.TryParse(m.SourceDefinitionId, out var d))
                def = d;
            EntityId? ent = m.HasSourceEntity ? new EntityId(m.SourceEntityId) : (EntityId?)null;
            ModifierId? mid = m.HasSourceModifier ? new ModifierId(m.SourceModifierId) : (ModifierId?)null;
            var source = new SourceRef((SourceKind)m.SourceKind, def, ent, mid);
            attrs.RestoreModifier(new AttributeModifier(
                new ModifierId(m.Id),
                (AttributeId)m.AttributeId,
                (ModifierOperation)m.Operation,
                m.Value,
                source));
        }

        static void InjectEntity(EntityStore store, Entity entity)
        {
            store.AddExisting(entity);
        }
    }
}
