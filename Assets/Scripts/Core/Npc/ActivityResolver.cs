using System.Collections.Generic;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Activity → WorkArea from world data (not professions).
    /// Prefers preferredWorkAreaIds／homeWorkAreaId; skips full areas; assigns soft SlotIndex.
    /// Housing areas with residentTags only admit matching personality tags.
    /// Idle can resolve without a work area (loiter in place).
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

            if (activity == ScheduleActivity.Idle)
                return TryResolveIdle(world, entity, durationTicks, out resolved);

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

            string workAreaId = null;
            var slotIndex = -1;
            if (route)
            {
                workAreaId = CandidateBuffer[index];
                if (!TryPickSlot(world, entity, workAreaId, out slotIndex))
                    return false;
            }
            else
            {
                for (var i = 0; i < CandidateBuffer.Count; i++)
                {
                    var id = CandidateBuffer[i];
                    if (!TryPickSlot(world, entity, id, out slotIndex))
                        continue;
                    workAreaId = id;
                    break;
                }

                if (workAreaId == null)
                    return false;
            }

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
                Route = route,
                SlotIndex = slotIndex
            };
            return true;
        }

        static bool TryResolveIdle(
            SimulationWorld world,
            Entity entity,
            ulong durationTicks,
            out ResolvedActivity resolved)
        {
            resolved = null;
            CollectCandidates(world, entity, ScheduleActivity.Idle, CandidateBuffer);
            if (CandidateBuffer.Count > 0)
            {
                for (var i = 0; i < CandidateBuffer.Count; i++)
                {
                    var id = CandidateBuffer[i];
                    if (!TryPickSlot(world, entity, id, out var slot))
                        continue;
                    if (!world.TryGetWorkArea(id, out var area) || string.IsNullOrEmpty(area.LocationId))
                        continue;
                    var at = entity.TryGet<EntityLocationComponent>(out var lc) &&
                             lc.HasLocation &&
                             string.Equals(lc.LocationId, area.LocationId, System.StringComparison.Ordinal);
                    resolved = new ResolvedActivity
                    {
                        Activity = ScheduleActivity.Idle,
                        WorkAreaId = id,
                        LocationId = area.LocationId,
                        NeedsMove = !at,
                        DurationTicks = durationTicks,
                        SlotIndex = slot
                    };
                    return true;
                }
            }

            var locationId = string.Empty;
            if (entity.TryGet<EntityLocationComponent>(out var cur) && cur.HasLocation)
                locationId = cur.LocationId;

            resolved = new ResolvedActivity
            {
                Activity = ScheduleActivity.Idle,
                WorkAreaId = string.Empty,
                LocationId = locationId,
                NeedsMove = false,
                DurationTicks = durationTicks,
                SlotIndex = -1
            };
            return true;
        }

        static bool TryPickSlot(
            SimulationWorld world,
            Entity entity,
            string workAreaId,
            out int slotIndex)
        {
            slotIndex = -1;
            if (!world.TryGetWorkArea(workAreaId, out var area))
                return false;
            var capacity = area.Capacity > 0 ? area.Capacity : 4;
            if (world.WorkAreaOccupancy.TryGet(entity.Id, out var heldArea, out var heldSlot) &&
                string.Equals(heldArea, workAreaId, System.StringComparison.Ordinal))
            {
                slotIndex = heldSlot;
                return true;
            }

            return world.WorkAreaOccupancy.TryFindFreeSlot(workAreaId, capacity, out slotIndex);
        }

        /// <summary>
        /// Ordered candidates: home → preferred → other allowed areas by id.
        /// Housing residentTags filter applies to Rest／Eat／Idle.
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
                if (!EntityMayUseArea(entity, area, activity))
                    continue;
                if (!WorkAreaAvailability.IsAvailable(world, area, activity, entity))
                    continue;
                allowed.Add(area.Id);
            }

            allowed.Sort(System.StringComparer.Ordinal);

            if (entity != null &&
                entity.TryGet<ActivityTendencyComponent>(out var tendency))
            {
                if (!string.IsNullOrWhiteSpace(tendency.HomeWorkAreaId) &&
                    IsHomeActivity(activity) &&
                    allowed.Contains(tendency.HomeWorkAreaId) &&
                    !into.Contains(tendency.HomeWorkAreaId))
                    into.Add(tendency.HomeWorkAreaId);

                if (tendency.PreferredWorkAreaIds.Count > 0)
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

        static bool IsHomeActivity(ScheduleActivity activity) =>
            activity == ScheduleActivity.Rest ||
            activity == ScheduleActivity.Eat ||
            activity == ScheduleActivity.Idle;

        /// <summary>
        /// Housing areas with residentTags: require tag intersect for home activities.
        /// Non-home activities ignore residentTags.
        /// </summary>
        public static bool EntityMayUseArea(Entity entity, WorkAreaDefinition area, ScheduleActivity activity)
        {
            if (area == null)
                return false;
            if (area.ResidentTags == null || area.ResidentTags.Count == 0)
                return true;
            if (!IsHomeActivity(activity))
                return true;
            if (entity == null)
                return false;
            if (!entity.TryGet<PersonalityProfileComponent>(out var profile))
                return false;
            for (var i = 0; i < area.ResidentTags.Count; i++)
            {
                var tag = area.ResidentTags[i];
                if (profile.HasTag(tag))
                    return true;
            }

            return false;
        }

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

    /// <summary>Soft capacity: area is available if a free slot remains (or this entity already holds one).</summary>
    public static class WorkAreaAvailability
    {
        public static bool IsAvailable(
            SimulationWorld world,
            WorkAreaDefinition area,
            ScheduleActivity activity)
        {
            return IsAvailable(world, area, activity, null);
        }

        public static bool IsAvailable(
            SimulationWorld world,
            WorkAreaDefinition area,
            ScheduleActivity activity,
            Entity entity)
        {
            if (world == null || area == null)
                return false;
            var capacity = area.Capacity > 0 ? area.Capacity : 4;
            if (entity != null &&
                world.WorkAreaOccupancy.TryGet(entity.Id, out var held, out _) &&
                string.Equals(held, area.Id, System.StringComparison.Ordinal))
                return true;
            return world.WorkAreaOccupancy.HasFreeSlot(area.Id, capacity);
        }
    }
}
