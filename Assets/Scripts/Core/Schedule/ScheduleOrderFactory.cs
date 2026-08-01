using XianXia.Core.Domain.Ids;
using XianXia.Core.Orders;
using XianXia.Core.Results;

namespace XianXia.Core.Schedule
{
    /// <summary>
    /// Builds Schedule-sourced Orders from a resolved activity block.
    /// </summary>
    public sealed class ScheduleOrderFactory
    {
        public Result<Order> Create(OrderId id, EntityId subject, ScheduleBlock block, ulong durationTicks)
        {
            if (block == null)
                return Result.Fail<Order>(ErrorCode.InvalidArgument, "ScheduleBlock is null.");
            return Create(id, subject, block.Activity, durationTicks);
        }

        public Result<Order> Create(
            OrderId id,
            EntityId subject,
            ScheduleActivity activity,
            ulong durationTicks)
        {
            if (durationTicks == 0)
                return Result.Fail<Order>(ErrorCode.InvalidArgument, "DurationTicks must be > 0.");

            var type = ScheduleActivityMapping.ToOrderType(activity);
            return Result.Ok(new Order(id, subject, type, OrderSource.Schedule, waitTicks: durationTicks));
        }
    }
}
