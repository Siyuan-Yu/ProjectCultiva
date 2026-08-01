using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Persistence
{
    public sealed class WorldSnapshot
    {
        public const int CurrentSchemaVersion = 1;

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
        public int CultivationProgress { get; set; }
        public int BreakthroughProgressRequired { get; set; }
        public int CultivationSpeed { get; set; }
        public string LearnedManualId { get; set; }
        public string RequiredRealmName { get; set; }
        public bool HasDailyTask { get; set; }
        public int LaborProgress { get; set; }
        public int LaborQuota { get; set; }
        public int RequiredAmount { get; set; }
        public int CompletedAmount { get; set; }
        public int Deviation { get; set; }
        public bool HasSchedule { get; set; }
        public string ScheduleDefinitionId { get; set; }
        public int ActiveOrderSource { get; set; }
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
    }

    public sealed class OrderSnapshotDto
    {
        public ulong Id { get; set; }
        public ulong SubjectId { get; set; }
        public int Type { get; set; }
        public int Source { get; set; }
        public ulong WaitTicks { get; set; }
    }
}
