using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Orders;

namespace XianXia.Core.Entities
{
    /// <summary>Minimal action/order slots.</summary>
    public sealed class ActionStateComponent : IComponent
    {
        public ActionId ActiveActionId { get; set; }

        public ActionClock? ActiveClock { get; set; }

        /// <summary>Source of the currently active Order/Action (Player vs Schedule).</summary>
        public OrderSource ActiveOrderSource { get; set; } = OrderSource.Player;

        public List<ulong> PendingOrderIds { get; } = new List<ulong>();

        public bool HasActiveAction => !ActiveActionId.IsNone;
    }
}
