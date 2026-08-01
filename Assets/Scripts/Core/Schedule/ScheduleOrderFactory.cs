using System.Collections.Generic;
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
            if (durationTicks == 0)
                return Result.Fail<Order>(ErrorCode.InvalidArgument, "DurationTicks must be > 0.");

            OrderType type;
            switch (block.Activity)
            {
                case ScheduleActivity.Labor:
                    type = OrderType.Labor;
                    break;
                case ScheduleActivity.Rest:
                    type = OrderType.Rest;
                    break;
                default:
                    return Result.Fail<Order>(ErrorCode.InvalidArgument, "Unsupported schedule activity.", block.Activity.ToString());
            }

            return Result.Ok(new Order(id, subject, type, OrderSource.Schedule, waitTicks: durationTicks));
        }
    }
}
