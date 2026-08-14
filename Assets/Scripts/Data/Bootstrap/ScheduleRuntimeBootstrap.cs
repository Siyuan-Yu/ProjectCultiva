using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Registers Content schedule definitions onto SimulationWorld.</summary>
    public static class ScheduleRuntimeBootstrap
    {
        public static Result Register(SimulationWorld world, DefinitionRegistry registry)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ScheduleRuntimeBootstrap args null.");

            foreach (var kv in registry.Schedules)
            {
                var mapped = ToRuntime(kv.Value);
                if (mapped.IsFailure)
                    return Result.Failure(mapped.Error);
                world.RegisterSchedule(mapped.Value);
            }

            return Result.Success();
        }

        public static Result<ScheduleDefinition> ToRuntime(ScheduleContentDefinition src)
        {
            if (src == null)
                return Result.Fail<ScheduleDefinition>(ErrorCode.InvalidArgument, "ScheduleContentDefinition null.");
            var id = src.Id.ToString();
            if (string.IsNullOrWhiteSpace(id))
                return Result.Fail<ScheduleDefinition>(ErrorCode.InvalidArgument, "schedule.id required.");

            try
            {
                var def = new ScheduleDefinition(id);
                if (src.Blocks == null || src.Blocks.Count == 0)
                {
                    return Result.Fail<ScheduleDefinition>(
                        ErrorCode.MissingRequiredField, "schedule.blocks required.", id);
                }

                for (var i = 0; i < src.Blocks.Count; i++)
                {
                    var b = src.Blocks[i];
                    if (b == null)
                        continue;
                    if (!ScheduleActivityMapping.TryParse(b.Activity, out var activity))
                    {
                        return Result.Fail<ScheduleDefinition>(
                            ErrorCode.InvalidArgument,
                            "Unknown schedule activity: " + b.Activity,
                            id);
                    }

                    var duration = b.OrderDurationTicks == 0 ? 6UL : b.OrderDurationTicks;
                    def.AddBlock(b.StartTick, b.EndTick, activity, duration);
                }

                if (def.Blocks.Count == 0)
                {
                    return Result.Fail<ScheduleDefinition>(
                        ErrorCode.MissingRequiredField, "schedule.blocks empty.", id);
                }

                return Result.Ok(def);
            }
            catch (System.Exception ex)
            {
                return Result.Fail<ScheduleDefinition>(
                    ErrorCode.ContentLoadFailed, ex.Message, id);
            }
        }
    }
}
