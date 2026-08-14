using System.Collections.Generic;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Activity → WorkArea from world data (not professions).
    /// Prefers character preferredWorkAreaIds; skips unavailable areas; multi-area activities cycle by RouteIndex.
    /// </summary>
    public static class ActivityResolver
    {
        static readonly List<string> CandidateBuffer = new List<string>(16);

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

            if (entity.TryGet<ActivityTendencyComponent>(out var tendency) && !tendency.CanDo(activity))
                return false;

            CollectCandidates(world, entity, activity, CandidateBuffer);
            if (CandidateBuffer.Count == 0)
                return false;

            var index = 0;
            var route = IsRouteActivity(activity) && CandidateBuffer.Count > 1;
            if (route)
            {
                if (!entity.TryGet<JobComponent>(out var routeState))
                {
                    routeState = new JobComponent();
                    entity.AddComponent(routeState);
                }

                var count = CandidateBuffer.Count;
                index = ((routeState.RouteIndex % count) + count) % count;
            }

            var workAreaId = CandidateBuffer[index];
            if (!world.TryGetWorkArea(workAreaId, out var area) ||
                string.IsNullOrEmpty(area.LocationId))
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
                Route = route
            };
            return true;
        }

        /// <summary>
        /// Ordered candidates: preferred first (still available), then other allowed areas by id.
        /// Availability stub: always true until occupancy／resource depletion is wired.
        /// </summary>
        public static void CollectCandidates(
            SimulationWorld world,
            Entity entity,
            ScheduleActivity activity,
            List<string> into)
        {
            into.Clear();
            if (world == null)
                return;

            var allowed = new List<string>();
            foreach (var kv in world.WorkAreas)
            {
                var area = kv.Value;
                if (area == null || string.IsNullOrEmpty(area.Id))
                    continue;
                if (!AllowsActivity(area, activity))
                    continue;
                if (!WorkAreaAvailability.IsAvailable(world, area, activity))
                    continue;
                allowed.Add(area.Id);
            }

            allowed.Sort(System.StringComparer.Ordinal);

            if (entity != null &&
                entity.TryGet<ActivityTendencyComponent>(out var tendency) &&
                tendency.PreferredWorkAreaIds.Count > 0)
            {
                for (var i = 0; i < tendency.PreferredWorkAreaIds.Count; i++)
                {
                    var id = tendency.PreferredWorkAreaIds[i];
                    if (string.IsNullOrEmpty(id))
                        continue;
                    if (!allowed.Contains(id))
                        continue;
                    if (!into.Contains(id))
                        into.Add(id);
                }
            }

            for (var i = 0; i < allowed.Count; i++)
            {
                if (!into.Contains(allowed[i]))
                    into.Add(allowed[i]);
            }
        }

        public static IReadOnlyList<string> RouteCandidates(
            SimulationWorld world,
            Entity entity,
            ScheduleActivity activity)
        {
            var list = new List<string>();
            CollectCandidates(world, entity, activity, list);
            return list;
        }

        static bool IsRouteActivity(ScheduleActivity activity) =>
            activity == ScheduleActivity.Patrol ||
            activity == ScheduleActivity.Inspect ||
            activity == ScheduleActivity.Explore;

        static bool AllowsActivity(WorkAreaDefinition area, ScheduleActivity activity)
        {
            if (area.AllowedActivities == null || area.AllowedActivities.Count == 0)
                return true;
            return ContainsActivity(area.AllowedActivities, activity);
        }

        static bool ContainsActivity(IList<string> names, ScheduleActivity activity)
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

    /// <summary>
    /// Hook for plot occupancy／depleted trees／daily till limits. Currently always available.
    /// </summary>
    public static class WorkAreaAvailability
    {
        public static bool IsAvailable(
            SimulationWorld world,
            WorkAreaDefinition area,
            ScheduleActivity activity)
        {
            if (world == null || area == null)
                return false;
            // Future: occupied slots, exhausted resources, till-count caps, etc.
            return true;
        }
    }
}
