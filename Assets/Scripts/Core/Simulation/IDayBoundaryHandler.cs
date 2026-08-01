namespace XianXia.Core.Simulation
{
    /// <summary>
    /// Hook at day boundaries. Phase D will attach QuotaConsequence here; Phase A keeps it empty.
    /// </summary>
    public interface IDayBoundaryHandler
    {
        /// <summary>Called after <c>DayEnded</c> is published for <paramref name="endedDayIndex"/>.</summary>
        void OnDayEnded(SimulationWorld world, ulong endedDayIndex);

        /// <summary>Called after <c>DayStarted</c> is published for <paramref name="startedDayIndex"/>.</summary>
        void OnDayStarted(SimulationWorld world, ulong startedDayIndex);
    }
}
