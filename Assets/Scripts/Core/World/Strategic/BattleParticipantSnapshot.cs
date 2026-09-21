using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.World.Strategic
{
    public enum BattleParticipantKind
    {
        MandatoryFriendly = 0,
        OptionalFriendly = 1,
        EnemyPrimary = 2,
        EnemyReinforcement = 3
    }

    public sealed class BattleParticipantRecord
    {
        public BattleParticipantKind Kind { get; set; }
        public EntityId EntityId { get; set; }
        public string SquadId { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public int CombatPower { get; set; }
        public bool Selected { get; set; }
        /// <summary>Phase 4 Debug：写入 Participants 的原因。</summary>
        public string IncludedReason { get; set; } = string.Empty;
    }

    public enum ActualBattleParticipantSide
    {
        Friendly = 0,
        Enemy = 1
    }

    /// <summary>
    /// One deduplicated combatant that actually entered the battle. This is derived from the
    /// frozen offer snapshot in record order; optional friendlies only enter when selected.
    /// </summary>
    public readonly struct ActualBattleParticipant
    {
        public ActualBattleParticipant(BattleParticipantRecord record, ActualBattleParticipantSide side)
        {
            Record = record;
            EntityId = record != null ? record.EntityId : XianXia.Core.Domain.Ids.EntityId.None;
            Side = side;
        }

        public BattleParticipantRecord Record { get; }
        public EntityId EntityId { get; }
        public ActualBattleParticipantSide Side { get; }
        public bool IsFriendly => Side == ActualBattleParticipantSide.Friendly;
        public bool IsEnemy => Side == ActualBattleParticipantSide.Enemy;
    }

    /// <summary>Single membership predicate for presentation, targeting, victory and reports.</summary>
    public static class ActualBattleParticipantQuery
    {
        public static bool IsActual(BattleParticipantRecord record)
        {
            if (record == null || record.EntityId.IsNone)
                return false;
            return record.Kind == BattleParticipantKind.MandatoryFriendly ||
                   (record.Kind == BattleParticipantKind.OptionalFriendly && record.Selected) ||
                   record.Kind == BattleParticipantKind.EnemyPrimary ||
                   record.Kind == BattleParticipantKind.EnemyReinforcement;
        }

        public static bool TryGetSide(BattleParticipantRecord record, out ActualBattleParticipantSide side)
        {
            side = ActualBattleParticipantSide.Friendly;
            if (!IsActual(record))
                return false;
            if (record.Kind == BattleParticipantKind.EnemyPrimary ||
                record.Kind == BattleParticipantKind.EnemyReinforcement)
                side = ActualBattleParticipantSide.Enemy;
            return true;
        }

        public static List<ActualBattleParticipant> Collect(BattleParticipantSnapshot snapshot)
        {
            var result = new List<ActualBattleParticipant>(snapshot?.Records.Count ?? 0);
            if (snapshot == null)
                return result;
            var seen = new HashSet<ulong>();
            for (var i = 0; i < snapshot.Records.Count; i++)
            {
                var record = snapshot.Records[i];
                if (!TryGetSide(record, out var side) || !seen.Add(record.EntityId.Value))
                    continue;
                result.Add(new ActualBattleParticipant(record, side));
            }
            return result;
        }

        public static bool TryFind(
            BattleParticipantSnapshot snapshot,
            EntityId id,
            out ActualBattleParticipant participant)
        {
            participant = default;
            if (snapshot == null || id.IsNone)
                return false;
            var seen = new HashSet<ulong>();
            for (var i = 0; i < snapshot.Records.Count; i++)
            {
                var record = snapshot.Records[i];
                if (!TryGetSide(record, out var side) || !seen.Add(record.EntityId.Value))
                    continue;
                if (record.EntityId != id)
                    continue;
                participant = new ActualBattleParticipant(record, side);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Frozen modern battle participant input. Legacy pending-engagement fields live only in
    /// snapshot DTOs and are consumed by the one-way migration adapter.
    /// </summary>
    public sealed class BattleParticipantSnapshot
    {
        public string OfferId { get; set; } = string.Empty;
        readonly List<BattleParticipantRecord> _records = new List<BattleParticipantRecord>(16);

        public IReadOnlyList<BattleParticipantRecord> Records => _records;

        public void Clear()
        {
            OfferId = string.Empty;
            _records.Clear();
        }

        public void Add(BattleParticipantRecord record)
        {
            if (record == null)
                return;
            _records.Add(record);
        }

        public List<EntityId> CollectSelectedFriendly()
        {
            var actual = ActualBattleParticipantQuery.Collect(this);
            var list = new List<EntityId>(actual.Count);
            for (var i = 0; i < actual.Count; i++)
                if (actual[i].IsFriendly)
                    list.Add(actual[i].EntityId);

            return list;
        }

        public void CollectEnemyEntityIds(List<EntityId> into)
        {
            into?.Clear();
            if (into == null)
                return;
            var actual = ActualBattleParticipantQuery.Collect(this);
            for (var i = 0; i < actual.Count; i++)
                if (actual[i].IsEnemy)
                    into.Add(actual[i].EntityId);
        }

        public void RemoveFriendlyRecords()
        {
            for (var i = _records.Count - 1; i >= 0; i--)
            {
                var kind = _records[i].Kind;
                if (kind == BattleParticipantKind.MandatoryFriendly ||
                    kind == BattleParticipantKind.OptionalFriendly)
                    _records.RemoveAt(i);
            }
        }

        public BattleParticipantRecord FindByEntity(EntityId id)
        {
            if (id.IsNone)
                return null;
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].EntityId == id)
                    return _records[i];
            }

            return null;
        }

        /// <summary>
        /// 纯 membership：该 EntityId 是否本场 Enemy participant（EnemyPrimary / EnemyReinforcement）。
        /// 只认 frozen snapshot，不查询 CombatLifeState —— 本场是否仍有可战之敌由调用方结合生命状态判定。
        /// </summary>
        public bool IsEnemyParticipant(EntityId id)
        {
            return ActualBattleParticipantQuery.TryFind(this, id, out var participant) &&
                   participant.IsEnemy;
        }

        /// <summary>
        /// 纯 membership：该 EntityId 是否本场 selected Friendly participant
        /// （MandatoryFriendly / OptionalFriendly 且 Selected）。
        /// </summary>
        public bool IsSelectedFriendlyParticipant(EntityId id)
        {
            return ActualBattleParticipantQuery.TryFind(this, id, out var participant) &&
                   participant.IsFriendly;
        }

    }
}
