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
        /// <summary>Soft slot within the work area (0..capacity-1); Host maps to a spot.</summary>
        public int SlotIndex { get; set; } = -1;
    }
}
