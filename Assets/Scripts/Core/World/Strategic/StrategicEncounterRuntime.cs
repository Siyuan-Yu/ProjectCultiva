using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.World.Strategic
{
    /// <summary>手动接战 LocalMap 刷敌会话（不入 Snapshot）。</summary>
    public sealed class StrategicEncounterRuntime
    {
        public bool SpawnOnNextMapLoad { get; set; }
        public string ArmyStackId { get; set; } = string.Empty;
        public string EncounterLinkId { get; set; } = string.Empty;
        public string PursueStackId { get; set; } = string.Empty;
        public int FallbackMemberCount { get; set; } = StrategicEncounterCatalog.DefaultFallbackMemberCount;
        public int FallbackCombatPowerPerMember { get; set; } = StrategicEncounterCatalog.DefaultFallbackCombatPower;
        readonly List<ulong> _spawnedEntityIds = new List<ulong>(8);
        readonly List<ulong> _engagedPartyIds = new List<ulong>(8);
        readonly List<ulong> _pursuePartyIds = new List<ulong>(8);

        public IReadOnlyList<ulong> SpawnedEntityIds => _spawnedEntityIds;
        public IReadOnlyList<ulong> EngagedPartyIds => _engagedPartyIds;
        public IReadOnlyList<ulong> PursuePartyIds => _pursuePartyIds;

        public bool HasEngagedParty => _engagedPartyIds.Count > 0;
        public bool HasPursueParty => _pursuePartyIds.Count > 0;

        public bool IsEngaged(EntityId id) =>
            !id.IsNone && _engagedPartyIds.Contains(id.Value);

        public void SetEngagedParty(IReadOnlyList<EntityId> party)
        {
            _engagedPartyIds.Clear();
            if (party == null)
                return;
            for (var i = 0; i < party.Count; i++)
                AddEngagedPartyMember(party[i]);
        }

        public void AddEngagedPartyMember(EntityId id)
        {
            if (id.IsNone || _engagedPartyIds.Contains(id.Value))
                return;
            _engagedPartyIds.Add(id.Value);
        }

        public void ClearEngagedParty() => _engagedPartyIds.Clear();

        public void SetPursueParty(IReadOnlyList<EntityId> party)
        {
            _pursuePartyIds.Clear();
            if (party == null)
                return;
            for (var i = 0; i < party.Count; i++)
            {
                if (party[i].IsNone || _pursuePartyIds.Contains(party[i].Value))
                    continue;
                _pursuePartyIds.Add(party[i].Value);
            }
        }

        public void ClearPursueParty() => _pursuePartyIds.Clear();

        public void TrackSpawn(ulong entityId)
        {
            if (entityId != 0 && !_spawnedEntityIds.Contains(entityId))
                _spawnedEntityIds.Add(entityId);
        }

        public void ResetSpawnPlan()
        {
            SpawnOnNextMapLoad = false;
            ArmyStackId = string.Empty;
            EncounterLinkId = string.Empty;
            FallbackMemberCount = StrategicEncounterCatalog.DefaultFallbackMemberCount;
            FallbackCombatPowerPerMember = StrategicEncounterCatalog.DefaultFallbackCombatPower;
        }

        public void ClearTrackedIds() => _spawnedEntityIds.Clear();

        public void RemoveTrackedSpawnAt(int index)
        {
            if (index >= 0 && index < _spawnedEntityIds.Count)
                _spawnedEntityIds.RemoveAt(index);
        }

        public void ClearPursuit()
        {
            PursueStackId = string.Empty;
            ClearPursueParty();
        }
    }
}
