using System;
using System.Collections.Generic;
using XianXia.Core.Bootstrap;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Opportunity
{
    public static class WorldOpportunityDiscoveryMode
    {
        public const string WorldVisible = "worldVisible";
        public const string PublicNotice = "publicNotice";
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
        public string SpawnTableId { get; set; } = string.Empty;
        public int DurationDays { get; set; } = 1;
        public float MinPlayerDistanceWorld { get; set; }
        public float MaxPlayerDistanceWorld { get; set; }
        public bool AllowInsideWorldSite { get; set; }
        public string DiscoveryMode { get; set; } = WorldOpportunityDiscoveryMode.WorldVisible;
        public string PublicNoticeText { get; set; } = string.Empty;
        public string PublicNoticeTitle { get; set; } = string.Empty;
        public bool PublicNoticeRevealExactLocation { get; set; }
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> ExpireOutcomes { get; } = new List<ContentOutcome>();
        public List<WorldOpportunityNpcCandidate> NpcCandidates { get; } = new List<WorldOpportunityNpcCandidate>();
    }

    public sealed class WorldOpportunityInstance
    {
        public string InstanceId { get; set; } = string.Empty;
        public string OpportunityDefinitionId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public EntityId SpawnedEntityId { get; set; } = EntityId.None;
        public ulong CreatedDayIndex { get; set; }
        public ulong ExpireDayIndexExclusive { get; set; }
        public string DiscoveryMode { get; set; } = string.Empty;
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

        public bool AddInstance(WorldOpportunityInstance instance)
        {
            if (instance == null || string.IsNullOrWhiteSpace(instance.InstanceId) || instance.SpawnedEntityId.IsNone ||
                _instances.ContainsKey(instance.InstanceId) || _instanceByEntity.ContainsKey(instance.SpawnedEntityId))
                return false;
            _instances.Add(instance.InstanceId, instance);
            _instanceByEntity.Add(instance.SpawnedEntityId, instance.InstanceId);
            return true;
        }

        public bool RemoveInstance(string instanceId)
        {
            if (!_instances.TryGetValue(instanceId ?? string.Empty, out var instance)) return false;
            _instances.Remove(instance.InstanceId);
            _instanceByEntity.Remove(instance.SpawnedEntityId);
            return true;
        }

        public bool WasRefreshedToday(string surfaceId, ulong dayIndex) =>
            _lastRefreshDayBySurface.TryGetValue(surfaceId ?? string.Empty, out var day) && day == dayIndex;

        public void MarkRefreshed(string surfaceId, ulong dayIndex) =>
            _lastRefreshDayBySurface[surfaceId ?? string.Empty] = dayIndex;

        public void RestoreSequence(ulong next) => NextInstanceSequence = next == 0 ? 1UL : next;

        public bool RestoreRefreshState(string surfaceId, ulong dayIndex) =>
            !string.IsNullOrWhiteSpace(surfaceId) && !_lastRefreshDayBySurface.ContainsKey(surfaceId) &&
            (_lastRefreshDayBySurface[surfaceId] = dayIndex) == dayIndex;
    }
}
