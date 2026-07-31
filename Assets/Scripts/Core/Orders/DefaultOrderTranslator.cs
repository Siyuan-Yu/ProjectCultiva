using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;

namespace XianXia.Core.Orders
{
    public sealed class DefaultOrderTranslator : IOrderTranslator
    {
        ulong _nextActionId = 1;

        public ulong PeekNextActionId => _nextActionId;

        public void RestoreNextActionId(ulong next) => _nextActionId = next == 0 ? 1UL : next;

        public Result<IAction> Translate(Order order)
        {
            if (order == null)
                return Result.Fail<IAction>(ErrorCode.InvalidArgument, "Order is null.");

            var actionId = new ActionId(_nextActionId++);
            switch (order.Type)
            {
                case OrderType.Wait:
                    if (order.WaitTicks == 0)
                        return Result.Fail<IAction>(ErrorCode.InvalidArgument, "WaitTicks must be > 0.");
                    return Result.Ok<IAction>(new WaitAction(actionId, order.Subject, order.Id, order.WaitTicks));

                case OrderType.ApplyModifier:
                    if (!order.ModifierAttribute.HasValue || !order.ModifierOperation.HasValue || !order.ModifierSource.HasValue)
                        return Result.Fail<IAction>(ErrorCode.InvalidArgument, "ApplyModifier fields incomplete.");
                    return Result.Ok<IAction>(new ApplyModifierAction(
                        actionId,
                        order.Subject,
                        order.Id,
                        order.ModifierAttribute.Value,
                        order.ModifierOperation.Value,
                        order.ModifierValue,
                        order.ModifierSource.Value));

                case OrderType.Cultivate:
                    if (order.WaitTicks == 0)
                        return Result.Fail<IAction>(ErrorCode.InvalidArgument, "Cultivate duration must be > 0.");
                    return Result.Ok<IAction>(new CultivateAction(actionId, order.Subject, order.Id, order.WaitTicks));

                default:
                    return Result.Fail<IAction>(ErrorCode.InvalidOperation, "Unsupported order type.");
            }
        }
    }
}
