using System;
using System.Collections.Generic;
using XianXia.Core.Bootstrap;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;

namespace XianXia.Core.Opportunity
{
    public static class WorldOpportunityDiscoveryMode
    {
        public const string WorldVisible = "worldVisible";
        public const string PublicNotice = "publicNotice";
        public const string HiddenUntilDiscovered = "hiddenUntilDiscovered";
    }

    public static class WorldOpportunitySpawnKind
    {
        public const string Npc = "npc";
        public const string WorldObject = "worldObject";
    }

    public sealed class WorldOpportunityDirectorSpec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public int TargetActiveMin { get; set; }
        public int TargetActiveMax { get; set; }
    }

    public sealed class WorldOpportunityNpcCandidate
    {
        public CharacterSpawnRequest Spawn { get; set; }
        public int Weight { get; set; } = 1;
    }

    public sealed class WorldOpportunitySpec
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
        public int MaxActive { get; set; } = 1;
        public string SpawnKind { get; set; } = WorldOpportunitySpawnKind.Npc;
        public string SpawnTableId { get; set; } = string.Empty;
        public string WorldObjectKind { get; set; } = string.Empty;
        public string WorldObjectLabel { get; set; } = string.Empty;
        public float WorldObjectWorldWidth { get; set; } = 1f;
        public float WorldObjectWorldHeight { get; set; } = 1f;
        public int DurationDays { get; set; } = 1;
        public float MinPlayerDistanceWorld { get; set; }
        public float MaxPlayerDistanceWorld { get; set; }
        public bool AllowInsideWorldSite { get; set; }
        public string DiscoveryMode { get; set; } = WorldOpportunityDiscoveryMode.WorldVisible;
        public string PublicNoticeText { get; set; } = string.Empty;
        public string PublicNoticeTitle { get; set; } = string.Empty;
        public bool PublicNoticeRevealExactLocation { get; set; }
        public float DiscoveryRadiusWorld { get; set; }
        public string DiscoveryNoticeTitle { get; set; } = string.Empty;
        public string DiscoveryNoticeText { get; set; } = string.Empty;
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> ExpireOutcomes { get; } = new List<ContentOutcome>();
        public List<WorldOpportunityNpcCandidate> NpcCandidates { get; } = new List<WorldOpportunityNpcCandidate>();
    }

    public sealed class WorldOpportunityInstance
    {
        public string InstanceId { get; set; } = string.Empty;
        public string OpportunityDefinitionId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public string SpawnKind { get; set; } = WorldOpportunitySpawnKind.Npc;
        public EntityId SpawnedEntityId { get; set; } = EntityId.None;
        public string WorldObjectInstanceId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public ulong CreatedDayIndex { get; set; }
        public ulong ExpireDayIndexExclusive { get; set; }
        public string DiscoveryMode { get; set; } = string.Empty;
        public bool IsDiscovered { get; set; }
    }

    public sealed class WorldOpportunityBoard
    {
        readonly Dictionary<string, WorldOpportunityDirectorSpec> _directors =
            new Dictionary<string, WorldOpportunityDirectorSpec>(StringComparer.Ordinal);
        readonly Dictionary<string, WorldOpportunitySpec> _specs =
            new Dictionary<string, WorldOpportunitySpec>(StringComparer.Ordinal);
        readonly Dictionary<string, WorldOpportunityInstance> _instances =
            new Dictionary<string, WorldOpportunityInstance>(StringComparer.Ordinal);
        readonly Dictionary<EntityId, string> _instanceByEntity =
            new Dictionary<EntityId, string>();
        readonly Dictionary<string, string> _instanceByWorldObject =
            new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, ulong> _lastRefreshDayBySurface =
            new Dictionary<string, ulong>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, WorldOpportunityDirectorSpec> Directors => _directors;
        public IReadOnlyDictionary<string, WorldOpportunitySpec> Specs => _specs;
        public IReadOnlyDictionary<string, WorldOpportunityInstance> ActiveInstances => _instances;
        public IReadOnlyDictionary<string, ulong> SurfaceRefreshStates => _lastRefreshDayBySurface;
        public ulong NextInstanceSequence { get; private set; } = 1;

        public void ClearDefinitions()
        {
            _directors.Clear();
            _specs.Clear();
        }

        public bool RegisterDirector(WorldOpportunityDirectorSpec spec)
        {
            if (spec == null || string.IsNullOrWhiteSpace(spec.Id) || string.IsNullOrWhiteSpace(spec.SurfaceId))
                return false;
            foreach (var existing in _directors.Values)
                if (string.Equals(existing.SurfaceId, spec.SurfaceId, StringComparison.Ordinal) &&
                    !string.Equals(existing.Id, spec.Id, StringComparison.Ordinal))
                    return false;
            _directors[spec.Id] = spec;
            return true;
        }

        public bool RegisterSpec(WorldOpportunitySpec spec)
        {
            if (spec == null || string.IsNullOrWhiteSpace(spec.Id) || string.IsNullOrWhiteSpace(spec.SurfaceId))
                return false;
            _specs[spec.Id] = spec;
            return true;
        }

        public bool TryGetDirectorForSurface(string surfaceId, out WorldOpportunityDirectorSpec director)
        {
            foreach (var candidate in _directors.Values)
                if (string.Equals(candidate.SurfaceId, surfaceId, StringComparison.Ordinal))
                {
                    director = candidate;
                    return true;
                }
            director = null;
            return false;
        }

        public bool TryGetSpec(string id, out WorldOpportunitySpec spec) =>
            _specs.TryGetValue(id ?? string.Empty, out spec);

        public bool TryGetByEntity(EntityId entityId, out WorldOpportunityInstance instance)
        {
            instance = null;
            return !entityId.IsNone &&
                   _instanceByEntity.TryGetValue(entityId, out var instanceId) &&
                   _instances.TryGetValue(instanceId, out instance);
        }

        public bool TryGetByWorldObject(string worldObjectInstanceId, out WorldOpportunityInstance instance)
        {
            instance = null;
            return !string.IsNullOrWhiteSpace(worldObjectInstanceId) &&
                   _instanceByWorldObject.TryGetValue(worldObjectInstanceId, out var instanceId) &&
                   _instances.TryGetValue(instanceId, out instance);
        }

        public bool TryGetInstance(string instanceId, out WorldOpportunityInstance instance) =>
            _instances.TryGetValue(instanceId ?? string.Empty, out instance);

        public int CountActive(string definitionId)
        {
            var count = 0;
            foreach (var instance in _instances.Values)
                if (string.Equals(instance.OpportunityDefinitionId, definitionId, StringComparison.Ordinal)) count++;
            return count;
        }

        public int CountActiveOnSurface(string surfaceId)
        {
            var count = 0;
            foreach (var instance in _instances.Values)
                if (string.Equals(instance.SurfaceId, surfaceId, StringComparison.Ordinal)) count++;
            return count;
        }

        public string AllocateInstanceId() => "opportunity:" + NextInstanceSequence++;

        public static string WorldObjectIdFor(string opportunityInstanceId) =>
            string.IsNullOrWhiteSpace(opportunityInstanceId)
                ? string.Empty
                : "worldObject:" + opportunityInstanceId;

        public bool AddInstance(WorldOpportunityInstance instance)
        {
            if (instance == null || string.IsNullOrWhiteSpace(instance.InstanceId) ||
                _instances.ContainsKey(instance.InstanceId))
                return false;
            var npc = string.Equals(instance.SpawnKind, WorldOpportunitySpawnKind.Npc, StringComparison.Ordinal);
            var worldObject = string.Equals(instance.SpawnKind, WorldOpportunitySpawnKind.WorldObject, StringComparison.Ordinal);
            if ((!npc && !worldObject) ||
                (npc && (instance.SpawnedEntityId.IsNone || !string.IsNullOrEmpty(instance.WorldObjectInstanceId))) ||
                (worldObject && (!instance.SpawnedEntityId.IsNone || string.IsNullOrWhiteSpace(instance.WorldObjectInstanceId))) ||
                (npc && _instanceByEntity.ContainsKey(instance.SpawnedEntityId)) ||
                (worldObject && _instanceByWorldObject.ContainsKey(instance.WorldObjectInstanceId))) return false;
            _instances.Add(instance.InstanceId, instance);
            if (npc) _instanceByEntity.Add(instance.SpawnedEntityId, instance.InstanceId);
            else _instanceByWorldObject.Add(instance.WorldObjectInstanceId, instance.InstanceId);
            return true;
        }

        public bool RemoveInstance(string instanceId)
        {
            if (!_instances.TryGetValue(instanceId ?? string.Empty, out var instance)) return false;
            _instances.Remove(instance.InstanceId);
            if (!instance.SpawnedEntityId.IsNone) _instanceByEntity.Remove(instance.SpawnedEntityId);
            if (!string.IsNullOrEmpty(instance.WorldObjectInstanceId))
                _instanceByWorldObject.Remove(instance.WorldObjectInstanceId);
            return true;
        }

        public sealed class RuntimeState
        {
            internal readonly List<WorldOpportunityInstance> Instances = new List<WorldOpportunityInstance>();
            internal readonly Dictionary<string, ulong> Refresh = new Dictionary<string, ulong>(StringComparer.Ordinal);
            internal ulong Next;
        }

        public RuntimeState CaptureRuntimeState()
        {
            var state = new RuntimeState { Next = NextInstanceSequence };
            foreach (var value in _instances.Values) state.Instances.Add(Clone(value));
            foreach (var pair in _lastRefreshDayBySurface) state.Refresh.Add(pair.Key, pair.Value);
            return state;
        }

        public void RestoreRuntimeState(RuntimeState state)
        {
            _instances.Clear(); _instanceByEntity.Clear(); _instanceByWorldObject.Clear();
            _lastRefreshDayBySurface.Clear();
            if (state == null) { NextInstanceSequence = 1; return; }
            NextInstanceSequence = state.Next;
            foreach (var instance in state.Instances) AddInstance(Clone(instance));
            foreach (var pair in state.Refresh) _lastRefreshDayBySurface.Add(pair.Key, pair.Value);
        }

        static WorldOpportunityInstance Clone(WorldOpportunityInstance value) => new WorldOpportunityInstance
        {
            InstanceId = value.InstanceId, OpportunityDefinitionId = value.OpportunityDefinitionId,
            SurfaceId = value.SurfaceId, SpawnKind = value.SpawnKind, SpawnedEntityId = value.SpawnedEntityId,
            WorldObjectInstanceId = value.WorldObjectInstanceId, WorldX = value.WorldX, WorldY = value.WorldY,
            CreatedDayIndex = value.CreatedDayIndex, ExpireDayIndexExclusive = value.ExpireDayIndexExclusive,
            DiscoveryMode = value.DiscoveryMode, IsDiscovered = value.IsDiscovered
        };

        public bool WasRefreshedToday(string surfaceId, ulong dayIndex) =>
            _lastRefreshDayBySurface.TryGetValue(surfaceId ?? string.Empty, out var day) && day == dayIndex;

        public void MarkRefreshed(string surfaceId, ulong dayIndex) =>
            _lastRefreshDayBySurface[surfaceId ?? string.Empty] = dayIndex;

        public void RestoreSequence(ulong next) => NextInstanceSequence = next == 0 ? 1UL : next;

        public bool RestoreRefreshState(string surfaceId, ulong dayIndex) =>
            !string.IsNullOrWhiteSpace(surfaceId) && !_lastRefreshDayBySurface.ContainsKey(surfaceId) &&
            (_lastRefreshDayBySurface[surfaceId] = dayIndex) == dayIndex;

        public Result ValidateRuntimeDefinitions(XianXia.Core.Simulation.SimulationWorld world = null)
        {
            foreach (var instance in _instances.Values)
                if (!_specs.TryGetValue(instance.OpportunityDefinitionId, out var spec) ||
                    !string.Equals(spec.SpawnKind, instance.SpawnKind, StringComparison.Ordinal) ||
                    !string.Equals(spec.SurfaceId, instance.SurfaceId, StringComparison.Ordinal) ||
                    (world != null && !world.SurfaceGround.TryGet(instance.SurfaceId, out _)))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Restored WorldOpportunity definition/spawnKind mismatch.", instance.OpportunityDefinitionId);
            return Result.Success();
        }
    }
}
