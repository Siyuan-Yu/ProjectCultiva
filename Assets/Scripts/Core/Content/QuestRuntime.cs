using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Content
{
    public sealed class QuestRuntime
    {
        /// <summary>稳定运行身份；固定任务等于 QuestId，人物委托由 QuestBoard 分配。</summary>
        public string QuestInstanceId { get; set; } = string.Empty;
        /// <summary>任务模板 DefinitionId（旧字段名保留兼容）。</summary>
        public string QuestId { get; set; } = string.Empty;
        public EntityId IssuerEntityId { get; set; } = EntityId.None;
        public string SourceOpportunityInstanceId { get; set; } = string.Empty;
        public string IssuerDisplayName { get; set; } = string.Empty;
        public EntityId AcceptedByEntityId { get; set; } = EntityId.None;
        public QuestStatus Status { get; set; } = QuestStatus.Inactive;
        /// <summary>Live objective progress (e.g. unique laborers done).</summary>
        public int ProgressCount { get; set; }
        /// <summary>Progress denominator for UI (0 = unknown / not a counter objective).</summary>
        public int ProgressMax { get; set; }
        /// <summary>World day index when the quest became Active (0 = n/a).</summary>
        public ulong AcceptedAtDayIndex { get; set; }
        /// <summary>First world day index when the quest is expired (exclusive).</summary>
        public ulong DeadlineDayIndexExclusive { get; set; }
        public bool DeliveryCompleted { get; set; }
        public string FailureReason { get; set; } = string.Empty;
    }
}
