using XianXia.Core.Domain.Time;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>Quest time limits keyed on world day index (see <see cref="WorldTick.TicksPerDay"/>).</summary>
    public static class QuestDeadline
    {
        public static ulong WorldDayIndex(SimulationWorld world)
        {
            if (world == null)
                return 0;
            return world.Tick.Value / (ulong)WorldTick.TicksPerDay;
        }

        public static void BindOnStart(QuestSpec spec, QuestRuntime runtime, SimulationWorld world)
        {
            runtime.AcceptedAtDayIndex = 0;
            runtime.DeadlineDayIndexExclusive = 0;
            if (spec == null || runtime == null || world == null || spec.DeadlineDays <= 0)
                return;

            var day = WorldDayIndex(world);
            runtime.AcceptedAtDayIndex = day;
            runtime.DeadlineDayIndexExclusive = day + (ulong)spec.DeadlineDays;
        }

        public static bool IsExpired(SimulationWorld world, QuestRuntime runtime)
        {
            if (world == null || runtime == null || runtime.DeadlineDayIndexExclusive == 0)
                return false;
            return WorldDayIndex(world) >= runtime.DeadlineDayIndexExclusive;
        }

        /// <summary>Inclusive days left including today; 0 = expired; -1 = no deadline.</summary>
        public static int RemainingDaysInclusive(SimulationWorld world, QuestRuntime runtime)
        {
            if (runtime == null || runtime.DeadlineDayIndexExclusive == 0)
                return -1;
            var day = WorldDayIndex(world);
            if (day >= runtime.DeadlineDayIndexExclusive)
                return 0;
            return (int)(runtime.DeadlineDayIndexExclusive - day);
        }

        public static string FormatRemaining(SimulationWorld world, QuestRuntime runtime)
        {
            var left = RemainingDaysInclusive(world, runtime);
            if (left < 0)
                return string.Empty;
            if (left <= 0)
                return "已超时";
            if (left == 1)
                return "剩余 1 天（今日内）";
            return "剩余 " + left + " 天";
        }
    }
}
