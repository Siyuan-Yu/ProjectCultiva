using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Persistence
{
    public sealed class WorldSnapshot
    {
        public const int CurrentSchemaVersion = 6;
        /// <summary>v1 development saves are explicitly unsupported.</summary>
        public const int LegacySchemaVersion = 1;
        /// <summary>v2 route-only saves are unsupported after hex migration.</summary>
        public const int LegacySchemaVersionV2 = 2;
        /// <summary>v3 lacks Residual Hex Presence — unsupported after residual migration.</summary>
        public const int LegacySchemaVersionV3 = 3;
        /// <summary>v4 uses NodeOwners — unsupported after Pure Hex ownership migration.</summary>
        public const int LegacySchemaVersionV4 = 4;
        /// <summary>v5 retains node/route DTO fields — unsupported after Pure Hex legacy purge.</summary>
        public const int LegacySchemaVersionV5 = 5;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<string> SuppressedCharacterContacts { get; set; } = new List<string>();
        public XianXia.Core.World.Strategic.CharacterEncounterState CharacterEncounter { get; set; }
        public ulong SnapshotId { get; set; }
        public ulong WorldTick { get; set; }
        public ulong RegionId { get; set; }
        public string EnabledPackageId { get; set; }
        public string EnabledPackageVersion { get; set; }
        public ulong RandomS0 { get; set; }
        public ulong RandomS1 { get; set; }
        public ulong RandomStreamId { get; set; }
        public int EventCursor { get; set; }
        public ulong NextEventId { get; set; }
        public ulong NextEntityId { get; set; }
        public ulong NextOrderId { get; set; }
        public ulong NextActionId { get; set; }
        public ulong NextModifierId { get; set; }
        public List<EntitySnapshotDto> Entities { get; set; } = new List<EntitySnapshotDto>();
        public List<ActiveActionSnapshotDto> ActiveActions { get; set; } = new List<ActiveActionSnapshotDto>();
        public List<OrderSnapshotDto> Orders { get; set; } = new List<OrderSnapshotDto>();
        public List<ScheduleDefinitionSnapshotDto> Schedules { get; set; } = new List<ScheduleDefinitionSnapshotDto>();
        public List<OpportunitySiteSnapshotDto> OpportunitySites { get; set; } = new List<OpportunitySiteSnapshotDto>();
        public List<ManualSnapshotDto> Manuals { get; set; } = new List<ManualSnapshotDto>();
        public int ObservationDiscoverChancePercent { get; set; } = 100;
        public StrategicSnapshotDto Strategic { get; set; } = new StrategicSnapshotDto();

        /// <summary>Party 共享背包 Runtime 真源（v6 optional；旧档缺省＝空背包）。</summary>
        public List<PartyInventorySlotSnapshotDto> PartyInventorySlots { get; set; } =
            new List<PartyInventorySlotSnapshotDto>();

        /// <summary>字段出现即表示 persistent world loot taken set 完整 authoritative。</summary>
        public bool HasTakenWorldLootSnapshotAuthority { get; set; }
        public List<string> TakenWorldLootSpotIds { get; set; } = new List<string>();

        /// <summary>RelationshipLedger 事件流（v6 optional；旧档缺省＝空）。</summary>
        public List<RelationshipEventSnapshotDto> RelationshipEvents { get; set; } =
            new List<RelationshipEventSnapshotDto>();

        /// <summary>Social Bond Runtime Board（v6 optional；旧档缺省＝空）。</summary>
        public List<SocialBondSnapshotDto> SocialBonds { get; set; } =
            new List<SocialBondSnapshotDto>();
        public long NextOutdoorConstructedAssetSequence { get; set; } = 1;
        public List<OutdoorConstructedAssetSnapshotDto> OutdoorConstructedAssets { get; set; } = new List<OutdoorConstructedAssetSnapshotDto>();
        public List<OutdoorDestructibleSnapshotDto> OutdoorDestructibles { get; set; } =
            new List<OutdoorDestructibleSnapshotDto>();
        public List<OutdoorFarmPlotSnapshotDto> OutdoorFarmPlots { get; set; } =
            new List<OutdoorFarmPlotSnapshotDto>();
    }

    public sealed class OutdoorConstructedAssetSnapshotDto
    {
        public string StableAssetId { get; set; } = string.Empty;
        public string BuildingId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public float WorldWidth { get; set; }
        public float WorldHeight { get; set; }
        public int CellsW { get; set; }
        public int CellsH { get; set; }
        public string BoundLocationId { get; set; } = string.Empty;
        public string BoundWorldSiteId { get; set; } = string.Empty;
    }

    public sealed class OutdoorDestructibleSnapshotDto
    { public string StableId { get; set; } = string.Empty; public int CurrentHp { get; set; } public bool Destroyed { get; set; } }
    public sealed class OutdoorFarmPlotSnapshotDto
    { public string StableCellId { get; set; } = string.Empty; public string CropId { get; set; } = string.Empty; public int CropStage { get; set; } public float Growth { get; set; } }

    public sealed class StrategicSnapshotDto
    {
        public string PlayerFactionId { get; set; } = string.Empty;
        public bool Ch01FormationScenarioCompat { get; set; }
        public bool HasSquadSnapshotAuthority { get; set; }
        public List<SquadSnapshotDto> Squads { get; set; } = new List<SquadSnapshotDto>();
        public bool HasSquadWorldMotionSnapshotAuthority { get; set; }
        public List<SquadWorldMotionSnapshotDto> SquadWorldMotions { get; set; } = new List<SquadWorldMotionSnapshotDto>();
        public string ControlledSquadId { get; set; } = string.Empty;
        public List<FormalArmySnapshotDto> FormalArmies { get; set; } = new List<FormalArmySnapshotDto>();
        public List<ArmyMembershipSnapshotDto> ArmyMemberships { get; set; } = new List<ArmyMembershipSnapshotDto>();
        /// <summary>Detached Residual Character Hex Presence（非 Group Domain）。</summary>
        public List<ResidualCharacterPresenceDto> ResidualCharacterPresences { get; set; } =
            new List<ResidualCharacterPresenceDto>();
        /// <summary>
        /// Phase 2A：Character World Presence（AtSite 存 SiteId；AtHex 存 Hex）。
        /// 可选字段；旧存档缺省时不做随机补全。
        /// </summary>
        public List<CharacterWorldPresenceSnapshotDto> CharacterWorldPresences { get; set; } =
            new List<CharacterWorldPresenceSnapshotDto>();
        public List<WorldSiteOwnerSnapshotDto> WorldSiteOwners { get; set; } = new List<WorldSiteOwnerSnapshotDto>();
        /// <summary>Field presence means the runtime-created Site set is authoritative.</summary>
        public bool HasRuntimeWorldSiteSnapshotAuthority { get; set; }
        public List<RuntimeWorldSiteSnapshotDto> RuntimeWorldSites { get; set; } =
            new List<RuntimeWorldSiteSnapshotDto>();
        /// <summary>Legacy read-only compatibility; new saves derive Region/Hex projection.</summary>
        public List<TerritoryRegionControllerSnapshotDto> TerritoryRegionControllers { get; set; } = new List<TerritoryRegionControllerSnapshotDto>();
        /// <summary>True means TerritoryClaims is the complete immutable acquisition history.</summary>
        public bool HasTerritoryClaimSnapshotAuthority { get; set; }
        public List<TerritoryClaimSnapshotDto> TerritoryClaims { get; set; } =
            new List<TerritoryClaimSnapshotDto>();
        /// <summary>字段出现即表示 Flag active set 完整 authoritative；空数组也有意义。</summary>
        public bool HasFactionFlagSnapshotAuthority { get; set; }
        public List<FactionFlagSnapshotDto> FactionFlags { get; set; } = new List<FactionFlagSnapshotDto>();
        public List<WarSnapshotDto> Wars { get; set; } = new List<WarSnapshotDto>();
        public List<AllianceSnapshotDto> Alliances { get; set; } = new List<AllianceSnapshotDto>();
        public List<VassalageSnapshotDto> Vassalages { get; set; } = new List<VassalageSnapshotDto>();
        public List<RetreatingArmySnapshotDto> RetreatingArmies { get; set; } = new List<RetreatingArmySnapshotDto>();
        /// <summary>字段出现即表示 fixed ControlCore physical runtime state 完整 authoritative。</summary>
        public bool HasControlCoreSnapshotAuthority { get; set; }
        public List<ControlCoreRuntimeSnapshotDto> ControlCores { get; set; } = new List<ControlCoreRuntimeSnapshotDto>();
        /// <summary>旧 v6 migration input；新存档不写。</summary>
        public List<LegacyCaptureObjectiveSnapshotDto> LegacyCaptureObjectives { get; set; } = new List<LegacyCaptureObjectiveSnapshotDto>();
        /// <summary>字段出现即表示所有 Site public stock runtime state 完整 authoritative。</summary>
        public bool HasWorldSitePublicStockSnapshotAuthority { get; set; }
        public List<WorldSitePublicStockSnapshotDto> WorldSitePublicStocks { get; set; } =
            new List<WorldSitePublicStockSnapshotDto>();
        /// <summary>
        /// Phase 2C：PlayerParty 连续世界位置（可选；旧存档缺省时不恢复 motion）。
        /// </summary>
        public PlayerPartyTravelSnapshotDto PlayerPartyTravel { get; set; }

        /// <summary>Phase 1：PlayerParty 成员与 Active（可选；旧存档可推断）。</summary>
        public PlayerPartyRuntimeSnapshotDto PlayerParty { get; set; }

        /// <summary>
        /// 当前 Loaded LocalMap 中 Character 的表现落点（可选；旧存档缺省则 Default Spawn）。
        /// 非 WorldLocation 真源；WorldSite 读档 Materialize 时使用。
        /// </summary>
        public List<LoadedLocalMapCharacterPlacementSnapshotDto> LoadedLocalMapCharacterPlacements { get; set; } =
            new List<LoadedLocalMapCharacterPlacementSnapshotDto>();

        /// <summary>
        /// SPACE-01：Separate Space Session（可选；旧存档缺省时尝试从 placements 迁移）。
        /// </summary>
        public SeparateSpaceSessionSnapshotDto SeparateSpace { get; set; }

        /// <summary>Phase 2D：Background Character 中途旅行状态（可选）。</summary>
        public List<BackgroundCharacterTravelSnapshotDto> BackgroundCharacterTravels { get; set; } =
            new List<BackgroundCharacterTravelSnapshotDto>();

        /// <summary>Phase 4：Pending Engagement 决策态（弹窗已出现时可恢复）。</summary>
        public PendingEngagementSnapshotDto PendingEngagement { get; set; }
    }

    public sealed class PendingEngagementSnapshotDto
    {
        public string EngagementId { get; set; } = string.Empty;
        public int InitiatorKind { get; set; }
        public string InitiatorFormalArmyId { get; set; } = string.Empty;
        public bool InitiatorIsPlayerSide { get; set; }
        public int DecisionSubjectKind { get; set; }
        public string DecisionSubjectFormalArmyId { get; set; } = string.Empty;
        public int BattleLocationHexQ { get; set; }
        public int BattleLocationHexR { get; set; }
        public List<int> BattleAreaHexQList { get; set; } = new List<int>(8);
        public List<int> BattleAreaHexRList { get; set; } = new List<int>(8);
        public List<int> SupportAreaHexQList { get; set; } = new List<int>(16);
        public List<int> SupportAreaHexRList { get; set; } = new List<int>(16);
        public string SupportBattleSiteId { get; set; } = string.Empty;
        public string SupportBattleSiteResolutionSource { get; set; } = string.Empty;
        public int InitiatorEngagementHexQ { get; set; }
        public int InitiatorEngagementHexR { get; set; }
        public string InitiatorEngagementSiteId { get; set; } = string.Empty;
        public string AttackerFormalArmyId { get; set; } = string.Empty;
        public string DefenderFormalArmyId { get; set; } = string.Empty;
        public bool PlayerPartyIncluded { get; set; }
        public bool InvolvesPlayerSide { get; set; }
        /// <summary>Phase 5S Persistence：frozen engagement 元数据（Load 后不重新推导）。</summary>
        public string PrimaryEnemyFactionId { get; set; } = string.Empty;
        public string PlayerInclusionReason { get; set; } = string.Empty;
        public bool RequiresPlayerDecision { get; set; }
        public string PendingBattleTriggerReason { get; set; } = string.Empty;
        public int InitiatorCommittedHexQ { get; set; } = int.MinValue;
        public int InitiatorCommittedHexR { get; set; } = int.MinValue;
        public int DefenderCommittedHexQ { get; set; } = int.MinValue;
        public int DefenderCommittedHexR { get; set; } = int.MinValue;
        public string OfferId { get; set; } = string.Empty;
        public string OfferTitle { get; set; } = string.Empty;
        public string ArmyStackId { get; set; } = string.Empty;
        public string EncounterLocalMapId { get; set; } = string.Empty;
        /// <summary>BattleOfferOrigin（Local-origin 决策态恢复用）。</summary>
        public int OfferOrigin { get; set; }
        public bool OfferRequiresWarDeclaration { get; set; }
        public string PendingWarAttackerFactionId { get; set; } = string.Empty;
        public string PendingWarDefenderFactionId { get; set; } = string.Empty;
        public List<string> PlayerFormalArmyIds { get; set; } = new List<string>(8);
        public List<string> EnemyFormalArmyIds { get; set; } = new List<string>(8);
        public List<ulong> PlayerPartyMemberIds { get; set; } = new List<ulong>(8);
        /// <summary>Retreat location 是否有值（避免把 null retreat 恢复成默认对象）。</summary>
        public bool RetreatHasValue { get; set; }
        public int RetreatArmyLocationKind { get; set; }
        public int RetreatPartyLocationKind { get; set; }
        public string RetreatSiteId { get; set; } = string.Empty;
        public float RetreatWorldX { get; set; }
        public float RetreatWorldY { get; set; }
        public int RetreatHexQ { get; set; }
        public int RetreatHexR { get; set; }
        public bool RetreatIsPlayerParty { get; set; }
        public string ParticipantOfferId { get; set; } = string.Empty;
        public string ParticipantAttackerArmyId { get; set; } = string.Empty;
        public string ParticipantDefenderArmyId { get; set; } = string.Empty;
        public string ParticipantPrimaryEnemyStackId { get; set; } = string.Empty;
        public int ParticipantBattleAnchorHexQ { get; set; }
        public int ParticipantBattleAnchorHexR { get; set; }
        public bool ParticipantHasBattleAnchorWorldPosition { get; set; }
        public float ParticipantBattleAnchorWorldX { get; set; }
        public float ParticipantBattleAnchorWorldY { get; set; }
        public string ParticipantBattleAnchorSurfaceId { get; set; } = string.Empty;
        /// <summary>ParticipantSnapshot 冻结的 LocalMap 决议（Auto/Manual 语义 authority）。</summary>
        public string ParticipantEncounterLocalMapId { get; set; } = string.Empty;
        public int ParticipantLocalMapResolutionKind { get; set; }
        /// <summary>旧存档缺省时无法区分 0=WorldSite 与缺失字段，须用 flag 决定是否走 legacy fallback。</summary>
        public bool HasParticipantLocalMapResolutionKind { get; set; }
        public List<PendingEngagementParticipantRecordDto> ParticipantRecords { get; set; } =
            new List<PendingEngagementParticipantRecordDto>(32);
    }

    public sealed class PendingEngagementParticipantRecordDto
    {
        public int Kind { get; set; }
        public ulong EntityId { get; set; }
        public string SquadId { get; set; } = string.Empty;
        public string ArmyStackId { get; set; } = string.Empty;
        public string FormalArmyId { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = string.Empty;
        public int CombatPower { get; set; }
        public bool Selected { get; set; }
        /// <summary>Phase 5S Persistence：record 完整冻结（IncludedReason + PreBattle）。</summary>
        public string IncludedReason { get; set; } = string.Empty;
        public bool HasPreBattle { get; set; }
        public int PreBattleMode { get; set; }
        public string PreBattleSiteId { get; set; } = string.Empty;
        public int PreBattleHexQ { get; set; } = int.MinValue;
        public int PreBattleHexR { get; set; } = int.MinValue;
        public string PreBattleFollowStackId { get; set; } = string.Empty;
        public string PreBattleCombatPursuitStackId { get; set; } = string.Empty;
        public bool PreBattleHasWorldPosition { get; set; }
        public float PreBattleWorldX { get; set; }
        public float PreBattleWorldY { get; set; }
        public string PreBattleSurfaceId { get; set; } = string.Empty;
    }

    /// <summary>Phase 2D：Background Character 旅行快照（WorldLocation + route progress）。</summary>
    public sealed class BackgroundCharacterTravelSnapshotDto
    {
        public ulong CharacterId { get; set; }
        public bool IsSurfaceRoute { get; set; }
        public string SurfaceId { get; set; } = string.Empty;
        public float SurfaceDestinationX { get; set; }
        public float SurfaceDestinationY { get; set; }
        public int LocationKind { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int CurrentHexQ { get; set; }
        public int CurrentHexR { get; set; }
        public bool IsTraveling { get; set; }
        public int DestinationHexQ { get; set; }
        public int DestinationHexR { get; set; }
        public string DestinationSiteId { get; set; } = string.Empty;
        public int SegmentIndex { get; set; }
        public float SegmentProgress { get; set; }
        public ulong LastProcessedWorldTick { get; set; }
        public List<HexCoordSnapshotDto> HexPath { get; set; } = new List<HexCoordSnapshotDto>();
    }

    /// <summary>Phase 2C：PlayerParty 开世界连续位置快照（MovementState 恢复为 Idle）。</summary>
    public sealed class PlayerPartyTravelSnapshotDto
    {
        public bool HasPosition { get; set; }
        public int LocationKind { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int CurrentHexQ { get; set; }
        public int CurrentHexR { get; set; }
        public bool IsMoving { get; set; }
        public bool HasContinuousPhysicalDestination { get; set; }
        public float DestinationWorldX { get; set; }
        public float DestinationWorldY { get; set; }
        public float ArrivalRadius { get; set; }
        public string DestinationSiteId { get; set; } = string.Empty;
        public int ExecutionMode { get; set; }
    }

    /// <summary>PlayerParty Runtime 成员快照（Host Session 层；Domain Character 仍存 entities）。</summary>
    public sealed class PlayerPartyRuntimeSnapshotDto
    {
        public ulong ActiveCharacterId { get; set; }
        public List<ulong> MemberCharacterIds { get; set; } = new List<ulong>();
    }

    /// <summary>Save 时当前 Loaded LocalMap 内 Character 的 Local 表现坐标。</summary>
    public sealed class LoadedLocalMapCharacterPlacementSnapshotDto
    {
        public ulong CharacterId { get; set; }
        public string LocalMapId { get; set; } = string.Empty;
        public float LocalX { get; set; }
        public float LocalZ { get; set; }
    }

    /// <summary>SPACE-01 Separate Space Session additive snapshot（不含 View）。</summary>
    public sealed class SeparateSpaceSessionSnapshotDto
    {
        public bool IsInSeparateSpace { get; set; }
        public int SpaceKind { get; set; }
        public string ActiveMapLayoutId { get; set; } = string.Empty;
        public string ActiveLocalPlaceSetId { get; set; } = string.Empty;
        public string EntryLocationId { get; set; } = string.Empty;
        public string ReturnLocationId { get; set; } = string.Empty;
        public bool HasOutdoorReturn { get; set; }
        public string ReturnSurfaceId { get; set; } = string.Empty;
        public float ReturnWorldX { get; set; }
        public float ReturnWorldY { get; set; }
        public ulong ActiveCharacterId { get; set; }
        public string EntryReason { get; set; } = string.Empty;
        public List<ulong> OccupantIds { get; set; } = new List<ulong>(8);
    }

    public sealed class FormalArmySnapshotDto
    {
        public string ArmyId { get; set; }
        public string FactionId { get; set; }
        public ulong LeaderCharacterId { get; set; }
        public List<ulong> MemberCharacterIds { get; set; } = new List<ulong>();
        public int State { get; set; }
        public bool UsesHexStrategicPosition { get; set; }
        public int CurrentHexQ { get; set; }
        public int CurrentHexR { get; set; }
        public int DestinationHexQ { get; set; }
        public int DestinationHexR { get; set; }
        public float StepProgress { get; set; }
        public int StepRemainingTicks { get; set; }
        public int StepTotalTicks { get; set; }
        public int CurrentPathIndex { get; set; }
        public List<HexCoordSnapshotDto> HexPath { get; set; } = new List<HexCoordSnapshotDto>();

        /// <summary>Phase 3 连续位置（可选；旧存档缺省时从 CurrentHex 迁移）。</summary>
        public int LocationKind { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public string DestinationSiteId { get; set; } = string.Empty;
        public int CurrentOrderKind { get; set; }
        public string OrderTargetArmyId { get; set; } = string.Empty;
        public float SegmentProgress { get; set; }
        public int SegmentIndex { get; set; }
        public int TravelMode { get; set; }
        /// <summary>soft-additive presence marker；旧档缺字段时为 false 并走 legacy restore。</summary>
        public bool HasSiteDepartureState { get; set; }
        public bool IsSiteDeparturePending { get; set; }
        public float SiteDepartureVirtualX { get; set; }
        public float SiteDepartureVirtualY { get; set; }
        public float SiteDepartureBoundaryX { get; set; }
        public float SiteDepartureBoundaryY { get; set; }
        public int SiteDepartureFootprintQ { get; set; }
        public int SiteDepartureFootprintR { get; set; }
        public int SiteDepartureExitQ { get; set; }
        public int SiteDepartureExitR { get; set; }

        /// <summary>Optional FormalArmy continuous Surface route state.</summary>
        public int RouteKind { get; set; }
        public string SurfaceId { get; set; } = string.Empty;
        public string SurfaceSourceRevision { get; set; } = string.Empty;
        public string SurfaceSourceHash { get; set; } = string.Empty;
        public float PhysicalDestinationX { get; set; }
        public float PhysicalDestinationY { get; set; }
        public int SurfaceWaypointIndex { get; set; }
        public string RouteDiagnostic { get; set; } = string.Empty;
        public List<WorldPointSnapshotDto> SurfacePath { get; set; } = new List<WorldPointSnapshotDto>();
    }

    public sealed class WorldPointSnapshotDto
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public sealed class HexCoordSnapshotDto
    {
        public int Q { get; set; }
        public int R { get; set; }
    }

    public sealed class ArmyMembershipSnapshotDto
    {
        public ulong CharacterId { get; set; }
        public string ArmyId { get; set; }
    }

    /// <summary>Snapshot DTO only：CharacterId + HexCoord。不保存 Relation / Group。</summary>
    public sealed class ResidualCharacterPresenceDto
    {
        public ulong CharacterId { get; set; }
        public int HexQ { get; set; }
        public int HexR { get; set; }
    }

    /// <summary>Phase 2A：持久化 Background／Party Character 的世界存在（SiteId 真源，不另存可漂移 Site Hex）。</summary>
    public sealed class CharacterWorldPresenceSnapshotDto
    {
        public string PersonalSurfaceId { get; set; } = string.Empty;
        public ulong CharacterId { get; set; }
        public int Mode { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public int HexQ { get; set; } = int.MinValue;
        public int HexR { get; set; } = int.MinValue;
        /// <summary>AtHex residual 精确连续落点标记（老存档无此字段 → false → legacy hex fallback）。</summary>
        public bool HasWorldPosition { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
    }

    public sealed class WorldSiteOwnerSnapshotDto
    {
        public string SiteId { get; set; }
        public string OwnerFactionId { get; set; }
        public int CoreMetadataFormat { get; set; }
        public string CoreAssetId { get; set; }
        public string CoreSurfaceId { get; set; }
        public int CoreLevel { get; set; }
        public float CoreWorldX { get; set; }
        public float CoreWorldY { get; set; }
        public bool CoreActive { get; set; }
    }

    public sealed class SquadSnapshotDto
    {
        public string SquadId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public ulong LeaderCharacterId { get; set; }
        public string LegacyArmyId { get; set; } = string.Empty;
        public int CommandKind { get; set; }
        public ulong CommandRevision { get; set; }
        public ulong CommandTargetCharacterId { get; set; }
        public List<ulong> MemberCharacterIds { get; set; } = new List<ulong>();
    }

    public sealed class SquadWorldMotionSnapshotDto
    {
        public string SquadId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public bool IsMoving { get; set; }
        public float DestinationX { get; set; }
        public float DestinationY { get; set; }
        public int WaypointIndex { get; set; }
        public float SegmentProgress { get; set; }
        public string SourceRevision { get; set; } = string.Empty;
        public string SourceHash { get; set; } = string.Empty;
        public List<WorldPointSnapshotDto> Route { get; set; } = new List<WorldPointSnapshotDto>();
    }

    public sealed class RuntimeWorldSiteSnapshotDto
    {
        public int CoreLevelFormat { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SiteType { get; set; } = string.Empty;
        public string OwnerFactionId { get; set; } = string.Empty;
        public long ControlEstablishedOrder { get; set; }
        public int AnchorQ { get; set; }
        public int AnchorR { get; set; }
        public string CoreAssetId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public bool HasWorldPosition { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int CoreLevel { get; set; }
        public float RangeWidth { get; set; }
        public float RangeHeight { get; set; }
        public bool IsCoreActive { get; set; }
        public bool CoreIsRemovable { get; set; }
    }

    /// <summary>TerritoryRegion 运行时 Controller（2J §17）；Region/Hexes/PrimaryWorldSiteId 属 Content identity 不重复持久化。</summary>
    public sealed class TerritoryRegionControllerSnapshotDto
    {
        public string RegionId { get; set; }
        public string ControlFactionId { get; set; }
    }

    public sealed class TerritoryClaimSnapshotDto
    {
        public int FormatVersion { get; set; }
        public string ClaimId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public long AcquiredOrder { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
    public sealed class FactionFlagSnapshotDto
    {
        public int SiteCoreFormat { get; set; }
        public string FlagId { get; set; }
        public string FactionId { get; set; }
        public int AnchorQ { get; set; }
        public int AnchorR { get; set; }
        public long EstablishedOrder { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public bool HasLocalPosition { get; set; }
        public float LocalX { get; set; }
        public float LocalZ { get; set; }
        public bool HasWorldPosition { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public string SiteId { get; set; }
        public string SurfaceId { get; set; }
        public bool IsSiteCore { get; set; }
    }

    public sealed class WarSnapshotDto
    {
        public string WarId { get; set; }
        public bool Active { get; set; }
        public List<string> Attackers { get; set; } = new List<string>();
        public List<string> Defenders { get; set; } = new List<string>();
    }

    public sealed class AllianceSnapshotDto
    {
        public string AllianceId { get; set; }
        public List<string> Members { get; set; } = new List<string>();
    }

    public sealed class VassalageSnapshotDto
    {
        public string VassalFactionId { get; set; }
        public string OverlordFactionId { get; set; }
    }

    public sealed class RetreatingArmySnapshotDto
    {
        public string RetreatingArmyId { get; set; }
        public string SourceArmyId { get; set; }
        public string FactionId { get; set; }
        public int HexQ { get; set; } = int.MinValue;
        public int HexR { get; set; } = int.MinValue;
        public List<ulong> MemberCharacterIds { get; set; } = new List<ulong>();
    }

    public sealed class ControlCoreRuntimeSnapshotDto
    {
        public string WorkAreaId { get; set; }
        public int CurrentDurability { get; set; }
        public float OccupyProgressSeconds { get; set; }
    }

    public sealed class WorldSitePublicStockSnapshotDto
    {
        public string SiteId { get; set; } = string.Empty;
        public List<WorldSitePublicStockEntrySnapshotDto> Entries { get; set; } =
            new List<WorldSitePublicStockEntrySnapshotDto>();
    }

    public sealed class WorldSitePublicStockEntrySnapshotDto
    {
        public string ResourceId { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public sealed class LegacyCaptureObjectiveSnapshotDto
    {
        public string ObjectiveId { get; set; }
        public string SiteId { get; set; }
        public string WorkAreaId { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        /// <summary>可重复争夺目标的当前占领读条；旧 v6 缺失时为 0。</summary>
        public float OccupyProgressSeconds { get; set; }
        /// <summary>内容定义的占领时长快照；旧 v6 缺失时由 Core shell 补全。</summary>
        public float OccupyHoldSeconds { get; set; }
        /// <summary>旧一次性占领标记，仅用于 Restore migration；不是 Runtime authority。</summary>
        public bool Completed { get; set; }
    }

    public sealed class OpportunitySiteSnapshotDto
    {
        public string Id { get; set; }
        public bool AllowsCultivation { get; set; }
        public string OfferedManualId { get; set; }
        public string NameKey { get; set; }
        public string Description { get; set; }
    }

    public sealed class ManualSnapshotDto
    {
        public string Id { get; set; }
        public string RequiredRealm { get; set; }
        public int CultivationSpeed { get; set; }
        public int BreakthroughProgress { get; set; }
    }

    public sealed class ScheduleDefinitionSnapshotDto
    {
        public string Id { get; set; }
        public List<ScheduleBlockSnapshotDto> Blocks { get; set; } = new List<ScheduleBlockSnapshotDto>();
    }

    public sealed class ScheduleBlockSnapshotDto
    {
        public int StartTickInDay { get; set; }
        public int EndTickInDay { get; set; }
        public int Activity { get; set; }
        public ulong OrderDurationTicks { get; set; }
    }

    public sealed class EntitySnapshotDto
    {
        public ulong Id { get; set; }
        public string DefinitionId { get; set; }
        public string DisplayName { get; set; }
        public int Tags { get; set; }
        public int Lifecycle { get; set; }
        public List<AttrBaseDto> Bases { get; set; } = new List<AttrBaseDto>();
        public List<ModifierSnapshotDto> Modifiers { get; set; } = new List<ModifierSnapshotDto>();
        public ulong ActiveActionId { get; set; }
        public ulong ActiveTotalTicks { get; set; }
        public ulong ActiveRemainingTicks { get; set; }
        public bool HasActiveClock { get; set; }
        public bool HasCultivation { get; set; }
        public int Realm { get; set; }
        public int CultivationMinorStage { get; set; }
        public int CultivationProgress { get; set; }
        public int BreakthroughProgressRequired { get; set; }
        public int CultivationSpeed { get; set; }
        public string LearnedManualId { get; set; }
        public int ManualMasteryTier { get; set; }
        public int ManualMasteryProgress { get; set; }
        public int ManualMasteryProgressRequired { get; set; }
        public bool HasManualMastery { get; set; }
        public List<string> CombatArtsLearned { get; set; } = new List<string>();
        public List<string> CombatArtsEquipped { get; set; } = new List<string>();
        public List<ArtMasterySnapshotDto> CombatArtMastery { get; set; } = new List<ArtMasterySnapshotDto>();
        public string RequiredRealmName { get; set; }
        public bool HasDailyTask { get; set; }
        public int LaborProgress { get; set; }
        public int LaborQuota { get; set; }
        public int RequiredAmount { get; set; }
        public int CompletedAmount { get; set; }
        public int Deviation { get; set; }
        public bool PendingReprimand { get; set; }
        public int LastSettledDeviation { get; set; }
        public bool HasSchedule { get; set; }
        public string ScheduleDefinitionId { get; set; }
        public int ActiveOrderSource { get; set; }
        public List<string> KnownSiteIds { get; set; } = new List<string>();
        public int PersonalConcealmentRisk { get; set; }
        /// <summary>EntityLocation 是运行时地点真源；false 兼容旧存档缺失该组件。</summary>
        public bool HasEntityLocation { get; set; }
        /// <summary>JSON 是否含新 Location 字段；仅用于旧档兼容回填判定，不是长期地点数据。</summary>
        public bool EntityLocationSnapshotFieldPresent { get; set; }
        public string LocationId { get; set; } = string.Empty;
        public bool HasPresentationOverride { get; set; }
        public float PresentationOverrideX { get; set; }
        public float PresentationOverrideZ { get; set; }

        /// <summary>Runtime FactionMembership（招募后可变；旧档缺省为空＝无归属）。</summary>
        public string FactionId { get; set; } = string.Empty;
        /// <summary><see cref="XianXia.Core.Social.FactionRoleKind"/> 整型。</summary>
        public int FactionRole { get; set; }

        /// <summary>CombatVitals 当前生命；旧档缺省时 Restore 不灌满（PoolsInitialized=false 仅在无 vitals 字段时）。</summary>
        public bool HasCombatVitals { get; set; }
        public int CurrentHp { get; set; }
        public int CurrentSpiritPower { get; set; }
        public bool VitalsPoolsInitialized { get; set; }

        /// <summary>Lifecycle 弥留到期 tick；0＝未计时。</summary>
        public ulong BleedOutAfterTick { get; set; }

        /// <summary>尸体留存；HasCorpse=false 时不恢复 CorpseComponent。</summary>
        public bool HasCorpse { get; set; }
        public ulong CorpseRemoveAfterTick { get; set; }

        /// <summary>弥留死亡责任者；optional，旧档缺省＝未知。</summary>
        public bool HasResponsibleAttacker { get; set; }
        public ulong ResponsibleAttackerEntityId { get; set; }

        /// <summary>PersonalityProfile tags（若运行中可变）。旧档缺省＝空。</summary>
        public List<string> PersonalityTags { get; set; } = new List<string>();
    }

    public sealed class PartyInventorySlotSnapshotDto
    {
        public string ItemId { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class RelationshipEventSnapshotDto
    {
        public ulong Tick { get; set; }
        public ulong FromEntityId { get; set; }
        public ulong ToEntityId { get; set; }
        public int Delta { get; set; }
        public string ReasonTag { get; set; } = string.Empty;
        public ulong CauseEventId { get; set; }
        public bool HasCauseEventId { get; set; }
        public int Axis { get; set; }
        public bool HasAxis { get; set; }
        public ulong ContextEntityId { get; set; }
        public bool HasContextEntityId { get; set; }
    }

    public sealed class SocialBondSnapshotDto
    {
        public int Kind { get; set; }
        public ulong FromEntityId { get; set; }
        public ulong ToEntityId { get; set; }
    }

    public sealed class AttrBaseDto
    {
        public int AttributeId { get; set; }
        public int Value { get; set; }
    }

    public sealed class ModifierSnapshotDto
    {
        public ulong Id { get; set; }
        public int AttributeId { get; set; }
        public int Operation { get; set; }
        public double Value { get; set; }
        public int SourceKind { get; set; }
        public string SourceDefinitionId { get; set; }
        public ulong SourceEntityId { get; set; }
        public bool HasSourceEntity { get; set; }
        public ulong SourceModifierId { get; set; }
        public bool HasSourceModifier { get; set; }
    }

    public sealed class ActiveActionSnapshotDto
    {
        public ulong Id { get; set; }
        public ulong SubjectId { get; set; }
        public ulong SourceOrderId { get; set; }
        public string Kind { get; set; }
        public int Status { get; set; }
        public ulong TotalTicks { get; set; }
        public ulong RemainingTicks { get; set; }
        /// <summary>Soft additive: WorkArea id for Move／Work，或恢复处稳定 ID。</summary>
        public string TargetRef { get; set; }
        /// <summary>Soft additive: ScheduleActivity int; 0 = unset.</summary>
        public int Activity { get; set; }
    }

    public sealed class OrderSnapshotDto
    {
        public ulong Id { get; set; }
        public ulong SubjectId { get; set; }
        public int Type { get; set; }
        public int Source { get; set; }
        public ulong WaitTicks { get; set; }
        /// <summary>Soft additive: WorkArea id for Move／Work.</summary>
        public string TargetRef { get; set; }
        /// <summary>Soft additive: ScheduleActivity int; 0 = unset.</summary>
        public int Activity { get; set; }
    }

    public sealed class ArtMasterySnapshotDto
    {
        public string ArtId { get; set; }
        public int Tier { get; set; }
        public int Progress { get; set; }
        public int ProgressRequired { get; set; }
    }
}
