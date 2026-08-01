using XianXia.Core.Domain.Ids;
using XianXia.Core.Orders;
using XianXia.Core.Results;

namespace XianXia.Core.Input
{
    /// <summary>
    /// Maps RTS player intent → Order with <see cref="OrderSource.Player"/>.
    /// </summary>
    public sealed class PlayerOrderFactory
    {
        public Result<Order> Create(OrderId id, PlayerCommandRequest request)
        {
            if (request == null)
                return Result.Fail<Order>(ErrorCode.InvalidArgument, "PlayerCommandRequest is null.");
            if (request.DurationTicks == 0)
                return Result.Fail<Order>(ErrorCode.InvalidArgument, "DurationTicks must be > 0.");

            switch (request.Kind)
            {
                case PlayerCommandKind.Labor:
                    return Result.Ok(new Order(
                        id,
                        request.Subject,
                        OrderType.Labor,
                        OrderSource.Player,
                        waitTicks: request.DurationTicks));

                case PlayerCommandKind.Rest:
                    return Result.Ok(new Order(
                        id,
                        request.Subject,
                        OrderType.Rest,
                        OrderSource.Player,
                        waitTicks: request.DurationTicks));

                case PlayerCommandKind.Observe:
                    return Result.Ok(new Order(
                        id,
                        request.Subject,
                        OrderType.Observe,
                        OrderSource.Player,
                        waitTicks: request.DurationTicks));

                case PlayerCommandKind.Cultivate:
                    return Result.Ok(new Order(
                        id,
                        request.Subject,
                        OrderType.Cultivate,
                        OrderSource.Player,
                        waitTicks: request.DurationTicks));

                default:
                    return Result.Fail<Order>(ErrorCode.InvalidArgument, "Unknown PlayerCommandKind.", request.Kind.ToString());
            }
        }
    }
}
