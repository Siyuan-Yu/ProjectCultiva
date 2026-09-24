using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Concealment;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Inventory;
using XianXia.Core.Labor;
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
                }

                if (entity.TryGet<CombatVitalsComponent>(out var vitals) && vitals != null)
                {
                    dto.HasCombatVitals = true;
                    dto.CurrentHp = vitals.CurrentHp;
                    dto.CurrentSpiritPower = vitals.CurrentSpiritPower;
                    dto.VitalsPoolsInitialized = vitals.PoolsInitialized;
                }

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

                snap.Entities.Add(dto);
            }

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
            CaptureSocialBonds(world, snap);
            CaptureRelationshipLedger(world, snap);
            CaptureOutdoorStatefulObjects(world, snap);
            CaptureWorldOpportunities(world, snap);
            CaptureWorldActivities(world, snap);
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
                    BoundWorldSiteId = asset.BoundWorldSiteId });
            foreach (var kv in world.OutdoorStatefulObjects.Destructibles)
                snap.OutdoorDestructibles.Add(new OutdoorDestructibleSnapshotDto
                { StableId = kv.Key, CurrentHp = kv.Value.Hp, Destroyed = kv.Value.Destroyed });
            foreach (var kv in world.OutdoorStatefulObjects.FarmPlots)
                snap.OutdoorFarmPlots.Add(new OutdoorFarmPlotSnapshotDto
                { StableCellId = kv.Key, CropId = kv.Value.CropId, CropStage = kv.Value.CropStage, Growth = kv.Value.Growth });
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

        public Result<(SimulationWorld world, SimulationLoop loop)> RestoreJson(string json, string expectedPackageVersion = null)
        {
            var parsed = _serializer.Deserialize(json);
            if (parsed.IsFailure)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(parsed.Error);

            return Restore(parsed.Value, expectedPackageVersion);
        }

        public Result<(SimulationWorld world, SimulationLoop loop)> Restore(WorldSnapshot snap, string expectedPackageVersion = null)
        {
            if (snap == null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Snapshot null.");
            if (snap.CharacterEncounter != null && snap.Strategic?.PendingEngagement != null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotInvalid, "Conflicting encounter identities.");
            if (snap.SchemaVersion >= WorldSnapshot.LegacySchemaVersion &&
                snap.SchemaVersion <= WorldSnapshot.LegacySchemaVersionV10)
            {
                return Result.Fail<(SimulationWorld, SimulationLoop)>(
                    ErrorCode.SnapshotVersionMismatch,
                    "Schema v1-v10 saves lack temporary Quest companion authority. Start a new game (schema v11 required).",
                    snap.SchemaVersion.ToString());
            }

            if (snap.SchemaVersion != WorldSnapshot.CurrentSchemaVersion)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotVersionMismatch, "Unsupported snapshot schema.", snap.SchemaVersion.ToString());
            if (snap.Entities == null)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(
                    ErrorCode.SnapshotInvalid,
                    "Snapshot entity authority is missing.");
            for (var i = 0; i < snap.Entities.Count; i++)
            {
                var entity = snap.Entities[i];
                if (entity == null || !entity.EntityLocationSnapshotFieldPresent)
                    return Result.Fail<(SimulationWorld, SimulationLoop)>(
                        ErrorCode.SnapshotInvalid,
                        "Snapshot lacks current EntityLocation authority and requires offline conversion.",
                        "EntityIndex=" + i + " EntityId=" + (entity?.Id ?? 0UL));
            }

            if (!string.IsNullOrEmpty(expectedPackageVersion) &&
                !string.Equals(snap.EnabledPackageVersion, expectedPackageVersion, StringComparison.Ordinal))
            {
                return Result.Fail<(SimulationWorld, SimulationLoop)>(
                    ErrorCode.IncompatibleContentVersion,
                    "Content package version mismatch.",
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
                    BleedOutAfterTick = e.BleedOutAfterTick
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

                var faction = new FactionMembershipComponent();
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
                    BoundWorldSiteId = dto.BoundWorldSiteId };
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
