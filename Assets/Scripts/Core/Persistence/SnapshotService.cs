using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Orders;
using XianXia.Core.Random;
using XianXia.Core.Results;
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

                snap.Entities.Add(dto);
            }

            foreach (var kv in world.ActiveActions)
            {
                var action = kv.Value;
                snap.ActiveActions.Add(new ActiveActionSnapshotDto
                {
                    Id = action.Id.Value,
                    SubjectId = action.Subject.Value,
                    SourceOrderId = action.SourceOrderId.Value,
                    Kind = action is WaitAction ? "Wait" : "ApplyModifier",
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
