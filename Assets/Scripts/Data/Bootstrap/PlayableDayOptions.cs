namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// Host／试玩装配选项. Does not change Core defaults when left null.
    /// </summary>
    public sealed class PlayableDayOptions
    {
        /// <summary>
        /// When set (0–100), overrides <see cref="XianXia.Core.Simulation.SimulationWorld.ObservationDiscoverChancePercent"/>
        /// for this session only. Null = keep world default (no Core rule change).
        /// </summary>
        public int? ObservationDiscoverChancePercent { get; set; }

        /// <summary>DailyTask.RequiredAmount applied to each character after spawn.</summary>
        public int DailyRequiredAmount { get; set; } = 10;
    }
}
