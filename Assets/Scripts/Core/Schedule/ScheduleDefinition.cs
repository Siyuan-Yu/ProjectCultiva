using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Time;

namespace XianXia.Core.Schedule
{
    /// <summary>
    /// Configurable default behavior for mortals/NPCs. Not a player restriction.
    /// Tick ranges assume <see cref="WorldTick.TicksPerDay"/>＝288（每 Tick＝5 游戏分钟）。
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

        /// <summary>
        /// Built-in factories kept as mirrors／fallback when Content schedule JSON is missing.
        /// Prefer editing <c>Content/BaseGame/Data/Schedules/schedules.json</c> via NpcEditor.
        /// </summary>
        public static ScheduleDefinition CreateDefaultLaborerDay(string id = "base:schedule_laborer_day")
        {
            return new ScheduleDefinition(id)
                .AddBlock(0, 24, ScheduleActivity.Rest, 6)
                .AddBlock(24, 144, ScheduleActivity.Labor, 12)
                .AddBlock(144, 168, ScheduleActivity.Rest, 6)
                .AddBlock(168, 240, ScheduleActivity.Labor, 12)
                .AddBlock(240, 288, ScheduleActivity.Rest, 6);
        }

        /// <summary>
        /// 凡人劳役日：深夜休息 → 白天劳役 → 正午吃饭 → 下午劳役 → 入夜 Explore → 再休息。
        /// Tick（288/日）：0–60 休｜60–132 劳｜132–156 饭｜156–228 劳｜228–264 夜探｜264–288 休。
        /// </summary>
        public static ScheduleDefinition CreateMortalDay(string id = "base:schedule_mortal_day")
        {
            return new ScheduleDefinition(id)
                .AddBlock(0, 60, ScheduleActivity.Rest, 6)
                .AddBlock(60, 132, ScheduleActivity.Labor, 12)
                .AddBlock(132, 156, ScheduleActivity.Eat, 6)
                .AddBlock(156, 228, ScheduleActivity.Labor, 12)
                .AddBlock(228, 264, ScheduleActivity.Explore, 12)
                .AddBlock(264, 288, ScheduleActivity.Rest, 6);
        }

        /// <summary>普通修士：休息／探索／修炼；入夜保留修炼窗。</summary>
        public static ScheduleDefinition CreateCultivatorDay(string id = "base:schedule_cultivator_day")
        {
            return new ScheduleDefinition(id)
                .AddBlock(0, 48, ScheduleActivity.Rest, 6)
                .AddBlock(48, 120, ScheduleActivity.Explore, 12)
                .AddBlock(120, 192, ScheduleActivity.Cultivate, 12)
                .AddBlock(192, 228, ScheduleActivity.Explore, 12)
                .AddBlock(228, 264, ScheduleActivity.Cultivate, 12)
                .AddBlock(264, 288, ScheduleActivity.Rest, 6);
        }

        /// <summary>主管：休息 → 巡查 → 检查 → 修炼 → 稀疏巡逻 → 休息。</summary>
        public static ScheduleDefinition CreateSupervisorDay(string id = "base:schedule_supervisor_day")
        {
            return new ScheduleDefinition(id)
                .AddBlock(0, 48, ScheduleActivity.Rest, 6)
                .AddBlock(48, 120, ScheduleActivity.Patrol, 12)
                .AddBlock(120, 168, ScheduleActivity.Inspect, 12)
                .AddBlock(168, 216, ScheduleActivity.Cultivate, 12)
                .AddBlock(216, 264, ScheduleActivity.Patrol, 12)
                .AddBlock(264, 288, ScheduleActivity.Rest, 6);
        }
    }
}
