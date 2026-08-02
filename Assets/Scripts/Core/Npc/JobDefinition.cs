using System.Collections.Generic;
using XianXia.Core.Schedule;

namespace XianXia.Core.Npc
{
    public sealed class JobActivityBinding
    {
        public ScheduleActivity Activity { get; set; }
        public List<string> WorkAreaIds { get; } = new List<string>();
        /// <summary>When true, cycle WorkAreaIds via <see cref="JobComponent.RouteIndex"/>.</summary>
        public bool Route { get; set; }
    }

    /// <summary>Who the NPC is at work: target areas per schedule activity.</summary>
    public sealed class JobDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PrimaryWorkAreaId { get; set; } = string.Empty;
        public List<JobActivityBinding> ActivityBindings { get; } = new List<JobActivityBinding>();

        public bool TryGetBinding(ScheduleActivity activity, out JobActivityBinding binding)
        {
            for (var i = 0; i < ActivityBindings.Count; i++)
            {
                if (ActivityBindings[i].Activity == activity)
                {
                    binding = ActivityBindings[i];
                    return true;
                }
            }

            binding = null;
            return false;
        }
    }
}
