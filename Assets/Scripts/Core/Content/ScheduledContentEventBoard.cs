using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    public sealed class ScheduledContentEventInstance
    {
        public string InstanceId { get; internal set; }
        public string EventId { get; internal set; }
        public ulong ScheduledAtTick { get; internal set; }
        public ulong ExecuteTick { get; internal set; }
        public EntityId ActorEntityId { get; internal set; }
        public EntityId TargetEntityId { get; internal set; }
        public string TargetKind { get; internal set; }
        public string TargetKey { get; internal set; }
        public string TargetDefinitionId { get; internal set; }
        public string TargetDisplayName { get; internal set; }
        public EntityId IssuerEntityId { get; internal set; }
        public string OpportunityInstanceId { get; internal set; }
        internal ulong Sequence => ulong.Parse(InstanceId.Substring("scheduled-event:".Length), CultureInfo.InvariantCulture);
        public ContentInteractionContext Context() => new ContentInteractionContext
        {
            ActorId = ActorEntityId, TargetEntityId = TargetEntityId, TargetKind = TargetKind,
            TargetKey = TargetKey, TargetDefinitionId = TargetDefinitionId, TargetDisplayName = TargetDisplayName,
            IssuerEntityId = IssuerEntityId, OpportunityInstanceId = OpportunityInstanceId
        };
    }

    /// <summary>Pending instances only. Items are immutable outside Core; no EventId deduplication.</summary>
    public sealed class ScheduledContentEventBoard
    {
        readonly List<ScheduledContentEventInstance> _pending = new List<ScheduledContentEventInstance>();
        public IReadOnlyList<ScheduledContentEventInstance> Pending => _pending.AsReadOnly();
        public ulong NextInstanceSequence { get; private set; } = 1;
        public string LastDiagnostic { get; private set; } = string.Empty;

        internal Result Schedule(SimulationWorld world, EntityId actor, string eventId, int days, ContentInteractionContext context)
        {
            if (days <= 0 || string.IsNullOrWhiteSpace(eventId) || !world.ContentEvents.TryGet(eventId, out _))
                return Result.Failure(ErrorCode.InvalidArgument, "scheduleEvent requires an existing Event and positive integer days.", eventId);
            if (NextInstanceSequence == ulong.MaxValue)
                return Result.Failure(ErrorCode.InvalidOperation, "Scheduled event sequence exhausted.");
            ulong deadline;
            try { deadline = checked(world.Tick.Value + checked((ulong)days * WorldTick.TicksPerDay)); }
            catch (OverflowException) { return Result.Failure(ErrorCode.InvalidArgument, "Scheduled event deadline overflow."); }
            var c = context ?? new ContentInteractionContext { ActorId = actor };
            var source = c.OpportunityInstanceId ?? string.Empty;
            if (string.IsNullOrEmpty(source))
            {
                if (world.WorldOpportunities.TryGetByEntity(c.TargetEntityId, out var npc)) source = npc.InstanceId;
                else if (c.TargetKind == "opportunityObject" && c.TargetKey != null &&
                    c.TargetKey.StartsWith("opportunityObject:", StringComparison.Ordinal) &&
                    world.WorldOpportunities.TryGetByWorldObject(c.TargetKey.Substring("opportunityObject:".Length), out var obj))
                    source = obj.InstanceId;
            }
            var item = new ScheduledContentEventInstance
            {
                InstanceId = "scheduled-event:" + NextInstanceSequence.ToString(CultureInfo.InvariantCulture),
                EventId = eventId, ScheduledAtTick = world.Tick.Value, ExecuteTick = deadline,
                ActorEntityId = c.ActorId, TargetEntityId = c.TargetEntityId, TargetKind = c.TargetKind ?? "",
                TargetKey = c.TargetKey ?? "", TargetDefinitionId = c.TargetDefinitionId ?? "",
                TargetDisplayName = c.TargetDisplayName ?? "", IssuerEntityId = c.IssuerEntityId,
                OpportunityInstanceId = source
            };
            if (!ValidContextShape(item)) return Result.Failure(ErrorCode.InvalidArgument, "Scheduled context identity is invalid.");
            var invalid = ScheduledContentEventDispatcher.ContextInvalidReason(world, item);
            if (invalid != null) return Result.Failure(ErrorCode.InvalidOperation, invalid);
            _pending.Add(item); NextInstanceSequence++;
            return Result.Success();
        }

        public List<ScheduledContentEventInstance> OrderedPending()
        {
            var items = new List<ScheduledContentEventInstance>(_pending);
            items.Sort((a, b) => { var tick = a.ExecuteTick.CompareTo(b.ExecuteTick); return tick != 0 ? tick : a.Sequence.CompareTo(b.Sequence); });
            return items;
        }
        internal void Consume(ScheduledContentEventInstance item, string outcome)
        { _pending.Remove(item); LastDiagnostic = item.InstanceId + " " + outcome; }
        internal sealed class RuntimeState
        {
            public List<ScheduledContentEventInstance> Items;
            public ulong Next;
            public string Diagnostic;
        }
        internal RuntimeState CaptureState() => new RuntimeState
        { Items = new List<ScheduledContentEventInstance>(_pending), Next = NextInstanceSequence, Diagnostic = LastDiagnostic };
        internal void RestoreState(RuntimeState state)
        { _pending.Clear(); _pending.AddRange(state.Items); NextInstanceSequence = state.Next; LastDiagnostic = state.Diagnostic ?? ""; }
        static bool ValidContextShape(ScheduledContentEventInstance item)
        {
            switch (item.TargetKind)
            {
                case "": return item.TargetEntityId.IsNone && string.IsNullOrEmpty(item.TargetKey) && string.IsNullOrEmpty(item.OpportunityInstanceId);
                case "npc": return !item.TargetEntityId.IsNone && !string.IsNullOrWhiteSpace(item.TargetDefinitionId) &&
                    item.TargetKey == item.TargetEntityId.Value.ToString(CultureInfo.InvariantCulture);
                case "opportunityObject": return item.TargetEntityId.IsNone && !string.IsNullOrWhiteSpace(item.OpportunityInstanceId) &&
                    !string.IsNullOrWhiteSpace(item.TargetDefinitionId) &&
                    item.TargetKey == "opportunityObject:" + XianXia.Core.Opportunity.WorldOpportunityBoard.WorldObjectIdFor(item.OpportunityInstanceId);
                case "controlCore": case "factionFlag": case "farmPlot": case "destructible":
                case "housing": case "workArea": case "recoverySpot": case "storageRoom":
                    return item.TargetEntityId.IsNone && !string.IsNullOrWhiteSpace(item.TargetDefinitionId) &&
                        item.TargetKey == item.TargetKind + ":" + item.TargetDefinitionId && string.IsNullOrEmpty(item.OpportunityInstanceId);
                default: return false;
            }
        }
        internal Result Restore(IEnumerable<ScheduledContentEventInstance> items, ulong next, ulong tick)
        {
            if (items == null || next == 0) return Result.Failure(ErrorCode.SnapshotInvalid, "Scheduled event authority missing.");
            var restored = new List<ScheduledContentEventInstance>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                const string prefix = "scheduled-event:";
                if (item == null || string.IsNullOrEmpty(item.InstanceId) || !item.InstanceId.StartsWith(prefix, StringComparison.Ordinal) ||
                    !ulong.TryParse(item.InstanceId.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) ||
                    sequence == 0 || sequence >= next || item.InstanceId != prefix + sequence.ToString(CultureInfo.InvariantCulture) ||
                    !ids.Add(item.InstanceId) || string.IsNullOrWhiteSpace(item.EventId) || item.ActorEntityId.IsNone ||
                    item.ScheduledAtTick > tick || item.ExecuteTick <= item.ScheduledAtTick ||
                    (item.ExecuteTick - item.ScheduledAtTick) % WorldTick.TicksPerDay != 0 ||
                    (item.ExecuteTick - item.ScheduledAtTick) / WorldTick.TicksPerDay > int.MaxValue ||
                    !ValidContextShape(item))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid scheduled event instance/sequence/deadline.", item?.InstanceId);
                restored.Add(item);
            }
            RestoreState(new RuntimeState { Items = restored, Next = next });
            return Result.Success();
        }
    }

    /// <summary>Host supplies presentation safety; Core retains all scheduling and cancellation authority.</summary>
    public static class ScheduledContentEventDispatcher
    {
        public static void Dispatch(SimulationWorld world, bool safeToPresent)
        {
            if (world == null || !safeToPresent || world.ContentEvents.HasActive) return;
            foreach (var item in world.ScheduledContentEvents.OrderedPending())
            {
                if (item.ExecuteTick > world.Tick.Value) break;
                string invalid = !world.ContentEvents.TryGet(item.EventId, out var spec)
                    ? "Event definition missing" : ContextInvalidReason(world, item);
                if (invalid == null && !ContentConditionEvaluator.AllPass(world, item.ActorEntityId, spec.Conditions, item.Context()))
                    invalid = "Event Conditions no longer satisfied";
                if (invalid != null) { world.ScheduledContentEvents.Consume(item, "cancelled: " + invalid); continue; }
                world.ContentEvents.SetScheduledActive(item);
                world.ScheduledContentEvents.Consume(item, "presented: " + item.EventId);
                world.Events.Publish(EventType.ContentEventPresented, world.Tick, target: item.ActorEntityId,
                    payload: item.EventId + ";scheduled=" + item.InstanceId);
                return;
            }
        }

        internal static string ContextInvalidReason(SimulationWorld world, ScheduledContentEventInstance item)
        {
            if (!ValidEntity(world, item.ActorEntityId)) return "Original Actor missing/dead/removed";
            if (!item.TargetEntityId.IsNone && !ValidEntity(world, item.TargetEntityId)) return "Original Target missing/dead/removed";
            if (!item.IssuerEntityId.IsNone && !ValidEntity(world, item.IssuerEntityId)) return "Original Issuer missing/dead/removed";
            if (!item.TargetEntityId.IsNone && !string.IsNullOrEmpty(item.TargetDefinitionId) &&
                world.Entities.TryGet(item.TargetEntityId, out var target) && target.DefinitionId.ToString() != item.TargetDefinitionId)
                return "Original Target identity mismatch";
            if (item.TargetKind == "npc" && (item.TargetEntityId.IsNone || item.TargetKey != item.TargetEntityId.Value.ToString(CultureInfo.InvariantCulture)))
                return "NPC stable identity missing/mismatched";
            if (!string.IsNullOrEmpty(item.OpportunityInstanceId))
            {
                if (!world.WorldOpportunities.TryGetInstance(item.OpportunityInstanceId, out var source)) return "Original Opportunity expired/resolved/removed";
                if (source.ExpireDayIndexExclusive <= world.Tick.Value / WorldTick.TicksPerDay) return "Original Opportunity expired";
                if (item.TargetKind == "opportunityObject")
                {
                    if (source.WorldObjectInstanceId == "" || item.TargetKey != "opportunityObject:" + source.WorldObjectInstanceId ||
                        item.TargetDefinitionId != source.OpportunityDefinitionId) return "Original Opportunity object identity mismatch";
                }
                else if (item.TargetEntityId.IsNone || source.SpawnedEntityId != item.TargetEntityId)
                    return "Original Opportunity NPC identity mismatch";
            }
            else if (item.TargetKind == "opportunityObject") return "Original Opportunity binding missing";
            if (!string.IsNullOrEmpty(item.TargetKind) && item.TargetKind != "npc" && item.TargetKind != "opportunityObject")
            {
                if (string.IsNullOrEmpty(item.TargetDefinitionId) || item.TargetKey != item.TargetKind + ":" + item.TargetDefinitionId)
                    return "WorldObject stable identity missing/mismatched";
                if (item.TargetKind == "factionFlag" && !world.Strategic.FactionFlags.Flags.ContainsKey(item.TargetDefinitionId))
                    return "Original faction flag removed";
                if (item.TargetKind == "destructible" && world.OutdoorStatefulObjects.IsDestructibleDestroyed(item.TargetDefinitionId))
                    return "Original destructible destroyed";
                if (item.TargetDefinitionId.StartsWith("asset:runtime:", StringComparison.Ordinal))
                {
                    // Plot identity appends cell coordinates to the persistent placement id.
                    var cellY = item.TargetDefinitionId.LastIndexOf(':');
                    var cellX = cellY > 0 ? item.TargetDefinitionId.LastIndexOf(':', cellY - 1) : -1;
                    var assetId = cellX >= 0 ? item.TargetDefinitionId.Substring(0, cellX) : "";
                    if (!world.OutdoorConstructedAssets.Assets.ContainsKey(assetId)) return "Original constructed object removed";
                }
            }
            return null;
        }
        static bool ValidEntity(SimulationWorld world, EntityId id) => !id.IsNone && world.Entities.TryGet(id, out var entity) &&
            (!entity.TryGet<LifecycleComponent>(out var life) || life.State == LifecycleState.Alive);
    }
}
