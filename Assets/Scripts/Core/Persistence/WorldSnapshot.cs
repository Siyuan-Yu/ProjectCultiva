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

        /// <summary>RelationshipLedger 事件流（v6 optional；旧档缺省＝空）。</summary>
        public List<RelationshipEventSnapshotDto> RelationshipEvents { get; set; } =
            new List<RelationshipEventSnapshotDto>();
    }

    public sealed class StrategicSnapshotDto
    {
        public string PlayerFactionId { get; set; } = string.Empty;
        public bool Ch01FormationScenarioCompat { get; set; }
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
        public List<TerritoryRegionControllerSnapshotDto> TerritoryRegionControllers { get; set; } = new List<TerritoryRegionControllerSnapshotDto>();
        public List<WarSnapshotDto> Wars { get; set; } = new List<WarSnapshotDto>();
        public List<AllianceSnapshotDto> Alliances { get; set; } = new List<AllianceSnapshotDto>();
        public List<VassalageSnapshotDto> Vassalages { get; set; } = new List<VassalageSnapshotDto>();
        public List<RetreatingArmySnapshotDto> RetreatingArmies { get; set; } = new List<RetreatingArmySnapshotDto>();
        public List<CaptureObjectiveSnapshotDto> CaptureObjectives { get; set; } = new List<CaptureObjectiveSnapshotDto>();
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
    }

    /// <summary>Phase 2D：Background Character 旅行快照（WorldLocation + route progress）。</summary>
    public sealed class BackgroundCharacterTravelSnapshotDto
    {
        public ulong CharacterId { get; set; }
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
    }

    /// <summary>TerritoryRegion 运行时 Controller（2J §17）；Region/Hexes/PrimaryWorldSiteId 属 Content identity 不重复持久化。</summary>
    public sealed class TerritoryRegionControllerSnapshotDto
    {
        public string RegionId { get; set; }
        public string ControlFactionId { get; set; }
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

    public sealed class CaptureObjectiveSnapshotDto
    {
        public string ObjectiveId { get; set; }
        public string SiteId { get; set; }
        public string WorkAreaId { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
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
        /// <summary>Soft additive: WorkArea id for Move／Work.</summary>
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
