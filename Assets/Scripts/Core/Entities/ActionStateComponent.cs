using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;

namespace XianXia.Core.Entities
{
    /// <summary>Minimal action/order slots. Full Order/Action types arrive in Phase 9.</summary>
    public sealed class ActionStateComponent : IComponent
    {
        public ActionId ActiveActionId { get; set; }

        public ActionClock? ActiveClock { get; set; }

        public List<ulong> PendingOrderIds { get; } = new List<ulong>();

        public bool HasActiveAction => !ActiveActionId.IsNone;
    }
}
