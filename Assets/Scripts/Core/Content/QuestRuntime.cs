namespace XianXia.Core.Content
{
    public sealed class QuestRuntime
    {
        public string QuestId { get; set; } = string.Empty;
        public QuestStatus Status { get; set; } = QuestStatus.Inactive;
        public int ProgressCount { get; set; }
    }
}
