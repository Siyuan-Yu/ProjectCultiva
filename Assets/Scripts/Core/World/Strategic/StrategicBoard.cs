using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Runtime-only presentation binding for the modern CharacterEncounter/manual-combat path
    /// when participants are shown on a Continuous Outdoor surface. CharacterEncounter and its
    /// participant records remain the domain authority; this state only scopes the active Host
    /// presentation and participant visibility.
    /// It is intentionally runtime-only and is rebuilt, never serialized.
    /// </summary>
    public sealed class ContinuousManualCombatPresentationState
    {
        readonly HashSet<ulong> _participantIds = new HashSet<ulong>();
        readonly HashSet<ulong> _friendlyIds = new HashSet<ulong>();
        readonly HashSet<ulong> _enemyIds = new HashSet<ulong>();

        public bool IsActive { get; private set; }
        public string OfferId { get; private set; } = string.Empty;
        public string SurfaceId { get; private set; } = string.Empty;
        public WorldVec2 BattleWorldAnchor { get; private set; }
        public IReadOnlyCollection<ulong> ParticipantIds => _participantIds;

        public bool Contains(EntityId id) =>
            IsActive && !id.IsNone && _participantIds.Contains(id.Value);

        public bool IsFriendly(EntityId id) =>
            IsActive && !id.IsNone && _friendlyIds.Contains(id.Value);

        public bool IsEnemy(EntityId id) =>
            IsActive && !id.IsNone && _enemyIds.Contains(id.Value);

        public bool AreOpposing(EntityId first, EntityId second) =>
            IsActive && ((IsFriendly(first) && IsEnemy(second)) ||
                         (IsEnemy(first) && IsFriendly(second)));

        public void Begin(
            string offerId,
            string surfaceId,
            WorldVec2 battleWorldAnchor,
            IReadOnlyList<ActualBattleParticipant> participants)
        {
            Clear();
            if (string.IsNullOrWhiteSpace(offerId) || string.IsNullOrWhiteSpace(surfaceId))
                return;
            OfferId = offerId.Trim();
            SurfaceId = surfaceId.Trim();
            BattleWorldAnchor = battleWorldAnchor;
            if (participants != null)
                for (var i = 0; i < participants.Count; i++)
                {
                    var participant = participants[i];
                    if (participant.EntityId.IsNone || !_participantIds.Add(participant.EntityId.Value))
                        continue;
                    if (participant.IsFriendly)
                        _friendlyIds.Add(participant.EntityId.Value);
                    else
                        _enemyIds.Add(participant.EntityId.Value);
                }
            IsActive = _participantIds.Count > 0;
            if (!IsActive)
                Clear();
        }

        public bool ClearOwned(string offerId)
        {
            if (!IsActive || !string.Equals(OfferId, offerId ?? string.Empty, System.StringComparison.Ordinal))
                return false;
            Clear();
            return true;
        }

        public void Clear()
        {
            IsActive = false;
            OfferId = string.Empty;
            SurfaceId = string.Empty;
            BattleWorldAnchor = default;
            _participantIds.Clear();
            _friendlyIds.Clear();
            _enemyIds.Clear();
        }
    }

    public sealed class StrategicBoard
    {
        public WorldSpatialRules SpatialRules { get; set; }
        public readonly System.Collections.Generic.HashSet<string> SuppressedCharacterContacts = new System.Collections.Generic.HashSet<string>();
        public XianXia.Core.Domain.Ids.EntityId PendingCharacterAttacker, PendingCharacterTarget;
        public CharacterEncounterState CharacterEncounter { get; set; }
        public FactionDiplomacyBoard Diplomacy { get; } = new FactionDiplomacyBoard();
        public WarBoard Wars { get; } = new WarBoard();
        public AllianceBoard Alliances { get; } = new AllianceBoard();
        public VassalageBoard Vassalages { get; } = new VassalageBoard();
        /// <summary>Unified persistent action-group membership authority.</summary>
        public SquadBoard Squads { get; } = new SquadBoard();
        public SquadWorldMotionBoard SquadWorldMotions { get; } = new SquadWorldMotionBoard();
        /// <summary>Continuous world strategic sites and administrative cores.</summary>
        public WorldSiteBoard Sites { get; } = new WorldSiteBoard();
        /// <summary>Site 行政范围的不可改写取得历史；Owner 仍由 WorldSite 提供。</summary>
        public TerritoryClaimBoard TerritoryClaims { get; } = new TerritoryClaimBoard();
        /// <summary>SiteId-keyed public administrative resources; ownership remains on WorldSite.</summary>
        public WorldSitePublicStockBoard SitePublicStocks { get; } = new WorldSitePublicStockBoard();
        public FactionFlagBoard FactionFlags { get; } = new FactionFlagBoard();
        public StrategicClockFreezeState ClockFreeze { get; } = new StrategicClockFreezeState();
        public BattleParticipantSnapshot Participants { get; } = new BattleParticipantSnapshot();
        public ContinuousManualCombatPresentationState ContinuousManualCombat { get; } =
            new ContinuousManualCombatPresentationState();
        public ManualBattleSettlementState ManualBattleSettlement { get; } =
            new ManualBattleSettlementState();

        /// <summary>Host 注入：Engagement 收集 PlayerParty 时使用（Domain 不依赖 Session）。</summary>
        public PlayerPartyRuntime PlayerPartyContext { get; set; }

        /// <summary>玩家帮派 id（占点后更新）。</summary>
        public string PlayerFactionId { get; set; } = StrategicFactionCatalog.PlayerFactionId;

        public bool IsWorldTickFrozen => ClockFreeze != null && ClockFreeze.IsWorldTickFrozen;

        public bool IsModalEncounter => ClockFreeze != null && ClockFreeze.IsModalEncounter;

    }
}
