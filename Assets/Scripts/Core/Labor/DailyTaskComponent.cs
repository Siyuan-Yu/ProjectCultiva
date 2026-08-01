using XianXia.Core.Entities;

namespace XianXia.Core.Labor
{
    /// <summary>
    /// VS0.2 daily labor quota counters (rules only; no supervisor / relationship).
    /// </summary>
    public sealed class DailyTaskComponent : IComponent
    {
        /// <summary>Expected labor units for the day.</summary>
        public int RequiredAmount { get; set; } = 10;

        /// <summary>Completed labor units (advanced by LaborAction).</summary>
        public int CompletedAmount { get; set; }

        /// <summary>Accumulated unfinished Schedule Labor from player overrides.</summary>
        public int Deviation { get; set; }

        /// <summary>Compatibility alias for CompletedAmount.</summary>
        public int LaborProgress
        {
            get => CompletedAmount;
            set => CompletedAmount = value;
        }

        /// <summary>Compatibility alias for RequiredAmount.</summary>
        public int LaborQuota
        {
            get => RequiredAmount;
            set => RequiredAmount = value;
        }
    }
}
