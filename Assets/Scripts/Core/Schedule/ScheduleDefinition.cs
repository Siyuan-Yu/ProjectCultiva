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
    }
}
