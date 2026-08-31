using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>手动接战 LocalMap 刷敌会话（不入 Snapshot）。</summary>
    public sealed class StrategicEncounterRuntime
    {
        public bool SpawnOnNextMapLoad { get; set; }
        /// <summary>场上敌军已清空：无结算弹窗；参战者可宏观下令，画面仍可留在 Encounter LocalMap。</summary>
        public bool FieldCleared { get; set; }
        /// <summary>结束 Modal 后仍保留战场（场上有弥留）。</summary>
        public bool BattlefieldLingering { get; set; }
        public string ActiveBattlefieldId { get; set; } = string.Empty;
        /// <summary>接战 Offer 确认进 LocalMap 时使用的 Registry BattlefieldId。</summary>
        public string PendingLingeringEnterBattlefieldId { get; set; } = string.Empty;
        /// <summary>
        /// 最近进入/结算的残留战场 Hex（Active session 用；历史见 Registry）。
        /// </summary>
        public int LingeringBattleAnchorHexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int LingeringBattleAnchorHexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        /// <summary>多场残留战场 Hex 注册表：同一 Army 可先后在 H1/H2/H3 留下独立残留。</summary>
        readonly List<HexCoord> _lingeringBattlefieldHexes = new List<HexCoord>(4);
        readonly List<LingeringBattlefieldRecord> _lingeringBattlefields = new List<LingeringBattlefieldRecord>(4);
        /// <summary>残留战场对应的 Encounter LocalMap Id。</summary>
        public string LingeringLocalMapId { get; set; } = string.Empty;
        public string ArmyStackId { get; set; } = string.Empty;
        public string EncounterLinkId { get; set; } = string.Empty;
        public string PursueStackId { get; set; } = string.Empty;
        public string PursueAttackerArmyId { get; set; } = string.Empty;
        public string PursueDefenderArmyId { get; set; } = string.Empty;
        /// <summary>Hex 移动抵达后进入敌方残留战场（非 Pursuit）。</summary>
        public string PendingLingeringAttackArmyId { get; set; } = string.Empty;
        public string PendingLingeringAttackStackId { get; set; } = string.Empty;
        public int PendingLingeringAttackHexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int PendingLingeringAttackHexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;

        public bool HasPendingLingeringAttack =>
            !string.IsNullOrEmpty(PendingLingeringAttackArmyId) &&
            PendingLingeringAttackHexQ != ArmyHexBattleAnchorService.InvalidHexComponent;
        public int FallbackMemberCount { get; set; } = StrategicEncounterCatalog.DefaultFallbackMemberCount;
        public int FallbackCombatPowerPerMember { get; set; } = StrategicEncounterCatalog.DefaultFallbackCombatPower;
        readonly List<ulong> _spawnedEntityIds = new List<ulong>(8);
        readonly List<ulong> _engagedPartyIds = new List<ulong>(8);
        readonly List<ulong> _pursuePartyIds = new List<ulong>(8);

        public IReadOnlyList<ulong> SpawnedEntityIds => _spawnedEntityIds;

        /// <summary>Active Encounter 专属 spawn 列表（Lingering 已 park 的在 Registry）。</summary>
        internal IList<ulong> MutableActiveSpawnedIds => _spawnedEntityIds;
        public IReadOnlyList<ulong> EngagedPartyIds => _engagedPartyIds;
        public IReadOnlyList<ulong> PursuePartyIds => _pursuePartyIds;
        public IReadOnlyList<HexCoord> LingeringBattlefieldHexes => _lingeringBattlefieldHexes;
        public IReadOnlyList<LingeringBattlefieldRecord> LingeringBattlefields => _lingeringBattlefields;
        public int LingeringBattlefieldHexCount => _lingeringBattlefieldHexes.Count;

        public bool HasEngagedParty => _engagedPartyIds.Count > 0;
        public bool HasPursueParty => _pursuePartyIds.Count > 0;

        public bool IsEngaged(EntityId id) =>
            !id.IsNone && _engagedPartyIds.Contains(id.Value);

        public bool IsPursue(EntityId id) =>
            !id.IsNone && _pursuePartyIds.Contains(id.Value);

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

        public void RemoveEngagedPartyMember(EntityId id)
        {
            if (id.IsNone)
                return;
            _engagedPartyIds.Remove(id.Value);
        }

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
            FieldCleared = false;
            // 仅重置当前遭遇刷怪计划；多场 Lingering Hex 注册表独立保留
            LingeringLocalMapId = string.Empty;
            ArmyStackId = string.Empty;
            EncounterLinkId = string.Empty;
            FallbackMemberCount = StrategicEncounterCatalog.DefaultFallbackMemberCount;
            FallbackCombatPowerPerMember = StrategicEncounterCatalog.DefaultFallbackCombatPower;
        }

        public bool HasLingeringBattleAnchorHex =>
            LingeringBattleAnchorHexQ != ArmyHexBattleAnchorService.InvalidHexComponent &&
            LingeringBattleAnchorHexR != ArmyHexBattleAnchorService.InvalidHexComponent;

        public void SetLingeringBattleAnchorHex(HexCoord hex)
        {
            LingeringBattleAnchorHexQ = hex.Q;
            LingeringBattleAnchorHexR = hex.R;
            RegisterLingeringBattlefieldHex(hex);
        }

        public bool TryGetLingeringBattleAnchorHex(out HexCoord hex)
        {
            hex = default;
            if (!HasLingeringBattleAnchorHex)
                return false;
            hex = new HexCoord(LingeringBattleAnchorHexQ, LingeringBattleAnchorHexR);
            return true;
        }

        public void ClearLingeringBattleAnchorHex()
        {
            LingeringBattleAnchorHexQ = ArmyHexBattleAnchorService.InvalidHexComponent;
            LingeringBattleAnchorHexR = ArmyHexBattleAnchorService.InvalidHexComponent;
        }

        public void RegisterLingeringBattlefield(HexCoord hex, string enemyStackId)
        {
            RegisterLingeringBattlefieldHex(hex);
            if (string.IsNullOrEmpty(enemyStackId))
                return;

            for (var i = 0; i < _lingeringBattlefields.Count; i++)
            {
                if (!_lingeringBattlefields[i].Hex.Equals(hex))
                    continue;
                _lingeringBattlefields[i].EnemyStackId = enemyStackId;
                return;
            }

            _lingeringBattlefields.Add(new LingeringBattlefieldRecord
            {
                Hex = hex,
                EnemyStackId = enemyStackId
            });
        }

        public bool TryGetLingeringBattlefieldAtHex(HexCoord hex, out LingeringBattlefieldRecord record)
        {
            record = null;
            for (var i = 0; i < _lingeringBattlefields.Count; i++)
            {
                var candidate = _lingeringBattlefields[i];
                if (candidate == null || !candidate.Hex.Equals(hex))
                    continue;
                record = candidate;
                return true;
            }

            return false;
        }

        public void RegisterLingeringBattlefieldHex(HexCoord hex)
        {
            for (var i = 0; i < _lingeringBattlefieldHexes.Count; i++)
            {
                if (_lingeringBattlefieldHexes[i].Equals(hex))
                    return;
            }

            _lingeringBattlefieldHexes.Add(hex);
        }

        public bool HasLingeringBattlefieldAtHex(HexCoord hex)
        {
            if (HasLingeringBattleAnchorHex &&
                LingeringBattleAnchorHexQ == hex.Q &&
                LingeringBattleAnchorHexR == hex.R)
                return true;
            for (var i = 0; i < _lingeringBattlefieldHexes.Count; i++)
            {
                if (_lingeringBattlefieldHexes[i].Equals(hex))
                    return true;
            }

            return false;
        }

        public void ClearAllLingeringBattlefieldHexes() => _lingeringBattlefieldHexes.Clear();

        public void ClearAllLingeringBattlefields() => _lingeringBattlefields.Clear();

        public void ClearTrackedIds() => _spawnedEntityIds.Clear();

        /// <summary>清空 Active 遭遇会话；不触碰 Registry 内已 park 的 battlefield。</summary>
        public void ClearActiveEncounterSession()
        {
            ActiveBattlefieldId = string.Empty;
            ClearTrackedIds();
            ClearEngagedParty();
            SpawnOnNextMapLoad = false;
        }

        /// <summary>
        /// 真实 WORLD_COMBAT 结束后完整清空本场 active battle transient。
        /// 不清 LingeringBattlefields Registry / StrategicResidualPresence / 历史 Hex residual /
        /// Pursuit（由 StrategicPursuitService 原流程处理）。BattlefieldLingering 保持不动：
        /// legacy Explicit/Auto compatibility 仍可能依赖 Registry；新 WORLD_COMBAT 不写不读它。
        /// </summary>
        public void ClearCompletedWorldCombatSession()
        {
            ActiveBattlefieldId = string.Empty;
            ClearTrackedIds();
            ClearEngagedParty();
            SpawnOnNextMapLoad = false;
            FieldCleared = false;
            ArmyStackId = string.Empty;
            EncounterLinkId = string.Empty;
            LingeringLocalMapId = string.Empty;
            PendingLingeringEnterBattlefieldId = string.Empty;
        }

        public void RemoveTrackedSpawnAt(int index)
        {
            if (index >= 0 && index < _spawnedEntityIds.Count)
                _spawnedEntityIds.RemoveAt(index);
        }

        public void ClearPursuit()
        {
            PursueStackId = string.Empty;
            PursueAttackerArmyId = string.Empty;
            PursueDefenderArmyId = string.Empty;
            ClearPursueParty();
        }

        public void ClearPendingLingeringAttack()
        {
            PendingLingeringAttackArmyId = string.Empty;
            PendingLingeringAttackStackId = string.Empty;
            PendingLingeringAttackHexQ = ArmyHexBattleAnchorService.InvalidHexComponent;
            PendingLingeringAttackHexR = ArmyHexBattleAnchorService.InvalidHexComponent;
        }
    }
}
