using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 单场残留战场的独立 Session 状态（ENCOUNTER-SCOPED）。
    /// Character LifeState 仍读 Domain；此处只存 Battlefield 身份与 Spawn/Participant 归属。
    /// </summary>
    public sealed class LingeringBattlefieldState
    {
        public string BattlefieldId { get; set; } = string.Empty;
        public HexCoord BattleAnchorHex { get; set; }
        public string EnemyStackId { get; set; } = string.Empty;
        public string EncounterLocalMapId { get; set; } =
            StrategicEncounterCatalog.DefaultEncounterLocalMapId;
        public string EncounterLinkId { get; set; } = string.Empty;
        public bool FieldCleared { get; set; }

        public BattleParticipantSnapshot Participants { get; } = new BattleParticipantSnapshot();

        readonly List<ulong> _spawnedEntityIds = new List<ulong>(8);

        public IReadOnlyList<ulong> SpawnedEntityIds => _spawnedEntityIds;

        internal IList<ulong> MutableSpawnedEntityIds => _spawnedEntityIds;

        public void TrackSpawn(ulong entityId)
        {
            if (entityId != 0 && !_spawnedEntityIds.Contains(entityId))
                _spawnedEntityIds.Add(entityId);
        }

        public void RemoveTrackedSpawnAt(int index)
        {
            if (index >= 0 && index < _spawnedEntityIds.Count)
                _spawnedEntityIds.RemoveAt(index);
        }

        public void ClearTrackedIds() => _spawnedEntityIds.Clear();

        public bool ContainsSpawn(ulong entityId) =>
            entityId != 0 && _spawnedEntityIds.Contains(entityId);
    }
}
