namespace XianXia.Core.Schedule
{
    /// <summary>
    /// One planned interval within a day (tick-in-day space, half-open [Start, End)).
    /// </summary>
    public sealed class ScheduleBlock
    {
        public ScheduleBlock(int startTickInDay, int endTickInDay, ScheduleActivity activity, ulong orderDurationTicks)
        {
            StartTickInDay = startTickInDay;
            EndTickInDay = endTickInDay;
            Activity = activity;
            OrderDurationTicks = orderDurationTicks;
        }

        public int StartTickInDay { get; }

        public int EndTickInDay { get; }

        public ScheduleActivity Activity { get; }

        /// <summary>Duration of each Schedule-sourced Order chunk while this block is active.</summary>
        public ulong OrderDurationTicks { get; }
    }
}
