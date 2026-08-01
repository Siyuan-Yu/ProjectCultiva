using XianXia.Core.Entities;

namespace XianXia.Core.Labor
{
    /// <summary>
    /// VS0.2 daily labor virtual progress. No economy / inventory gameplay.
    /// </summary>
    public sealed class DailyTaskComponent : IComponent
    {
        public int LaborProgress { get; set; }

        /// <summary>Soft quota target for later Override penalty (Phase C). Not enforced in Phase A.</summary>
        public int LaborQuota { get; set; } = 10;
    }
}
