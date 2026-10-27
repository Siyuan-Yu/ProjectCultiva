using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;

namespace XianXia.Core.Npc
{
    /// <summary>Daily shuffle persisted through existing content progress; WorldTick remains the clock.</summary>
    public sealed class SupervisorRoutineDriver
    {
        public const string ScheduleId = "base:schedule_huangcun_supervisor_day";
        const string Key = "base:huangcun_supervisor_daily_routine";
        SimulationWorld _world;
        ulong _day = ulong.MaxValue;

        public static bool Applies(Entity entity) => entity.TryGet<ScheduleComponent>(out var s) && s.DefinitionId == ScheduleId;
        public static bool IsLeisure(SimulationWorld world, Entity entity) => Applies(entity) &&
            world.TryGetSchedule(ScheduleId, out var d) && d.TryResolve(world.Tick, out var b) && b.Activity == ScheduleActivity.Idle;

        public void Ensure(SimulationWorld world)
        {
            var day = world.Tick.Value / (ulong)WorldTick.TicksPerDay;
            if (ReferenceEquals(world, _world) && day == _day) return;
            if (!world.TryGetSchedule(ScheduleId, out _)) return;
            if (!world.ContentDaily.IsMarkedToday(Key, world.Tick))
            {
                world.ContentCounters.Set(Key, world.Random.NextInt(0, 24 * WorldTick.TicksPerDay));
                world.ContentDaily.MarkToday(Key, world.Tick);
            }
            var roll = world.ContentCounters.Get(Key);
            var rotation = roll % WorldTick.TicksPerDay;
            var permutation = roll / WorldTick.TicksPerDay;
            var remaining = new System.Collections.Generic.List<int> { 0, 1, 2, 3 };
            var activities = new[] { ScheduleActivity.Rest, ScheduleActivity.Patrol, ScheduleActivity.Cultivate, ScheduleActivity.Idle };
            var lengths = new[] { 96, 60, 60, 72 };
            var ticks = new ScheduleActivity[WorldTick.TicksPerDay];
            var cursor = 0;
            for (var n = 4; n > 0; n--)
            {
                var index = permutation % n; permutation /= n;
                var item = remaining[index]; remaining.RemoveAt(index);
                for (var t = 0; t < lengths[item]; t++) ticks[(cursor++ + rotation) % ticks.Length] = activities[item];
            }
            var definition = new ScheduleDefinition(ScheduleId);
            for (var start = 0; start < ticks.Length;)
            {
                var end = start + 1;
                while (end < ticks.Length && ticks[end] == ticks[start]) end++;
                definition.AddBlock(start, end, ticks[start], ticks[start] == ScheduleActivity.Patrol ? 4UL : 6UL);
                start = end;
            }
            world.RegisterSchedule(definition);
            _world = world; _day = day;
        }
    }
}
