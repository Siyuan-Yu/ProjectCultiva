using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public enum ManualBattleReportCondition
    {
        Intact = 0,
        Injured = 1,
        SeriouslyInjured = 2,
        Incapacitated = 3,
        Dead = 4,
        Captured = 5,
        Removed = 6,
        MissingOrUnavailable = 7
    }

    public sealed class ManualBattleReportParticipant
    {
        public EntityId EntityId { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public ActualBattleParticipantSide Side { get; internal set; }
        public ManualBattleReportCondition EntryCondition { get; internal set; }
        public ManualBattleReportCondition FinalCondition { get; internal set; }
        public bool EntryHpAvailable { get; internal set; }
        public int EntryHp { get; internal set; }
        public int EntryMaxHp { get; internal set; }
        public bool FinalHpAvailable { get; internal set; }
        public int FinalHp { get; internal set; }
        public int FinalMaxHp { get; internal set; }
        public bool ChangedDuringBattle => EntryCondition != FinalCondition ||
                                           (EntryHpAvailable && FinalHpAvailable && EntryHp != FinalHp);
    }

    public sealed class ManualBattleReport
    {
        readonly List<ManualBattleReportParticipant> _participants = new List<ManualBattleReportParticipant>();
        public string OfferId { get; internal set; } = string.Empty;
        public bool PlayerWon { get; internal set; }
        public string ResultReason { get; internal set; } = string.Empty;
        public IReadOnlyList<ManualBattleReportParticipant> Participants => _participants;
        internal void Add(ManualBattleReportParticipant participant) => _participants.Add(participant);
        public int CountSide(ActualBattleParticipantSide side)
        {
            var count = 0;
            for (var i = 0; i < _participants.Count; i++)
                if (_participants[i].Side == side)
                    count++;
            return count;
        }
        public int Count(ActualBattleParticipantSide side, ManualBattleReportCondition condition)
        {
            var count = 0;
            for (var i = 0; i < _participants.Count; i++)
                if (_participants[i].Side == side && _participants[i].FinalCondition == condition)
                    count++;
            return count;
        }
    }

    public sealed class ManualBattleReportDraft
    {
        internal sealed class Entry
        {
            public EntityId Id;
            public string Name = string.Empty;
            public ActualBattleParticipantSide Side;
            public ManualBattleReportCondition Condition;
            public bool HpAvailable;
            public int Hp;
            public int MaxHp;
        }

        readonly List<Entry> _entries = new List<Entry>();
        public string OfferId { get; internal set; } = string.Empty;
        internal IReadOnlyList<Entry> Entries => _entries;
        internal void Add(Entry entry) => _entries.Add(entry);
    }

    /// <summary>
    /// Runtime-only identity and immutable report inputs for one manual battle. It outlives the
    /// mutable Participants snapshot through settlement and is cleared only after report close
    /// or session replacement.
    /// </summary>
    public sealed class ManualBattleSettlementState
    {
        public bool IsInitialized { get; private set; }
        public bool IsCommitted { get; private set; }
        public string OfferId { get; private set; } = string.Empty;
        public ManualBattleReportDraft Draft { get; private set; }
        public ManualBattleReport CommittedReport { get; private set; }

        public bool Begin(
            SimulationWorld world,
            BattleParticipantSnapshot snapshot,
            string offerId,
            bool entryStateKnown)
        {
            if (world == null || snapshot == null || string.IsNullOrWhiteSpace(offerId))
                return false;
            var draft = entryStateKnown
                ? ManualBattleReportBuilder.CaptureEntry(world, snapshot, offerId)
                : ManualBattleReportBuilder.CaptureEntryUnknown(world, snapshot, offerId);
            return Begin(draft);
        }

        public bool Begin(ManualBattleReportDraft draft)
        {
            if (draft == null || draft.Entries.Count == 0 || string.IsNullOrWhiteSpace(draft.OfferId))
                return false;
            Clear();
            OfferId = draft.OfferId.Trim();
            Draft = draft;
            IsInitialized = true;
            return true;
        }

        public bool Matches(BattleParticipantSnapshot snapshot)
        {
            if (!IsInitialized || Draft == null || snapshot == null)
                return false;
            var actual = ActualBattleParticipantQuery.Collect(snapshot);
            if (actual.Count != Draft.Entries.Count)
                return false;
            for (var i = 0; i < actual.Count; i++)
                if (actual[i].EntityId != Draft.Entries[i].Id ||
                    actual[i].Side != Draft.Entries[i].Side)
                    return false;
            return true;
        }

        public bool Commit(ManualBattleReport report)
        {
            if (!IsInitialized || IsCommitted || report == null ||
                !string.Equals(report.OfferId, OfferId, StringComparison.Ordinal))
                return false;
            CommittedReport = report;
            IsCommitted = true;
            return true;
        }

        public bool ClearOwned(string offerId)
        {
            if (!IsInitialized || !string.Equals(OfferId, offerId ?? string.Empty, StringComparison.Ordinal))
                return false;
            Clear();
            return true;
        }

        public void Clear()
        {
            IsInitialized = false;
            IsCommitted = false;
            OfferId = string.Empty;
            Draft = null;
            CommittedReport = null;
        }
    }

    /// <summary>Read-only battle report snapshots. Never initializes or heals combat vitals.</summary>
    public static class ManualBattleReportBuilder
    {
        public const double SeriousInjuryHpRatio = 0.30d;

        public static ManualBattleReportDraft CaptureEntry(
            SimulationWorld world,
            BattleParticipantSnapshot snapshot) =>
            CaptureEntry(world, snapshot, snapshot?.OfferId, true);

        public static ManualBattleReportDraft CaptureEntry(
            SimulationWorld world,
            BattleParticipantSnapshot snapshot,
            string offerId) =>
            CaptureEntry(world, snapshot, offerId, true);

        public static ManualBattleReportDraft CaptureEntryUnknown(
            SimulationWorld world,
            BattleParticipantSnapshot snapshot,
            string offerId) =>
            CaptureEntry(world, snapshot, offerId, false);

        static ManualBattleReportDraft CaptureEntry(
            SimulationWorld world,
            BattleParticipantSnapshot snapshot,
            string offerId,
            bool captureEntryState)
        {
            if (world == null || snapshot == null || string.IsNullOrWhiteSpace(offerId))
                return null;
            var actual = ActualBattleParticipantQuery.Collect(snapshot);
            if (actual.Count == 0)
                return null;
            var draft = new ManualBattleReportDraft { OfferId = offerId.Trim() };
            for (var i = 0; i < actual.Count; i++)
            {
                var participant = actual[i];
                var condition = ManualBattleReportCondition.MissingOrUnavailable;
                var hpAvailable = false;
                var hp = 0;
                var maxHp = 0;
                if (captureEntryState)
                    CaptureState(world, participant.EntityId, out condition, out hpAvailable, out hp, out maxHp);
                var name = participant.EntityId.ToString();
                if (world.Entities.TryGet(participant.EntityId, out var entity) && entity != null &&
                    !string.IsNullOrWhiteSpace(entity.DisplayName))
                    name = entity.DisplayName;
                draft.Add(new ManualBattleReportDraft.Entry
                {
                    Id = participant.EntityId,
                    Name = name,
                    Side = participant.Side,
                    Condition = condition,
                    HpAvailable = hpAvailable,
                    Hp = hp,
                    MaxHp = maxHp
                });
            }
            return draft;
        }

        public static ManualBattleReport CaptureFinal(
            SimulationWorld world,
            ManualBattleReportDraft draft,
            bool playerWon,
            string reason)
        {
            if (world == null || draft == null)
                return null;
            var report = new ManualBattleReport
            {
                OfferId = draft.OfferId,
                PlayerWon = playerWon,
                ResultReason = string.IsNullOrWhiteSpace(reason)
                    ? playerWon
                        ? "本次有效敌方已失去战斗能力。"
                        : "我方参战者已失去战斗能力。"
                    : reason.Trim()
            };
            for (var i = 0; i < draft.Entries.Count; i++)
            {
                var entry = draft.Entries[i];
                CaptureState(world, entry.Id, out var finalCondition, out var finalHpAvailable, out var finalHp, out var finalMaxHp);
                report.Add(new ManualBattleReportParticipant
                {
                    EntityId = entry.Id,
                    Name = entry.Name,
                    Side = entry.Side,
                    EntryCondition = entry.Condition,
                    FinalCondition = finalCondition,
                    EntryHpAvailable = entry.HpAvailable,
                    EntryHp = entry.Hp,
                    EntryMaxHp = entry.MaxHp,
                    FinalHpAvailable = finalHpAvailable,
                    FinalHp = finalHp,
                    FinalMaxHp = finalMaxHp
                });
            }
            return report;
        }

        internal static void CaptureState(
            SimulationWorld world,
            EntityId id,
            out ManualBattleReportCondition condition,
            out bool hpAvailable,
            out int hp,
            out int maxHp)
        {
            condition = ManualBattleReportCondition.MissingOrUnavailable;
            hpAvailable = false;
            hp = maxHp = 0;
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return;
            if (entity.TryGet<CombatVitalsComponent>(out var vitals) && vitals != null &&
                entity.TryGet<AttributesComponent>(out var attributes) && attributes != null)
            {
                maxHp = Math.Max(1, attributes.GetFinal(AttributeId.MaxHp));
                hp = Math.Max(0, Math.Min(vitals.CurrentHp, maxHp));
                hpAvailable = vitals.PoolsInitialized;
            }
            if (!entity.TryGet<LifecycleComponent>(out var life) || life == null)
                return;
            if (life.IsRemoved)
            {
                condition = ManualBattleReportCondition.Removed;
                return;
            }
            if (life.State == LifecycleState.Captured)
            {
                condition = ManualBattleReportCondition.Captured;
                return;
            }
            if (life.IsDead)
            {
                condition = ManualBattleReportCondition.Dead;
                return;
            }
            if (life.IsIncapacitated)
            {
                condition = ManualBattleReportCondition.Incapacitated;
                return;
            }
            if (!hpAvailable)
                return;
            var ratio = maxHp > 0 ? (double)hp / maxHp : 0d;
            condition = ratio <= SeriousInjuryHpRatio
                ? ManualBattleReportCondition.SeriouslyInjured
                : ratio < 1d
                    ? ManualBattleReportCondition.Injured
                    : ManualBattleReportCondition.Intact;
        }
    }
}
