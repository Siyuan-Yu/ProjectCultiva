using XianXia.Core.Schedule;

namespace XianXia.Core.Npc
{
    public sealed class ResolvedActivity
    {
        public ScheduleActivity Activity { get; set; }
        public string WorkAreaId { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public bool NeedsMove { get; set; }
        public ulong DurationTicks { get; set; }
        public bool Route { get; set; }
    }
}
