using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class ScheduleBlockEntry
    {
        public int StartTick { get; set; }
        public int EndTick { get; set; }
        public string Activity { get; set; } = string.Empty;
        public ulong OrderDurationTicks { get; set; } = 6;
    }

    /// <summary>Content type = schedule. Day plan in tick-in-day space (288 ticks/day).</summary>
    public sealed class ScheduleContentDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ScheduleBlockEntry> Blocks { get; set; } = new List<ScheduleBlockEntry>();
    }
}
