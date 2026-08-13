namespace XianXia.Core.Content
{
    public enum QuestStatus
    {
        Inactive = 0,
        Active = 1,
        /// <summary>目标已达成，等待玩家领取奖励。</summary>
        ReadyToClaim = 2,
        Completed = 3,
        Failed = 4
    }

    public static class QuestStatusUtil
    {
        /// <summary>目标已完成（含待领奖）；用于链式解锁／questCompleted 条件。</summary>
        public static bool IsObjectivesDone(QuestStatus status) =>
            status == QuestStatus.ReadyToClaim || status == QuestStatus.Completed;
    }
}
