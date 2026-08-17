using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.World.Strategic
{
    /// <summary>全战式接战弹窗数据。</summary>
    public sealed class BattleOfferPending
    {
        public string OfferId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PlayerLabel { get; set; } = string.Empty;
        public string EnemyLabel { get; set; } = string.Empty;
        public int PlayerPower { get; set; }
        public int EnemyPower { get; set; }
        public int AutoWinPercent { get; set; }
        public string ArmyStackId { get; set; } = string.Empty;
        public string EncounterLocalMapId { get; set; } = string.Empty;
        public bool Resolved { get; set; }
        public bool PlayerWonAuto { get; set; }
        /// <summary>同栈已有手动接战进行时，仅允许新成员加入。</summary>
        public bool IsJoinOngoingBattle { get; set; }
        readonly List<ulong> _playerPartyIds = new List<ulong>(8);

        public IReadOnlyList<ulong> PlayerPartyIds => _playerPartyIds;

        public void SetPlayerParty(IReadOnlyList<EntityId> party)
        {
            _playerPartyIds.Clear();
            if (party == null)
                return;
            for (var i = 0; i < party.Count; i++)
            {
                if (!party[i].IsNone && !_playerPartyIds.Contains(party[i].Value))
                    _playerPartyIds.Add(party[i].Value);
            }
        }

        public void ClearPlayerParty() => _playerPartyIds.Clear();
    }

    public sealed class StrategicBoard
    {
        public FactionDiplomacyBoard Diplomacy { get; } = new FactionDiplomacyBoard();
        public ArmyStackBoard Armies { get; } = new ArmyStackBoard();
        public BattleOfferPending BattleOffer { get; } = new BattleOfferPending();
        public ArrivalNoticePending ArrivalNotice { get; } = new ArrivalNoticePending();
        public StrategicEncounterRuntime Encounter { get; } = new StrategicEncounterRuntime();

        /// <summary>玩家帮派 id（占点后更新）。</summary>
        public string PlayerFactionId { get; set; } = StrategicFactionCatalog.PlayerFactionId;

        public bool HasBattleOffer =>
            BattleOffer != null && !BattleOffer.Resolved && !string.IsNullOrEmpty(BattleOffer.OfferId);

        public bool HasArrivalNotice =>
            ArrivalNotice != null && !ArrivalNotice.Resolved && !string.IsNullOrEmpty(ArrivalNotice.NoticeId);

        public bool HasBlockingInterrupt => HasBattleOffer || HasArrivalNotice;

        public void ClearBattleOffer()
        {
            BattleOffer.OfferId = string.Empty;
            BattleOffer.ArmyStackId = string.Empty;
            BattleOffer.IsJoinOngoingBattle = false;
            BattleOffer.ClearPlayerParty();
            BattleOffer.Resolved = true;
        }

        public void ClearArrivalNotice()
        {
            ArrivalNotice.NoticeId = string.Empty;
            ArrivalNotice.Summary = string.Empty;
            ArrivalNotice.PlaceLabel = string.Empty;
            ArrivalNotice.FocusNodeId = string.Empty;
            ArrivalNotice.ClearArrived();
            ArrivalNotice.Resolved = true;
        }
    }

    /// <summary>我方宏观到站提示（非遇敌）。</summary>
    public sealed class ArrivalNoticePending
    {
        public string NoticeId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string PlaceLabel { get; set; } = string.Empty;
        public string FocusNodeId { get; set; } = string.Empty;
        public bool Resolved { get; set; } = true;
        readonly List<ulong> _arrivedIds = new List<ulong>(8);

        public IReadOnlyList<ulong> ArrivedIds => _arrivedIds;

        public void SetArrived(IReadOnlyList<EntityId> party)
        {
            _arrivedIds.Clear();
            if (party == null)
                return;
            for (var i = 0; i < party.Count; i++)
            {
                if (!party[i].IsNone && !_arrivedIds.Contains(party[i].Value))
                    _arrivedIds.Add(party[i].Value);
            }
        }

        public void ClearArrived() => _arrivedIds.Clear();
    }
}
