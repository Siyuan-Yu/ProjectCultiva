namespace XianXia.Core.Domain.Time
{
    /// <summary>
    /// Derived day／hour view of <see cref="WorldTick"/>. Not a second timeline; never stored separately.
    /// 1 hour = 4 ticks; 1 day = 96 ticks (see <see cref="WorldTick.TicksPerDay"/>).
    /// </summary>
    public readonly struct DayClock
    {
        public const int TicksPerHour = 4;

        public DayClock(ulong dayIndex, int tickInDay, int hourOfDay)
        {
            DayIndex = dayIndex;
            TickInDay = tickInDay;
            HourOfDay = hourOfDay;
        }

        public ulong DayIndex { get; }

        public int TickInDay { get; }

        public int HourOfDay { get; }

        public static DayClock FromWorldTick(WorldTick tick)
        {
            var value = tick.Value;
            var dayIndex = value / (ulong)WorldTick.TicksPerDay;
            var tickInDay = (int)(value % (ulong)WorldTick.TicksPerDay);
            var hourOfDay = tickInDay / TicksPerHour;
            return new DayClock(dayIndex, tickInDay, hourOfDay);
        }

        public override string ToString() =>
            "day=" + DayIndex + ";tickInDay=" + TickInDay + ";hour=" + HourOfDay;
    }
}
