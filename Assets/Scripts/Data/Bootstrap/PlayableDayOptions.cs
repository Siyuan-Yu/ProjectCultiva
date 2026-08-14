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

        /// <summary>
        /// Optional openingScenario id (e.g. base:scenario_chapter1_harness).
        /// Null／empty = <see cref="PlayableDayBootstrap.DefaultScenarioId"/>.
        /// </summary>
        public string OpeningScenarioId { get; set; }

        /// <summary>
        /// Level Tester 名册 id（type=characterRoster）。有则用名册 entries 刷人，不用 scenario.spawns。
        /// Null／empty = 仍用 scenario.spawns。
        /// </summary>
        public string CharacterRosterId { get; set; }
    }
}
