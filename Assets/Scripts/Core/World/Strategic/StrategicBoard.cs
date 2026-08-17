namespace XianXia.Core.World.Strategic
{
    public enum StrategicInterruptKind
    {
        RouteEncounter = 0,
        BattleOffer = 1
    }

    /// <summary>路上随机遭遇（113-E）。</summary>
    public sealed class RouteEncounterPending
    {
        public string RouteId { get; set; } = string.Empty;
        public string EncounterId { get; set; } = string.Empty;
        public string LocalMapId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Resolved { get; set; }
    }

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
    }

    public sealed class StrategicBoard
    {
        public FactionDiplomacyBoard Diplomacy { get; } = new FactionDiplomacyBoard();
        public ArmyStackBoard Armies { get; } = new ArmyStackBoard();
        public RouteEncounterPending RouteEncounter { get; } = new RouteEncounterPending();
        public BattleOfferPending BattleOffer { get; } = new BattleOfferPending();

        /// <summary>玩家帮派 id（占点后更新）。</summary>
        public string PlayerFactionId { get; set; } = StrategicFactionCatalog.PlayerFactionId;

        public bool HasBlockingInterrupt =>
            (RouteEncounter != null && !RouteEncounter.Resolved && !string.IsNullOrEmpty(RouteEncounter.EncounterId)) ||
            (BattleOffer != null && !BattleOffer.Resolved && !string.IsNullOrEmpty(BattleOffer.OfferId));

        public void ClearRouteEncounter()
        {
            RouteEncounter.RouteId = string.Empty;
            RouteEncounter.EncounterId = string.Empty;
            RouteEncounter.LocalMapId = string.Empty;
            RouteEncounter.Title = string.Empty;
            RouteEncounter.Resolved = true;
        }

        public void ClearBattleOffer()
        {
            BattleOffer.OfferId = string.Empty;
            BattleOffer.ArmyStackId = string.Empty;
            BattleOffer.Resolved = true;
        }
    }
}
