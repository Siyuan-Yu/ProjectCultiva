namespace XianXia.Core.Domain.Time
{
    /// <summary>
    /// Derived day／hour view of <see cref="WorldTick"/>. Not a second timeline; never stored separately.
    /// 1 hour = 12 ticks；1 day = 288 ticks（每 Tick＝5 游戏分钟）。
    /// </summary>
    public readonly struct DayClock
    {
        public const int TicksPerHour = 12;

        public DayClock(ulong dayIndex, int tickInDay, int hourOfDay)
        {
            DayIndex = dayIndex;
            TickInDay = tickInDay;
            HourOfDay = hourOfDay;
        }

        public ulong DayIndex { get; }

        public int TickInDay { get; }

        public int HourOfDay { get; }

        /// <summary>当前小时内的游戏分钟：0／15／30／45（每 Tick＝15 分钟）。</summary>
        public int MinuteOfHour => (TickInDay % TicksPerHour) * WorldTick.GameMinutesPerTick;

        public static DayClock FromWorldTick(WorldTick tick)
        {
            var value = tick.Value;
            var dayIndex = value / (ulong)WorldTick.TicksPerDay;
            var tickInDay = (int)(value % (ulong)WorldTick.TicksPerDay);
            var hourOfDay = tickInDay / TicksPerHour;
            return new DayClock(dayIndex, tickInDay, hourOfDay);
        }

        public override string ToString() =>
            "day=" + DayIndex + ";tickInDay=" + TickInDay +
            ";hour=" + HourOfDay + ";minute=" + MinuteOfHour;
    }
}
