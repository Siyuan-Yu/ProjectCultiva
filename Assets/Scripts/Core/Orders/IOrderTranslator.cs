using XianXia.Core.Actions;
using XianXia.Core.Results;

namespace XianXia.Core.Orders
{
    public interface IOrderTranslator
    {
        Result<IAction> Translate(Order order);
    }
}
