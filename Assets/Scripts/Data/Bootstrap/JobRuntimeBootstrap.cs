using System;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Registers WorkArea／Job content onto SimulationWorld (session catalogs).</summary>
    public static class JobRuntimeBootstrap
    {
        public static Result Register(SimulationWorld world, DefinitionRegistry registry)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "JobRuntimeBootstrap args null.");

            foreach (var kv in registry.WorkAreas)
            {
                var src = kv.Value;
                var area = new WorkAreaDefinition
                {
                    Id = src.Id.ToString(),
                    Name = src.Name ?? string.Empty,
                    LocationId = src.LocationId ?? string.Empty,
                    OffsetX = src.OffsetX,
                    OffsetZ = src.OffsetZ,
                    Capacity = src.Capacity > 0 ? src.Capacity : 4,
                    IsControlCore = src.IsControlCore,
                    MaxDurability = src.MaxDurability,
                    Defense = src.Defense,
                    OccupyHoldSeconds = src.OccupyHoldSeconds > 0.01f ? src.OccupyHoldSeconds : 10f
                };
                if (src.Tags != null)
                    area.Tags.AddRange(src.Tags);
                if (src.AllowedActivities != null)
                    area.AllowedActivities.AddRange(src.AllowedActivities);
                if (src.ResidentTags != null)
                    area.ResidentTags.AddRange(src.ResidentTags);
                if (src.GrantsPrivileges != null)
                    area.GrantsPrivileges.AddRange(src.GrantsPrivileges);
                world.RegisterWorkArea(area);
            }

            foreach (var kv in registry.Jobs)
            {
                var src = kv.Value;
                var job = new JobDefinition
                {
                    Id = src.Id.ToString(),
                    Name = src.Name ?? string.Empty,
                    PrimaryWorkAreaId = src.PrimaryWorkAreaId ?? string.Empty
                };
                if (src.ActivityBindings != null)
                {
                    foreach (var entry in src.ActivityBindings)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.Activity))
                            continue;
                        if (!ScheduleActivityMapping.TryParse(entry.Activity, out var activity))
                            continue;
                        var binding = new JobActivityBinding
                        {
                            Activity = activity,
                            Route = string.Equals(entry.Mode, "route", StringComparison.OrdinalIgnoreCase)
                        };
                        if (entry.WorkAreaIds != null)
                            binding.WorkAreaIds.AddRange(entry.WorkAreaIds);
                        job.ActivityBindings.Add(binding);
                    }
                }

                world.RegisterJob(job);
            }

            return Result.Success();
        }
    }
}
