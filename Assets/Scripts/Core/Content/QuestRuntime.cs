namespace XianXia.Core.Content
{
    public sealed class QuestRuntime
    {
        public string QuestId { get; set; } = string.Empty;
        public QuestStatus Status { get; set; } = QuestStatus.Inactive;
        /// <summary>Live objective progress (e.g. unique laborers done).</summary>
        public int ProgressCount { get; set; }
        /// <summary>Progress denominator for UI (0 = unknown / not a counter objective).</summary>
        public int ProgressMax { get; set; }
    }
}
