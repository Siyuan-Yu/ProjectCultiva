using System.Collections.Generic;
using XianXia.Core.Entities;
using XianXia.Core.Schedule;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Per-person activity tendency: can-do + priority + preferred places.
    /// Not a profession. Labor site choice uses preferredWorkAreaIds then other valid areas.
    /// </summary>
    public sealed class ActivityTendencyComponent : IComponent
    {
        readonly Dictionary<ScheduleActivity, bool> _capabilities =
            new Dictionary<ScheduleActivity, bool>();
        readonly Dictionary<ScheduleActivity, int> _priorities =
            new Dictionary<ScheduleActivity, int>();

        public List<string> PreferredWorkAreaIds { get; } = new List<string>();

        /// <summary>Assigned housing work area; Rest／Eat prefer this when allowed.</summary>
        public string HomeWorkAreaId { get; set; } = string.Empty;

        public void SetCapability(ScheduleActivity activity, bool enabled) =>
            _capabilities[activity] = enabled;

        public void SetPriority(ScheduleActivity activity, int priority) =>
            _priorities[activity] = priority;

        public bool CanDo(ScheduleActivity activity)
        {
            if (_capabilities.Count == 0)
                return true;
            if (_capabilities.TryGetValue(activity, out var on))
                return on;
            // 新活动（如发呆）未写进旧人物 JSON 时默认允许，避免全员无法回退发呆。
            return activity == ScheduleActivity.Idle;
        }

        public int PriorityOf(ScheduleActivity activity) =>
            _priorities.TryGetValue(activity, out var p) ? p : 0;

        public void CopyPrioritiesTo(List<(ScheduleActivity Activity, int Priority)> dest)
        {
            dest.Clear();
            foreach (ScheduleActivity a in System.Enum.GetValues(typeof(ScheduleActivity)))
            {
                if ((int)a <= 0)
                    continue;
                if (!CanDo(a))
                    continue;
                dest.Add((a, PriorityOf(a)));
            }

            dest.Sort((x, y) =>
            {
                var c = y.Priority.CompareTo(x.Priority);
                return c != 0 ? c : x.Activity.CompareTo(y.Activity);
            });
        }
    }
}
