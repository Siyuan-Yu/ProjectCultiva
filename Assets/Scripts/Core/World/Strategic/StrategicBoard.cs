using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>BattleOffer 来源：决定决策按钮集合（Local-origin 禁 Auto）与宣战 commitment 语义。</summary>
    public enum BattleOfferOrigin
    {
        /// <summary>WorldMap 战略指令（FormalArmy / PlayerParty remote attack、追击到站、残留再入）。</summary>
        StrategicCommand = 0,
        /// <summary>LocalMap 玩家主动 hostile action（对 FormalArmy member 的军事攻击）。</summary>
        LocalMapHostileAction = 1
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
        /// <summary>Phase E+：Formal Army 接战双方。</summary>
        public string AttackerArmyId { get; set; } = string.Empty;
        public string DefenderArmyId { get; set; } = string.Empty;
        public string EncounterLocalMapId { get; set; } = string.Empty;
        public bool Resolved { get; set; }
        public bool PlayerWonAuto { get; set; }
        public string LastAutoBattleSummary { get; set; } = string.Empty;
        /// <summary>自动战胜后是否直接击杀敌军（否则仅击溃，敌军栈可能残存）。</summary>
        public bool ExecuteOnWin { get; set; }
        /// <summary>Offer 来源（决策态 authority；Local-origin 强制禁 Auto）。</summary>
        public BattleOfferOrigin Origin { get; set; } = BattleOfferOrigin.StrategicCommand;
        /// <summary>确认「手动战斗」时才 DeclareWar 的 pending 声明标记（Neutral Local 军事攻击）。</summary>
        public bool RequiresWarDeclaration { get; set; }
        public string PendingWarAttackerFactionId { get; set; } = string.Empty;
        public string PendingWarDefenderFactionId { get; set; } = string.Empty;
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
        public WarBoard Wars { get; } = new WarBoard();
        public AllianceBoard Alliances { get; } = new AllianceBoard();
        public VassalageBoard Vassalages { get; } = new VassalageBoard();
        public CaptureObjectiveBoard CaptureObjectives { get; } = new CaptureObjectiveBoard();
        public RetreatingArmyBoard RetreatingArmies { get; } = new RetreatingArmyBoard();
        public ArmyStackBoard Armies { get; } = new ArmyStackBoard();
        /// <summary>Formal Army 领域真源（Phase A）；与 Prototype <see cref="Armies"/> 并存。</summary>
        public FormalArmyBoard FormalArmies { get; } = new FormalArmyBoard();
        /// <summary>Hex 战略重要地点（155）；替代 Node 的地点职责。</summary>
        public WorldSiteBoard Sites { get; } = new WorldSiteBoard();
        public BattleOfferPending BattleOffer { get; } = new BattleOfferPending();
        public ArrivalNoticePending ArrivalNotice { get; } = new ArrivalNoticePending();
        public StrategicEncounterRuntime Encounter { get; } = new StrategicEncounterRuntime();
        public LingeringBattlefieldRegistry LingeringBattlefields { get; } = new LingeringBattlefieldRegistry();
        public StrategicClockFreezeState ClockFreeze { get; } = new StrategicClockFreezeState();
        public BattleParticipantSnapshot Participants { get; } = new BattleParticipantSnapshot();
        public PendingEngagementRuntime PendingEngagement { get; } = new PendingEngagementRuntime();
        public BattleInterruptQueue InterruptQueue { get; } = new BattleInterruptQueue();

        /// <summary>Ch01 / LevelTester：启用 presence-based 组军场景 Adapter。</summary>
        public bool Ch01FormationScenarioCompat { get; set; }

        /// <summary>Host 注入：Engagement 收集 PlayerParty 时使用（Domain 不依赖 Session）。</summary>
        public PlayerPartyRuntime PlayerPartyContext { get; set; }

        /// <summary>ReinforcementRange 战略 TravelCost 阈值（遗留）。≤0 忽略。</summary>
        public int ReinforcementTravelCostThreshold { get; set; }

        /// <summary>支援最大节点跳数（遗留）。&lt;0 忽略。</summary>
        public int ReinforcementMaxHops { get; set; } = -1;

        /// <summary>支援世界坐标半径（大地图 XY）。≤0 用默认 ≈2～3 人头像宽。</summary>
        public float ReinforcementWorldRadius { get; set; }

        /// <summary>派人探望弥留成功后，到站打开大地图时衔接「进入残留战场」菜单。</summary>
        public ulong PendingLingeringVisitIncapId { get; set; }

        readonly List<ulong> _pendingLingeringVisitPartyIds = new List<ulong>(8);

        public IReadOnlyList<ulong> PendingLingeringVisitPartyIds => _pendingLingeringVisitPartyIds;

        public void SetPendingLingeringVisit(ulong focusIncapId, IReadOnlyList<EntityId> party)
        {
            PendingLingeringVisitIncapId = focusIncapId;
            _pendingLingeringVisitPartyIds.Clear();
            if (party == null)
                return;
            for (var i = 0; i < party.Count; i++)
            {
                if (!party[i].IsNone)
                    _pendingLingeringVisitPartyIds.Add(party[i].Value);
            }
        }

        public void ClearPendingLingeringVisit()
        {
            PendingLingeringVisitIncapId = 0;
            _pendingLingeringVisitPartyIds.Clear();
        }

        /// <summary>玩家帮派 id（占点后更新）。</summary>
        public string PlayerFactionId { get; set; } = StrategicFactionCatalog.PlayerFactionId;

        public bool HasBattleOffer =>
            BattleOffer != null && !BattleOffer.Resolved && !string.IsNullOrEmpty(BattleOffer.OfferId);

        public bool HasArrivalNotice =>
            ArrivalNotice != null && !ArrivalNotice.Resolved && !string.IsNullOrEmpty(ArrivalNotice.NoticeId);

        public bool HasBlockingInterrupt =>
            HasBattleOffer || HasArrivalNotice ||
            (Participants != null && Participants.IsAutoSettlement);

        public bool IsWorldTickFrozen => ClockFreeze != null && ClockFreeze.IsWorldTickFrozen;

        public bool IsModalEncounter => ClockFreeze != null && ClockFreeze.IsModalEncounter;

        public void ClearBattleOffer()
        {
            BattleOffer.OfferId = string.Empty;
            BattleOffer.ArmyStackId = string.Empty;
            BattleOffer.AttackerArmyId = string.Empty;
            BattleOffer.DefenderArmyId = string.Empty;
            BattleOffer.Origin = BattleOfferOrigin.StrategicCommand;
            BattleOffer.RequiresWarDeclaration = false;
            BattleOffer.PendingWarAttackerFactionId = string.Empty;
            BattleOffer.PendingWarDefenderFactionId = string.Empty;
            BattleOffer.ClearPlayerParty();
            BattleOffer.Resolved = true;
            PendingEngagement?.Clear();
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
