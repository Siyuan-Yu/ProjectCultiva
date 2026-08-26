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
        public List<WarSnapshotDto> Wars { get; set; } = new List<WarSnapshotDto>();
        public List<AllianceSnapshotDto> Alliances { get; set; } = new List<AllianceSnapshotDto>();
        public List<VassalageSnapshotDto> Vassalages { get; set; } = new List<VassalageSnapshotDto>();
        public List<RetreatingArmySnapshotDto> RetreatingArmies { get; set; } = new List<RetreatingArmySnapshotDto>();
        public List<CaptureObjectiveSnapshotDto> CaptureObjectives { get; set; } = new List<CaptureObjectiveSnapshotDto>();
        /// <summary>
        /// Phase 2C：PlayerParty 连续世界位置（可选；旧存档缺省时不恢复 motion）。
        /// </summary>
        public PlayerPartyTravelSnapshotDto PlayerPartyTravel { get; set; }

        /// <summary>Phase 2D：Background Character 中途旅行状态（可选）。</summary>
        public List<BackgroundCharacterTravelSnapshotDto> BackgroundCharacterTravels { get; set; } =
            new List<BackgroundCharacterTravelSnapshotDto>();
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
        public float WorldX { get; set; }
        public float WorldY { get; set; }
    }

    public sealed class WorldSiteOwnerSnapshotDto
    {
        public string SiteId { get; set; }
        public string OwnerFactionId { get; set; }
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
