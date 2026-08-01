using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Labor;
using XianXia.Core.Orders;
using XianXia.Core.Random;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;

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

        public Result<string> CaptureJson(SimulationWorld world, SimulationLoop loop)
        {
            var snap = Capture(world, loop);
            return _serializer.Serialize(snap);
        }

        public WorldSnapshot Capture(SimulationWorld world, SimulationLoop loop)
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
                NextModifierId = 1
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
                    dto.Lifecycle = (int)life.State;

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
                    dto.CultivationProgress = cultivation.Progress;
                    dto.BreakthroughProgressRequired = cultivation.BreakthroughProgressRequired;
                    dto.CultivationSpeed = cultivation.CultivationSpeed;
                    dto.LearnedManualId = cultivation.LearnedManualId.HasValue
                        ? cultivation.LearnedManualId.Value.ToString()
                        : string.Empty;
                    dto.RequiredRealmName = cultivation.RequiredRealmName ?? string.Empty;
                }

                if (entity.TryGet<DailyTaskComponent>(out var daily))
                {
                    dto.HasDailyTask = true;
                    dto.LaborProgress = daily.LaborProgress;
                    dto.LaborQuota = daily.LaborQuota;
                }

                if (entity.TryGet<ScheduleComponent>(out var schedule))
                {
                    dto.HasSchedule = true;
                    dto.ScheduleDefinitionId = schedule.DefinitionId ?? string.Empty;
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

            foreach (var kv in world.ActiveActions)
            {
                var action = kv.Value;
                string kind;
                if (action is WaitAction) kind = "Wait";
                else if (action is CultivateAction) kind = "Cultivate";
                else if (action is LaborAction) kind = "Labor";
                else if (action is RestAction) kind = "Rest";
                else kind = "ApplyModifier";

                snap.ActiveActions.Add(new ActiveActionSnapshotDto
                {
                    Id = action.Id.Value,
                    SubjectId = action.Subject.Value,
                    SourceOrderId = action.SourceOrderId.Value,
                    Kind = kind,
                    Status = (int)action.Status,
                    TotalTicks = action.Clock.TotalDurationTicks,
                    RemainingTicks = action.Clock.RemainingTicks
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
                        WaitTicks = order.WaitTicks
                    });
                }
            }

            return snap;
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
            if (snap.SchemaVersion != WorldSnapshot.CurrentSchemaVersion)
                return Result.Fail<(SimulationWorld, SimulationLoop)>(ErrorCode.SnapshotVersionMismatch, "Unsupported snapshot schema.", snap.SchemaVersion.ToString());

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
                entity.AddComponent(new LifecycleComponent((LifecycleState)e.Lifecycle));
                var actionState = new ActionStateComponent
                {
                    ActiveActionId = new ActionId(e.ActiveActionId)
                };
                if (e.HasActiveClock)
                    actionState.ActiveClock = new ActionClock(e.ActiveTotalTicks, e.ActiveRemainingTicks);
                entity.AddComponent(actionState);

                if (e.HasCultivation)
                {
                    var cultivation = new CultivationComponent
                    {
                        Realm = (RealmStage)e.Realm,
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

                    entity.AddComponent(cultivation);
                }
                else
                {
                    entity.AddComponent(new CultivationComponent());
                }

                if (e.HasDailyTask)
                {
                    entity.AddComponent(new DailyTaskComponent
                    {
                        LaborProgress = e.LaborProgress,
                        LaborQuota = e.LaborQuota > 0 ? e.LaborQuota : 10
                    });
                }
                else
                {
                    entity.AddComponent(new DailyTaskComponent());
                }

                if (e.HasSchedule && !string.IsNullOrEmpty(e.ScheduleDefinitionId))
                    entity.AddComponent(new ScheduleComponent(e.ScheduleDefinitionId));

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
            }

            foreach (var group in snap.Orders.GroupBy(o => o.SubjectId))
            {
                var queue = world.GetOrCreateOrderQueue(new EntityId(group.Key));
                foreach (var o in group)
                {
                    queue.Enqueue(new Order(
                        new OrderId(o.Id),
                        new EntityId(o.SubjectId),
                        (OrderType)o.Type,
                        (OrderSource)o.Source,
                        waitTicks: o.WaitTicks));
                }
            }

            var loop = new SimulationLoop(world);
            loop.RestoreNextOrderId(snap.NextOrderId);
            return Result.Ok((world, loop));
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
