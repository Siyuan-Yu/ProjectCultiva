using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Time;

namespace XianXia.Core.Schedule
{
    /// <summary>
    /// Configurable default behavior for mortals/NPCs. Not a player restriction.
    /// </summary>
    public sealed class ScheduleDefinition
    {
        readonly List<ScheduleBlock> _blocks = new List<ScheduleBlock>();

        public ScheduleDefinition(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Schedule definition id required.", nameof(id));
            Id = id;
        }

        public string Id { get; }

        public IReadOnlyList<ScheduleBlock> Blocks => _blocks;

        public ScheduleDefinition AddBlock(int startTickInDay, int endTickInDay, ScheduleActivity activity, ulong orderDurationTicks)
        {
            if (startTickInDay < 0 || endTickInDay > WorldTick.TicksPerDay || startTickInDay >= endTickInDay)
                throw new ArgumentException("Invalid schedule block range.");
            if (orderDurationTicks == 0)
                throw new ArgumentException("OrderDurationTicks must be > 0.");

            _blocks.Add(new ScheduleBlock(startTickInDay, endTickInDay, activity, orderDurationTicks));
            return this;
        }

        public bool TryResolve(WorldTick tick, out ScheduleBlock block)
        {
            var tickInDay = (int)(tick.Value % (ulong)WorldTick.TicksPerDay);
            foreach (var candidate in _blocks)
            {
                if (tickInDay >= candidate.StartTickInDay && tickInDay < candidate.EndTickInDay)
                {
                    block = candidate;
                    return true;
                }
            }

            block = null;
            return false;
        }

        /// <summary>Sample laborer day: Rest → Labor → Rest → Labor → Rest.</summary>
        public static ScheduleDefinition CreateDefaultLaborerDay(string id = "base:schedule_laborer_day")
        {
            return new ScheduleDefinition(id)
                .AddBlock(0, 8, ScheduleActivity.Rest, 2)
                .AddBlock(8, 48, ScheduleActivity.Labor, 4)
                .AddBlock(48, 56, ScheduleActivity.Rest, 2)
                .AddBlock(56, 80, ScheduleActivity.Labor, 4)
                .AddBlock(80, 96, ScheduleActivity.Rest, 2);
        }

        /// <summary>
        /// 凡人劳役日（对齐 21 时段骨架）：深夜休息 → 白天劳役 → 正午吃饭 → 下午劳役 → 入夜自由缝（Explore）→ 再休息。
        /// Tick：0–20 休｜20–44 劳｜44–52 饭｜52–76 劳｜76–88 夜探｜88–96 休。
        /// </summary>
        public static ScheduleDefinition CreateMortalDay(string id = "base:schedule_mortal_day")
        {
            return new ScheduleDefinition(id)
                .AddBlock(0, 20, ScheduleActivity.Rest, 2)
                .AddBlock(20, 44, ScheduleActivity.Labor, 4)
                .AddBlock(44, 52, ScheduleActivity.Eat, 2)
                .AddBlock(52, 76, ScheduleActivity.Labor, 4)
                .AddBlock(76, 88, ScheduleActivity.Explore, 4)
                .AddBlock(88, 96, ScheduleActivity.Rest, 2);
        }

        /// <summary>普通修士：休息／探索／修炼；入夜保留修炼窗。</summary>
        public static ScheduleDefinition CreateCultivatorDay(string id = "base:schedule_cultivator_day")
        {
            return new ScheduleDefinition(id)
                .AddBlock(0, 16, ScheduleActivity.Rest, 2)
                .AddBlock(16, 40, ScheduleActivity.Explore, 4)
                .AddBlock(40, 64, ScheduleActivity.Cultivate, 4)
                .AddBlock(64, 76, ScheduleActivity.Explore, 4)
                .AddBlock(76, 88, ScheduleActivity.Cultivate, 4)
                .AddBlock(88, 96, ScheduleActivity.Rest, 2);
        }

        /// <summary>主管：日间巡查／检查；入夜稀疏巡逻。</summary>
        public static ScheduleDefinition CreateSupervisorDay(string id = "base:schedule_supervisor_day")
        {
            return new ScheduleDefinition(id)
                .AddBlock(0, 16, ScheduleActivity.Rest, 2)
                .AddBlock(16, 40, ScheduleActivity.Patrol, 4)
                .AddBlock(40, 64, ScheduleActivity.Inspect, 4)
                .AddBlock(64, 76, ScheduleActivity.Patrol, 4)
                .AddBlock(76, 88, ScheduleActivity.Patrol, 4)
                .AddBlock(88, 96, ScheduleActivity.Rest, 2);
        }
    }
}
