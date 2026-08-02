using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Schedule activity + Job → concrete WorkArea／Location. No hardcoded coordinates.
    /// </summary>
    public static class ActivityResolver
    {
        public static bool TryResolve(
            SimulationWorld world,
            Entity entity,
            ScheduleActivity activity,
            ulong durationTicks,
            out ResolvedActivity resolved)
        {
            resolved = null;
            if (world == null || entity == null)
                return false;
            if (!entity.TryGet<JobComponent>(out var job) || !job.HasJob)
                return false;
            if (!world.TryGetJob(job.JobId, out var jobDef))
                return false;
            if (!jobDef.TryGetBinding(activity, out var binding) ||
                binding.WorkAreaIds == null ||
                binding.WorkAreaIds.Count == 0)
                return false;

            var index = 0;
            if (binding.Route)
            {
                var count = binding.WorkAreaIds.Count;
                index = ((job.RouteIndex % count) + count) % count;
            }

            var workAreaId = binding.WorkAreaIds[index];
            if (!world.TryGetWorkArea(workAreaId, out var area) ||
                string.IsNullOrEmpty(area.LocationId))
                return false;

            if (area.AllowedActivities.Count > 0 &&
                !ContainsActivity(area.AllowedActivities, activity))
                return false;

            if (world.WorldRegion.TryGet(area.LocationId, out var loc) &&
                loc.AllowedActivities.Count > 0 &&
                !ContainsActivity(loc.AllowedActivities, activity))
                return false;

            var atTarget = entity.TryGet<EntityLocationComponent>(out var locComp) &&
                           locComp.HasLocation &&
                           string.Equals(locComp.LocationId, area.LocationId, System.StringComparison.Ordinal);

            resolved = new ResolvedActivity
            {
                Activity = activity,
                WorkAreaId = workAreaId,
                LocationId = area.LocationId,
                NeedsMove = !atTarget,
                DurationTicks = durationTicks,
                Route = binding.Route
            };
            return true;
        }

        static bool ContainsActivity(System.Collections.Generic.IList<string> names, ScheduleActivity activity)
        {
            var needle = activity.ToString();
            for (var i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], needle, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
