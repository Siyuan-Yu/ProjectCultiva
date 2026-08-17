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
        public StrategicEncounterRuntime Encounter { get; } = new StrategicEncounterRuntime();

        /// <summary>玩家帮派 id（占点后更新）。</summary>
        public string PlayerFactionId { get; set; } = StrategicFactionCatalog.PlayerFactionId;

        public bool HasBlockingInterrupt =>
            BattleOffer != null && !BattleOffer.Resolved && !string.IsNullOrEmpty(BattleOffer.OfferId);

        public void ClearBattleOffer()
        {
            BattleOffer.OfferId = string.Empty;
            BattleOffer.ArmyStackId = string.Empty;
            BattleOffer.ClearPlayerParty();
            BattleOffer.Resolved = true;
        }
    }
}
