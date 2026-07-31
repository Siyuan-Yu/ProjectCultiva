namespace XianXia.Unity.Tasks
{
    public sealed class DailyTaskState
    {
        public DailyTaskState(DailyTaskDefinition definition)
        {
            Definition = definition;
        }

        public DailyTaskDefinition Definition { get; }
        public int Progress { get; private set; }
        public bool IsComplete => Progress >= Definition.RequiredAmount;
        public bool CompletionRewardApplied { get; private set; }

        public bool AddProgress(int amount)
        {
            if (amount <= 0 || IsComplete)
            {
                return false;
            }

            Progress = System.Math.Min(Definition.RequiredAmount, Progress + amount);
            return IsComplete;
        }

        public void MarkCompletionRewardApplied()
        {
            CompletionRewardApplied = true;
        }
    }
}
